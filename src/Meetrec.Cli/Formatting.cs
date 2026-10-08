namespace Meetrec.Cli;

internal static class Formatting
{
    public static string Megabytes(long bytes) => $"{bytes / 1024.0 / 1024.0:0.0} МБ";
}
