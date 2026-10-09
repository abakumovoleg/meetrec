namespace Meetrec.Tests;

public sealed class SessionFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("meetrec-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void WriteThenRead_RoundTripsSession()
    {
        // Arrange
        var path = Path.Combine(_dir, SessionFile.FileName);
        var session = new Session(new DateTimeOffset(2026, 10, 8, 12, 0, 34, TimeSpan.Zero), "MacBook Pro Microphone", "Системный звук", 12, "0.1.0");

        // Act
        SessionFile.Write(path, session);
        var read = SessionFile.Read(path);

        // Assert
        Assert.Equal(session, read);
        var json = File.ReadAllText(path);
        Assert.Contains("\"offsetMs\": 12", json);
        Assert.Contains("Системный звук", json);
        Assert.DoesNotContain("\r", json);
    }

    [Fact]
    public void Read_MissingFields_UsesDefaults()
    {
        // Arrange
        var path = Path.Combine(_dir, SessionFile.FileName);
        File.WriteAllText(path, """{ "startedAtUtc": "2026-10-08T12:00:00Z", "version": "0.1.0" }""");

        // Act
        var session = SessionFile.Read(path);

        // Assert
        Assert.Equal(0, session.OffsetMs);
        Assert.Null(session.MicDevice);
    }

    [Fact]
    public void Read_BrokenJson_ThrowsWithPath()
    {
        // Arrange
        var path = Path.Combine(_dir, SessionFile.FileName);
        File.WriteAllText(path, "{ не json");

        // Act
        var act = () => SessionFile.Read(path);

        // Assert
        var error = Assert.Throws<MeetrecException>(act);
        Assert.Contains(path, error.Message);
    }

    [Fact]
    public void Read_NullJson_Throws()
    {
        // Arrange
        var path = Path.Combine(_dir, SessionFile.FileName);
        File.WriteAllText(path, "null");

        // Act
        var act = () => SessionFile.Read(path);

        // Assert
        Assert.Throws<MeetrecException>(act);
    }
}
