using Meetrec.Core.Transcript;

namespace Meetrec.Asr;

// Whisper loses speech that follows a long stretch without it: the 30 s window hallucinates ("Продолжение
// следует...") with timestamps smeared over the speech, and the real words are skipped. Such regions are found by
// the density of short words and transcribed again on their own; regions Whisper handled are left untouched.
public static class SpeechRepair
{
    public static IReadOnlyList<SpeechRegion> FindSparse(
        IReadOnlyList<SpeechRegion> regions, IReadOnlyList<Word> words, double minWordsPerSecond, TimeSpan maxWordLength) =>
        [.. regions.Where(r =>
            words.Count(w => w.End - w.Start <= maxWordLength && Overlaps(w, r)) / (r.End - r.Start).TotalSeconds < minWordsPerSecond)];

    public static IReadOnlyList<SpeechRegion> Windows(
        IReadOnlyList<SpeechRegion> sparse, TimeSpan duration, TimeSpan groupGap, TimeSpan padding)
    {
        var groups = new List<SpeechRegion>();
        foreach (var region in sparse.OrderBy(r => r.Start))
        {
            if (groups.Count > 0 && region.Start - groups[^1].End < groupGap)
            {
                groups[^1] = groups[^1] with { End = region.End };
            }
            else
            {
                groups.Add(region);
            }
        }

        return [.. groups.Select(g => new SpeechRegion(
            g.Start > padding ? g.Start - padding : TimeSpan.Zero,
            g.End + padding < duration ? g.End + padding : duration))];
    }

    public static IReadOnlyList<Word> Replace(IReadOnlyList<Word> words, IReadOnlyList<Word> repaired, IReadOnlyList<SpeechRegion> sparse)
    {
        bool InSparse(Word w) => sparse.Any(r => Overlaps(w, r));
        return [.. words.Where(w => !InSparse(w)).Concat(repaired.Where(InSparse)).OrderBy(w => w.Start)];
    }

    private static bool Overlaps(Word word, SpeechRegion region) => word.End > region.Start && word.Start < region.End;
}
