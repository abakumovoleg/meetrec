using Meetrec.Capture.Meetzone;
using Meetrec.Core.Audio;

namespace Meetrec.Tests;

public sealed class MeetzoneRecorderTests : IDisposable
{
    private const int SampleRate = 16_000;
    private const int BlockSamples = 4096;

    private static readonly DateTimeOffset Wall = DateTimeOffset.FromUnixTimeMilliseconds(1_791_533_729_000);

    private readonly string _dir = Directory.CreateTempSubdirectory("meetrec-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Handle_SlotAudio_GoesToItsOwnerWithOffsetAndName()
    {
        // Arrange
        var recorder = new MeetzoneRecorder(_dir);
        Conference(recorder);

        // Act
        recorder.Handle(Block("in-2-3", 12.5, 0.25f));
        recorder.Finish();

        // Assert
        var track = Assert.Single(recorder.Tracks());
        Assert.Equal(new SessionTrack("mz-ruskutdusov.wav", TrackSource.Participant, 2_500, Speaker: new SessionSpeaker("mz:ruskutdusov", "Ruslan Kutdusov")), track);
        Assert.Equal(Wall, recorder.StartedAt);
        Assert.Equal(0.25f, Load(track).Samples[0], 0.001f);
    }

    [Fact]
    public void Handle_ReconnectedParticipant_JoinsSlotsIntoOneTrackWithSilenceBetween()
    {
        // Arrange
        var recorder = new MeetzoneRecorder(_dir);
        Conference(recorder);
        recorder.Handle(Block("in-2-3", 11, 0.25f));
        recorder.Handle(new Presence("6db2446f", "Ruslan Kutdusov", "ruskutdusov", false, true));
        recorder.Handle(new Presence("20972802", "Ruslan Kutdusov", "ruskutdusov", false, false));
        recorder.Handle(new Slots([new Slot("in-2-10", 2971282645, null)]));
        recorder.Handle(new Sources([new SourceOwner(2971282645, "20972802")]));

        // Act
        recorder.Handle(Block("in-2-10", 14, 0.5f));
        recorder.Finish();

        // Assert
        var track = Assert.Single(recorder.Tracks());
        var clip = Load(track);
        Assert.Equal(3 * SampleRate + BlockSamples, clip.Samples.Length);
        Assert.Equal(0f, clip.Samples[2 * SampleRate]);
        Assert.Equal(0.5f, clip.Samples[3 * SampleRate], 0.001f);
    }

    [Fact]
    public void Handle_OwnOutgoingTrack_IsNamedFromOwnPresenceAndListedFirst()
    {
        // Arrange
        var recorder = new MeetzoneRecorder(_dir);
        Conference(recorder);
        recorder.Handle(Block("in-2-3", 10.2, 0.25f));

        // Act
        recorder.Handle(Block(Audio.Outgoing, 11, 0.5f));
        recorder.Finish();

        // Assert
        Assert.Equal(
            [("mz:olabakumov", "Oleg Abakumov", 1_000), ("mz:ruskutdusov", "Ruslan Kutdusov", 200)],
            recorder.Tracks().Select(t => (t.Speaker!.Id, t.Speaker.Name, t.OffsetMs)));
    }

    [Fact]
    public void Handle_AudioBeforeOwnerIsKnown_WaitsAndIsWrittenWhenItIs()
    {
        // Arrange
        var recorder = new MeetzoneRecorder(_dir);
        recorder.Handle(new Started("c1", TimeSpan.FromSeconds(10), Wall));
        recorder.Handle(new Slots([new Slot("in-2-3", 912173308, null)]));
        recorder.Handle(Block("in-2-3", 11, 0.25f));
        recorder.Handle(Block("in-2-3", 12, 0.25f));
        recorder.Handle(new Sources([new SourceOwner(912173308, "6db2446f")]));

        // Act
        recorder.Handle(new Presence("6db2446f", "Ruslan Kutdusov", "ruskutdusov", false, false));
        recorder.Finish();

        // Assert
        var track = Assert.Single(recorder.Tracks());
        Assert.Equal(1_000, track.OffsetMs);
        Assert.Equal(SampleRate + BlockSamples, Load(track).Samples.Length);
    }

    [Fact]
    public void Finish_AudioWhoseOwnerNeverBecameKnown_IsKeptUnderItsSlot()
    {
        // Arrange
        var recorder = new MeetzoneRecorder(_dir);
        recorder.Handle(new Started("c1", TimeSpan.Zero, Wall));
        recorder.Handle(Block("in-2-7", 1, 0.25f));

        // Act
        recorder.Finish();

        // Assert
        var track = Assert.Single(recorder.Tracks());
        Assert.Equal(("mz-in-2-7.wav", "Спикер ?"), (track.File, track.Speaker!.Name));
    }

    [Fact]
    public void Handle_ReloadedPage_PlacesNewContextByWallClock()
    {
        // Arrange
        var recorder = new MeetzoneRecorder(_dir);
        Conference(recorder);
        recorder.Handle(Block("in-2-3", 10, 0.25f));
        recorder.Handle(new Started("c2", TimeSpan.FromSeconds(1), Wall + TimeSpan.FromSeconds(30)));
        recorder.Handle(new Slots([new Slot("in-1-3", 912173308, null)]));

        // Act
        recorder.Handle(Block("in-1-3", 2, 0.5f));
        recorder.Finish();

        // Assert
        var clip = Load(Assert.Single(recorder.Tracks()));
        Assert.Equal(31 * SampleRate + BlockSamples, clip.Samples.Length);
        Assert.Equal(0.5f, clip.Samples[31 * SampleRate], 0.001f);
    }

    [Fact]
    public void Handle_PeerToPeerSlot_TakesOwnerFromSdp()
    {
        // Arrange
        var recorder = new MeetzoneRecorder(_dir);
        recorder.Handle(new Started("c1", TimeSpan.Zero, Wall));
        recorder.Handle(new Presence("6db2446f", "Ruslan Kutdusov", "ruskutdusov", false, false));
        recorder.Handle(new Slots([new Slot("in-1-0", 42, "6db2446f")]));

        // Act
        recorder.Handle(Block("in-1-0", 1, 0.25f));
        recorder.Finish();

        // Assert
        Assert.Equal("mz:ruskutdusov", Assert.Single(recorder.Tracks()).Speaker!.Id);
    }

    [Fact]
    public void Handle_OverlappingBlock_KeepsOnlyItsNewPart()
    {
        // Arrange
        var recorder = new MeetzoneRecorder(_dir);
        Conference(recorder);
        recorder.Handle(Block("in-2-3", 11, 0.25f));

        // Act
        recorder.Handle(Block("in-2-3", 11 + 0.5 * BlockSamples / SampleRate, 0.5f));
        recorder.Finish();

        // Assert
        var clip = Load(Assert.Single(recorder.Tracks()));
        Assert.Equal(BlockSamples + BlockSamples / 2, clip.Samples.Length);
        Assert.Equal(0.25f, clip.Samples[BlockSamples - 1], 0.001f);
        Assert.Equal(0.5f, clip.Samples[BlockSamples], 0.001f);
    }

    [Fact]
    public void Snapshot_NamesWhoIsHeardAtTheLatestTime()
    {
        // Arrange
        var recorder = new MeetzoneRecorder(_dir);
        Conference(recorder);
        recorder.Handle(Block("in-2-3", 11, 0.25f));
        recorder.Handle(Block(Audio.Outgoing, 20, 0.25f));

        // Act
        var snapshot = recorder.Snapshot();
        recorder.Finish();

        // Assert
        Assert.Equal(2, snapshot.Participants);
        Assert.Equal(["Oleg Abakumov"], snapshot.Speaking);
    }

    // Recording started at AudioContext time 10 s; Ruslan speaks through slot in-2-3.
    private static void Conference(MeetzoneRecorder recorder)
    {
        recorder.Handle(new Started("c1", TimeSpan.FromSeconds(10), Wall));
        recorder.Handle(new Presence("e158c45b", "Oleg Abakumov", "olabakumov", true, false));
        recorder.Handle(new Presence("6db2446f", "Ruslan Kutdusov", "ruskutdusov", false, false));
        recorder.Handle(new Slots([new Slot("in-2-3", 912173308, null)]));
        recorder.Handle(new Sources([new SourceOwner(912173308, "6db2446f")]));
    }

    private static Audio Block(string track, double at, float level)
    {
        var pcm = new byte[BlockSamples * 2];
        var value = (short)Math.Round(level * 32767);
        for (var i = 0; i < BlockSamples; i++)
        {
            BitConverter.TryWriteBytes(pcm.AsSpan(i * 2), value);
        }

        return new Audio(track, TimeSpan.FromSeconds(at), pcm);
    }

    private AudioClip Load(SessionTrack track) => AudioLoader.LoadMono16k(Path.Combine(_dir, track.File));
}
