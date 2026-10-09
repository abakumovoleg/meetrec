using System.Globalization;
using Meetrec.Core.Transcript;

namespace Meetrec.Core.Audio;

// Whisper's typical output on silence or faint leaked speech. Removed before SpeechRepair, so that a region left
// with nothing real is transcribed again on its own; a lone word in a wrong script there turned out to be a real
// "Да." recognised as Korean.
public static class Hallucinations
{
    private static readonly string[][] Phrases = [["продолжение", "следует"]];

    public static IReadOnlyList<Word> Remove(IReadOnlyList<Word> words)
    {
        var keep = words.Select(w => IsReadableScript(w.Text)).ToArray();
        foreach (var phrase in Phrases)
        {
            for (var i = 0; i + phrase.Length <= words.Count; i++)
            {
                if (phrase.Select((p, k) => Normalize(words[i + k].Text) == p).All(m => m))
                {
                    Array.Fill(keep, false, i, phrase.Length);
                }
            }
        }

        return [.. words.Where((_, i) => keep[i])];
    }

    private static bool IsReadableScript(string text) =>
        text.Where(char.IsLetter).All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= 'а' and <= 'я') or (>= 'А' and <= 'Я') or 'ё' or 'Ё');

    private static string Normalize(string text) =>
        new string([.. text.Where(char.IsLetter)]).ToLower(CultureInfo.InvariantCulture);
}
