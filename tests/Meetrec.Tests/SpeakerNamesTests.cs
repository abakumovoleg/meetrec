using Meetrec.Core.Transcript;
using static Meetrec.Tests.TestData;

namespace Meetrec.Tests;

public class SpeakerNamesTests
{
    [Fact]
    public void Nameable_MixedSpeakers_ListsDiarizedInTableOrderWithoutMeAndUnknown()
    {
        // Arrange
        var conversation = Conversation(
        [
            Said(SpeakerId.Me, 0, "привет"),
            Said(S2, 1, "да"),
            Said(SpeakerId.Unknown, 2, "ну"),
            Said(S1, 3, "ага"),
            Said(S2, 4, "ещё"),
        ]);

        // Act
        var nameable = SpeakerNames.Nameable(conversation);

        // Assert
        Assert.Equal([S2, S1], nameable.Select(s => s.Id));
    }

    [Fact]
    public void Apply_Name_RenamesSpeakerAndKeepsIds()
    {
        // Arrange
        var conversation = Conversation([Said(S1, 0, "привет всем")]);

        // Act
        var result = SpeakerNames.Apply(conversation, new Dictionary<SpeakerId, string> { [S1] = "Олег" });

        // Assert
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal("Олег", result.NameOf(utterance.Speaker));
        Assert.All(utterance.Words, w => Assert.Equal(S1, w.Speaker));
    }

    [Fact]
    public void Apply_SameNameForAdjacentSpeakers_MergesUtterances()
    {
        // Arrange
        var conversation = Conversation([Said(S1, 0, "раз два"), Said(S2, 1, "три")]);

        // Act
        var result = SpeakerNames.Apply(conversation, new Dictionary<SpeakerId, string> { [S1] = "Саша", [S2] = "Саша" });

        // Assert
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal("раз два три", utterance.Text);
        Assert.Equal([S1, S1, S2], utterance.Words.Select(w => w.Speaker));
    }

    [Fact]
    public void Apply_SameNameForSpeakersOnDifferentTracks_KeepsUtterancesApart()
    {
        // Arrange
        var conversation = Conversation([Said(SpeakerId.Me, 0, "раз"), Said(S1, 0.5, "два")]);

        // Act
        var result = SpeakerNames.Apply(conversation, new Dictionary<SpeakerId, string> { [S1] = Speakers.Me });

        // Assert
        Assert.Equal(2, result.Utterances.Count);
    }

    [Fact]
    public void Apply_MeAndUnknown_AreNotRenamed()
    {
        // Arrange
        var conversation = Conversation([Said(SpeakerId.Me, 0, "да"), Said(SpeakerId.Unknown, 3, "нет")]);
        var names = new Dictionary<SpeakerId, string> { [SpeakerId.Me] = "Олег", [SpeakerId.Unknown] = "Кто-то" };

        // Act
        var result = SpeakerNames.Apply(conversation, names);

        // Assert
        Assert.Equal([Speakers.Me, Speakers.Unknown], result.Utterances.Select(u => result.NameOf(u.Speaker)));
    }

    [Fact]
    public void Reset_NamedConversation_RestoresDefaultNamesAndSplitsMergedSpeakers()
    {
        // Arrange
        var named = Conversation([Utterance(Word("раз", 0, 0.5, S1), Word("два", 0.6, 1, S2))], (S1, "Саша"), (S2, "Саша"));

        // Act
        var result = SpeakerNames.Reset(named);

        // Assert
        Assert.Equal([Speakers.Numbered(1), Speakers.Numbered(2)], result.Utterances.Select(u => result.NameOf(u.Speaker)));
    }
}
