using Meetrec.Capture.Windows;
using Meetrec.Core.Audio;
using NAudio.Wave;

namespace Meetrec.Tests;

public class Mono16kResamplerTests
{
    private static byte[] Float32(params float[] samples)
    {
        var bytes = new byte[samples.Length * 4];
        for (var i = 0; i < samples.Length; i++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 4), samples[i]);
        }

        return bytes;
    }

    private static byte[] Pcm16(params float[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 2), (short)(samples[i] * 32767f));
        }

        return bytes;
    }

    private static float[] Interleave(float[] left, float[] right) =>
        [.. left.Zip(right).SelectMany(pair => new[] { pair.First, pair.Second })];

    [Fact]
    public void Convert_AlreadyMono16k_ReturnsSamplesUnchanged()
    {
        // Arrange
        var resampler = new Mono16kResampler(WaveFormat.CreateIeeeFloatWaveFormat(16_000, 1));
        float[] samples = [0f, 0.25f, -0.5f, 1f];

        // Act
        var converted = resampler.Convert(Float32(samples)).ToArray();

        // Assert
        Assert.Equal(samples.Length, converted.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            Assert.Equal(samples[i], converted[i], 0.001f);
        }
    }

    [Fact]
    public void Convert_Pcm16Mono16k_IsAccepted()
    {
        // Arrange
        var resampler = new Mono16kResampler(new WaveFormat(16_000, 16, 1));

        // Act
        var converted = resampler.Convert(Pcm16(0.5f, -0.5f)).ToArray();

        // Assert
        Assert.Equal(2, converted.Length);
        Assert.Equal(0.5f, converted[0], 0.001f);
        Assert.Equal(-0.5f, converted[1], 0.001f);
    }

    [Fact]
    public void Convert_Stereo48k_DownmixesAndResamplesToOneThird()
    {
        // Arrange
        var resampler = new Mono16kResampler(WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2));
        var frames = 48_000;
        var interleaved = Interleave(WavFile.Constant(0.4f, frames), WavFile.Constant(0.6f, frames));

        // Act
        var converted = resampler.Convert(Float32(interleaved)).ToArray();

        // Assert
        Assert.InRange(converted.Length, 15_900, 16_000);
        // The resampler's filter needs a few samples to settle, so the steady part is what carries the level.
        Assert.All(converted[100..], sample => Assert.Equal(0.5f, sample, 0.01f));
    }

    [Fact]
    public void Convert_SplitAcrossCalls_KeepsTotalLength()
    {
        // Arrange
        var format = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 1);
        var samples = WavFile.Sine(440, 48_000, 0.5, 0.8f);
        var whole = new Mono16kResampler(format).Convert(Float32(samples)).Length;
        var split = new Mono16kResampler(format);

        // Act
        var first = split.Convert(Float32(samples[..12_000])).Length;
        var second = split.Convert(Float32(samples[12_000..])).Length;

        // Assert
        Assert.InRange(first + second, whole - 1, whole + 1);
    }

    [Fact]
    public void ConvertSilence_ReturnsZerosOfTheSameDuration()
    {
        // Arrange
        var resampler = new Mono16kResampler(WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2));

        // Act
        var converted = resampler.ConvertSilence(48_000 * 2 * 4).ToArray();

        // Assert
        Assert.InRange(converted.Length, 15_900, 16_000);
        Assert.All(converted, sample => Assert.Equal(0f, sample));
    }
}
