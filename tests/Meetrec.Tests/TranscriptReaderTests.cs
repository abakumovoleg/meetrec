using Meetrec.Core.Output;
using Meetrec.Core.Transcript;
using static Meetrec.Tests.TestData;

namespace Meetrec.Tests;

public sealed class TranscriptReaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("meetrec-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Read_WrittenJson_RestoresUtterancesWordsAndLabels()
    {
        // Arrange
        Utterance[] utterances =
        [
            Utterance(Word(" привет", 0.5, 1.0)),
            Utterance(Word("да", 2.0, 2.25, "Олег", Track.System, Speakers.Numbered(1)), Word("ну", 2.5, 2.75, "Олег", Track.System, Speakers.Numbered(1))),
        ];
        var path = Assert.Single(TranscriptWriter.WriteAll(_dir, utterances, ["json"]));

        // Act
        var result = TranscriptReader.Read(path);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(new TranscriptWord("привет", TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1), Speakers.Me, Track.Mic), result[0].Words[0]);
        Assert.Equal("Олег", result[1].Speaker);
        Assert.Equal(Track.System, result[1].Track);
        Assert.Equal(utterances[1].Words, result[1].Words);
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
