using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Meetrec.Core.Transcript;

namespace Meetrec.Core.Output;

public sealed class JsonFormatter : ITranscriptFormatter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

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
                u.Words.Select(w => new WordDto(w.Text.Trim(), Seconds(w.Start), Seconds(w.End), w.Speaker, w.Track)).ToList()))
            .ToList());

        writer.Write(JsonSerializer.Serialize(dto, Options).ReplaceLineEndings("\n"));
        writer.Write('\n');
    }

    private static double Seconds(TimeSpan time) => Math.Round(time.TotalSeconds, 3);

    private sealed record TranscriptDto(IReadOnlyList<UtteranceDto> Utterances);

    private sealed record UtteranceDto(
        string Speaker,
        Track Track,
        double Start,
        double End,
        string Text,
        IReadOnlyList<WordDto> Words);

    private sealed record WordDto(string Text, double Start, double End, string Speaker, Track Track);
}
