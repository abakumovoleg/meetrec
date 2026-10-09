using Meetrec.Core.Transcript;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Meetrec.Asr;

public sealed class WhisperTranscriber : IDisposable
{
    public const string AutoLanguage = "auto";

    private readonly WhisperFactory _factory;

    private WhisperTranscriber(WhisperFactory factory) => _factory = factory;

    public static string? LoadedRuntime => RuntimeOptions.LoadedLibrary?.ToString();

    public static WhisperTranscriber Load(string modelPath) => new(WhisperFactory.FromPath(modelPath));

    public async Task<WhisperResult> TranscribeAsync(
        float[] samples, int sampleRate, string language, IProgress<int>? progress, CancellationToken ct)
    {
        // Whisper's DTW timestamps would place word starts better, but whisper.cpp drops all text after
        // the first 30 s window when DTW is on, so word bounds come from one-word segments instead.
        var builder = _factory.CreateBuilder()
            .WithLanguage(language)
            .WithTokenTimestamps()
            .SplitOnWord()
            .WithMaxSegmentLength(1);

        if (progress is not null)
        {
            builder = builder.WithProgressHandler(progress.Report);
        }

        await using var processor = builder.Build();

        var duration = TimeSpan.FromSeconds((double)samples.Length / sampleRate);
        var words = new List<Word>();
        string? detected = null;
        await foreach (var segment in processor.ProcessAsync(samples, ct))
        {
            detected ??= segment.Language;
            var text = segment.Text.Trim();
            if (text.Length == 0 || segment.Start >= duration)
            {
                continue;
            }

            words.Add(new Word(text, segment.Start, segment.End > duration ? duration : segment.End));
        }

        return new WhisperResult(words, detected);
    }

    public void Dispose() => _factory.Dispose();
}

public sealed record WhisperResult(IReadOnlyList<Word> Words, string? Language);
