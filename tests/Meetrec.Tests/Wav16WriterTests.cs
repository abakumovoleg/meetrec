using Meetrec.Capture.Windows;
using Meetrec.Core.Audio;

namespace Meetrec.Tests;

public sealed class Wav16WriterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("meetrec-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Append_WrittenSamples_ReadBackAs16kMono()
    {
        // Arrange
        var path = PathOf("track.wav");
        float[] samples = [0f, 0.5f, -0.5f, 1f, -1f];

        // Act
        using (var writer = new Wav16Writer(path))
        {
            writer.Append(samples);
        }

        var clip = AudioLoader.LoadMono16k(path);

        // Assert
        Assert.Equal(16_000, clip.SampleRate);
        Assert.Equal(samples.Length, clip.Samples.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            Assert.Equal(samples[i], clip.Samples[i], 0.001f);
        }
    }

    [Fact]
    public void Append_SamplesOutsideRange_AreClamped()
    {
        // Arrange
        var path = PathOf("loud.wav");

        // Act
        using (var writer = new Wav16Writer(path))
        {
            writer.Append([2f, -2f]);
        }

        var clip = AudioLoader.LoadMono16k(path);

        // Assert
        Assert.Equal(1f, clip.Samples[0], 0.001f);
        Assert.Equal(-1f, clip.Samples[1], 0.001f);
    }

    [Fact]
    public void AppendSilence_AddsZerosAndCountsThem()
    {
        // Arrange
        var path = PathOf("silence.wav");

        // Act
        using (var writer = new Wav16Writer(path))
        {
            writer.Append([0.5f]);
            writer.AppendSilence(24_000);
            Assert.Equal(24_001, writer.SampleCount);
        }

        var clip = AudioLoader.LoadMono16k(path);

        // Assert
        Assert.Equal(24_001, clip.Samples.Length);
        Assert.All(clip.Samples[1..], sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void AppendSilence_NonPositiveCount_WritesNothing()
    {
        // Arrange
        var path = PathOf("empty.wav");

        // Act
        using var writer = new Wav16Writer(path);
        writer.AppendSilence(0);
        writer.AppendSilence(-100);

        // Assert
        Assert.Equal(0, writer.SampleCount);
    }

    // While recording goes on the header is rewritten every second, so a recording cut short by a crash still
    // declares the audio it holds.
    [Fact]
    public void UpdateHeader_BeforeClosing_HeaderCountsWhatIsWritten()
    {
        // Arrange
        var path = PathOf("partial.wav");
        using var writer = new Wav16Writer(path);
        writer.Append(WavFile.Constant(0.25f, 8_000));

        // Act
        writer.UpdateHeader();

        // Assert
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var header = new byte[44];
        file.ReadExactly(header);
        Assert.Equal(8_000u * 2, BitConverter.ToUInt32(header, 40));
        Assert.Equal(36u + (8_000u * 2), BitConverter.ToUInt32(header, 4));
        Assert.Equal(44L + (8_000 * 2), file.Length);
    }

    private string PathOf(string name) => Path.Combine(_dir, name);
}
