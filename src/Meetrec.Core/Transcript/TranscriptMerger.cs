namespace Meetrec.Core.Transcript;

public static class TranscriptMerger
{
    public static readonly TimeSpan NearestSegmentTolerance = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan UtterancePauseThreshold = TimeSpan.FromSeconds(1.5);

    public static IReadOnlyList<Utterance> Merge(
        IReadOnlyList<Word> mic,
        IReadOnlyList<Word> sys,
        IReadOnlyList<DiarSegment> segs,
        int offsetMs)
    {
        var offset = TimeSpan.FromMilliseconds(offsetMs);

        var micWords = mic.Select(w => (Word: w with { Start = w.Start + offset, End = w.End + offset }, Track: Track.Mic, Speaker: (int?)null));
        var sysWords = sys.Select(w => (Word: w, Track: Track.System, Speaker: FindSpeaker(w, segs)));

        var ordered = micWords.Concat(sysWords).OrderBy(x => x.Word.Start).ToList();

        var labels = new Dictionary<int, string>();
        var words = new List<TranscriptWord>(ordered.Count);
        foreach (var (word, track, speaker) in ordered)
        {
            var label = track == Track.Mic ? Speakers.Me : Label(speaker, labels);
            words.Add(new TranscriptWord(word.Text, word.Start, word.End, label, track));
        }

        return Group(words);
    }

    private static string Label(int? speaker, Dictionary<int, string> labels)
    {
        if (speaker is not { } id)
        {
            return Speakers.Unknown;
        }

        if (!labels.TryGetValue(id, out var label))
        {
            label = Speakers.Numbered(labels.Count + 1);
            labels[id] = label;
        }

        return label;
    }

    private static int? FindSpeaker(Word word, IReadOnlyList<DiarSegment> segs)
    {
        DiarSegment? best = null;
        var bestOverlap = TimeSpan.Zero;
        DiarSegment? nearest = null;
        var nearestDistance = TimeSpan.MaxValue;

        foreach (var seg in segs)
        {
            var overlap = Min(word.End, seg.End) - Max(word.Start, seg.Start);
            if (overlap > bestOverlap)
            {
                best = seg;
                bestOverlap = overlap;
                continue;
            }

            var distance = Max(seg.Start - word.End, word.Start - seg.End);
            if (distance < nearestDistance)
            {
                nearest = seg;
                nearestDistance = distance;
            }
        }

        if (best is not null)
        {
            return best.Speaker;
        }

        return nearest is not null && nearestDistance <= NearestSegmentTolerance ? nearest.Speaker : null;
    }

    public static IReadOnlyList<Utterance> Group(IReadOnlyList<TranscriptWord> words)
    {
        var utterances = new List<Utterance>();
        var current = new List<TranscriptWord>();

        foreach (var word in words)
        {
            if (current.Count > 0 && StartsNewUtterance(current[^1], word))
            {
                utterances.Add(ToUtterance(current));
                current = [];
            }

            current.Add(word);
        }

        if (current.Count > 0)
        {
            utterances.Add(ToUtterance(current));
        }

        return utterances;
    }

    private static bool StartsNewUtterance(TranscriptWord previous, TranscriptWord next) =>
        previous.Speaker != next.Speaker
        || previous.Track != next.Track
        || next.Start - previous.End > UtterancePauseThreshold;

    private static Utterance ToUtterance(List<TranscriptWord> words) => new(words[0].Speaker, words[0].Track, words);

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
