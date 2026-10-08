namespace Meetrec.Core.Capture;

public sealed class SilenceWatch(TimeSpan window)
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(10);

    private bool _heard;
    private bool _reported;

    public bool ShouldWarn(TimeSpan elapsed, float level)
    {
        _heard |= level > 0;
        if (_heard || _reported || elapsed < window)
        {
            return false;
        }

        _reported = true;
        return true;
    }
}
