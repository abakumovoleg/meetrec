namespace Meetrec.Asr;

public static class SpeechSegments
{
    public static IReadOnlyList<SpeechRegion> Windows(IReadOnlyList<SpeechRegion> speech, TimeSpan duration, TimeSpan gap, TimeSpan padding)
    {
        var windows = new List<SpeechRegion>();
        foreach (var region in speech.OrderBy(r => r.Start))
        {
            var start = region.Start - padding < TimeSpan.Zero ? TimeSpan.Zero : region.Start - padding;
            var end = region.End + padding > duration ? duration : region.End + padding;
            if (windows.Count > 0 && region.Start - windows[^1].End <= gap - padding)
            {
                windows[^1] = windows[^1] with { End = end > windows[^1].End ? end : windows[^1].End };
            }
            else
            {
                windows.Add(new SpeechRegion(start, end));
            }
        }

        return windows;
    }

    // Consecutive windows go into one pack while it, with a separator after each window, fits the length;
    // a longer window is a pack of its own.
    public static IReadOnlyList<IReadOnlyList<SpeechRegion>> Pack(IReadOnlyList<SpeechRegion> windows, TimeSpan length, TimeSpan separator)
    {
        var packs = new List<List<SpeechRegion>>();
        var filled = TimeSpan.Zero;
        foreach (var window in windows)
        {
            var size = window.End - window.Start + separator;
            if (packs.Count == 0 || filled + size > length)
            {
                packs.Add([]);
                filled = TimeSpan.Zero;
            }

            packs[^1].Add(window);
            filled += size;
        }

        return packs;
    }
}
