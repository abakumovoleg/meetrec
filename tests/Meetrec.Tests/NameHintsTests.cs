using Meetrec.Core.Transcript;
using static Meetrec.Tests.TestData;

namespace Meetrec.Tests;

public class NameHintsTests
{
    [Fact]
    public void Find_NameAddressedBeforeReply_SuggestsItForReplyingSpeaker()
    {
        // Arrange
        Utterance[] utterances = [Said(S1, 0, "Так, ладно. Ром, поведи тогда"), Said(S2, 4, "Да, давай")];

        // Act
        var hints = NameHints.Find(Conversation(utterances));

        // Assert
        var hint = Assert.Single(hints);
        Assert.Equal(S2, hint.Key);
        Assert.Equal("Рома", hint.Value.Name);
        Assert.Equal(1, hint.Value.Mentions);
        Assert.Equal(TimeSpan.FromSeconds(0.8), hint.Value.At);
        Assert.Equal("Так, ладно. Ром, поведи тогда", hint.Value.Quote);
    }

    [Theory]
    [InlineData("и Максима, наверное")]
    [InlineData("Ну, Максим")]
    [InlineData("Ну, привет, Стол, да")]
    [InlineData("Ну, олег, да")]
    public void Find_NotAnAddress_SuggestsNothing(string text)
    {
        // Arrange
        Utterance[] utterances = [Said(S1, 0, text), Said(S2, 2, "да")];

        // Act
        var hints = NameHints.Find(Conversation(utterances));

        // Assert
        Assert.Empty(hints);
    }

    [Fact]
    public void Find_ReplyBySameSpeakerOrAfterLongPause_SuggestsNothing()
    {
        // Arrange
        Utterance[] sameSpeaker = [Said(S1, 0, "Да, Олег, извини"), Said(S1, 3, "ну что")];
        Utterance[] longPause = [Said(S1, 0, "Да, Олег, извини"), Said(S2, 10, "ну что")];

        // Act
        var afterSame = NameHints.Find(Conversation(sameSpeaker));
        var afterPause = NameHints.Find(Conversation(longPause));

        // Assert
        Assert.Empty(afterSame);
        Assert.Empty(afterPause);
    }

    [Fact]
    public void Find_NameEarlyInLongUtterance_SuggestsNothing()
    {
        // Arrange
        var text = "Саша, " + string.Join(' ', Enumerable.Repeat("слово", 40));
        Utterance[] utterances = [Said(S1, 0, text), Said(S2, 17, "да")];

        // Act
        var hints = NameHints.Find(Conversation(utterances));

        // Assert
        Assert.Empty(hints);
    }

    [Fact]
    public void Find_ReplyFromMicOrUnknown_SuggestsNothing()
    {
        // Arrange
        Utterance[] utterances =
        [
            Said(S1, 0, "Так, Олег, что скажешь?"),
            Said(SpeakerId.Me, 2, "да"),
            Said(S1, 4, "Так, Саша, что скажешь?"),
            Said(SpeakerId.Unknown, 6, "да"),
        ];

        // Act
        var hints = NameHints.Find(Conversation(utterances));

        // Assert
        Assert.Empty(hints);
    }

    [Fact]
    public void Find_SeveralNamesForSpeaker_PicksMostMentioned()
    {
        // Arrange
        Utterance[] utterances =
        [
            Said(S1, 0, "Так, Рома, Юра?"),
            Said(S2, 3, "да"),
            Said(S1, 5, "Ну, Ром, давай"),
            Said(S2, 7, "ага"),
        ];

        // Act
        var hints = NameHints.Find(Conversation(utterances));

        // Assert
        var hint = Assert.Single(hints).Value;
        Assert.Equal("Рома", hint.Name);
        Assert.Equal(2, hint.Mentions);
    }

    [Fact]
    public void Find_SameNameForTwoSpeakers_GoesToMostMentioned()
    {
        // Arrange
        Utterance[] utterances =
        [
            Said(S1, 0, "Ну, Саша?"),
            Said(S2, 2, "да"),
            Said(S1, 4, "Ну, Саша?"),
            Said(S3, 6, "да"),
            Said(S1, 8, "Ну, Саша?"),
            Said(S3, 10, "да"),
        ];

        // Act
        var hints = NameHints.Find(Conversation(utterances));

        // Assert
        var hint = Assert.Single(hints);
        Assert.Equal(S3, hint.Key);
        Assert.Equal(2, hint.Value.Mentions);
    }

    [Theory]
    [InlineData("Рома", "Рома")]
    [InlineData("ром", "Рома")]
    [InlineData("Тань", "Таня")]
    [InlineData("Алена", "Алёна")]
    [InlineData("Серёг", "Серёга")]
    [InlineData("Стол", null)]
    [InlineData("Р", null)]
    public void RussianNames_Find_ReturnsDictionaryForm(string word, string? expected)
    {
        // Arrange
        var input = word;

        // Act
        var name = RussianNames.Find(input);

        // Assert
        Assert.Equal(expected, name);
    }
}
