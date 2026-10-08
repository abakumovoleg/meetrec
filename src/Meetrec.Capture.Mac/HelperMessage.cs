using System.Text.Json;
using Meetrec.Core.Capture;

namespace Meetrec.Capture.Mac;

internal abstract record HelperMessage
{
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
                return null;
            }

            return type.GetString() switch
            {
                "started" => new Started(root.GetProperty("offsetMs").GetInt32()),
                "level" => new Level(new LevelInfo(
                    root.TryGetProperty("mic", out var mic) ? mic.GetSingle() : null,
                    root.GetProperty("sys").GetSingle())),
                "device" => new Device(new MicrophoneDevice(
                    root.GetProperty("uid").GetString()!,
                    root.GetProperty("name").GetString()!,
                    root.GetProperty("default").GetBoolean())),
                _ => null,
            };
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    public sealed record Started(int OffsetMs) : HelperMessage;

    public sealed record Level(LevelInfo Info) : HelperMessage;

    public sealed record Device(MicrophoneDevice Microphone) : HelperMessage;
}
