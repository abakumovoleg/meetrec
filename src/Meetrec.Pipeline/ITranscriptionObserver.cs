using Meetrec.Asr;

namespace Meetrec.Pipeline;

public interface ITranscriptionObserver
{
    void Warning(string message);

    void AudioLoaded(TimeSpan duration, int tracks);

    void TranscriberLoaded();

    void StageStarted(PipelineStage stage);

    void StageProgress(PipelineStage stage, int percent);

    void StageCompleted(PipelineStage stage, TimeSpan elapsed);

    void TrackRecognized(string track, RecognitionStats stats);
}

public enum StageKind
{
    Recognition,
    Diarization,
}

public sealed record PipelineStage(StageKind Kind, string? Track = null);
