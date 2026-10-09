namespace Meetrec.Core.Transcript;

// Words of speakers sharing a name are grouped together, so Speaker is the first word's and may differ from later ones.
public sealed record Utterance(SpeakerId Speaker, string Track, IReadOnlyList<TranscriptWord> Words)
{
    public TimeSpan Start => Words[0].Start;

    public TimeSpan End => Words[^1].End;

    public string Text => string.Join(' ', Words.Select(w => w.Text.Trim()).Where(t => t.Length > 0));
}
