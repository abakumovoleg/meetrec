using Meetrec.Asr;
using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;

namespace Meetrec.Tests;

public class SpeechRecognizerTests
{
    private const int SampleRate = 16_000;

    private static readonly RecognitionOptions Options = RecognitionOptions.Default;

    [Fact]
    public async Task RecognizeAsync_OnlyQuietRegions_DoesNotCallTranscriber()
    {
        // Arrange
        var clip = Clip(30, quiet: Region(4, 7));
        var transcriber = new FakeTranscriber(clip, _ => []);
        var recognizer = new SpeechRecognizer(transcriber, new FakeVad(Region(5, 6)), Options);

        // Act
        var result = await recognizer.RecognizeAsync(clip, "ru", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(transcriber.Calls);
        Assert.Empty(result.Words);
        Assert.Equal(new RecognitionStats(null, 0, 1, TimeSpan.Zero, 0, 0, 0, 0), result.Stats);
    }

    [Fact]
    public async Task RecognizeAsync_MainPass_StartsLeadBeforeFirstSpeechAndShiftsTimestampsBack()
    {
        // Arrange
        var clip = Clip(30);
        Word[] said = [W("Привет,", 10.2, 10.6), W("коллеги.", 10.7, 11.5)];
        var transcriber = new FakeTranscriber(clip, _ => said);
        var recognizer = new SpeechRecognizer(transcriber, new FakeVad(Region(10, 12)), Options);

        // Act
        var result = await recognizer.RecognizeAsync(clip, "ru", null, TestContext.Current.CancellationToken);

        // Assert
        var call = Assert.Single(transcriber.Calls);
        Assert.Equal(Region(10 - 0.25 - 1, 30), call.Window);
        Assert.Equal(Options.Prompts["ru"], call.Prompt);
        Assert.Equal(said, result.Words);
    }

    [Fact]
    public async Task RecognizeAsync_LanguageWithoutPrompt_PassesNoPrompt()
    {
        // Arrange
        var clip = Clip(30);
        var transcriber = new FakeTranscriber(clip, _ => [W("Hello", 10.2, 10.6), W("there.", 10.7, 11.5)]);
        var recognizer = new SpeechRecognizer(transcriber, new FakeVad(Region(10, 12)), Options);

        // Act
        await recognizer.RecognizeAsync(clip, "auto", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(Assert.Single(transcriber.Calls).Prompt);
    }

    [Fact]
    public async Task RecognizeAsync_RegionWithoutWords_IsTranscribedAgainInItsOwnWindow()
    {
        // Arrange
        var clip = Clip(60);
        Word[] first = [W("Так,", 10.2, 10.5), W("давайте.", 10.6, 11.2)];
        Word[] second = [W("Да,", 40.2, 40.5), W("хорошо.", 40.6, 41.0)];
        var transcriber = new FakeTranscriber(clip, window => window.Start < Seconds(10) ? first : second);
        var recognizer = new SpeechRecognizer(transcriber, new FakeVad(Region(10, 12), Region(40, 42)), Options);

        // Act
        var result = await recognizer.RecognizeAsync(clip, "ru", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, transcriber.Calls.Count);
        Assert.Equal(Region(40 - 0.25 - 1, 42 + 0.25 + 1), transcriber.Calls[1].Window);
        Assert.Equal([.. first, .. second], result.Words);
        Assert.Equal(1, result.Stats.RepairWindows);
    }

    [Fact]
    public async Task RecognizeAsync_RepairedWindow_IsFilteredForHallucinations()
    {
        // Arrange
        var clip = Clip(60);
        Word[] first = [W("Так,", 10.2, 10.5), W("давайте.", 10.6, 11.2)];
        Word[] second = [W("그래요.", 40.0, 40.3), W("Да.", 40.4, 40.8)];
        var transcriber = new FakeTranscriber(clip, window => window.Start < Seconds(10) ? first : second);
        var recognizer = new SpeechRecognizer(transcriber, new FakeVad(Region(10, 12), Region(40, 42)), Options);

        // Act
        var result = await recognizer.RecognizeAsync(clip, "ru", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Так,", "давайте.", "Да."], result.Words.Select(w => w.Text));
        Assert.Equal(1, result.Stats.Hallucinations);
    }

    [Fact]
    public async Task RecognizeAsync_WordsFarFromSpeech_AreDroppedAndCounted()
    {
        // Arrange
        var clip = Clip(60, quiet: Region(20, 25));
        Word[] speech = [W("Так,", 10.2, 10.5), W("давайте.", 10.6, 11.2)];
        var transcriber = new FakeTranscriber(clip, _ => [.. speech, W("Спасибо.", 21.5, 22.0)]);
        var recognizer = new SpeechRecognizer(transcriber, new FakeVad(Region(10, 12), Region(21, 23)), Options);

        // Act
        var result = await recognizer.RecognizeAsync(clip, "ru", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(speech, result.Words);
        Assert.Equal(new RecognitionStats("ru", 1, 1, Seconds(2.5), 3, 0, 1, 0), result.Stats);
    }

    [Fact]
    public async Task RecognizeSegmentsAsync_NoSpeech_DoesNotCallTranscriber()
    {
        // Arrange
        var clip = Clip(30, quiet: Region(4, 7));
        var transcriber = new PackTranscriber(_ => []);
        var recognizer = new SpeechRecognizer(transcriber, new FakeVad(Region(5, 6)), Options);

        // Act
        var result = await recognizer.RecognizeSegmentsAsync(clip, "ru", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(transcriber.Lengths);
        Assert.Empty(result.Words);
    }

    [Fact]
    public async Task RecognizeSegmentsAsync_SpeechWindows_AreTranscribedAsOnePackAndMappedBack()
    {
        // Arrange: windows 9.25–12.75 and 59.25–62.75 (VAD region + mask padding 0.25 + window padding 0.5) lie in
        // the pack at 0 and at 3.5 + 1 s of separator.
        var clip = Clip(120);
        var transcriber = new PackTranscriber(_ => [W("Так,", 0.5, 0.9), W("хорошо.", 5.0, 5.5)]);
        var recognizer = new SpeechRecognizer(transcriber, new FakeVad(Region(10, 12), Region(60, 62)), Options);

        // Act
        var result = await recognizer.RecognizeSegmentsAsync(clip, "ru", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([(3.5 + 1 + 3.5 + 1) * SampleRate], transcriber.Lengths);
        Assert.Equal([W("Так,", 9.75, 10.15), W("хорошо.", 59.75, 60.25)], result.Words);
        Assert.Equal(2, result.Stats.SpeechWindows);
    }

    [Fact]
    public async Task RecognizeSegmentsAsync_WordInSeparator_GoesToNextWindow()
    {
        // Arrange
        var clip = Clip(120);
        var transcriber = new PackTranscriber(_ => [W("Да,", 3.9, 4.1), W("хорошо.", 5.0, 5.5)]);
        var recognizer = new SpeechRecognizer(transcriber, new FakeVad(Region(10, 12), Region(60, 62)), Options);

        // Act
        var result = await recognizer.RecognizeSegmentsAsync(clip, "ru", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([W("Да,", 59.75, 59.75), W("хорошо.", 59.75, 60.25)], result.Words);
    }

    [Fact]
    public async Task RecognizeSegmentsAsync_Hallucinations_AreRemovedAndCounted()
    {
        // Arrange
        var clip = Clip(60);
        var transcriber = new PackTranscriber(_ => [W("그래요.", 0.5, 0.8), W("Да.", 1.0, 1.4)]);
        var recognizer = new SpeechRecognizer(transcriber, new FakeVad(Region(10, 12)), Options);

        // Act
        var result = await recognizer.RecognizeSegmentsAsync(clip, "ru", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Да."], result.Words.Select(w => w.Text));
        Assert.Equal(1, result.Stats.Hallucinations);
    }

    [Fact]
    public async Task RecognizeSegmentsAsync_ZeroPackLength_TranscribesEachWindowOnItsOwn()
    {
        // Arrange
        var clip = Clip(120);
        var options = Options with { Segments = Options.Segments with { PackLength = TimeSpan.Zero } };
        var transcriber = new PackTranscriber(_ => [W("Да.", 1.0, 1.4)]);
        var recognizer = new SpeechRecognizer(transcriber, new FakeVad(Region(10, 12), Region(60, 62)), options);

        // Act
        var result = await recognizer.RecognizeSegmentsAsync(clip, "ru", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([3.5 * SampleRate, 3.5 * SampleRate], transcriber.Lengths);
        Assert.Equal([W("Да.", 10.25, 10.65), W("Да.", 60.25, 60.65)], result.Words);
    }

    private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);

    private static SpeechRegion Region(double start, double end) => new(Seconds(start), Seconds(end));

    private static Word W(string text, double start, double end) => new(text, Seconds(start), Seconds(end));

    // Loud everywhere except the quiet regions. Each sample encodes its own index, so that the fake transcriber can
    // tell which part of the clip it was given.
    private static AudioClip Clip(double seconds, params SpeechRegion[] quiet)
    {
        var samples = new float[(int)(seconds * SampleRate)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = Encode(i, samples.Length);
        }

        foreach (var region in quiet)
        {
            var from = (int)(region.Start.TotalSeconds * SampleRate);
            Array.Clear(samples, from, (int)(region.End.TotalSeconds * SampleRate) - from);
        }

        return new AudioClip(samples, SampleRate);
    }

    private static float Encode(int index, int count) => (float)(0.5 + 0.5 * index / count);

    private static int Decode(float sample, int count) => (int)Math.Round((sample - 0.5) * 2 * count);

    private sealed class FakeVad(params SpeechRegion[] regions) : IVad
    {
        public IReadOnlyList<SpeechRegion> Detect(float[] samples, int sampleRate, VadOptions options) => regions;
    }

    // Answers with fixed words in the time of whatever samples it is given, like Whisper on a pack of windows.
    private sealed class PackTranscriber(Func<int, IReadOnlyList<Word>> said) : ITranscriber
    {
        public List<double> Lengths { get; } = [];

        public Task<WindowTranscription> TranscribeAsync(
            ReadOnlyMemory<float> samples, int sampleRate, string language, string? prompt, IProgress<int>? progress, CancellationToken ct)
        {
            Lengths.Add(samples.Length);
            return Task.FromResult(new WindowTranscription(said(samples.Length), "ru"));
        }

        public void Dispose()
        {
        }
    }

    // Answers with the words said inside the window, in window-relative time like Whisper.
    private sealed class FakeTranscriber(AudioClip clip, Func<SpeechRegion, IReadOnlyList<Word>> said) : ITranscriber
    {
        public List<(SpeechRegion Window, string? Prompt)> Calls { get; } = [];

        public Task<WindowTranscription> TranscribeAsync(
            ReadOnlyMemory<float> samples, int sampleRate, string language, string? prompt, IProgress<int>? progress, CancellationToken ct)
        {
            var start = Seconds((double)Decode(samples.Span[0], clip.Samples.Length) / sampleRate);
            var window = new SpeechRegion(start, start + Seconds((double)samples.Length / sampleRate));
            Calls.Add((window, prompt));

            var words = said(window)
                .Where(w => w.Start >= window.Start && w.End <= window.End)
                .Select(w => w with { Start = w.Start - start, End = w.End - start });
            return Task.FromResult(new WindowTranscription([.. words], "ru"));
        }

        public void Dispose()
        {
        }
    }
}
