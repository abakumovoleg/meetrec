using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Meetrec.Core.Audio;

public static class AudioLoader
{
    public const int TargetSampleRate = 16_000;

    public static AudioClip LoadMono16k(string path)
    {
        try
        {
            return Load(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or InvalidDataException or ArgumentException)
        {
            throw new MeetrecException($"Не удалось прочитать {path}: {e.Message}", e);
        }
    }

    // Only the header is read; a file cut short by a killed writer still counts (its header promises more than there is).
    public static bool IsReadable(string path)
    {
        try
        {
            using var reader = new WaveFileReader(path);
            return reader.SampleCount > 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or InvalidDataException or ArgumentException)
        {
            return false;
        }
    }

    private static AudioClip Load(string path)
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

    // The resampler yields exactly the expected count (checked for 8-48 kHz, mono and stereo, up to an hour), so the
    // array is allocated once: growing a list and copying it out doubled the peak memory on long recordings.
    private static float[] ReadAll(ISampleProvider provider, int expected)
    {
        var result = new float[expected];
        var filled = 0;
        int read;
        while (filled < result.Length && (read = provider.Read(result.AsSpan(filled))) > 0)
        {
            filled += read;
        }

        var tail = new List<float>();
        var buffer = new float[TargetSampleRate];
        while ((read = provider.Read(buffer)) > 0)
        {
            tail.AddRange(buffer.AsSpan(0, read));
        }

        if (tail.Count > 0)
        {
            return [.. result, .. tail];
        }

        Array.Resize(ref result, filled);
        return result;
    }
}
