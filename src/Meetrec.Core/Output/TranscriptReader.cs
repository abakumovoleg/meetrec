using System.Text.Json;
using Meetrec.Core.Transcript;
using static Meetrec.Core.Output.TranscriptJson;

namespace Meetrec.Core.Output;

public static class TranscriptReader
{
    public static IReadOnlyList<Utterance> Read(string path)
    {
        var dto = JsonSerializer.Deserialize<TranscriptDto>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"Пустая расшифровка: {path}");

        return [.. dto.Utterances
            .Where(u => u.Words.Count > 0)
            .Select(u => new Utterance(u.Speaker, u.Track, [.. u.Words.Select(ToWord)]))];
    }

    private static TranscriptWord ToWord(WordDto w) =>
        new(w.Text, TimeSpan.FromSeconds(w.Start), TimeSpan.FromSeconds(w.End), w.Speaker, w.Track, w.Label);
}
