using System.Text.Json;
using Meetrec.Core.Capture;

namespace Meetrec.Capture.Meetzone;

// Messages of the meetrec browser extension; see browser/meetzone/README.md for the protocol.
public abstract record MeetzoneMessage
{
    public const int ProtocolVersion = 1;

    public static MeetzoneMessage? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return Text(root, "t") switch
            {
                "hello" => new Hello(Int(root, "protocol"), Text(root, "version"), OptionalText(root, "url")),
                "started" => new Started(Text(root, "context"), Seconds(root, "at"), DateTimeOffset.FromUnixTimeMilliseconds((long)Double(root, "wall"))),
                "presence" => new Presence(
                    Text(root, "endpoint"), OptionalText(root, "name"), OptionalText(root, "login"), Bool(root, "own"), Bool(root, "left")),
                "sources" => new Sources([.. root.GetProperty("sources").EnumerateArray().Select(s => new SourceOwner(Long(s, "ssrc"), Text(s, "owner")))]),
                "slots" => new Slots([.. root.GetProperty("slots").EnumerateArray().Select(s => new Slot(Text(s, "track"), Long(s, "ssrc"), OptionalText(s, "owner")))]),
                "audio" => new Audio(Text(root, "track"), Seconds(root, "at"), Convert.FromBase64String(Text(root, "pcm"))),
                "error" => new PageError(Text(root, "message")),
                "closed" => new PageClosed(),
                _ => null,
            };
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new CaptureException($"Непонятное сообщение расширения: {e.Message}");
        }
    }

    public static string Start() => """{"t":"start"}""";

    public static string Stop() => """{"t":"stop"}""";

    private static string Text(JsonElement e, string name) => e.GetProperty(name).GetString() ?? throw new FormatException($"нет {name}");

    private static string? OptionalText(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int Int(JsonElement e, string name) => e.GetProperty(name).GetInt32();

    private static long Long(JsonElement e, string name) => e.GetProperty(name).GetInt64();

    private static double Double(JsonElement e, string name) => e.GetProperty(name).GetDouble();

    private static TimeSpan Seconds(JsonElement e, string name) => TimeSpan.FromSeconds(Double(e, name));

    private static bool Bool(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}

public sealed record Hello(int Protocol, string Version, string? Url) : MeetzoneMessage;

// Recording began in an AudioContext; At is its time then, Wall the matching wall clock.
public sealed record Started(string Context, TimeSpan At, DateTimeOffset Wall) : MeetzoneMessage;

public sealed record Presence(string Endpoint, string? Name, string? Login, bool Own, bool Left) : MeetzoneMessage;

public sealed record SourceOwner(long Ssrc, string Owner);

// AudioSourcesMap of the videobridge: which endpoint currently speaks through a slot's ssrc.
public sealed record Sources(IReadOnlyList<SourceOwner> Owners) : MeetzoneMessage;

// Owner is known only for a peer-to-peer connection (two participants), whose SDP names the sender's endpoint.
public sealed record Slot(string Track, long Ssrc, string? Owner);

// Incoming audio tracks of the page and the ssrc of each, from the remote SDP.
public sealed record Slots(IReadOnlyList<Slot> Tracks) : MeetzoneMessage;

// 16 kHz mono little-endian PCM16 that starts at At in the AudioContext; the own outgoing track is "out".
public sealed record Audio(string Track, TimeSpan At, byte[] Pcm) : MeetzoneMessage
{
    public const string Outgoing = "out";
}

public sealed record PageError(string Message) : MeetzoneMessage;

public sealed record PageClosed : MeetzoneMessage;
