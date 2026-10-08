using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;

namespace Meetrec.Core.Tests;

public class SpeechRepairTests
{
    [Fact]
    public void FindSparse_HallucinationSmearedOverSpeech_FlagsRegion()
    {
        // Arrange
        var regions = new[] { Region(15, 19), Region(20, 24) };
        var words = new[] { Word("Продолжение", 0, 15), Word("следует...", 15, 30) };

        // Act
        var sparse = SpeechRepair.FindSparse(regions, words, SpeechRepair.DefaultMinWordsPerSecond, SpeechRepair.DefaultMaxWordLength);

        // Assert
        Assert.Equal(regions, sparse);
    }

    [Fact]
    public void FindSparse_NormalSpeechAndShortRegionWithOneWord_KeepsBoth()
    {
        // Arrange
        var regions = new[] { Region(0, 2), Region(5, 6.5) };
        var words = new[] { Word("Коллеги,", 0, 0.5), Word("добрый", 0.5, 1), Word("день.", 1, 1.6), Word("Да.", 5.2, 5.6) };

        // Act
        var sparse = SpeechRepair.FindSparse(regions, words, SpeechRepair.DefaultMinWordsPerSecond, SpeechRepair.DefaultMaxWordLength);

        // Assert
        Assert.Empty(sparse);
    }

    [Fact]
    public void Windows_CloseRegionsGroupedAndPadded_ClampedToClip()
    {
        // Arrange
        var sparse = new[] { Region(0.5, 2), Region(5, 6), Region(30, 33) };

        // Act
        var windows = SpeechRepair.Windows(sparse, TimeSpan.FromSeconds(33.5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal([Region(0, 7), Region(29, 33.5)], windows);
    }

    [Fact]
    public void Replace_SwapsOnlyWordsOfSparseRegions()
    {
        // Arrange
        var sparse = new[] { Region(15, 19) };
        var words = new[] { Word("Продолжение", 0, 16), Word("Итак.", 25, 25.5) };
        var repaired = new[] { Word("Коллеги,", 15.1, 15.6), Word("добрый", 15.6, 16), Word("лишнее", 20, 20.5) };

        // Act
        var result = SpeechRepair.Replace(words, repaired, sparse);

        // Assert
        Assert.Equal(["Коллеги,", "добрый", "Итак."], result.Select(w => w.Text));
    }

    private static SpeechRegion Region(double start, double end) => new(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end));

    private static Word Word(string text, double start, double end) => new(text, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end));
}
