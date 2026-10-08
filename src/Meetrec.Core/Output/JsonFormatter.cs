using System.Text.Json;
using Meetrec.Core.Transcript;
using static Meetrec.Core.Output.TranscriptJson;

namespace Meetrec.Core.Output;

public sealed class JsonFormatter : ITranscriptFormatter
{
    public string Format => "json";

    public void Write(IReadOnlyList<Utterance> utterances, TextWriter writer)
    {
        var dto = new TranscriptDto(utterances
            .Select(u => new UtteranceDto(
                u.Speaker,
                u.Track,
                Seconds(u.Start),
                Seconds(u.End),
                u.Text,
                u.Words.Select(w => new WordDto(w.Text.Trim(), Seconds(w.Start), Seconds(w.End), w.Speaker, w.Track, w.Label)).ToList()))
            .ToList());

        writer.Write(JsonSerializer.Serialize(dto, Options).ReplaceLineEndings("\n"));
        writer.Write('\n');
    }

    private static double Seconds(TimeSpan time) => Math.Round(time.TotalSeconds, 3);
}
