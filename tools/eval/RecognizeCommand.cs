using System.Text.Encodings.Web;
using System.Text.Json;
using Meetrec.Asr;
using Meetrec.Core.Audio;
using Meetrec.Core.Models;

namespace Meetrec.Eval;

internal static class RecognizeCommand
{
    public static async Task<int> ExecuteAsync(Args args)
    {
        var wav = args.At(0, "wav");
        var output = args.At(1, "words.json");
        var model = args.Get("model", ModelCatalog.DefaultWhisper);
        var language = args.Get("lang", "ru");
        var options = OptionsFile.Read(args);
        var store = await Models.EnsureAsync(Models.ForRecognition(model));

        var clip = AudioLoader.LoadMono16k(wav);
        using var whisper = WhisperTranscriber.Load(store.PathOf(ModelCatalog.WhisperModel(model)));
        var recognizer = new SpeechRecognizer(whisper, new SileroVad(store.PathOf(ModelCatalog.SileroVad)), options);
        var recognition = await recognizer.RecognizeAsync(clip, language, null, CancellationToken.None);

        var words = recognition.Words.Select(w => new { text = w.Text, start = w.Start.TotalSeconds, end = w.End.TotalSeconds });
        File.WriteAllText(output, JsonSerializer.Serialize(words, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine(recognition.Stats);
        Console.WriteLine($"{recognition.Words.Count} слов → {output}");
        return 0;
    }
}
