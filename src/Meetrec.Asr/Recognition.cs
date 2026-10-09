using Meetrec.Core.Transcript;

namespace Meetrec.Asr;

public sealed record Recognition(IReadOnlyList<Word> Words, RecognitionStats Stats);

public sealed record RecognitionStats(
    string? Language,
    int SpeechRegions,
    int QuietRegions,
    TimeSpan Speech,
    int WhisperWords,
    int DroppedWords,
    int RepairWindows);
