using Meetrec.Core.Transcript;

namespace Meetrec.Core.Output;

public sealed class MarkdownFormatter : ITranscriptFormatter
{
    public string Format => "md";

    public void Write(IReadOnlyList<Utterance> utterances, TextWriter writer)
    {
        for (var i = 0; i < utterances.Count; i++)
        {
            if (i > 0)
            {
                writer.Write('\n');
            }

            var u = utterances[i];
            writer.Write($"[{FormatClock(u.Start)}] {u.Speaker}: {u.Text}\n");
        }
    }

    private static string FormatClock(TimeSpan time) =>
        $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
}
