namespace Meetrec.Core.Transcript;

public sealed record DiarSegment(TimeSpan Start, TimeSpan End, int Speaker);
