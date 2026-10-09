using Meetrec.Core.Transcript;

namespace Meetrec.Core.Output;

public sealed class MarkdownFormatter : ITranscriptFormatter
{
    public string Format => "md";

    public void Write(Conversation conversation, TextWriter writer)
    {
        var utterances = conversation.Utterances;
        for (var i = 0; i < utterances.Count; i++)
        {
            if (i > 0)
            {
                writer.Write('\n');
            }

            var u = utterances[i];
            writer.Write($"[{TimeFormat.Clock(u.Start)}] {conversation.NameOf(u.Speaker)}: {u.Text}\n");
        }
    }
}
