using Meetrec.Core.Output;
using static Meetrec.Tests.TestData;

namespace Meetrec.Tests;

public sealed class TranscriptWriterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("meetrec-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void WriteAll_RequestedFormats_WritesTranscriptFilesWithoutBom()
    {
        // Arrange
        var conversation = Conversation([Utterance(Word("тест", 0, 1))]);

        // Act
        var paths = TranscriptWriter.WriteAll(_dir, conversation, ["md", "SRT", "md"], "transcript");

        // Assert
        Assert.Equal(
            [Path.Combine(_dir, "transcript.md"), Path.Combine(_dir, "transcript.srt")],
            paths);
        var bytes = File.ReadAllBytes(paths[0]);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal("[00:00:00] Я: тест\n", File.ReadAllText(paths[0]));
        Assert.False(File.Exists(Path.Combine(_dir, "transcript.json")));
    }

    [Fact]
    public void WriteAll_UnknownFormat_Throws()
    {
        // Arrange
        var conversation = Conversation([Utterance(Word("тест", 0, 1))]);

        // Act
        var act = () => TranscriptWriter.WriteAll(_dir, conversation, ["txt"], "transcript");

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void WriteAll_CustomFileName_UsesItForEveryFormat()
    {
        // Arrange
        var conversation = Conversation([Utterance(Word("тест", 0, 1))]);

        // Act
        var paths = TranscriptWriter.WriteAll(_dir, conversation, ["md", "json"], "call.transcript");

        // Assert
        Assert.Equal(
            [Path.Combine(_dir, "call.transcript.md"), Path.Combine(_dir, "call.transcript.json")],
            paths);
    }

    [Fact]
    public void WriteAll_MissingDirectory_ThrowsWithPath()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "meetrec-tests-missing-" + Guid.NewGuid());

        // Act
        var act = () => TranscriptWriter.WriteAll(directory, Conversation([]), ["md"], "transcript");

        // Assert
        var error = Assert.Throws<MeetrecException>(act);
        Assert.Contains(directory, error.Message);
    }
}
