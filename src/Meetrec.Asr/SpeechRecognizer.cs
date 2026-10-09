using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;

namespace Meetrec.Asr;

public sealed class SpeechRecognizer(WhisperTranscriber whisper, SileroVad vad)
{
    public async Task<Recognition> RecognizeAsync(
        AudioClip clip, string language, IProgress<int>? progress, CancellationToken ct)
    {
        var detected = vad.Detect(clip.Samples, clip.SampleRate);
        var regions = SpeechLevel.DropQuiet(clip, detected, SpeechLevel.DefaultMinDbfs);
        var mask = SpeechMask.Build(regions, clip.Duration, SpeechMask.DefaultPadding, SpeechMask.DefaultMergeGap);
        var speech = mask.Regions.Aggregate(TimeSpan.Zero, (sum, r) => sum + (r.End - r.Start));
        if (mask.IsEmpty)
        {
            progress?.Report(100);
            return new Recognition([], new RecognitionStats(null, 0, detected.Count, TimeSpan.Zero, 0, 0, 0));
        }

        // Pauses are deliberately not cut out before Whisper: on real calls condensed audio lost phrases and
        // degraded the text, so VAD is applied to the result instead.
        var result = await whisper.TranscribeAsync(clip.Samples, clip.SampleRate, language, progress, ct);
        var words = result.Words;

        var sparse = SpeechRepair.FindSparse(mask.Regions, words, SpeechRepair.DefaultMinWordsPerSecond, SpeechRepair.DefaultMaxWordLength);
        var windows = SpeechRepair.Windows(sparse, clip.Duration, SpeechRepair.DefaultGroupGap, SpeechRepair.DefaultWindowPadding);
        if (sparse.Count > 0)
        {
            var repaired = new List<Word>();
            foreach (var window in windows)
            {
                repaired.AddRange(await TranscribeWindowAsync(clip, window, language, ct));
            }

            words = SpeechRepair.Replace(words, repaired, sparse);
        }

        var masked = mask.Apply(words, SpeechMask.DefaultMaxSnapDistance);
        var stats = new RecognitionStats(
            result.Language, mask.Regions.Count, detected.Count - regions.Count, speech, words.Count, words.Count - masked.Count, sparse.Count > 0 ? windows.Count : 0);
        return new Recognition(masked, stats);
    }

    private async Task<IEnumerable<Word>> TranscribeWindowAsync(AudioClip clip, SpeechRegion window, string language, CancellationToken ct)
    {
        var from = (int)(window.Start.TotalSeconds * clip.SampleRate);
        var to = Math.Min(clip.Samples.Length, (int)(window.End.TotalSeconds * clip.SampleRate));
        var result = await whisper.TranscribeAsync(clip.Samples[from..to], clip.SampleRate, language, null, ct);
        return result.Words.Select(w => w with { Start = w.Start + window.Start, End = w.End + window.Start });
    }
}
