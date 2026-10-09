using Meetrec.Core.Output;
using Meetrec.Core.Transcript;
using static Meetrec.Tests.TestData;

namespace Meetrec.Tests;

public class MarkdownFormatterTests
{
    [Fact]
    public void Write_Utterances_RendersTimestampSpeakerAndTextSeparatedByBlankLine()
    {
        // Arrange
        var first = Utterance(Word(" Привет,", 83.7, 84.0), Word(" коллеги", 84.1, 84.6));
        var second = Utterance(Word(" Добрый день", 85, 86, S2));

        // Act
        var result = Render(new MarkdownFormatter(), Conversation([first, second]));

        // Assert
        Assert.Equal("[00:01:23] Я: Привет, коллеги\n\n[00:01:25] Спикер 2: Добрый день\n", result);
    }

    [Fact]
    public void Write_TimeOverAnHour_RendersTotalHours()
    {
        // Arrange
        var utterance = Utterance(Word("поздно", 3600 * 2 + 61.9, 3600 * 2 + 62.5));

        // Act
        var result = Render(new MarkdownFormatter(), Conversation([utterance]));

        // Assert
        Assert.Equal("[02:01:01] Я: поздно\n", result);
    }

    [Fact]
    public void Write_NoUtterances_RendersEmpty()
    {
        // Arrange
        var formatter = new MarkdownFormatter();

        // Act
        var result = Render(formatter, Conversation([]));

        // Assert
        Assert.Equal("", result);
    }
}
