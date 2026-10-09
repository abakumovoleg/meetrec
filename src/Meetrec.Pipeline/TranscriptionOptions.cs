using Meetrec.Asr;
using Meetrec.Core.Models;

namespace Meetrec.Pipeline;

public sealed record TranscriptionOptions(
    string Language, string WhisperModel, RecognitionOptions Recognition, DiarizationSettings Diarization);
