using System.Diagnostics;
using System.Globalization;
using Meetrec.Asr;
using Meetrec.Core;
using Meetrec.Core.Audio;
using Meetrec.Core.Models;
using Meetrec.Core.Transcript;

namespace Meetrec.Eval;

// Word timestamps against known truth: words and phrases spoken by `say -v Milena` (the only Russian voice that
// works) and glued together with known pauses. Text is synthetic, so the fixtures carry no personal data.
internal static class TimingCommand
{
    private const int SampleRate = AudioLoader.TargetSampleRate;

    private static readonly string[] WordTexts =
        ("Коллеги добрый день давайте начнём встречу сегодня обсудим перенос сервиса компенсаций на новую платформу "
        + "сначала расскажу про сроки потом про риски по срокам планируем закончить миграцию до конца ноября основной "
        + "риск это нагрузка на базу данных при пересчёте выплат поэтому предлагаю сделать пересчёт пачками и заранее "
        + "прогнать нагрузочное тестирование вопросы есть").Split(' ');

    private static readonly string[] PhraseTexts =
    [
        "Коллеги, добрый день. Давайте начнём встречу.",
        "Сегодня обсудим перенос сервиса компенсаций на новую платформу.",
        "По срокам планируем закончить миграцию до конца ноября.",
        "Основной риск — это нагрузка на базу данных при пересчёте выплат.",
    ];

    // Varied pauses between words, as in live speech, with a longer one after every eighth word.
    private static readonly double[] WordGaps = [0.12, 0.25, 0.18, 0.4, 0.15, 0.3, 0.22, 1.3];

    private const double PhraseGap = 1.5;

    public static async Task<int> ExecuteAsync(Args args)
    {
        var fixtures = Path.GetFullPath(args.At(0, "fixtures"));
        var model = args.Get("model", ModelCatalog.DefaultWhisper);
        var options = OptionsFile.Read(args);
        var words = Generate(Path.Combine(fixtures, "words"), WordTexts);
        var phrases = Generate(Path.Combine(fixtures, "phrases"), PhraseTexts);
        var (wordsAudio, wordTruth) = Build(words.Zip(WordTexts), i => WordGaps[i % WordGaps.Length]);
        var (phrasesAudio, phraseTruth) = Build(phrases.Zip(PhraseTexts), _ => PhraseGap);

        var store = await Models.EnsureAsync(Models.ForRecognition(model));
        using var whisper = WhisperTranscriber.Load(store.PathOf(ModelCatalog.WhisperModel(model)));
        var recognizer = new SpeechRecognizer(whisper, new SileroVad(store.PathOf(ModelCatalog.SileroVad)), options);

        Console.WriteLine($"{model}, рантайм {WhisperTranscriber.LoadedRuntime}");
        var elapsed = Stopwatch.StartNew();
        var recognizedWords = await RecognizeAsync(recognizer, wordsAudio);
        Console.WriteLine($"слова: {wordsAudio.Length / (double)SampleRate:0.0} с аудио, {elapsed.Elapsed.TotalSeconds:0.0} с, {recognizedWords.Count} слов");
        ReportWords(wordTruth, recognizedWords);

        elapsed.Restart();
        var recognizedPhrases = await RecognizeAsync(recognizer, phrasesAudio);
        Console.WriteLine($"фразы: {phrasesAudio.Length / (double)SampleRate:0.0} с аудио, {elapsed.Elapsed.TotalSeconds:0.0} с");
        ReportPhrases(phraseTruth, recognizedPhrases);
        return 0;
    }

    private static async Task<IReadOnlyList<Word>> RecognizeAsync(SpeechRecognizer recognizer, float[] audio) =>
        (await recognizer.RecognizeAsync(new AudioClip(audio, SampleRate), "ru", null, CancellationToken.None)).Words;

    private static string[] Generate(string directory, string[] texts)
    {
        Directory.CreateDirectory(directory);
        var paths = texts.Select((_, i) => Path.Combine(directory, $"{i:00}.wav")).ToArray();
        for (var i = 0; i < texts.Length; i++)
        {
            if (File.Exists(paths[i]))
            {
                continue;
            }

            if (!OperatingSystem.IsMacOS())
            {
                throw new MeetrecException($"Нет {paths[i]}: фикстуры генерируются через say, только на macOS");
            }

            using var say = Process.Start("say", ["-v", "Milena", "-o", paths[i], "--file-format=WAVE", "--data-format=LEI16@16000", texts[i]]);
            say.WaitForExit();
            if (say.ExitCode != 0)
            {
                throw new MeetrecException($"say завершился с кодом {say.ExitCode} на «{texts[i]}»");
            }
        }

        return paths;
    }

    private static (float[] Audio, List<Truth> Truth) Build(IEnumerable<(string File, string Text)> parts, Func<int, double> gapAfter)
    {
        var audio = new List<float>(new float[SampleRate / 2]);
        var truth = new List<Truth>();
        var i = 0;
        foreach (var (file, text) in parts)
        {
            var samples = AudioLoader.LoadMono16k(file).Samples;
            var (from, to) = Trim(samples);
            var start = audio.Count / (double)SampleRate;
            audio.AddRange(samples[from..to]);
            truth.Add(new Truth(text, start, audio.Count / (double)SampleRate));
            audio.AddRange(new float[(int)(gapAfter(i++) * SampleRate)]);
        }

        audio.AddRange(new float[SampleRate]);
        return ([.. audio], truth);
    }

    // `say` pads the speech with silence; truth starts and ends at the first and last frame above 3 % of the peak.
    private static (int From, int To) Trim(float[] samples)
    {
        const int frame = 160;
        var peak = samples.Max(MathF.Abs);
        bool Loud(int f) => samples.AsSpan(f * frame, Math.Min(frame, samples.Length - f * frame)).ToArray().Max(MathF.Abs) > peak * 0.03;
        var frames = samples.Length / frame;
        var first = Enumerable.Range(0, frames).First(Loud);
        var last = Enumerable.Range(0, frames).Last(Loud);
        return (first * frame, Math.Min(samples.Length, (last + 1) * frame));
    }

    private static void ReportWords(List<Truth> truth, IReadOnlyList<Word> recognized)
    {
        var pairs = Align([.. truth.Select(t => Normalize(t.Text))], [.. recognized.Select(w => Normalize(w.Text))]);
        var startErrors = pairs.Select(p => Math.Abs(recognized[p.J].Start.TotalSeconds - truth[p.I].Start)).Order().ToList();
        var endErrors = pairs.Select(p => Math.Abs(recognized[p.J].End.TotalSeconds - truth[p.I].End)).Order().ToList();
        var covered = pairs.Count(p => Overlap(truth[p.I], recognized[p.J]) > 0.5 * (truth[p.I].End - truth[p.I].Start));
        Console.WriteLine(Invariant($"  совпало {pairs.Count}/{truth.Count}; ошибка начала: медиана {Quantile(startErrors, 0.5):0.000}, p90 {Quantile(startErrors, 0.9):0.000}, max {startErrors.LastOrDefault():0.000}; ")
            + Invariant($"конца: медиана {Quantile(endErrors, 0.5):0.000}, p90 {Quantile(endErrors, 0.9):0.000}, max {endErrors.LastOrDefault():0.000}; ")
            + $"начало ≤ 0,2 с у {Percent(startErrors.Count(e => e <= 0.2), startErrors.Count)} %, перекрытие > 50 % у {Percent(covered, pairs.Count)} %");
        Console.WriteLine("  распознано: " + string.Join(" ", recognized.Take(14).Select(w => Invariant($"{w.Text}[{w.Start.TotalSeconds:0.00}-{w.End.TotalSeconds:0.00}]"))));
        Console.WriteLine("  эталон:     " + string.Join(" ", truth.Take(14).Select(t => Invariant($"{t.Text}[{t.Start:0.00}-{t.End:0.00}]"))));
    }

    private static void ReportPhrases(List<Truth> truth, IReadOnlyList<Word> recognized)
    {
        foreach (var t in truth)
        {
            var inside = recognized.Where(w => w.End.TotalSeconds > t.Start - 0.75 && w.Start.TotalSeconds < t.End + 0.75).ToList();
            Console.WriteLine(inside.Count == 0
                ? Invariant($"  [{t.Start:0.00}-{t.End:0.00}] ПРОПУЩЕНА")
                : Invariant($"  эталон [{t.Start:0.00}-{t.End:0.00}] распознано [{inside[0].Start.TotalSeconds:0.00}-{inside[^1].End.TotalSeconds:0.00}] ")
                    + Invariant($"Δначала {inside[0].Start.TotalSeconds - t.Start:+0.00;-0.00} Δконца {inside[^1].End.TotalSeconds - t.End:+0.00;-0.00} | ")
                    + string.Join(' ', inside.Select(w => w.Text)));
        }
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static string Normalize(string text) =>
        new([.. text.ToLower(CultureInfo.InvariantCulture).Replace('ё', 'е').Where(char.IsLetterOrDigit)]);

    private static double Overlap(Truth t, Word w) =>
        Math.Max(0, Math.Min(t.End, w.End.TotalSeconds) - Math.Max(t.Start, w.Start.TotalSeconds));

    private static double Quantile(List<double> sorted, double q) =>
        sorted.Count == 0 ? double.NaN : sorted[(int)Math.Min(sorted.Count - 1, Math.Floor(q * sorted.Count))];

    private static int Percent(int part, int total) => (int)Math.Round(part * 100.0 / Math.Max(1, total));

    // Longest common subsequence of normalised texts.
    private static List<(int I, int J)> Align(List<string> a, List<string> b)
    {
        var lengths = new int[a.Count + 1, b.Count + 1];
        for (var i = a.Count - 1; i >= 0; i--)
        {
            for (var j = b.Count - 1; j >= 0; j--)
            {
                lengths[i, j] = a[i] == b[j] ? lengths[i + 1, j + 1] + 1 : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        var pairs = new List<(int, int)>();
        for (int i = 0, j = 0; i < a.Count && j < b.Count;)
        {
            if (a[i] == b[j])
            {
                pairs.Add((i++, j++));
            }
            else if (lengths[i + 1, j] >= lengths[i, j + 1])
            {
                i++;
            }
            else
            {
                j++;
            }
        }

        return pairs;
    }

    private sealed record Truth(string Text, double Start, double End);
}
