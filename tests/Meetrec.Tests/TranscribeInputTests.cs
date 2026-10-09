using Meetrec.Core.Transcript;

namespace Meetrec.Tests;

public sealed class TranscribeInputTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("meetrec-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Resolve_SessionWithBothTracks_TakesOffsetsAndSpeakersFromSession()
    {
        // Arrange
        Touch(SessionLayout.MicFile);
        Touch(SessionLayout.SystemFile);
        WriteSession(
            new SessionTrack(SessionLayout.MicFile, TrackSource.Mic, 42, Speaker: new SessionSpeaker("me", "Я")),
            new SessionTrack(SessionLayout.SystemFile, TrackSource.System, 0));

        // Act
        var input = TranscribeInput.Resolve(_dir);

        // Assert
        Assert.Equal(
            [
                new SourceTrack("mic", Path.Combine(_dir, SessionLayout.MicFile), TimeSpan.FromMilliseconds(42), FixedSpeaker.Me, TrackSource.Mic),
                new SourceTrack("system", Path.Combine(_dir, SessionLayout.SystemFile), TimeSpan.Zero, DiarizedSpeakers.Instance, TrackSource.System),
            ],
            input.Tracks);
        Assert.Equal(1, input.FixedSpeakers);
        Assert.Equal(_dir, input.OutputDirectory);
        Assert.Equal("transcript", input.OutputName);
    }

    [Fact]
    public void Resolve_SessionOfParticipants_GivesEachTrackItsSpeaker()
    {
        // Arrange
        Touch("anna.wav");
        Touch("boris.wav");
        WriteSession(
            new SessionTrack("anna.wav", TrackSource.Participant, 0, Speaker: new SessionSpeaker("mz:anna", "Анна")),
            new SessionTrack("boris.wav", TrackSource.Participant, 1500, Speaker: new SessionSpeaker("mz:boris", "Борис")));

        // Act
        var input = TranscribeInput.Resolve(_dir);

        // Assert
        Assert.Equal(["anna", "boris"], input.Tracks.Select(t => t.Id));
        Assert.Equal(new FixedSpeaker(new SpeakerId("mz:boris"), "Борис"), input.Tracks[1].Speakers);
        Assert.Equal(2, input.FixedSpeakers);
    }

    [Fact]
    public void Resolve_SessionTrackFileMissing_Throws()
    {
        // Arrange
        Touch(SessionLayout.SystemFile);
        WriteSession(new SessionTrack("anna.wav", TrackSource.Participant, 0, Speaker: new SessionSpeaker("mz:anna", "Анна")));

        // Act
        var act = () => TranscribeInput.Resolve(_dir);

        // Assert
        Assert.Contains("anna.wav", Assert.Throws<MeetrecException>(act).Message);
    }

    [Fact]
    public void Resolve_BothTracksWithoutSessionFile_TakesTracksWithoutOffsets()
    {
        // Arrange
        Touch(SessionLayout.MicFile);
        Touch(SessionLayout.SystemFile);

        // Act
        var input = TranscribeInput.Resolve(_dir);

        // Assert
        Assert.Null(input.Session);
        Assert.Equal([FixedSpeaker.Me, DiarizedSpeakers.Instance], input.Tracks.Select(t => t.Speakers));
        Assert.All(input.Tracks, t => Assert.Equal(TimeSpan.Zero, t.Offset));
    }

    [Fact]
    public void Resolve_SessionWithoutMicAndSessionFile_HasOnlyDiarizedSystemTrack()
    {
        // Arrange
        Touch(SessionLayout.SystemFile);

        // Act
        var input = TranscribeInput.Resolve(_dir);

        // Assert
        var track = Assert.Single(input.Tracks);
        Assert.Equal(DiarizedSpeakers.Instance, track.Speakers);
        Assert.Equal(0, input.FixedSpeakers);
    }

    [Fact]
    public void Resolve_SessionWithoutSystemTrack_Throws()
    {
        // Arrange
        Touch(SessionLayout.MicFile);

        // Act
        var act = () => TranscribeInput.Resolve(_dir);

        // Assert
        Assert.Throws<MeetrecException>(act);
    }

    [Fact]
    public void Resolve_SingleWav_TreatsItAsDiarizedSystemTrackAndWritesNextToIt()
    {
        // Arrange
        var file = Touch("Созвон.WAV");

        // Act
        var input = TranscribeInput.Resolve(file);

        // Assert
        Assert.Equal([new SourceTrack("system", file, TimeSpan.Zero, DiarizedSpeakers.Instance, TrackSource.System)], input.Tracks);
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

    private void WriteSession(params SessionTrack[] tracks) =>
        SessionFile.Write(Path.Combine(_dir, SessionLayout.SessionFileName), new Session(DateTimeOffset.UnixEpoch, "0.1.0", tracks));

    private string Touch(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, []);
        return path;
    }
}
