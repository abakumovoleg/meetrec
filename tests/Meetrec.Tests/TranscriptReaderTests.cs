using Meetrec.Core.Output;
using Meetrec.Core.Transcript;
using static Meetrec.Tests.TestData;

namespace Meetrec.Tests;

public sealed class TranscriptReaderTests : IDisposable
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private readonly string _dir = Directory.CreateTempSubdirectory("meetrec-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Read_WrittenJson_RestoresSpeakerTableAndUtterances()
    {
        // Arrange
        var conversation = Conversation(
            [Utterance(Word(" привет", 0.5, 1.0)), Utterance(Word("да", 2.0, 2.25, S1), Word("ну", 2.5, 2.75, S1)), Utterance(Word("угу", 6, 6.5, SpeakerId.Unknown))],
            (S1, "Олег"));
        var path = Assert.Single(TranscriptWriter.WriteAll(_dir, conversation, ["json"], "transcript"));

        // Act
        var result = TranscriptReader.Read(path);

        // Assert
        Assert.Equal(2, result.SchemaVersion);
        Assert.False(result.IsOutdated);
        Assert.Equal(conversation.Speakers, result.Conversation.Speakers);
        Assert.Equal(
            new TranscriptWord("привет", TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1), SpeakerId.Me, MicTrack),
            result.Conversation.Utterances[0].Words[0]);
        Assert.Equal(conversation.Utterances[1].Words, result.Conversation.Utterances[1].Words);
    }

    [Fact]
    public void Read_Version1_MapsLabelsToIdsAndNamesToTable()
    {
        // Arrange
        var path = Path.Combine(Fixtures, "transcript-v1.json");

        // Act
        var result = TranscriptReader.Read(path);

        // Assert
        Assert.True(result.IsOutdated);
        Assert.Equal(
            [
                new TranscriptSpeaker(SpeakerId.Me, Speakers.Me, MicTrack, Diarized: false),
                new TranscriptSpeaker(S1, "Олег", SystemTrack, Diarized: true),
                new TranscriptSpeaker(S2, "Рома", SystemTrack, Diarized: true),
                new TranscriptSpeaker(S3, "Олег", SystemTrack, Diarized: true),
                new TranscriptSpeaker(SpeakerId.Unknown, Speakers.Unknown, SystemTrack, Diarized: true),
            ],
            result.Conversation.Speakers);
    }

    [Theory]
    [InlineData("md")]
    [InlineData("srt")]
    public void Read_Version1_RendersAsTheVersionThatWroteIt(string format)
    {
        // Arrange
        var formatter = format == "md" ? (ITranscriptFormatter)new MarkdownFormatter() : new SrtFormatter();
        var expected = File.ReadAllText(Path.Combine(Fixtures, $"transcript-v1.{format}"));

        // Act
        var rendered = Render(formatter, TranscriptReader.Read(Path.Combine(Fixtures, "transcript-v1.json")).Conversation);

        // Assert
        Assert.Equal(expected, rendered);
    }

    [Fact]
    public void Read_Version1RenamedBack_RegroupsLikeTheVersionThatWroteIt()
    {
        // Arrange
        var conversation = TranscriptReader.Read(Path.Combine(Fixtures, "transcript-v1.json")).Conversation;

        // Act
        var reset = SpeakerNames.Reset(conversation);

        // Assert
        Assert.Equal(
            [Speakers.Me, Speakers.Numbered(1), Speakers.Numbered(2), Speakers.Me, Speakers.Numbered(1), Speakers.Numbered(3), Speakers.Unknown],
            reset.Utterances.Select(u => reset.NameOf(u.Speaker)));
    }

    [Fact]
    public void Read_NewerSchema_Throws()
    {
        // Arrange
        var path = Path.Combine(_dir, "transcript.json");
        File.WriteAllText(path, """{ "schemaVersion": 3, "speakers": [], "utterances": [] }""");

        // Act
        var act = () => TranscriptReader.Read(path);

        // Assert
        Assert.Contains("более новой версией", Assert.Throws<MeetrecException>(act).Message);
    }

    [Fact]
    public void Read_SpeakerMissingFromTable_ThrowsWithPath()
    {
        // Arrange
        var path = Path.Combine(_dir, "transcript.json");
        File.WriteAllText(path, """
            { "schemaVersion": 2, "speakers": [],
              "utterances": [{ "speakerId": "me", "speaker": "Я", "track": "mic", "start": 0, "end": 1, "text": "да",
                               "words": [{ "text": "да", "start": 0, "end": 1, "speakerId": "me", "track": "mic" }] }] }
            """);

        // Act
        var act = () => TranscriptReader.Read(path);

        // Assert
        Assert.Contains(path, Assert.Throws<MeetrecException>(act).Message);
    }

    [Fact]
    public void Read_BrokenJson_ThrowsWithPath()
    {
        // Arrange
        var path = Path.Combine(_dir, "transcript.json");
        File.WriteAllText(path, "{ \"utterances\": [");

        // Act
        var act = () => TranscriptReader.Read(path);

        // Assert
        var error = Assert.Throws<MeetrecException>(act);
        Assert.Contains(path, error.Message);
    }
}
