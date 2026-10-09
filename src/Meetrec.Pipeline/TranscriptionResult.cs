using Meetrec.Asr;
using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;

namespace Meetrec.Pipeline;

public sealed record TranscriptionResult(
    IReadOnlyList<Utterance> Utterances,
    IReadOnlyList<TrackRecognition> Tracks,
    IReadOnlyList<StageTiming> Stages,
    AudioClip SystemAudio);

public sealed record TrackRecognition(string Track, RecognitionStats Stats);

public sealed record StageTiming(PipelineStage Stage, TimeSpan Elapsed);
