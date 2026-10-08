namespace Meetrec.Core.Transcript;

public sealed record TranscriptWord(string Text, TimeSpan Start, TimeSpan End, string Speaker, Track Track);
