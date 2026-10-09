using Meetrec.Capture.Windows;
using Meetrec.Core.Audio;
using Meetrec.Core.Capture;
using NAudio.Wave;

namespace Meetrec.Tests;

public sealed class CaptureTrackTests : IDisposable
{
    private static readonly WaveFormat Format = WaveFormat.CreateIeeeFloatWaveFormat(16_000, 1);

    private readonly string _dir = Directory.CreateTempSubdirectory("meetrec-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static byte[] Packet(float level, int samples)
    {
        var bytes = new byte[samples * 4];
        for (var i = 0; i < samples; i++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 4), level);
        }

        return bytes;
    }

    private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);

    // The first loopback packet of a meeting may arrive long after recording started, and what came before it is
    // silence that still has to be in the track.
    [Fact]
    public void Write_FirstPacketAfterAPause_PrependsSilence()
    {
        // Arrange
        var path = PathOf("gap.wav");
        var track = new CaptureTrack(path, Format);

        // Act
        track.Write(Packet(0.5f, 1_600), silent: false, Seconds(3));
        track.Dispose();

        // Assert
        var clip = AudioLoader.LoadMono16k(path);
        Assert.Equal(3 * 16_000 + 1_600, clip.Samples.Length);
        Assert.All(clip.Samples[..(3 * 16_000)], sample => Assert.Equal(0f, sample));
        Assert.Equal(0.5f, clip.Samples[^1], 0.001f);
    }

    [Fact]
    public void Write_TimingJitterBelowThreshold_AddsNoSilence()
    {
        // Arrange
        var path = PathOf("jitter.wav");
        var track = new CaptureTrack(path, Format);

        // Act
        track.Write(Packet(0.5f, 1_600), silent: false, TimeSpan.Zero);
        track.Write(Packet(0.5f, 1_600), silent: false, Seconds(0.1) + CaptureTrack.GapThreshold - Seconds(0.001));
        track.Dispose();

        // Assert
        var clip = AudioLoader.LoadMono16k(path);
        Assert.Equal(3_200, clip.Samples.Length);
    }

    [Fact]
    public void Write_GapAboveThreshold_IsFilledWithSilence()
    {
        // Arrange
        var path = PathOf("pause.wav");
        var track = new CaptureTrack(path, Format);

        // Act
        track.Write(Packet(0.5f, 1_600), silent: false, TimeSpan.Zero);
        track.Write(Packet(0.5f, 1_600), silent: false, Seconds(10));
        track.Dispose();

        // Assert
        var clip = AudioLoader.LoadMono16k(path);
        Assert.Equal(10 * 16_000 + 1_600, clip.Samples.Length);
        Assert.All(clip.Samples[1_600..(10 * 16_000)], sample => Assert.Equal(0f, sample));
    }

    // WASAPI flags silent packets and leaves their contents undefined.
    [Fact]
    public void Write_SilentPacket_WritesZerosOfTheSameLength()
    {
        // Arrange
        var path = PathOf("silent.wav");
        var track = new CaptureTrack(path, Format);

        // Act
        track.Write(Packet(0.9f, 1_600), silent: true, TimeSpan.Zero);
        track.Dispose();

        // Assert
        var clip = AudioLoader.LoadMono16k(path);
        Assert.Equal(1_600, clip.Samples.Length);
        Assert.All(clip.Samples, sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void Finish_PadsToTheRecordedLength()
    {
        // Arrange
        var path = PathOf("tail.wav");
        var track = new CaptureTrack(path, Format);
        track.Write(Packet(0.5f, 1_600), silent: false, TimeSpan.Zero);

        // Act
        track.Finish(Seconds(5));

        // Assert
        var clip = AudioLoader.LoadMono16k(path);
        Assert.Equal(5 * 16_000, clip.Samples.Length);
    }

    [Fact]
    public void Finish_ShorterThanWhatIsWritten_KeepsTheAudio()
    {
        // Arrange
        var path = PathOf("short.wav");
        var track = new CaptureTrack(path, Format);
        track.Write(Packet(0.5f, 16_000), silent: false, TimeSpan.Zero);

        // Act
        track.Finish(Seconds(0.5));

        // Assert
        Assert.Equal(16_000, AudioLoader.LoadMono16k(path).Samples.Length);
    }

    [Fact]
    public void TakePeak_ReportsTheLoudestSampleSinceTheLastCall()
    {
        // Arrange
        var path = PathOf("peak.wav");
        using var track = new CaptureTrack(path, Format);

        // Act
        track.Write(Packet(-0.75f, 160), silent: false, TimeSpan.Zero);
        var peak = track.TakePeak();
        var afterwards = track.TakePeak();

        // Assert
        Assert.Equal(0.75f, peak, 0.001f);
        Assert.Equal(0f, afterwards);
    }

    [Fact]
    public void Write_AfterDispose_IsIgnored()
    {
        // Arrange
        var path = PathOf("closed.wav");
        var track = new CaptureTrack(path, Format);
        track.Write(Packet(0.5f, 1_600), silent: false, TimeSpan.Zero);
        track.Dispose();

        // Act
        track.Write(Packet(0.5f, 1_600), silent: false, Seconds(1));
        track.FlushHeader();

        // Assert
        Assert.Equal(1_600, AudioLoader.LoadMono16k(path).Samples.Length);
    }

    private string PathOf(string name) => Path.Combine(_dir, name);
}
