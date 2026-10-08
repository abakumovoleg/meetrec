using System.Text;

namespace Meetrec.Core.Tests;

internal static class WavFile
{
    public static void WritePcm16(string path, int sampleRate, float[][] channels) =>
        Write(path, sampleRate, channels, formatTag: 1, bitsPerSample: 16,
            (w, s) => w.Write((short)Math.Clamp(MathF.Round(s * 32767f), short.MinValue, short.MaxValue)));

    public static void WriteFloat32(string path, int sampleRate, float[][] channels) =>
        Write(path, sampleRate, channels, formatTag: 3, bitsPerSample: 32, (w, s) => w.Write(s));

    public static float[] Sine(double frequency, int sampleRate, double seconds, float amplitude) =>
        Enumerable.Range(0, (int)(sampleRate * seconds))
            .Select(i => amplitude * (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate))
            .ToArray();

    public static float[] Constant(float value, int length) => Enumerable.Repeat(value, length).ToArray();

    private static void Write(
        string path, int sampleRate, float[][] channels, short formatTag, short bitsPerSample, Action<BinaryWriter, float> writeSample)
    {
        var frames = channels[0].Length;
        var blockAlign = (short)(channels.Length * bitsPerSample / 8);
        var dataSize = frames * blockAlign;

        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write(formatTag);
        writer.Write((short)channels.Length);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        for (var i = 0; i < frames; i++)
        {
            foreach (var channel in channels)
            {
                writeSample(writer, channel[i]);
            }
        }
    }
}
