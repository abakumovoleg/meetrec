using Meetrec.Core;
using Meetrec.Core.Transcript;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Meetrec.Asr;

public sealed class WhisperTranscriber : ITranscriber, IDisposable
{
    public const string AutoLanguage = "auto";

    private readonly WhisperFactory _factory;

    private WhisperTranscriber(WhisperFactory factory) => _factory = factory;

    public static string? LoadedRuntime => RuntimeOptions.LoadedLibrary?.ToString();

    public static WhisperTranscriber Load(string modelPath)
    {
        var factory = WhisperFactory.FromPath(modelPath);
        try
        {
            // The model itself is loaded lazily by the first builder; a broken file should fail here, not mid-recognition.
            factory.CreateBuilder();
            return new(factory);
        }
        catch (WhisperModelLoadException e)
        {
            factory.Dispose();
            throw new MeetrecException($"Не удалось загрузить модель Whisper {modelPath} — удалите файл, и он скачается заново: {e.Message}", e);
        }
    }

    public async Task<WindowTranscription> TranscribeAsync(
        ReadOnlyMemory<float> samples, int sampleRate, string language, string? prompt, IProgress<int>? progress, CancellationToken ct)
    {
        // Whisper's DTW timestamps would place word starts better, but whisper.cpp drops all text after
        // the first 30 s window when DTW is on, so word bounds come from one-word segments instead.
        var builder = _factory.CreateBuilder()
            .WithLanguage(language)
            .WithTokenTimestamps()
            .SplitOnWord()
            .WithMaxSegmentLength(1);

        if (prompt is not null)
        {
            builder = builder.WithPrompt(prompt);
        }

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

        return new WindowTranscription(words, detected);
    }

    public void Dispose() => _factory.Dispose();
}
