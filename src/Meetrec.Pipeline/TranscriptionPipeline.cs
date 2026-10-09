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
        if (input.HasMicTrack && input.Session is null)
        {
            observer.Warning($"нет {SessionFile.FileName}, дорожки склеиваются без сдвига.");
        }

        var mic = input.MicPath is null ? null : AudioLoader.LoadMono16k(input.MicPath);
        var system = AudioLoader.LoadMono16k(input.SystemPath);
        observer.AudioLoaded(system.Duration, mic is null ? 1 : 2);

        var tracks = new List<TrackRecognition>();
        var stages = new List<StageTiming>();
        IReadOnlyList<Word> micWords = [];
        IReadOnlyList<Word> systemWords;
        using (var transcriber = engines.LoadTranscriber())
        {
            observer.TranscriberLoaded();
            var recognizer = new SpeechRecognizer(transcriber, engines.Vad, options.Recognition);
            if (mic is not null)
            {
                micWords = await RecognizeAsync(recognizer, mic, TranscribeInput.MicFileName, options.Language, observer, tracks, stages, ct);
            }

            systemWords = await RecognizeAsync(
                recognizer, system, Path.GetFileName(input.SystemPath), options.Language, observer, tracks, stages, ct);
        }

        ct.ThrowIfCancellationRequested();
        var diarization = options.Diarization;
        var segments = RunStage(new PipelineStage(StageKind.Diarization), observer, stages, progress =>
            engines.Diarizer.Diarize(system, diarization.NumClusters, diarization.Threshold, progress));

        var utterances = TranscriptMerger.Merge(micWords, systemWords, segments, input.OffsetMs);
        return new TranscriptionResult(utterances, tracks, stages, system);
    }

    private static async Task<IReadOnlyList<Word>> RecognizeAsync(
        SpeechRecognizer recognizer,
        AudioClip clip,
        string track,
        string language,
        ITranscriptionObserver observer,
        List<TrackRecognition> tracks,
        List<StageTiming> stages,
        CancellationToken ct)
    {
        var recognition = await RunStage(new PipelineStage(StageKind.Recognition, track), observer, stages, progress =>
            recognizer.RecognizeAsync(clip, language, progress, ct));
        tracks.Add(new TrackRecognition(track, recognition.Stats));
        observer.TrackRecognized(track, recognition.Stats);
        return recognition.Words;
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
