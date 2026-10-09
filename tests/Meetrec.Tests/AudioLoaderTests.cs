using Meetrec.Core.Audio;

namespace Meetrec.Tests;

public sealed class AudioLoaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("meetrec-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string PathOf(string name) => Path.Combine(_dir, name);

    private static int CountZeroCrossings(float[] samples) =>
        samples.Zip(samples.Skip(1)).Count(p => p.First < 0 != p.Second < 0);

    private static double Rms(ReadOnlySpan<float> samples)
    {
        var sum = 0.0;
        foreach (var s in samples)
        {
            sum += s * s;
        }

        return Math.Sqrt(sum / samples.Length);
    }

    [Fact]
    public void LoadMono16k_Mono16kPcm16_ReturnsSamplesUnchanged()
    {
        // Arrange
        float[] samples = [0f, 0.5f, -0.5f, 0.25f, -1f];
        var path = PathOf("mono16k.wav");
        WavFile.WritePcm16(path, 16_000, [samples]);

        // Act
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
    public void LoadMono16k_Stereo_AveragesChannels()
    {
        // Arrange
        var left = WavFile.Constant(0.5f, 100).Concat(WavFile.Constant(0.25f, 100)).ToArray();
        var right = WavFile.Constant(-0.5f, 100).Concat(WavFile.Constant(0.25f, 100)).ToArray();
        var path = PathOf("stereo16k.wav");
        WavFile.WritePcm16(path, 16_000, [left, right]);

        // Act
        var clip = AudioLoader.LoadMono16k(path);

        // Assert
        Assert.Equal(200, clip.Samples.Length);
        Assert.All(clip.Samples[..100], s => Assert.Equal(0f, s, 0.001f));
        Assert.All(clip.Samples[100..], s => Assert.Equal(0.25f, s, 0.001f));
    }

    [Fact]
    public void LoadMono16k_SixChannels_AveragesAllChannels()
    {
        // Arrange
        var channels = Enumerable.Range(0, 6).Select(ch => WavFile.Constant(ch * 0.1f, 160)).ToArray();
        var path = PathOf("surround.wav");
        WavFile.WritePcm16(path, 16_000, channels);

        // Act
        var clip = AudioLoader.LoadMono16k(path);

        // Assert
        Assert.All(clip.Samples, s => Assert.Equal(0.25f, s, 0.001f));
    }

    [Fact]
    public void LoadMono16k_Stereo44100Sine_ResamplesKeepingDurationFrequencyAndLevel()
    {
        // Arrange
        var sine = WavFile.Sine(440, 44_100, 1.0, 0.5f);
        var path = PathOf("stereo44k.wav");
        WavFile.WritePcm16(path, 44_100, [sine, sine]);

        // Act
        var clip = AudioLoader.LoadMono16k(path);

        // Assert
        Assert.InRange(clip.Samples.Length, 16_000 - 16, 16_000 + 16);
        Assert.InRange(CountZeroCrossings(clip.Samples), 880 - 4, 880 + 4);
        Assert.Equal(0.5 / Math.Sqrt(2), Rms(clip.Samples.AsSpan(1_000, 14_000)), 0.01);
    }

    [Fact]
    public void LoadMono16k_Float32At48k_ResamplesToTargetDuration()
    {
        // Arrange
        var sine = WavFile.Sine(1_000, 48_000, 2.0, 0.3f);
        var path = PathOf("float48k.wav");
        WavFile.WriteFloat32(path, 48_000, [sine]);

        // Act
        var clip = AudioLoader.LoadMono16k(path);

        // Assert
        Assert.Equal(2.0, clip.Duration.TotalSeconds, 0.001);
        Assert.InRange(CountZeroCrossings(clip.Samples), 4_000 - 8, 4_000 + 8);
    }

    [Fact]
    public void LoadMono16k_8kUpsample_ResamplesToTargetDuration()
    {
        // Arrange
        var sine = WavFile.Sine(300, 8_000, 3.0, 0.5f);
        var path = PathOf("phone8k.wav");
        WavFile.WritePcm16(path, 8_000, [sine]);

        // Act
        var clip = AudioLoader.LoadMono16k(path);

        // Assert
        Assert.Equal(3.0, clip.Duration.TotalSeconds, 0.001);
        Assert.InRange(CountZeroCrossings(clip.Samples), 1_800 - 6, 1_800 + 6);
    }

    [Fact]
    public void LoadMono16k_LongRecording_DoesNotDriftInDuration()
    {
        // Arrange
        var sine = WavFile.Sine(200, 44_100, 120.0, 0.2f);
        var path = PathOf("long44k.wav");
        WavFile.WritePcm16(path, 44_100, [sine]);

        // Act
        var clip = AudioLoader.LoadMono16k(path);

        // Assert
        Assert.Equal(120.0, clip.Duration.TotalSeconds, 0.001);
    }

    [Fact]
    public void LoadMono16k_NotAWavFile_Throws()
    {
        // Arrange
        var path = PathOf("garbage.wav");
        File.WriteAllText(path, "это не wav");

        // Act
        var act = () => AudioLoader.LoadMono16k(path);

        // Assert
        var error = Assert.Throws<MeetrecException>(act);
        Assert.Contains(path, error.Message);
    }
}
