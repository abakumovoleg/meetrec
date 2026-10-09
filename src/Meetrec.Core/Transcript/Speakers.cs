using System.Globalization;

namespace Meetrec.Core.Transcript;

// Default display names; the transcript's speaker table holds the current ones.
public static class Speakers
{
    public const string Me = "Я";

    public const string Unknown = "Спикер ?";

    private const string NumberedPrefix = "Спикер ";

    public static string Numbered(int number) => NumberedPrefix + number.ToString(CultureInfo.InvariantCulture);

    public static bool TryParseNumbered(string name, out int number)
    {
        number = 0;
        return name.StartsWith(NumberedPrefix, StringComparison.Ordinal)
            && int.TryParse(name.AsSpan(NumberedPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    public static string DefaultName(SpeakerId id)
    {
        if (id == SpeakerId.Unknown)
        {
            return Unknown;
        }

        var separator = id.Value.LastIndexOf(':');
        return separator >= 0 && int.TryParse(id.Value.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? Numbered(number)
            : id.Value;
    }
}
