using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Meetrec.Core.Output;

// Schema 2: speaker table plus speakerId on utterances and words; the name in an utterance is only for reading.
// Schema 1 (no schemaVersion): speaker name and "mic"/"system" track on every word, diarization label in "label".
internal static class TranscriptJson
{
    public const int SchemaVersion = 2;

    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public sealed record TranscriptDto(int? SchemaVersion, IReadOnlyList<SpeakerDto>? Speakers, IReadOnlyList<UtteranceDto> Utterances);

    public sealed record SpeakerDto(string Id, string Name, string Track, bool Diarized);

    public sealed record UtteranceDto(
        string? SpeakerId,
        string Speaker,
        string Track,
        double Start,
        double End,
        string Text,
        IReadOnlyList<WordDto> Words);

    public sealed record WordDto(string Text, double Start, double End, string? SpeakerId, string Track, string? Speaker = null, string? Label = null);
}
