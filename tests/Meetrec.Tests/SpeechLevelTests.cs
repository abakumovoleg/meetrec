using Meetrec.Asr;
using Meetrec.Core.Audio;

namespace Meetrec.Tests;

public class SpeechLevelTests
{
    private const int SampleRate = 16_000;

    [Fact]
    public void DropQuiet_KeepsSpeechAndDropsNearSilentRegion()
    {
        // Arrange
        var samples = WavFile.Sine(220, SampleRate, 1, 0.1f)
            .Concat(WavFile.Sine(220, SampleRate, 1, 0.00005f))
            .ToArray();
        var clip = new AudioClip(samples, SampleRate);
        var loud = Region(0, 1);
        var quiet = Region(1, 2);

        // Act
        var kept = SpeechLevel.DropQuiet(clip, [loud, quiet], RecognitionOptions.Default.MinSpeechDbfs);

        // Assert
        Assert.Equal([loud], kept);
    }

    [Fact]
    public void Dbfs_SingleClickInSilence_StaysQuiet()
    {
        // Arrange
        var samples = new float[SampleRate];
        samples[100] = 0.9f;
        var clip = new AudioClip(samples, SampleRate);

        // Act
        var level = SpeechLevel.Dbfs(clip, Region(0, 1));

        // Assert
        Assert.True(level < RecognitionOptions.Default.MinSpeechDbfs);
    }

    [Fact]
    public void Dbfs_SineOfKnownAmplitude_MatchesItsRms()
    {
        // Arrange
        var clip = new AudioClip(WavFile.Sine(440, SampleRate, 1, 0.5f), SampleRate);

        // Act
        var level = SpeechLevel.Dbfs(clip, Region(0, 1));

        // Assert
        Assert.InRange(level, -9.1, -8.9);
    }

    private static SpeechRegion Region(double start, double end) => new(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end));
}
