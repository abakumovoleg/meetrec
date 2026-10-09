using Meetrec.Core.Transcript;

namespace Meetrec.Asr;

public interface ITranscriber
{
    // Word timestamps are relative to the start of the samples.
    Task<WindowTranscription> TranscribeAsync(
        ReadOnlyMemory<float> samples, int sampleRate, string language, string? prompt, IProgress<int>? progress, CancellationToken ct);
}

public sealed record WindowTranscription(IReadOnlyList<Word> Words, string? Language);
