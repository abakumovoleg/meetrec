namespace Meetrec.Core.Output;

public static class TimeFormat
{
    public static string Clock(TimeSpan time) => $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
}
