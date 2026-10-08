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
        return mask.Apply(words, SpeechMask.DefaultMaxSnapDistance);
    }
}
