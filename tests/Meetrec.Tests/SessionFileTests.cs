using Meetrec.Core.Transcript;

namespace Meetrec.Tests;

public sealed class SessionFileTests : IDisposable
{
    private static readonly SessionSpeaker Me = new(SpeakerId.Me.Value, Speakers.Me);

    private readonly string _dir = Directory.CreateTempSubdirectory("meetrec-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string SessionPath => Path.Combine(_dir, SessionLayout.SessionFileName);

    [Fact]
    public void WriteThenRead_RoundTripsSession()
    {
        // Arrange
        var session = new Session(new DateTimeOffset(2026, 10, 8, 12, 0, 34, TimeSpan.Zero), "0.1.0",
        [
            new SessionTrack(SessionLayout.MicFile, TrackSource.Mic, 12, "MacBook Pro Microphone", Me),
            new SessionTrack(SessionLayout.SystemFile, TrackSource.System, 0, "Системный звук"),
        ]);

        // Act
        SessionFile.Write(SessionPath, session);
        var read = SessionFile.Read(SessionPath);

        // Assert
        Assert.Equal(session.StartedAtUtc, read.StartedAtUtc);
        Assert.Equal(session.Version, read.Version);
        Assert.Equal(session.Tracks, read.Tracks);
        var json = File.ReadAllText(SessionPath);
        Assert.Contains("\"schemaVersion\": 2", json);
        Assert.Contains("\"source\": \"mic\"", json);
        Assert.Contains("Системный звук", json);
        Assert.DoesNotContain("micDevice", json);
        Assert.DoesNotContain("\r", json);
    }

    [Fact]
    public void Read_Version1WithMicFile_ConvertsToMicAndSystemTracks()
    {
        // Arrange
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "session-v1.json"), SessionPath);
        File.WriteAllBytes(Path.Combine(_dir, SessionLayout.MicFile), []);

        // Act
        var session = SessionFile.Read(SessionPath);

        // Assert
        Assert.Equal("0.1.0", session.Version);
        Assert.Equal(
            [
                new SessionTrack(SessionLayout.MicFile, TrackSource.Mic, 500, "MacBook Pro Microphone", Me),
                new SessionTrack(SessionLayout.SystemFile, TrackSource.System, 0, "Системный звук"),
            ],
            session.Tracks);
    }

    [Fact]
    public void Read_Version1WithoutMicFile_HasOnlySystemTrack()
    {
        // Arrange
        File.WriteAllText(SessionPath, """{ "startedAtUtc": "2026-10-08T12:00:00Z", "version": "0.1.0" }""");

        // Act
        var session = SessionFile.Read(SessionPath);

        // Assert
        Assert.Equal([new SessionTrack(SessionLayout.SystemFile, TrackSource.System, 0)], session.Tracks);
    }

    [Fact]
    public void Read_NewerSchema_Throws()
    {
        // Arrange
        File.WriteAllText(SessionPath, """{ "schemaVersion": 3, "startedAtUtc": "2026-10-08T12:00:00Z", "version": "9.0.0", "tracks": [] }""");

        // Act
        var act = () => SessionFile.Read(SessionPath);

        // Assert
        Assert.Contains("более новой версией", Assert.Throws<MeetrecException>(act).Message);
    }

    [Fact]
    public void Read_BrokenJson_ThrowsWithPath()
    {
        // Arrange
        File.WriteAllText(SessionPath, "{ не json");

        // Act
        var act = () => SessionFile.Read(SessionPath);

        // Assert
        var error = Assert.Throws<MeetrecException>(act);
        Assert.Contains(SessionPath, error.Message);
    }

    [Fact]
    public void Read_NullJson_Throws()
    {
        // Arrange
        File.WriteAllText(SessionPath, "null");

        // Act
        var act = () => SessionFile.Read(SessionPath);

        // Assert
        Assert.Throws<MeetrecException>(act);
    }
}
