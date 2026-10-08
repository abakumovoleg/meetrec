using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Meetrec.Core.Transcript;

namespace Meetrec.Core.Output;

internal static class TranscriptJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public sealed record TranscriptDto(IReadOnlyList<UtteranceDto> Utterances);

    public sealed record UtteranceDto(
        string Speaker,
        Track Track,
        double Start,
        double End,
        string Text,
        IReadOnlyList<WordDto> Words);

    public sealed record WordDto(string Text, double Start, double End, string Speaker, Track Track, string? Label = null);
}
