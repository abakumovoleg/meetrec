using Meetrec.Asr;
using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;

namespace Meetrec.Pipeline;

// Audio of diarized tracks is kept for the names dialog, which plays speakers' phrases from it.
public sealed record TranscriptionResult(
    Conversation Conversation,
    IReadOnlyList<TrackRecognition> Tracks,
    IReadOnlyList<StageTiming> Stages,
    IReadOnlyDictionary<string, AudioClip> DiarizedAudio);

public sealed record TrackRecognition(string Track, RecognitionStats Stats);

public sealed record StageTiming(PipelineStage Stage, TimeSpan Elapsed);
