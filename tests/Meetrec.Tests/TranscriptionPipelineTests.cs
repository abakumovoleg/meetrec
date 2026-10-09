using Meetrec.Asr;
using Meetrec.Core.Audio;
using Meetrec.Core.Models;
using Meetrec.Core.Transcript;
using Meetrec.Diarization;
using Meetrec.Pipeline;

namespace Meetrec.Tests;

public sealed class TranscriptionPipelineTests : IDisposable
{
    private const int SampleRate = 16_000;
    private const float MicLevel = 0.3f;
    private const float SystemLevel = 0.6f;

    private static readonly TranscriptionOptions Options = new(
        "ru", ModelCatalog.DefaultWhisper, RecognitionOptions.Default, DiarizationSettings.Choose(null, hasMicTrack: true, null));

    private readonly string _dir = Directory.CreateTempSubdirectory("meetrec-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task RunAsync_SessionWithOffset_MergesTracksOnSessionTime()
    {
        // Arrange
        WriteSession(offsetMs: 2_000);
        var transcriber = new FakeTranscriber();
        var diarizer = new FakeDiarizer(transcriber);
        var pipeline = new TranscriptionPipeline(new TranscriptionEngines(() => transcriber, new FakeVad(), diarizer));

        // Act
        var result = await pipeline.RunAsync(TranscribeInput.Resolve(_dir), Options, new RecordingObserver(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [(Speakers.Numbered(1), 1.2, "Здравствуйте."), (Speakers.Me, 3.0, "Привет.")],
            result.Utterances.Select(u => (u.Speaker, u.Start.TotalSeconds, u.Text)));
        Assert.Equal(["mic.wav", "system.wav"], result.Tracks.Select(t => t.Track));
        Assert.Equal(SystemLevel, result.SystemAudio.Samples[0]);
    }

    [Fact]
    public async Task RunAsync_ReleasesTranscriberBeforeDiarization()
    {
        // Arrange
        WriteSession(offsetMs: 0);
        var transcriber = new FakeTranscriber();
        var diarizer = new FakeDiarizer(transcriber);
        var pipeline = new TranscriptionPipeline(new TranscriptionEngines(() => transcriber, new FakeVad(), diarizer));

        // Act
        await pipeline.RunAsync(TranscribeInput.Resolve(_dir), Options, new RecordingObserver(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(diarizer.TranscriberWasDisposed);
    }

    [Fact]
    public async Task RunAsync_ReportsStagesInOrder()
    {
        // Arrange
        WriteSession(offsetMs: 0);
        var transcriber = new FakeTranscriber();
        var observer = new RecordingObserver();
        var pipeline = new TranscriptionPipeline(new TranscriptionEngines(() => transcriber, new FakeVad(), new FakeDiarizer(transcriber)));

        // Act
        var result = await pipeline.RunAsync(TranscribeInput.Resolve(_dir), Options, observer, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ["audio 2", "transcriber", "start Recognition mic.wav", "done Recognition mic.wav", "track mic.wav",
             "start Recognition system.wav", "done Recognition system.wav", "track system.wav", "start Diarization ", "done Diarization "],
            observer.Events.Where(e => !e.StartsWith("progress", StringComparison.Ordinal)));
        Assert.Equal(3, result.Stages.Count);
    }

    [Fact]
    public async Task RunAsync_MicWithoutSessionFile_WarnsAndMergesWithoutOffset()
    {
        // Arrange
        WriteTracks();
        var transcriber = new FakeTranscriber();
        var observer = new RecordingObserver();
        var pipeline = new TranscriptionPipeline(new TranscriptionEngines(() => transcriber, new FakeVad(), new FakeDiarizer(transcriber)));

        // Act
        var result = await pipeline.RunAsync(TranscribeInput.Resolve(_dir), Options, observer, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(observer.Events, e => e.StartsWith("warning", StringComparison.Ordinal));
        Assert.Equal(1.0, result.Utterances.Single(u => u.Speaker == Speakers.Me).Start.TotalSeconds);
    }

    private void WriteSession(int offsetMs)
    {
        WriteTracks();
        SessionFile.Write(Path.Combine(_dir, SessionFile.FileName), new Session(DateTimeOffset.UnixEpoch, "mic", "system", offsetMs, "0.1.0"));
    }

    // Each track has its own constant level, so that the fake transcriber can tell them apart.
    private void WriteTracks()
    {
        WavFile.WriteFloat32(Path.Combine(_dir, TranscribeInput.MicFileName), SampleRate, [WavFile.Constant(MicLevel, 10 * SampleRate)]);
        WavFile.WriteFloat32(Path.Combine(_dir, TranscribeInput.SystemFileName), SampleRate, [WavFile.Constant(SystemLevel, 10 * SampleRate)]);
    }

    private static Word W(string text, double start, double end) => new(text, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end));

    private sealed class FakeVad : IVad
    {
        public IReadOnlyList<SpeechRegion> Detect(float[] samples, int sampleRate, VadOptions options) =>
            [new SpeechRegion(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3))];
    }

    // Speech starts at 1 s, so the main pass starts at 0 and window time equals track time.
    private sealed class FakeTranscriber : ITranscriber
    {
        public bool Disposed { get; private set; }

        public Task<WindowTranscription> TranscribeAsync(
            ReadOnlyMemory<float> samples, int sampleRate, string language, string? prompt, IProgress<int>? progress, CancellationToken ct)
        {
            progress?.Report(100);
            IReadOnlyList<Word> words = samples.Span[0] == MicLevel
                ? [W("Привет.", 1.0, 1.5)]
                : [W("Здравствуйте.", 1.2, 2.0)];
            return Task.FromResult(new WindowTranscription(words, language));
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeDiarizer(FakeTranscriber transcriber) : IDiarizer
    {
        public bool TranscriberWasDisposed { get; private set; }

        public IReadOnlyList<DiarSegment> Diarize(AudioClip clip, int? numSpeakers, float threshold, IProgress<int>? progress)
        {
            TranscriberWasDisposed = transcriber.Disposed;
            progress?.Report(100);
            return [new DiarSegment(TimeSpan.Zero, clip.Duration, 0)];
        }
    }

    private sealed class RecordingObserver : ITranscriptionObserver
    {
        public List<string> Events { get; } = [];

        public void Warning(string message) => Events.Add($"warning {message}");

        public void AudioLoaded(TimeSpan duration, int tracks) => Events.Add($"audio {tracks}");

        public void TranscriberLoaded() => Events.Add("transcriber");

        public void StageStarted(PipelineStage stage) => Events.Add($"start {stage.Kind} {stage.Track}");

        public void StageProgress(PipelineStage stage, int percent) => Events.Add($"progress {stage.Kind} {percent}");

        public void StageCompleted(PipelineStage stage, TimeSpan elapsed) => Events.Add($"done {stage.Kind} {stage.Track}");

        public void TrackRecognized(string track, RecognitionStats stats) => Events.Add($"track {track}");
    }
}
