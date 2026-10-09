namespace Meetrec.Core.Tests;

public sealed class TranscribeInputTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("meetrec-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Resolve_SessionWithBothTracks_UsesSessionOffset()
    {
        // Arrange
        Touch(TranscribeInput.MicFileName);
        Touch(TranscribeInput.SystemFileName);
        SessionFile.Write(Path.Combine(_dir, SessionFile.FileName), new Session(DateTimeOffset.UnixEpoch, null, null, 42, "0.1.0"));

        // Act
        var input = TranscribeInput.Resolve(_dir);

        // Assert
        Assert.Equal(Path.Combine(_dir, TranscribeInput.MicFileName), input.MicPath);
        Assert.Equal(Path.Combine(_dir, TranscribeInput.SystemFileName), input.SystemPath);
        Assert.Equal(42, input.OffsetMs);
        Assert.Equal(_dir, input.OutputDirectory);
        Assert.Equal("transcript", input.OutputName);
    }

    [Fact]
    public void Resolve_SessionWithoutMicAndSessionFile_HasNoMicTrackAndZeroOffset()
    {
        // Arrange
        Touch(TranscribeInput.SystemFileName);

        // Act
        var input = TranscribeInput.Resolve(_dir);

        // Assert
        Assert.False(input.HasMicTrack);
        Assert.Null(input.Session);
        Assert.Equal(0, input.OffsetMs);
    }

    [Fact]
    public void Resolve_SessionWithoutSystemTrack_Throws()
    {
        // Arrange
        Touch(TranscribeInput.MicFileName);

        // Act
        var act = () => TranscribeInput.Resolve(_dir);

        // Assert
        Assert.Throws<MeetrecException>(act);
    }

    [Fact]
    public void Resolve_SingleWav_TreatsItAsSystemTrackAndWritesNextToIt()
    {
        // Arrange
        var file = Touch("Созвон.WAV");

        // Act
        var input = TranscribeInput.Resolve(file);

        // Assert
        Assert.Null(input.MicPath);
        Assert.Equal(file, input.SystemPath);
        Assert.Equal(_dir, input.OutputDirectory);
        Assert.Equal("Созвон.transcript", input.OutputName);
    }

    [Fact]
    public void Resolve_NonWavFile_Throws()
    {
        // Arrange
        var file = Touch("audio.mp4");

        // Act
        var act = () => TranscribeInput.Resolve(file);

        // Assert
        Assert.Throws<MeetrecException>(act);
    }

    [Fact]
    public void Resolve_MissingPath_Throws()
    {
        // Arrange
        var path = Path.Combine(_dir, "нет");

        // Act
        var act = () => TranscribeInput.Resolve(path);

        // Assert
        Assert.Throws<MeetrecException>(act);
    }

    private string Touch(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, []);
        return path;
    }
}
