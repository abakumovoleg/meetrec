namespace Meetrec.Cli;

internal static class Formatting
{
    public static string Megabytes(long bytes) => $"{bytes / 1024.0 / 1024.0:0.0} МБ";

    public static string Duration(TimeSpan duration) =>
        duration.TotalMinutes >= 1 ? $"{(int)duration.TotalMinutes} мин {duration.Seconds} с" : $"{duration.TotalSeconds:0.0} с";
}
