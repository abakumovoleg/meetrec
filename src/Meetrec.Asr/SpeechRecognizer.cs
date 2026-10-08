using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;

namespace Meetrec.Asr;

public sealed class SpeechRecognizer(WhisperTranscriber whisper, SileroVad vad)
{
    public async Task<IReadOnlyList<Word>> RecognizeAsync(
        AudioClip clip, string language, IProgress<int>? progress, CancellationToken ct)
    {
        var mask = SpeechMask.Build(
            vad.Detect(clip.Samples, clip.SampleRate), clip.Duration, SpeechMask.DefaultPadding, SpeechMask.DefaultMergeGap);
        if (mask.IsEmpty)
        {
            progress?.Report(100);
            return [];
        }

        // Pauses are deliberately not cut out before Whisper: on real calls condensed audio lost phrases and
        // degraded the text, so VAD is applied to the result instead.
        var words = await whisper.TranscribeAsync(clip.Samples, clip.SampleRate, language, progress, ct);

        var sparse = SpeechRepair.FindSparse(mask.Regions, words, SpeechRepair.DefaultMinWordsPerSecond, SpeechRepair.DefaultMaxWordLength);
        if (sparse.Count > 0)
        {
            var repaired = new List<Word>();
            foreach (var window in SpeechRepair.Windows(sparse, clip.Duration, SpeechRepair.DefaultGroupGap, SpeechRepair.DefaultWindowPadding))
            {
                repaired.AddRange(await TranscribeWindowAsync(clip, window, language, ct));
            }

            words = SpeechRepair.Replace(words, repaired, sparse);
        }

        return mask.Apply(words, SpeechMask.DefaultMaxSnapDistance);
    }

    private async Task<IEnumerable<Word>> TranscribeWindowAsync(AudioClip clip, SpeechRegion window, string language, CancellationToken ct)
    {
        var from = (int)(window.Start.TotalSeconds * clip.SampleRate);
        var to = Math.Min(clip.Samples.Length, (int)(window.End.TotalSeconds * clip.SampleRate));
        var words = await whisper.TranscribeAsync(clip.Samples[from..to], clip.SampleRate, language, null, ct);
        return words.Select(w => w with { Start = w.Start + window.Start, End = w.End + window.Start });
    }
}
