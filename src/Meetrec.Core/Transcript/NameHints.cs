namespace Meetrec.Core.Transcript;

public sealed record NameHint(string Name, int Mentions, TimeSpan At, string Quote);

// A name set off by commas right before another speaker answers is most likely that speaker's name:
// "…ничего нет, Ром, поведи тогда и расскажи про задачи" → the next speaker is Рома.
public static class NameHints
{
    public static readonly TimeSpan ReplyWindow = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan AddressWindow = TimeSpan.FromSeconds(10);

    private const int QuoteWordsBefore = 5;

    private const int QuoteWordsAfter = 5;

    private static readonly char[] Punctuation = [',', '.', '?', '!', ':', ';', '…', '—', '-'];

    // Expects utterances labeled by diarization (see SpeakerNames.Reset). Each name is suggested to one speaker at most.
    public static IReadOnlyDictionary<string, NameHint> Find(IReadOnlyList<Utterance> utterances)
    {
        var mentions = new Dictionary<(string Speaker, string Name), List<(Utterance Utterance, int Index)>>();
        for (var i = 0; i + 1 < utterances.Count; i++)
        {
            var (said, reply) = (utterances[i], utterances[i + 1]);
            if (reply.Speaker == said.Speaker
                || reply.Track != Track.System
                || reply.Speaker == Speakers.Unknown
                || reply.Start - said.End > ReplyWindow)
            {
                continue;
            }

            for (var w = 0; w < said.Words.Count; w++)
            {
                if (said.Words[w].End >= said.End - AddressWindow && AddressedName(said.Words, w) is { } name)
                {
                    var key = (reply.Speaker, name);
                    if (!mentions.TryGetValue(key, out var list))
                    {
                        mentions[key] = list = [];
                    }

                    list.Add((said, w));
                }
            }
        }

        var hints = new Dictionary<string, NameHint>();
        var used = new HashSet<string>();
        foreach (var ((speaker, name), list) in mentions.OrderByDescending(m => m.Value.Count).ThenBy(m => m.Value[0].Utterance.Start))
        {
            if (hints.ContainsKey(speaker) || !used.Add(name))
            {
                continue;
            }

            var (utterance, index) = list[^1];
            hints[speaker] = new NameHint(name, list.Count, utterance.Words[index].Start, Quote(utterance.Words, index));
        }

        return hints;
    }

    private static string? AddressedName(IReadOnlyList<TranscriptWord> words, int index)
    {
        var text = words[index].Text.Trim();
        if (text.Length < 2 || !char.IsUpper(text[0]) || text[^1] is not (',' or '?' or '!'))
        {
            return null;
        }

        if (index > 0 && words[index - 1].Text.Trim() is var previous && (previous.Length == 0 || !Punctuation.Contains(previous[^1])))
        {
            return null;
        }

        return RussianNames.Find(text.TrimEnd(Punctuation));
    }

    private static string Quote(IReadOnlyList<TranscriptWord> words, int index)
    {
        var from = Math.Max(0, index - QuoteWordsBefore);
        var to = Math.Min(words.Count, index + QuoteWordsAfter + 1);
        var text = string.Join(' ', words.Skip(from).Take(to - from).Select(w => w.Text.Trim()));
        return $"{(from > 0 ? "…" : "")}{text}{(to < words.Count ? "…" : "")}";
    }
}
