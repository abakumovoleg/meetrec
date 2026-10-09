using Meetrec.Core.Transcript;

namespace Meetrec.Core.Audio;

public sealed class SpeechMask
{
    public static readonly TimeSpan DefaultPadding = TimeSpan.FromSeconds(0.25);

    public static readonly TimeSpan DefaultMergeGap = TimeSpan.FromSeconds(0.5);

    public static readonly TimeSpan DefaultMaxSnapDistance = TimeSpan.FromSeconds(1.5);

    private readonly List<SpeechRegion> _regions;

    private SpeechMask(List<SpeechRegion> regions) => _regions = regions;

    public IReadOnlyList<SpeechRegion> Regions => _regions;

    public bool IsEmpty => _regions.Count == 0;

    public TimeSpan Lead(TimeSpan padding) =>
        IsEmpty || _regions[0].Start <= padding ? TimeSpan.Zero : _regions[0].Start - padding;

    public static SpeechMask Build(IEnumerable<SpeechRegion> regions, TimeSpan duration, TimeSpan padding, TimeSpan mergeGap)
    {
        var merged = new List<SpeechRegion>();
        foreach (var region in regions.OrderBy(r => r.Start))
        {
            var start = Max(TimeSpan.Zero, region.Start - padding);
            var end = Min(duration, region.End + padding);
            if (end <= start)
            {
                continue;
            }

            if (merged.Count > 0 && start - merged[^1].End <= mergeGap)
            {
                merged[^1] = merged[^1] with { End = Max(merged[^1].End, end) };
            }
            else
            {
                merged.Add(new SpeechRegion(start, end));
            }
        }

        return new SpeechMask(merged);
    }

    // Whisper often gives words at the start of a segment zero-length or shifted timestamps that land in the
    // pause next to real speech, so words near speech are snapped into it; only words far from any speech
    // (typical hallucinations on silence) are dropped.
    public IReadOnlyList<Word> Apply(IReadOnlyList<Word> words, TimeSpan maxSnapDistance)
    {
        var result = new List<Word>(words.Count);
        foreach (var word in words)
        {
            var region = BestOverlap(word) ?? Nearest(word, maxSnapDistance);
            if (region is null)
            {
                continue;
            }

            var start = Clamp(word.Start, region.Start, region.End);
            var end = Clamp(word.End, start, region.End);
            result.Add(word with { Start = start, End = end });
        }

        return result;
    }

    private SpeechRegion? BestOverlap(Word word)
    {
        SpeechRegion? best = null;
        var bestOverlap = TimeSpan.Zero;
        foreach (var region in _regions)
        {
            var overlap = Min(word.End, region.End) - Max(word.Start, region.Start);
            if (overlap > bestOverlap)
            {
                best = region;
                bestOverlap = overlap;
            }
        }

        return best;
    }

    private SpeechRegion? Nearest(Word word, TimeSpan maxDistance)
    {
        SpeechRegion? best = null;
        var bestDistance = TimeSpan.MaxValue;
        foreach (var region in _regions)
        {
            var distance = Max(region.Start - word.End, word.Start - region.End);
            if (distance <= maxDistance && distance < bestDistance)
            {
                best = region;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max) => value < min ? min : value > max ? max : value;

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
