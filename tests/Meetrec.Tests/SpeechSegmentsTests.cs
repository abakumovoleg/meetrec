using Meetrec.Asr;

namespace Meetrec.Tests;

public class SpeechSegmentsTests
{
    [Fact]
    public void Windows_SpeechCloserThanGap_JoinsIntoOnePaddedWindow()
    {
        // Arrange
        SpeechRegion[] speech = [Region(10, 12), Region(13.5, 15), Region(20, 21)];

        // Act
        var windows = SpeechSegments.Windows(speech, Seconds(60), gap: Seconds(2), padding: Seconds(0.5));

        // Assert
        Assert.Equal([Region(9.5, 15.5), Region(19.5, 21.5)], windows);
    }

    [Fact]
    public void Windows_PaddingPastClipEdges_IsCut()
    {
        // Arrange
        SpeechRegion[] speech = [Region(0.2, 1), Region(29, 29.8)];

        // Act
        var windows = SpeechSegments.Windows(speech, Seconds(30), gap: Seconds(2), padding: Seconds(0.5));

        // Assert
        Assert.Equal([Region(0, 1.5), Region(28.5, 30)], windows);
    }

    [Fact]
    public void Pack_FillsPacksUpToLengthWithSeparators()
    {
        // Arrange
        SpeechRegion[] windows = [Region(0, 9), Region(20, 29), Region(40, 49), Region(60, 100), Region(110, 112)];

        // Act
        var packs = SpeechSegments.Pack(windows, Seconds(28), Seconds(1));

        // Assert
        Assert.Equal(
            [[Region(0, 9), Region(20, 29)], [Region(40, 49)], [Region(60, 100)], [Region(110, 112)]],
            packs.Select(p => p.ToArray()));
    }

    [Fact]
    public void Pack_ZeroLength_GivesEachWindowItsOwnPack()
    {
        // Arrange
        SpeechRegion[] windows = [Region(0, 1), Region(5, 6)];

        // Act
        var packs = SpeechSegments.Pack(windows, TimeSpan.Zero, Seconds(1));

        // Assert
        Assert.Equal([[Region(0, 1)], [Region(5, 6)]], packs.Select(p => p.ToArray()));
    }

    private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);

    private static SpeechRegion Region(double start, double end) => new(Seconds(start), Seconds(end));
}
