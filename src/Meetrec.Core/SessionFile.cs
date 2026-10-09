using System.Text.Encodings.Web;
using System.Text.Json;

namespace Meetrec.Core;

public static class SessionFile
{
    public const string FileName = "session.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static Session Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<Session>(File.ReadAllText(path), Options)
                ?? throw new MeetrecException($"Пустой {FileName}: {path}");
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
            File.WriteAllText(path, JsonSerializer.Serialize(session, Options).ReplaceLineEndings("\n") + "\n");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MeetrecException($"Не удалось записать {path}: {e.Message}", e);
        }
    }
}
