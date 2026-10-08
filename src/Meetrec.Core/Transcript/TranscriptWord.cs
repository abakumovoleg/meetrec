namespace Meetrec.Core.Transcript;

// Label keeps the diarization label ("Спикер 2") after Speaker is replaced by a name, so names can be changed later.
public sealed record TranscriptWord(string Text, TimeSpan Start, TimeSpan End, string Speaker, Track Track, string? Label = null)
{
    public string SpeakerLabel => Label ?? Speaker;
}
