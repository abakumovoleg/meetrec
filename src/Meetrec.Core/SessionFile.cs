using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Meetrec.Core.Transcript;

namespace Meetrec.Core;

// Written as schema 2; schema 1 (no schemaVersion: one offset for mic.wav, device names at the top) is still read.
public static class SessionFile
{
    public const int SchemaVersion = 2;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static Session Read(string path)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<SessionDto>(File.ReadAllText(path), Options)
                ?? throw new MeetrecException($"Пустой {SessionLayout.SessionFileName}: {path}");
            return dto.SchemaVersion switch
            {
                null => FromV1(dto, Path.GetDirectoryName(path)!),
                SchemaVersion => new Session(dto.StartedAtUtc, dto.Version, dto.Tracks ?? []),
                _ => throw new MeetrecException($"{path} записан более новой версией meetrec (схема {dto.SchemaVersion})"),
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new MeetrecException($"Не удалось прочитать {path}: {e.Message}", e);
        }
    }

    public static void Write(string path, Session session)
    {
        try
        {
            var dto = new SessionDto(SchemaVersion, session.StartedAtUtc, session.Version, session.Tracks);
            File.WriteAllText(path, JsonSerializer.Serialize(dto, Options).ReplaceLineEndings("\n") + "\n");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MeetrecException($"Не удалось записать {path}: {e.Message}", e);
        }
    }

    // Schema 1 was also written with --no-mic, so the mic track is there only if its file is.
    private static Session FromV1(SessionDto dto, string directory)
    {
        var me = new SessionSpeaker(SpeakerId.Me.Value, Speakers.Me);
        SessionTrack[] mic = File.Exists(Path.Combine(directory, SessionLayout.MicFile))
            ? [new SessionTrack(SessionLayout.MicFile, TrackSource.Mic, dto.OffsetMs ?? 0, dto.MicDevice, me)]
            : [];
        return new Session(dto.StartedAtUtc, dto.Version, [.. mic, new SessionTrack(SessionLayout.SystemFile, TrackSource.System, 0, dto.SystemDevice)]);
    }

    private sealed record SessionDto(
        int? SchemaVersion,
        DateTimeOffset StartedAtUtc,
        string Version,
        IReadOnlyList<SessionTrack>? Tracks,
        string? MicDevice = null,
        string? SystemDevice = null,
        int? OffsetMs = null);
}
