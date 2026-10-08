using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;

namespace Meetrec.Core.Tests;

public class SpeechMaskTests
{
    private static readonly TimeSpan Snap = TimeSpan.FromSeconds(1.5);

    private static TimeSpan T(double seconds) => TimeSpan.FromSeconds(seconds);

    private static SpeechRegion R(double start, double end) => new(T(start), T(end));

    private static Word W(string text, double start, double end) => new(text, T(start), T(end));

    private static SpeechMask Build(params SpeechRegion[] regions) => SpeechMask.Build(regions, T(100), T(0), T(0.5));

    [Fact]
    public void Build_PadsRegionsAndClampsToDuration()
    {
        // Arrange
        SpeechRegion[] regions = [R(0.1, 2), R(8, 9.9)];

        // Act
        var mask = SpeechMask.Build(regions, T(10), T(0.25), T(0.5));

        // Assert
        Assert.Equal([R(0, 2.25), R(7.75, 10)], mask.Regions);
    }

    [Fact]
    public void Build_RegionsCloserThanMergeGap_AreMerged()
    {
        // Arrange
        SpeechRegion[] regions = [R(3, 4), R(1, 2), R(4.7, 5)];

        // Act
        var mask = SpeechMask.Build(regions, T(10), T(0.1), T(0.5));

        // Assert
        Assert.Equal([R(0.9, 2.1), R(2.9, 5.1)], mask.Regions);
    }

    [Fact]
    public void Build_NoRegions_IsEmpty()
    {
        // Arrange
        SpeechRegion[] regions = [];

        // Act
        var mask = SpeechMask.Build(regions, T(10), T(0.25), T(0.5));

        // Assert
        Assert.True(mask.IsEmpty);
    }

    [Fact]
    public void Apply_WordsInsideSpeech_AreUnchanged()
    {
        // Arrange
        var mask = Build(R(1, 2), R(5, 6));
        Word[] words = [W("раз", 1.1, 1.5), W("два", 5.2, 5.9)];

        // Act
        var result = mask.Apply(words, Snap);

        // Assert
        Assert.Equal(words, result);
    }

    [Fact]
    public void Apply_WordStretchedOverPause_IsClampedToDominantRegion()
    {
        // Arrange
        var mask = Build(R(1, 2), R(5, 6));
        Word[] words = [W("Привет.", 1.8, 5.6)];

        // Act
        var result = mask.Apply(words, Snap);

        // Assert
        Assert.Equal([W("Привет.", 5.0, 5.6)], result);
    }

    [Fact]
    public void Apply_ZeroLengthWordInsideSpeech_IsKept()
    {
        // Arrange
        var mask = Build(R(1, 2));
        Word[] words = [W("Сегодня", 1.5, 1.5), W("обсудим", 1.5, 1.5)];

        // Act
        var result = mask.Apply(words, Snap);

        // Assert
        Assert.Equal(words, result);
    }

    [Fact]
    public void Apply_WordInPauseNearSpeech_IsSnappedIntoNearestRegion()
    {
        // Arrange
        var mask = Build(R(1, 2), R(5, 6));
        Word[] words = [W("У", 4.0, 4.0), W("меня", 4.0, 4.2), W("после", 2.5, 2.8)];

        // Act
        var result = mask.Apply(words, Snap);

        // Assert
        Assert.Equal([W("У", 5.0, 5.0), W("меня", 5.0, 5.0), W("после", 2.0, 2.0)], result);
    }

    [Fact]
    public void Apply_WordFarFromSpeech_IsDropped()
    {
        // Arrange
        var mask = Build(R(1, 2), R(5, 6));
        Word[] words = [W("ок", 1.2, 1.4), W("Продолжение", 7.6, 8.0), W("следует...", 8.0, 20.0)];

        // Act
        var result = mask.Apply(words, Snap);

        // Assert
        Assert.Equal([W("ок", 1.2, 1.4)], result);
    }

    [Fact]
    public void Apply_EmptyMask_DropsEverything()
    {
        // Arrange
        var mask = Build();
        Word[] words = [W("галлюцинация", 1, 2)];

        // Act
        var result = mask.Apply(words, Snap);

        // Assert
        Assert.Empty(result);
    }
}
