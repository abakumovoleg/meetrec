using System.Diagnostics;
using Meetrec.Asr;
using Meetrec.Core;
using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;

namespace Meetrec.Pipeline;

public sealed class TranscriptionPipeline(TranscriptionEngines engines)
{
    public async Task<TranscriptionResult> RunAsync(
        TranscribeInput input, TranscriptionOptions options, ITranscriptionObserver observer, CancellationToken ct)
    {
        if (input.Session is null && input.Tracks.Count > 1)
        {
            observer.Warning($"нет {SessionLayout.SessionFileName}, дорожки склеиваются без сдвига.");
        }

        var clips = input.Tracks.Select(t => AudioLoader.LoadMono16k(t.Path)).ToList();
        observer.AudioLoaded(clips.Select((c, i) => input.Tracks[i].Offset + c.Duration).Max(), clips.Count);

        var tracks = new List<TrackRecognition>();
        var stages = new List<StageTiming>();
        var words = new List<IReadOnlyList<Word>>();
        using (var transcriber = engines.LoadTranscriber())
        {
            observer.TranscriberLoaded();
            var recognizer = new SpeechRecognizer(transcriber, engines.Vad, options.Recognition);
            for (var i = 0; i < clips.Count; i++)
            {
                var name = Path.GetFileName(input.Tracks[i].Path);
                var segments = input.Tracks[i].Source == TrackSource.Participant;
                var recognition = await RunStage(new PipelineStage(StageKind.Recognition, name), observer, stages, progress => segments
                    ? recognizer.RecognizeSegmentsAsync(clips[i], options.Language, progress, ct)
                    : recognizer.RecognizeAsync(clips[i], options.Language, progress, ct));
                tracks.Add(new TrackRecognition(name, recognition.Stats));
                observer.TrackRecognized(name, recognition.Stats);
                words.Add(recognition.Words);
            }
        }

        var merged = new List<TrackWords>();
        var diarizedAudio = new Dictionary<string, AudioClip>();
        for (var i = 0; i < clips.Count; i++)
        {
            var track = input.Tracks[i];
            IReadOnlyList<DiarSegment> segments = [];
            if (track.Speakers is DiarizedSpeakers)
            {
                ct.ThrowIfCancellationRequested();
                segments = Diarize(clips[i], track, input.Tracks.Count(t => t.Speakers is DiarizedSpeakers) > 1, options, observer, stages);
                diarizedAudio[track.Id] = clips[i];
            }

            merged.Add(new TrackWords(track.Id, track.Offset, track.Speakers, words[i], segments));
        }

        return new TranscriptionResult(TranscriptMerger.Merge(merged), tracks, stages, diarizedAudio);
    }

    private IReadOnlyList<DiarSegment> Diarize(
        AudioClip clip, SourceTrack track, bool named, TranscriptionOptions options, ITranscriptionObserver observer, List<StageTiming> stages)
    {
        var settings = options.Diarization;
        var stage = new PipelineStage(StageKind.Diarization, named ? Path.GetFileName(track.Path) : null);
        return RunStage(stage, observer, stages, progress => engines.Diarizer.Diarize(clip, settings.NumClusters, settings.Threshold, progress));
    }

    private static async Task<T> RunStage<T>(
        PipelineStage stage, ITranscriptionObserver observer, List<StageTiming> stages, Func<IProgress<int>, Task<T>> run)
    {
        var elapsed = Stopwatch.StartNew();
        observer.StageStarted(stage);
        var result = await run(new StageProgress(observer, stage));
        Complete(stage, elapsed.Elapsed, observer, stages);
        return result;
    }

    private static T RunStage<T>(PipelineStage stage, ITranscriptionObserver observer, List<StageTiming> stages, Func<IProgress<int>, T> run)
    {
        var elapsed = Stopwatch.StartNew();
        observer.StageStarted(stage);
        var result = run(new StageProgress(observer, stage));
        Complete(stage, elapsed.Elapsed, observer, stages);
        return result;
    }

    private static void Complete(PipelineStage stage, TimeSpan elapsed, ITranscriptionObserver observer, List<StageTiming> stages)
    {
        stages.Add(new StageTiming(stage, elapsed));
        observer.StageCompleted(stage, elapsed);
    }

    // Reports synchronously: Progress<T> would post to a thread pool and could deliver after the stage completed.
    private sealed class StageProgress(ITranscriptionObserver observer, PipelineStage stage) : IProgress<int>
    {
        public void Report(int value) => observer.StageProgress(stage, value);
    }
}
