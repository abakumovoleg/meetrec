namespace Meetrec.Core.Audio;

public sealed record AudioClip(float[] Samples, int SampleRate)
{
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / SampleRate);

    public AudioClip Slice(TimeSpan start, TimeSpan end)
    {
        var from = Math.Clamp((int)(start.TotalSeconds * SampleRate), 0, Samples.Length);
        var to = Math.Clamp((int)(end.TotalSeconds * SampleRate), from, Samples.Length);
        return new AudioClip(Samples[from..to], SampleRate);
    }
}
