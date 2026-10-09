using Meetrec.Core.Transcript;
using static Meetrec.Tests.TestData;

namespace Meetrec.Tests;

public class SpeakerProfilesTests
{
    [Fact]
    public void Build_Speakers_CountsShareAndUtterances()
    {
        // Arrange
        Utterance[] utterances =
        [
            Utterance(Word("а", 0, 3)),
            Utterance(Word("б", 3, 4, S1)),
            Utterance(Word("в", 4, 10, S2)),
        ];

        // Act
        var profiles = SpeakerProfiles.Build(Conversation(utterances));

        // Assert
        Assert.Equal([S1, S2], profiles.Select(p => p.Id));
        Assert.Equal([Speakers.Numbered(1), Speakers.Numbered(2)], profiles.Select(p => p.Label));
        Assert.Equal(0.1, profiles[0].TalkShare, 3);
        Assert.Equal(0.6, profiles[1].TalkShare, 3);
        Assert.Equal(1, profiles[1].UtteranceCount);
    }

    [Fact]
    public void Build_ManyUtterances_SamplesLongestFromEachPartInTimeOrder()
    {
        // Arrange
        Utterance[] utterances =
        [
            Said(S1, 0, "привет"),
            Said(S1, 10, "раз два три"),
            Said(S1, 20, "да"),
            Said(S1, 30, "раз два"),
            Said(S1, 40, "ну"),
            Said(S1, 50, "раз два три четыре"),
        ];

        // Act
        var profile = Assert.Single(SpeakerProfiles.Build(Conversation(utterances)));

        // Assert
        Assert.Equal(["раз два три", "раз два", "раз два три четыре"], profile.Samples.Select(s => s.Text));
        Assert.Equal("раз два три четыре", profile.LongestUtterances[0].Text);
        Assert.Equal(SpeakerProfiles.LongestCount, profile.LongestUtterances.Count);
    }

    [Fact]
    public void Build_FewUtterances_SamplesEachOnce()
    {
        // Arrange
        Utterance[] utterances = [Said(S1, 0, "раз"), Said(S1, 5, "два")];

        // Act
        var profile = Assert.Single(SpeakerProfiles.Build(Conversation(utterances)));

        // Assert
        Assert.Equal(["раз", "два"], profile.Samples.Select(s => s.Text));
    }
}
