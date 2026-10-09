namespace Meetrec.Cli;

internal static class LevelBar
{
    public const int Width = 10;

    private const double FloorDb = -60;

    // Peaks are shown on a dB scale: on a linear one ordinary speech barely moves the bar.
    public static string Render(float peak)
    {
        var filled = peak <= 0 ? 0 : (int)Math.Round(Math.Clamp((20 * Math.Log10(peak) - FloorDb) / -FloorDb, 0, 1) * Width);
        return new string('█', filled) + new string('·', Width - filled);
    }
}
