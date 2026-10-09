using Meetrec.Core.Transcript;

namespace Meetrec.Tests;

public class TranscriptMergerTests
{
    private static Word W(string text, double start, double end) =>
        new(text, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end));

    private static DiarSegment S(double start, double end, int speaker) =>
        new(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), speaker);

    private static Conversation Merge(Word[] mic, Word[] sys, DiarSegment[] segs, int offsetMs) => TranscriptMerger.Merge(
    [
        new TrackWords("mic", TimeSpan.FromMilliseconds(offsetMs), FixedSpeaker.Me, mic, []),
        new TrackWords("system", TimeSpan.Zero, DiarizedSpeakers.Instance, sys, segs),
    ]);

    private static IEnumerable<string> Names(Conversation conversation) => conversation.Utterances.Select(u => conversation.NameOf(u.Speaker));

    [Fact]
    public void Merge_MicWords_AreMe()
    {
        // Arrange
        Word[] mic = [W("привет", 0, 0.5), W("всем", 0.6, 1.0)];

        // Act
        var result = Merge(mic, [], [], 0);

        // Assert
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(SpeakerId.Me, utterance.Speaker);
        Assert.Equal("mic", utterance.Track);
        Assert.All(utterance.Words, w => Assert.Equal(SpeakerId.Me, w.Speaker));
        Assert.Equal("привет всем", utterance.Text);
    }

    [Fact]
    public void Merge_MicWords_IgnoreDiarizationSegments()
    {
        // Arrange
        Word[] mic = [W("да", 1, 1.5)];
        DiarSegment[] segs = [S(0, 10, 7)];

        // Act
        var result = Merge(mic, [], segs, 0);

        // Assert
        Assert.Equal(SpeakerId.Me, Assert.Single(result.Utterances).Speaker);
    }

    [Fact]
    public void Merge_SystemWord_TakesSegmentWithMaxOverlap()
    {
        // Arrange
        Word[] sys = [W("слово", 1.0, 2.0)];
        DiarSegment[] segs = [S(0, 1.2, 5), S(1.2, 3, 9)];

        // Act
        var result = Merge([], sys, segs, 0);

        // Assert
        var word = Assert.Single(Assert.Single(result.Utterances).Words);
        Assert.Equal(SpeakerId.Diarized("system", 1), word.Speaker);
        Assert.Equal("system", word.Track);
    }

    [Fact]
    public void Merge_SystemWordsOfDifferentSegments_GetDifferentSpeakers()
    {
        // Arrange
        Word[] sys = [W("раз", 0.1, 0.5), W("два", 1.1, 1.5)];
        DiarSegment[] segs = [S(0, 1.0, 4), S(1.0, 2.0, 2)];

        // Act
        var result = Merge([], sys, segs, 0);

        // Assert
        Assert.Equal([Speakers.Numbered(1), Speakers.Numbered(2)], Names(result));
    }

    [Fact]
    public void Merge_SystemWordWithoutOverlap_TakesNearestSegmentWithinOneSecond()
    {
        // Arrange
        Word[] sys = [W("эхо", 5.0, 5.4)];
        DiarSegment[] segs = [S(0, 3.0, 1), S(6.2, 8, 3)];

        // Act
        var result = Merge([], sys, segs, 0);

        // Assert
        Assert.Equal(SpeakerId.Diarized("system", 1), Assert.Single(result.Utterances).Speaker);
        Assert.Equal("system", result.Utterances[0].Track);
    }

    [Fact]
    public void Merge_SystemWordExactlyOneSecondFromSegment_TakesThatSegment()
    {
        // Arrange
        Word[] sys = [W("край", 4.0, 4.5)];
        DiarSegment[] segs = [S(0, 3.0, 8)];

        // Act
        var result = Merge([], sys, segs, 0);

        // Assert
        Assert.Equal(SpeakerId.Diarized("system", 1), Assert.Single(result.Utterances).Speaker);
    }

    [Fact]
    public void Merge_SystemWordFartherThanOneSecond_IsUnknownSpeaker()
    {
        // Arrange
        Word[] sys = [W("далеко", 5.0, 5.5)];
        DiarSegment[] segs = [S(0, 3.9, 1), S(6.6, 8, 2)];

        // Act
        var result = Merge([], sys, segs, 0);

        // Assert
        Assert.Equal(SpeakerId.Unknown, Assert.Single(result.Utterances).Speaker);
    }

    [Fact]
    public void Merge_NoSegments_AllSystemWordsAreUnknown()
    {
        // Arrange
        Word[] sys = [W("а", 0, 0.2), W("б", 0.3, 0.5)];

        // Act
        var result = Merge([], sys, [], 0);

        // Assert
        var utterance = Assert.Single(result.Utterances);
        Assert.Equal(SpeakerId.Unknown, utterance.Speaker);
        Assert.Equal(2, utterance.Words.Count);
    }

    [Fact]
    public void Merge_MicWords_AreShiftedByOffset()
    {
        // Arrange
        Word[] mic = [W("я", 1.0, 1.5)];

        // Act
        var result = Merge(mic, [], [], 250);

        // Assert
        var word = Assert.Single(Assert.Single(result.Utterances).Words);
        Assert.Equal(TimeSpan.FromMilliseconds(1250), word.Start);
        Assert.Equal(TimeSpan.FromMilliseconds(1750), word.End);
    }

    [Fact]
    public void Merge_SystemWords_AreNotShiftedByOffset()
    {
        // Arrange
        Word[] sys = [W("они", 1.0, 1.5)];

        // Act
        var result = Merge([], sys, [S(0, 2, 1)], 250);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(1), Assert.Single(result.Utterances).Start);
    }

    [Fact]
    public void Merge_Offset_ChangesOrderOfUtterances()
    {
        // Arrange
        Word[] mic = [W("я", 1.0, 1.2)];
        Word[] sys = [W("они", 1.5, 1.7)];
        DiarSegment[] segs = [S(1.4, 2, 1)];

        // Act
        var result = Merge(mic, sys, segs, 800);

        // Assert
        Assert.Equal([Speakers.Numbered(1), Speakers.Me], Names(result));
    }

    [Fact]
    public void Merge_WordsFromBothTracks_AreSortedByStart()
    {
        // Arrange
        Word[] mic = [W("м1", 0, 0.5), W("м2", 4, 4.5)];
        Word[] sys = [W("с1", 2, 2.5), W("с2", 6, 6.5)];
        DiarSegment[] segs = [S(0, 10, 1)];

        // Act
        var result = Merge(mic, sys, segs, 0);

        // Assert
        Assert.Equal(["м1", "с1", "м2", "с2"], result.Utterances.Select(u => u.Text));
    }

    [Fact]
    public void Merge_UnsortedInput_IsSortedByStart()
    {
        // Arrange
        Word[] sys = [W("второе", 1.0, 1.4), W("первое", 0.2, 0.8)];

        // Act
        var result = Merge([], sys, [S(0, 2, 1)], 0);

        // Assert
        Assert.Equal("первое второе", Assert.Single(result.Utterances).Text);
    }

    [Fact]
    public void Merge_SameSpeakerShortPauses_FormOneUtterance()
    {
        // Arrange
        Word[] mic = [W("один", 0, 0.5), W("два", 1.9, 2.3), W("три", 3.8, 4.0)];

        // Act
        var result = Merge(mic, [], [], 0);

        // Assert
        Assert.Equal("один два три", Assert.Single(result.Utterances).Text);
    }

    [Fact]
    public void Merge_PauseLongerThanThreshold_StartsNewUtterance()
    {
        // Arrange
        Word[] mic = [W("один", 0, 0.5), W("два", 2.01, 2.3)];

        // Act
        var result = Merge(mic, [], [], 0);

        // Assert
        Assert.Equal(["один", "два"], result.Utterances.Select(u => u.Text));
    }

    [Fact]
    public void Merge_SpeakerChange_StartsNewUtterance()
    {
        // Arrange
        Word[] mic = [W("вопрос", 0, 0.5)];
        Word[] sys = [W("ответ", 0.6, 1.0)];

        // Act
        var result = Merge(mic, sys, [S(0.5, 2, 3)], 0);

        // Assert
        Assert.Equal([Speakers.Me, Speakers.Numbered(1)], Names(result));
        Assert.Equal(TimeSpan.FromSeconds(0.6), result.Utterances[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(1.0), result.Utterances[1].End);
    }

    [Fact]
    public void Merge_SystemSpeakers_AreNumberedByFirstAppearance()
    {
        // Arrange
        Word[] sys = [W("а", 0.1, 0.4), W("б", 1.1, 1.4), W("в", 2.1, 2.4), W("г", 3.1, 3.4)];
        DiarSegment[] segs = [S(0, 1, 7), S(1, 2, 0), S(2, 3, 7), S(3, 4, 3)];

        // Act
        var result = Merge([], sys, segs, 0);

        // Assert
        Assert.Equal(
            [Speakers.Numbered(1), Speakers.Numbered(2), Speakers.Numbered(1), Speakers.Numbered(3)],
            Names(result));
    }

    [Fact]
    public void Merge_UnknownSpeaker_DoesNotConsumeNumber()
    {
        // Arrange
        Word[] sys = [W("потерян", 0, 0.3), W("найден", 5.1, 5.4)];
        DiarSegment[] segs = [S(5, 6, 4)];

        // Act
        var result = Merge([], sys, segs, 0);

        // Assert
        Assert.Equal([Speakers.Unknown, Speakers.Numbered(1)], Names(result));
    }

    [Fact]
    public void Merge_EmptyInput_ReturnsNoUtterances()
    {
        // Arrange
        Word[] none = [];

        // Act
        var result = Merge(none, none, [], 0);

        // Assert
        Assert.Empty(result.Utterances);
    }

    [Fact]
    public void Merge_SpeakerTable_ListsSpeakersByFirstAppearanceWithDefaultNames()
    {
        // Arrange
        Word[] mic = [W("вопрос", 3, 3.5)];
        Word[] sys = [W("потерян", 0, 0.3), W("ответ", 5.1, 5.4)];

        // Act
        var result = Merge(mic, sys, [S(5, 6, 4)], 0);

        // Assert
        Assert.Equal(
            [
                new TranscriptSpeaker(SpeakerId.Unknown, Speakers.Unknown, "system", Diarized: true),
                new TranscriptSpeaker(SpeakerId.Me, Speakers.Me, "mic", Diarized: false),
                new TranscriptSpeaker(SpeakerId.Diarized("system", 1), Speakers.Numbered(1), "system", Diarized: true),
            ],
            result.Speakers);
    }

    [Fact]
    public void Merge_FixedTracksWithOffsets_AreMergedOnSessionTimeWithTheirNames()
    {
        // Arrange
        var anna = new FixedSpeaker(new SpeakerId("mz:anna"), "Анна");
        var boris = new FixedSpeaker(new SpeakerId("mz:boris"), "Борис");
        TrackWords[] tracks =
        [
            new("me", TimeSpan.Zero, FixedSpeaker.Me, [W("Начнём?", 1, 1.5)], []),
            new("anna", TimeSpan.FromSeconds(2), anna, [W("Да.", 0.5, 0.8)], []),
            new("boris", TimeSpan.FromSeconds(5), boris, [W("Готов.", 0, 0.5)], []),
        ];

        // Act
        var result = TranscriptMerger.Merge(tracks);

        // Assert
        Assert.Equal(["Я", "Анна", "Борис"], Names(result));
        Assert.Equal([1.0, 2.5, 5.0], result.Utterances.Select(u => u.Start.TotalSeconds));
        Assert.All(result.Speakers, s => Assert.False(s.IsNameable));
    }

    [Fact]
    public void Merge_TwoDiarizedTracks_NumberSpeakersAcrossTracks()
    {
        // Arrange
        TrackWords[] tracks =
        [
            new("a", TimeSpan.Zero, DiarizedSpeakers.Instance, [W("раз", 0, 0.5)], [S(0, 1, 0)]),
            new("b", TimeSpan.Zero, DiarizedSpeakers.Instance, [W("два", 3, 3.5)], [S(3, 4, 0)]),
        ];

        // Act
        var result = TranscriptMerger.Merge(tracks);

        // Assert
        Assert.Equal([SpeakerId.Diarized("a", 1), SpeakerId.Diarized("b", 2)], result.Utterances.Select(u => u.Speaker));
        Assert.Equal([Speakers.Numbered(1), Speakers.Numbered(2)], Names(result));
    }

    [Fact]
    public void Merge_DiarizedTrackWithOffset_FindsSpeakerOnTrackTime()
    {
        // Arrange
        TrackWords[] tracks = [new("system", TimeSpan.FromSeconds(10), DiarizedSpeakers.Instance, [W("слово", 1, 1.5)], [S(0.5, 2, 3)])];

        // Act
        var result = TranscriptMerger.Merge(tracks);

        // Assert
        var word = Assert.Single(Assert.Single(result.Utterances).Words);
        Assert.Equal(SpeakerId.Diarized("system", 1), word.Speaker);
        Assert.Equal(TimeSpan.FromSeconds(11), word.Start);
    }

    [Fact]
    public void Utterance_Text_TrimsWhisperLeadingSpaces()
    {
        // Arrange
        Word[] mic = [W(" Привет,", 0, 0.4), W(" как", 0.5, 0.7), W(" дела?", 0.8, 1.1)];

        // Act
        var result = Merge(mic, [], [], 0);

        // Assert
        Assert.Equal("Привет, как дела?", Assert.Single(result.Utterances).Text);
    }
}
