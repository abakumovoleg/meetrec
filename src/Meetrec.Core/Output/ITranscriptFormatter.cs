using Meetrec.Core.Transcript;

namespace Meetrec.Core.Output;

public interface ITranscriptFormatter
{
    string Format { get; }

    void Write(IReadOnlyList<Utterance> utterances, TextWriter writer);
}
