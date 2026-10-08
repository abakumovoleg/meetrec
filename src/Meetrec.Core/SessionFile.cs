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

    public static Session Read(string path) =>
        JsonSerializer.Deserialize<Session>(File.ReadAllText(path), Options)
        ?? throw new InvalidDataException($"Пустой {FileName}: {path}");

    public static void Write(string path, Session session) =>
        File.WriteAllText(path, JsonSerializer.Serialize(session, Options).ReplaceLineEndings("\n") + "\n");
}
