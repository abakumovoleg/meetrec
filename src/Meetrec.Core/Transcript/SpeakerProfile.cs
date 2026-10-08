namespace Meetrec.Core.Transcript;

public sealed record SpeakerProfile(
    string Label,
    double TalkShare,
    int UtteranceCount,
    IReadOnlyList<Utterance> Samples,
    IReadOnlyList<Utterance> LongestUtterances);

public static class SpeakerProfiles
{
    public const int SampleCount = 3;

    public const int LongestCount = 5;

    // Expects utterances labeled by diarization (see SpeakerNames.Reset), so that speakers already merged under one name
    // are still shown separately.
    public static IReadOnlyList<SpeakerProfile> Build(IReadOnlyList<Utterance> utterances)
    {
        var total = utterances.Sum(u => (u.End - u.Start).TotalSeconds);
        return [.. SpeakerNames.Current(utterances).Select(s =>
        {
            var own = utterances.Where(u => u.Track == Track.System && u.Speaker == s.Label).ToList();
            var talk = own.Sum(u => (u.End - u.Start).TotalSeconds);
            return new SpeakerProfile(
                s.Label,
                total > 0 ? talk / total : 0,
                own.Count,
                PickSamples(own),
                [.. own.OrderByDescending(u => u.End - u.Start).Take(LongestCount)]);
        })];
    }

    // The longest utterance from each part of the meeting: the first ones are usually just greetings.
    private static List<Utterance> PickSamples(List<Utterance> own)
    {
        var parts = Math.Min(SampleCount, own.Count);
        return [.. Enumerable.Range(0, parts)
            .Select(i => own[(i * own.Count / parts)..((i + 1) * own.Count / parts)].MaxBy(u => u.Words.Count)!)];
    }
}
