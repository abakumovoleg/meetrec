namespace Meetrec.Core.Transcript;

public sealed record TrackWords(string Track, TimeSpan Offset, SpeakerSource Source, IReadOnlyList<Word> Words, IReadOnlyList<DiarSegment> Segments);

public static class TranscriptMerger
{
    public static readonly TimeSpan NearestSegmentTolerance = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan UtterancePauseThreshold = TimeSpan.FromSeconds(1.5);

    // Words of all tracks on session time (track time + offset). Diarized speakers are numbered by first appearance
    // across all diarized tracks, so that display names stay unique.
    public static Conversation Merge(IReadOnlyList<TrackWords> tracks)
    {
        var ordered = tracks
            .SelectMany(t => t.Words.Select(w => (Track: t, Word: w)))
            .OrderBy(x => x.Word.Start + x.Track.Offset)
            .ToList();

        var speakers = new List<TranscriptSpeaker>();
        var known = new HashSet<SpeakerId>();
        var numbered = new Dictionary<(string Track, int Cluster), SpeakerId>();
        var words = new List<TranscriptWord>(ordered.Count);
        foreach (var (track, word) in ordered)
        {
            var speaker = track.Source is FixedSpeaker fixedSpeaker
                ? new TranscriptSpeaker(fixedSpeaker.Id, fixedSpeaker.Name, track.Track, Diarized: false)
                : Diarized(track, FindCluster(word, track.Segments), numbered);
            if (known.Add(speaker.Id))
            {
                speakers.Add(speaker);
            }

            words.Add(new TranscriptWord(word.Text, word.Start + track.Offset, word.End + track.Offset, speaker.Id, track.Track));
        }

        return new Conversation(speakers, Group(words, speakers));
    }

    public static IReadOnlyList<Utterance> Group(IReadOnlyList<TranscriptWord> words, IReadOnlyList<TranscriptSpeaker> speakers)
    {
        var names = speakers.ToDictionary(s => s.Id, s => s.Name);
        var utterances = new List<Utterance>();
        var current = new List<TranscriptWord>();

        foreach (var word in words)
        {
            if (current.Count > 0 && StartsNewUtterance(current[^1], word, names))
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

    private static TranscriptSpeaker Diarized(TrackWords track, int? cluster, Dictionary<(string, int), SpeakerId> numbered)
    {
        if (cluster is not { } c)
        {
            return new TranscriptSpeaker(SpeakerId.Unknown, Speakers.Unknown, track.Track, Diarized: true);
        }

        if (!numbered.TryGetValue((track.Track, c), out var id))
        {
            id = SpeakerId.Diarized(track.Track, numbered.Count + 1);
            numbered[(track.Track, c)] = id;
        }

        return new TranscriptSpeaker(id, Speakers.DefaultName(id), track.Track, Diarized: true);
    }

    private static int? FindCluster(Word word, IReadOnlyList<DiarSegment> segs)
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

    private static bool StartsNewUtterance(TranscriptWord previous, TranscriptWord next, Dictionary<SpeakerId, string> names) =>
        names[previous.Speaker] != names[next.Speaker]
        || previous.Track != next.Track
        || next.Start - previous.End > UtterancePauseThreshold;

    private static Utterance ToUtterance(List<TranscriptWord> words) => new(words[0].Speaker, words[0].Track, words);

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
