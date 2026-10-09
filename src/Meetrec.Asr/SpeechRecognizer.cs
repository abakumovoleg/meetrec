using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;

namespace Meetrec.Asr;

public sealed class SpeechRecognizer(ITranscriber transcriber, IVad vad, RecognitionOptions options)
{
    public async Task<Recognition> RecognizeAsync(
        AudioClip clip, string language, IProgress<int>? progress, CancellationToken ct)
    {
        var (detected, regions, mask, speech) = DetectSpeech(clip);
        if (mask.IsEmpty)
        {
            progress?.Report(100);
            return new Recognition([], new RecognitionStats(null, 0, detected.Count, TimeSpan.Zero, 0, 0, 0, 0));
        }

        var prompt = options.Prompts.GetValueOrDefault(language);

        // Pauses are deliberately not cut out before Whisper: on real calls condensed audio lost phrases and
        // degraded the text, so VAD is applied to the result instead.
        var result = await TranscribeWindowAsync(
            clip, new SpeechRegion(mask.Lead(options.Lead), clip.Duration), language, prompt, progress, ct);
        var words = Hallucinations.Remove(result.Words, options.Hallucinations);
        var hallucinations = result.Words.Count - words.Count;

        var repair = options.Repair;
        var sparse = SpeechRepair.FindSparse(mask.Regions, words, repair.MinWordsPerSecond, repair.MaxWordLength);
        var windows = SpeechRepair.Windows(sparse, clip.Duration, repair.GroupGap, repair.WindowPadding);
        if (sparse.Count > 0)
        {
            var repaired = new List<Word>();
            foreach (var window in windows)
            {
                var raw = (await TranscribeWindowAsync(clip, window, language, prompt, null, ct)).Words;
                var kept = Hallucinations.Remove(raw, options.Hallucinations);
                hallucinations += raw.Count - kept.Count;
                repaired.AddRange(kept);
            }

            words = SpeechRepair.Replace(words, repaired, sparse);
        }

        var masked = mask.Apply(words, options.Mask.MaxSnapDistance);
        var stats = new RecognitionStats(
            result.Language, mask.Regions.Count, detected.Count - regions.Count, speech, words.Count, hallucinations, words.Count - masked.Count, sparse.Count > 0 ? windows.Count : 0);
        return new Recognition(masked, stats);
    }

    // For a track of one person who is silent most of the time: only windows around speech go to Whisper. A pass over
    // the whole track would spend Whisper's time on silence and invent words on the faint noise in it.
    public async Task<Recognition> RecognizeSegmentsAsync(
        AudioClip clip, string language, IProgress<int>? progress, CancellationToken ct)
    {
        var (detected, regions, mask, speech) = DetectSpeech(clip);
        if (mask.IsEmpty)
        {
            progress?.Report(100);
            return new Recognition([], new RecognitionStats(null, 0, detected.Count, TimeSpan.Zero, 0, 0, 0, 0));
        }

        var prompt = options.Prompts.GetValueOrDefault(language);
        var windows = SpeechSegments.Windows(mask.Regions, clip.Duration, options.Segments.Gap, options.Segments.Padding);
        var packs = SpeechSegments.Pack(windows, options.Segments.PackLength, options.Segments.Separator);
        var total = windows.Aggregate(TimeSpan.Zero, (sum, w) => sum + (w.End - w.Start));
        var done = TimeSpan.Zero;
        var words = new List<Word>();
        var whisperWords = 0;
        string? detectedLanguage = null;
        foreach (var pack in packs)
        {
            var result = pack.Count == 1
                ? await TranscribeWindowAsync(clip, pack[0], language, prompt, null, ct)
                : await TranscribePackAsync(clip, pack, language, prompt, ct);
            detectedLanguage ??= result.Language;
            whisperWords += result.Words.Count;
            words.AddRange(Hallucinations.Remove(result.Words, options.Hallucinations));
            done += pack.Aggregate(TimeSpan.Zero, (sum, w) => sum + (w.End - w.Start));
            progress?.Report((int)(100 * done / total));
        }

        var masked = mask.Apply(words, options.Mask.MaxSnapDistance);
        var stats = new RecognitionStats(
            detectedLanguage, mask.Regions.Count, detected.Count - regions.Count, speech, words.Count, whisperWords - words.Count, words.Count - masked.Count, 0, windows.Count);
        return new Recognition(masked, stats);
    }

    private (IReadOnlyList<SpeechRegion> Detected, IReadOnlyList<SpeechRegion> Regions, SpeechMask Mask, TimeSpan Speech) DetectSpeech(AudioClip clip)
    {
        var detected = vad.Detect(clip.Samples, clip.SampleRate, options.Vad);
        var regions = SpeechLevel.DropQuiet(clip, detected, options.MinSpeechDbfs);
        var mask = SpeechMask.Build(regions, clip.Duration, options.Mask.Padding, options.Mask.MergeGap);
        return (detected, regions, mask, mask.Regions.Aggregate(TimeSpan.Zero, (sum, r) => sum + (r.End - r.Start)));
    }

    // Windows are laid out one after another with silence between them. A word goes back to the window its middle
    // falls into; a word in the silence goes to the next window: Whisper starts words early, and windows already end
    // with padding after speech.
    private async Task<WindowTranscription> TranscribePackAsync(
        AudioClip clip, IReadOnlyList<SpeechRegion> pack, string language, string? prompt, CancellationToken ct)
    {
        var separator = (int)(options.Segments.Separator.TotalSeconds * clip.SampleRate);
        var parts = new List<(TimeSpan At, SpeechRegion Window)>();
        var samples = new List<float>();
        foreach (var window in pack)
        {
            var from = (int)(window.Start.TotalSeconds * clip.SampleRate);
            var to = Math.Min(clip.Samples.Length, (int)(window.End.TotalSeconds * clip.SampleRate));
            parts.Add((TimeSpan.FromSeconds((double)samples.Count / clip.SampleRate), window));
            samples.AddRange(clip.Samples.AsSpan(from, to - from));
            samples.AddRange(new float[separator]);
        }

        var result = await transcriber.TranscribeAsync(samples.ToArray(), clip.SampleRate, language, prompt, null, ct);
        var words = result.Words.Select(w =>
        {
            var middle = (w.Start + w.End) / 2;
            var index = parts.FindIndex(p => middle < p.At + (p.Window.End - p.Window.Start));
            var (at, window) = parts[index < 0 ? parts.Count - 1 : index];
            var length = window.End - window.Start;
            return w with { Start = window.Start + Clamp(w.Start - at, length), End = window.Start + Clamp(w.End - at, length) };
        });
        return result with { Words = [.. words] };
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan max) => value < TimeSpan.Zero ? TimeSpan.Zero : value > max ? max : value;

    private async Task<WindowTranscription> TranscribeWindowAsync(
        AudioClip clip, SpeechRegion window, string language, string? prompt, IProgress<int>? progress, CancellationToken ct)
    {
        var from = (int)(window.Start.TotalSeconds * clip.SampleRate);
        var to = Math.Min(clip.Samples.Length, (int)(window.End.TotalSeconds * clip.SampleRate));
        var result = await transcriber.TranscribeAsync(clip.Samples.AsMemory(from, to - from), clip.SampleRate, language, prompt, progress, ct);
        return result with { Words = [.. result.Words.Select(w => w with { Start = w.Start + window.Start, End = w.End + window.Start })] };
    }
}
