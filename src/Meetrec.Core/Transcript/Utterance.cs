namespace Meetrec.Core.Transcript;

public sealed record Utterance(string Speaker, Track Track, IReadOnlyList<TranscriptWord> Words)
{
    public TimeSpan Start => Words[0].Start;

    public TimeSpan End => Words[^1].End;

    public string Text => string.Join(' ', Words.Select(w => w.Text.Trim()).Where(t => t.Length > 0));
}
