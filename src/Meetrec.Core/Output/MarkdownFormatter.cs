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
            writer.Write($"[{TimeFormat.Clock(u.Start)}] {u.Speaker}: {u.Text}\n");
        }
    }
}
