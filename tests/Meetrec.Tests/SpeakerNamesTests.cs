using Meetrec.Core.Transcript;
using static Meetrec.Tests.TestData;

namespace Meetrec.Tests;

public class SpeakerNamesTests
{
    private static readonly string S1 = Speakers.Numbered(1);
    private static readonly string S2 = Speakers.Numbered(2);

    [Fact]
    public void Current_MixedTracks_ListsSystemSpeakersInOrderOfAppearanceWithoutMeAndUnknown()
    {
        // Arrange
        Utterance[] utterances =
        [
            Said(Speakers.Me, 0, "привет", Track.Mic),
            Said(S2, 1, "да"),
            Said(Speakers.Unknown, 2, "ну"),
            Said(S1, 3, "ага"),
            Said(S2, 4, "ещё"),
        ];

        // Act
        var current = SpeakerNames.Current(utterances);

        // Assert
        Assert.Equal([new SpeakerName(S2, S2), new SpeakerName(S1, S1)], current);
    }

    [Fact]
    public void Apply_Name_ReplacesSpeakerAndKeepsLabel()
    {
        // Arrange
        Utterance[] utterances = [Said(S1, 0, "привет всем")];

        // Act
        var result = SpeakerNames.Apply(utterances, new Dictionary<string, string> { [S1] = "Олег" });

        // Assert
        var utterance = Assert.Single(result);
        Assert.Equal("Олег", utterance.Speaker);
        Assert.All(utterance.Words, w => Assert.Equal(S1, w.Label));
        Assert.Equal([new SpeakerName(S1, "Олег")], SpeakerNames.Current(result));
    }

    [Fact]
    public void Apply_SameNameForAdjacentSpeakers_MergesUtterances()
    {
        // Arrange
        Utterance[] utterances = [Said(S1, 0, "раз два"), Said(S2, 1, "три")];

        // Act
        var result = SpeakerNames.Apply(utterances, new Dictionary<string, string> { [S1] = "Саша", [S2] = "Саша" });

        // Assert
        var utterance = Assert.Single(result);
        Assert.Equal("раз два три", utterance.Text);
        Assert.Equal([S1, S1, S2], utterance.Words.Select(w => w.Label));
    }

    [Fact]
    public void Apply_LabelAsName_ClearsLabel()
    {
        // Arrange
        Utterance[] named = [Utterance(Word("да", 0, 0.5, "Олег", Track.System, S1))];

        // Act
        var result = SpeakerNames.Apply(named, new Dictionary<string, string> { [S1] = S1 });

        // Assert
        var word = Assert.Single(Assert.Single(result).Words);
        Assert.Equal(S1, word.Speaker);
        Assert.Null(word.Label);
    }

    [Fact]
    public void Apply_MeAndUnknown_AreNotRenamed()
    {
        // Arrange
        Utterance[] utterances = [Said(Speakers.Me, 0, "да", Track.Mic), Said(Speakers.Unknown, 3, "нет")];
        var names = new Dictionary<string, string> { [Speakers.Me] = "Олег", [Speakers.Unknown] = "Кто-то" };

        // Act
        var result = SpeakerNames.Apply(utterances, names);

        // Assert
        Assert.Equal([Speakers.Me, Speakers.Unknown], result.Select(u => u.Speaker));
    }

    [Fact]
    public void Reset_NamedTranscript_RestoresLabelsAndSplitsMergedSpeakers()
    {
        // Arrange
        Utterance[] named = [Utterance(Word("раз", 0, 0.5, "Саша", Track.System, S1), Word("два", 0.6, 1, "Саша", Track.System, S2))];

        // Act
        var result = SpeakerNames.Reset(named);

        // Assert
        Assert.Equal([S1, S2], result.Select(u => u.Speaker));
        Assert.All(result.SelectMany(u => u.Words), w => Assert.Null(w.Label));
    }
}
