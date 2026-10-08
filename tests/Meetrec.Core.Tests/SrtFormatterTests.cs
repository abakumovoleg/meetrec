using Meetrec.Core.Output;
using Meetrec.Core.Transcript;
using static Meetrec.Core.Tests.TestData;

namespace Meetrec.Core.Tests;

public class SrtFormatterTests
{
    [Fact]
    public void Write_Utterances_RendersNumberedCuesWithSpeaker()
    {
        // Arrange
        var first = Utterance(Word(" Привет", 3661.042, 3661.5), Word(" всем", 3661.6, 3662.25));
        var second = Utterance(Word(" Здравствуйте", 3663, 3664, Speakers.Numbered(1), Track.System));

        // Act
        var result = Render(new SrtFormatter(), first, second);

        // Assert
        Assert.Equal(
            "1\n01:01:01,042 --> 01:01:02,250\nЯ: Привет всем\n\n" +
            "2\n01:01:03,000 --> 01:01:04,000\nСпикер 1: Здравствуйте\n",
            result);
    }

    [Fact]
    public void Write_UtteranceLongerThanMaxDuration_IsSplitIntoCues()
    {
        // Arrange
        var utterance = Utterance(
            Word("раз", 0, 1), Word("два", 2, 3), Word("три", 5, 6.5), Word("четыре", 7, 8), Word("пять", 9, 10));

        // Act
        var result = Render(new SrtFormatter(), utterance);

        // Assert
        Assert.Equal(
            "1\n00:00:00,000 --> 00:00:06,500\nЯ: раз два три\n\n" +
            "2\n00:00:07,000 --> 00:00:10,000\nЯ: четыре пять\n",
            result);
    }

    [Fact]
    public void Write_UtteranceLongerThanMaxText_IsSplitWithEveryCueWithinLimit()
    {
        // Arrange
        var words = Enumerable.Range(0, 30)
            .Select(i => Word("слово" + i, i * 0.1, i * 0.1 + 0.05))
            .ToArray();
        var utterance = Utterance(words);

        // Act
        var result = Render(new SrtFormatter(), utterance);

        // Assert
        var texts = result.Split('\n').Where(l => l.StartsWith("Я: ")).Select(l => l["Я: ".Length..]).ToList();
        Assert.True(texts.Count > 1);
        Assert.All(texts, t => Assert.True(t.Length <= SrtFormatter.MaxCueTextLength));
        Assert.Equal(string.Join(' ', words.Select(w => w.Text)), string.Join(' ', texts));
    }

    [Fact]
    public void Write_SingleWordLongerThanLimits_StaysInOneCue()
    {
        // Arrange
        var utterance = Utterance(Word(new string('а', 100), 0, 9));

        // Act
        var result = Render(new SrtFormatter(), utterance);

        // Assert
        Assert.StartsWith("1\n00:00:00,000 --> 00:00:09,000\n", result);
        Assert.DoesNotContain("\n2\n", result);
    }
}
