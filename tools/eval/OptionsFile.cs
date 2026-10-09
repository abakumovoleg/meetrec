using System.Text.Encodings.Web;
using System.Text.Json;
using Meetrec.Asr;
using Meetrec.Core;

namespace Meetrec.Eval;

// Missing properties keep their defaults, so a file may hold only the values being tried.
internal static class OptionsFile
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static RecognitionOptions Read(Args args)
    {
        if (!args.Named.TryGetValue("options", out var path))
        {
            return RecognitionOptions.Default;
        }

        try
        {
            return JsonSerializer.Deserialize<RecognitionOptions>(File.ReadAllText(path), Json)
                ?? throw new MeetrecException($"Пустой файл параметров: {path}");
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            throw new MeetrecException($"Не удалось прочитать {path}: {e.Message}", e);
        }
    }

    public static int Print()
    {
        Console.WriteLine(JsonSerializer.Serialize(RecognitionOptions.Default, Json));
        return 0;
    }
}
