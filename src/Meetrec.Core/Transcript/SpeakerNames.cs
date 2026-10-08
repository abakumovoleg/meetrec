namespace Meetrec.Core.Transcript;

public sealed record SpeakerName(string Label, string Name);

public static class SpeakerNames
{
    public static IReadOnlyList<SpeakerName> Current(IReadOnlyList<Utterance> utterances) =>
        [.. utterances
            .SelectMany(u => u.Words)
            .Where(IsNameable)
            .DistinctBy(w => w.SpeakerLabel)
            .Select(w => new SpeakerName(w.SpeakerLabel, w.Speaker))];

    public static IReadOnlyList<Utterance> Apply(IReadOnlyList<Utterance> utterances, IReadOnlyDictionary<string, string> namesByLabel) =>
        TranscriptMerger.Group([.. utterances.SelectMany(u => u.Words).Select(w => Rename(w, namesByLabel))]);

    public static IReadOnlyList<Utterance> Reset(IReadOnlyList<Utterance> utterances) =>
        Apply(utterances, Current(utterances).ToDictionary(s => s.Label, s => s.Label));

    private static bool IsNameable(TranscriptWord word) => word.Track == Track.System && word.SpeakerLabel != Speakers.Unknown;

    private static TranscriptWord Rename(TranscriptWord word, IReadOnlyDictionary<string, string> namesByLabel)
    {
        if (!IsNameable(word) || !namesByLabel.TryGetValue(word.SpeakerLabel, out var name))
        {
            return word;
        }

        var label = word.SpeakerLabel;
        return word with { Speaker = name, Label = name == label ? null : label };
    }
}
