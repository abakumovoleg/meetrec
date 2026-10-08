using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Meetrec.Core.Audio;

public static class AudioLoader
{
    public const int TargetSampleRate = 16_000;

    public static AudioClip LoadMono16k(string path)
    {
        using var reader = new WaveFileReader(path);
        ISampleProvider provider = reader.ToSampleProvider();

        if (provider.WaveFormat.Channels > 1)
        {
            provider = new MonoDownmixSampleProvider(provider);
        }

        if (provider.WaveFormat.SampleRate != TargetSampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, TargetSampleRate);
        }

        var expected = (int)Math.Ceiling(reader.SampleCount * (double)TargetSampleRate / reader.WaveFormat.SampleRate);
        return new AudioClip(ReadAll(provider, expected), TargetSampleRate);
    }

    private static float[] ReadAll(ISampleProvider provider, int capacityHint)
    {
        var result = new List<float>(capacityHint + TargetSampleRate);
        var buffer = new float[TargetSampleRate];
        int read;
        while ((read = provider.Read(buffer)) > 0)
        {
            result.AddRange(buffer.AsSpan(0, read));
        }

        return [.. result];
    }
}
