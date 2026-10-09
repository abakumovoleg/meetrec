using System.Globalization;

namespace Meetrec.Core.Transcript;

// Identity of a speaker that survives renames; names live in the transcript's speaker table.
public readonly record struct SpeakerId(string Value)
{
    public static readonly SpeakerId Me = new("me");

    public static readonly SpeakerId Unknown = new("unknown");

    public static SpeakerId Diarized(string track, int number) => new(string.Create(CultureInfo.InvariantCulture, $"{track}:{number}"));

    public override string ToString() => Value;
}
