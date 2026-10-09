using Meetrec.Core.Transcript;

namespace Meetrec.Core.Output;

public sealed class SrtFormatter : ITranscriptFormatter
{
    public static readonly TimeSpan MaxCueDuration = TimeSpan.FromSeconds(7);

    public const int MaxCueTextLength = 84;

    public string Format => "srt";

    public void Write(Conversation conversation, TextWriter writer)
    {
        var index = 0;
        foreach (var cue in conversation.Utterances.SelectMany(SplitIntoCues))
        {
            if (index > 0)
            {
                writer.Write('\n');
            }

            index++;
            writer.Write($"{index}\n{FormatTimestamp(cue.Start)} --> {FormatTimestamp(cue.End)}\n{conversation.NameOf(cue.Speaker)}: {cue.Text}\n");
        }
    }

    private static IEnumerable<Utterance> SplitIntoCues(Utterance utterance)
    {
        var current = new List<TranscriptWord>();
        var length = 0;

        foreach (var word in utterance.Words)
        {
            var wordLength = word.Text.Trim().Length;
            if (current.Count > 0
                && (word.End - current[0].Start > MaxCueDuration || length + 1 + wordLength > MaxCueTextLength))
            {
                yield return utterance with { Words = current };
                current = [];
                length = 0;
            }

            length += current.Count > 0 ? 1 + wordLength : wordLength;
            current.Add(word);
        }

        if (current.Count > 0)
        {
            yield return utterance with { Words = current };
        }
    }

    private static string FormatTimestamp(TimeSpan time) =>
        $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00},{time.Milliseconds:000}";
}
