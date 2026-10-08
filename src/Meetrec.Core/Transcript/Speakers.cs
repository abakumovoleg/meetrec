namespace Meetrec.Core.Transcript;

public static class Speakers
{
    public const string Me = "Я";

    public const string Unknown = "Спикер ?";

    public static string Numbered(int number) => $"Спикер {number}";
}
