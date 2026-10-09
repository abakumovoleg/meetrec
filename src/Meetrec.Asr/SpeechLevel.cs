using Meetrec.Core.Audio;

namespace Meetrec.Asr;

// Silero is nearly level-invariant: a silent headset mic that faintly picks up the other side of the call is
// "speech" to it at -90 dBFS, and Whisper turns such regions into "Thank you." hallucinations.
public static class SpeechLevel
{
    private const double FramePercentile = 0.95;
    private static readonly TimeSpan FrameLength = TimeSpan.FromMilliseconds(20);

    public static IReadOnlyList<SpeechRegion> DropQuiet(AudioClip clip, IReadOnlyList<SpeechRegion> regions, double minDbfs) =>
        [.. regions.Where(r => Dbfs(clip, r) >= minDbfs)];

    // A high percentile of short-frame RMS rather than the peak, so that a single click does not make a region loud.
    public static double Dbfs(AudioClip clip, SpeechRegion region)
    {
        var frame = (int)(FrameLength.TotalSeconds * clip.SampleRate);
        var from = (int)(region.Start.TotalSeconds * clip.SampleRate);
        var to = Math.Min(clip.Samples.Length, (int)(region.End.TotalSeconds * clip.SampleRate));
        var rms = new List<double>();
        for (var start = from; start + frame <= to; start += frame)
        {
            double sum = 0;
            for (var i = start; i < start + frame; i++)
            {
                sum += (double)clip.Samples[i] * clip.Samples[i];
            }

            rms.Add(Math.Sqrt(sum / frame));
        }

        if (rms.Count == 0)
        {
            return double.NegativeInfinity;
        }

        rms.Sort();
        var level = rms[Math.Min(rms.Count - 1, (int)(rms.Count * FramePercentile))];
        return level > 0 ? 20 * Math.Log10(level) : double.NegativeInfinity;
    }
}
