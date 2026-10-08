namespace Meetrec.Cli;

internal static class Formatting
{
    public static string Megabytes(long bytes) => $"{bytes / 1024.0 / 1024.0:0.0} МБ";

    public static string Duration(TimeSpan duration) =>
        duration.TotalMinutes >= 1 ? $"{(int)duration.TotalMinutes} мин {duration.Seconds} с" : $"{duration.TotalSeconds:0.0} с";

    public static string Clock(TimeSpan time) => $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";

    public static string Count(int n, string one, string few, string many)
    {
        var word = (n % 100) is >= 11 and <= 14 ? many : (n % 10) switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many,
        };
        return $"{n} {word}";
    }
}
