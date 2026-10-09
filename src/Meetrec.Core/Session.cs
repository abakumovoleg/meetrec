namespace Meetrec.Core;

public sealed record Session(DateTimeOffset StartedAtUtc, string Version, IReadOnlyList<SessionTrack> Tracks);

// OffsetMs: delay of the track's start relative to the session start; track time + OffsetMs = session time.
public sealed record SessionTrack(string File, TrackSource Source, int OffsetMs, string? Device = null, SessionSpeaker? Speaker = null);

public enum TrackSource
{
    Mic,
    System,
    Participant,
}

// A track with a speaker is one known person; a track without one is diarized.
public sealed record SessionSpeaker(string Id, string Name);
