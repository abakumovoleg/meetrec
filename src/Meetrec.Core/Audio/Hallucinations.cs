using System.Globalization;
using Meetrec.Core.Transcript;

namespace Meetrec.Core.Audio;

// Whisper's typical output on silence or faint leaked speech, and its decoding loops. Removed before SpeechRepair,
// so that a region left with nothing real is transcribed again on its own; a lone word in a wrong script there
// turned out to be a real "Да." recognised as Korean.
public static class Hallucinations
{
    private static readonly string[][] Phrases = [["продолжение", "следует"]];

    // Live speech repeats single words ("Привет." x5, "Да," x7) and short phrases twice; a loop repeated a
    // six-word phrase 6-10 times in a row.
    private const int LoopMinPhrase = 2;
    private const int LoopMaxPhrase = 15;
    private const int LoopMinRepeats = 4;

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

        MarkLoops(words.Select(w => Normalize(w.Text)).ToArray(), keep);
        return [.. words.Where((_, i) => keep[i])];
    }

    // The first occurrence of a looped phrase is usually what was actually said, so only the repeats are dropped.
    private static void MarkLoops(string[] text, bool[] keep)
    {
        var i = 0;
        while (i < text.Length)
        {
            var (phrase, repeats) = (0, 0);
            for (var n = LoopMinPhrase; n <= LoopMaxPhrase && i + n <= text.Length; n++)
            {
                var r = 1;
                while (i + (r + 1) * n <= text.Length && text.AsSpan(i + r * n, n).SequenceEqual(text.AsSpan(i, n)))
                {
                    r++;
                }

                if (r >= LoopMinRepeats && r * n > phrase * repeats)
                {
                    (phrase, repeats) = (n, r);
                }
            }

            if (repeats == 0)
            {
                i++;
                continue;
            }

            Array.Fill(keep, false, i + phrase, phrase * (repeats - 1));
            i += phrase * repeats;
        }
    }

    // Latin: Basic, Latin-1 Supplement, Extended-A/B, Extended Additional. Cyrillic: main block and Supplement.
    private static bool IsReadableScript(string text) =>
        text.Where(char.IsLetter).All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z')
            or (>= '\u00C0' and <= '\u024F') or (>= '\u1E00' and <= '\u1EFF') or (>= '\u0400' and <= '\u052F'));

    private static string Normalize(string text) =>
        new string([.. text.Where(char.IsLetter)]).ToLower(CultureInfo.InvariantCulture);
}
