using Meetrec.Asr;
using Meetrec.Pipeline;

namespace Meetrec.Cli;

internal sealed class ConsoleTranscriptionObserver(TranscribeSettings settings) : ITranscriptionObserver
{
    private ConsoleStageProgress? _stage;

    public void Warning(string message) => Console.Error.WriteLine($"Внимание: {message}");

    public void AudioLoaded(TimeSpan duration, int tracks) =>
        Console.Error.WriteLine($"Запись: {Formatting.Duration(duration)}, {(tracks == 1 ? "одна дорожка" : "микрофон и системный звук")}");

    public void TranscriberLoaded() =>
        Console.Error.WriteLine($"Whisper: {settings.Model}, рантайм {WhisperTranscriber.LoadedRuntime ?? "?"}");

    public void StageStarted(PipelineStage stage) =>
        _stage = new ConsoleStageProgress(stage.Kind == StageKind.Diarization ? "Диаризация" : $"Распознавание {stage.Track}");

    public void StageProgress(PipelineStage stage, int percent) => _stage?.Report(percent);

    public void StageCompleted(PipelineStage stage, TimeSpan elapsed)
    {
        _stage?.Complete(elapsed);
        _stage = null;
    }

    public void TrackRecognized(string track, RecognitionStats stats) =>
        Console.Error.WriteLine(Diagnostics.Recognition(stats, settings.Language));
}
