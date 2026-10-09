namespace Meetrec.Core.Transcript;

// Who speaks on a track: one known speaker (the user's mic, a Meetzone participant) or several told apart by diarization.
public abstract record SpeakerSource;

public sealed record FixedSpeaker(SpeakerId Id, string Name) : SpeakerSource
{
    public static readonly FixedSpeaker Me = new(SpeakerId.Me, Speakers.Me);
}

public sealed record DiarizedSpeakers : SpeakerSource
{
    public static readonly DiarizedSpeakers Instance = new();
}
