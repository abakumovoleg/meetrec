using System.Text.Json;
using Meetrec.Core.Capture;

namespace Meetrec.Capture.Mac;

internal abstract record HelperMessage
{
    // Lines that are not protocol messages (or of a type this version does not know) come back as Unrecognized,
    // so that they can be shown when the helper fails instead of being lost.
    public static HelperMessage? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("t", out var type))
            {
                return new Unrecognized(line);
            }

            return type.GetString() switch
            {
                "hello" => new Hello(
                    root.GetProperty("protocol").GetInt32(),
                    root.TryGetProperty("version", out var version) ? version.GetString() : null),
                "started" => new Started(root.GetProperty("offsetMs").GetInt32()),
                "level" => new Level(new LevelInfo(
                    root.TryGetProperty("mic", out var mic) ? mic.GetSingle() : null,
                    root.GetProperty("sys").GetSingle())),
                "device" => new Device(new MicrophoneDevice(
                    root.GetProperty("uid").GetString()!,
                    root.GetProperty("name").GetString()!,
                    root.GetProperty("default").GetBoolean())),
                "permissions" => new Permissions(new CapturePermissions(
                    ParsePermission(root.GetProperty("mic").GetString()),
                    ParsePermission(root.GetProperty("system").GetString()))),
                _ => new Unrecognized(line),
            };
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return new Unrecognized(line);
        }
    }

    private static PermissionStatus ParsePermission(string? value) => value switch
    {
        "granted" => PermissionStatus.Granted,
        "denied" => PermissionStatus.Denied,
        _ => PermissionStatus.Unknown,
    };

    public sealed record Hello(int Protocol, string? Version) : HelperMessage;

    public sealed record Started(int OffsetMs) : HelperMessage;

    public sealed record Permissions(CapturePermissions Status) : HelperMessage;

    public sealed record Level(LevelInfo Info) : HelperMessage;

    public sealed record Device(MicrophoneDevice Microphone) : HelperMessage;

    public sealed record Unrecognized(string Line) : HelperMessage;
}
