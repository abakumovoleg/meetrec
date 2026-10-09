using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;

namespace Meetrec.Asr;

public sealed class SpeechRecognizer(ITranscriber transcriber, IVad vad, RecognitionOptions options)
{
    public async Task<Recognition> RecognizeAsync(
        AudioClip clip, string language, IProgress<int>? progress, CancellationToken ct)
    {
        var detected = vad.Detect(clip.Samples, clip.SampleRate, options.Vad);
        var regions = SpeechLevel.DropQuiet(clip, detected, options.MinSpeechDbfs);
        var mask = SpeechMask.Build(regions, clip.Duration, options.Mask.Padding, options.Mask.MergeGap);
        var speech = mask.Regions.Aggregate(TimeSpan.Zero, (sum, r) => sum + (r.End - r.Start));
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

    private async Task<WindowTranscription> TranscribeWindowAsync(
        AudioClip clip, SpeechRegion window, string language, string? prompt, IProgress<int>? progress, CancellationToken ct)
    {
        var from = (int)(window.Start.TotalSeconds * clip.SampleRate);
        var to = Math.Min(clip.Samples.Length, (int)(window.End.TotalSeconds * clip.SampleRate));
        var result = await transcriber.TranscribeAsync(clip.Samples.AsMemory(from, to - from), clip.SampleRate, language, prompt, progress, ct);
        return result with { Words = [.. result.Words.Select(w => w with { Start = w.Start + window.Start, End = w.End + window.Start })] };
    }
}
