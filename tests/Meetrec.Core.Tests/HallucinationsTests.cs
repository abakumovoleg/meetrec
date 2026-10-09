using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;

namespace Meetrec.Core.Tests;

public class HallucinationsTests
{
    private static Word W(string text, double start) => new(text, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(start + 0.5));

    [Fact]
    public void Remove_ContinuedPhrase_IsDroppedWithPunctuationAndCase()
    {
        // Arrange
        Word[] words = [W("Да,", 0), W("Продолжение", 1), W("следует...", 2), W("хорошо.", 3)];

        // Act
        var result = Hallucinations.Remove(words);

        // Assert
        Assert.Equal(["Да,", "хорошо."], result.Select(w => w.Text));
    }

    [Fact]
    public void Remove_PhraseWordsApart_AreKept()
    {
        // Arrange
        Word[] words = [W("продолжение", 0), W("работы", 1), W("следует", 2), W("обсудить", 3)];

        // Act
        var result = Hallucinations.Remove(words);

        // Assert
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void Remove_WordsOutsideCyrillicAndLatin_AreDropped()
    {
        // Arrange
        Word[] words = [W("그래요.", 0), W("CSV,", 1), W("ИРМ-24", 2), W("ёлка", 3), W("你好", 4)];

        // Act
        var result = Hallucinations.Remove(words);

        // Assert
        Assert.Equal(["CSV,", "ИРМ-24", "ёлка"], result.Select(w => w.Text));
    }

    [Fact]
    public void Remove_PhraseLoopedFourTimes_KeepsFirstOccurrence()
    {
        // Arrange
        string[] phrase = ["ты", "гони", "меня,", "я..."];
        var texts = new[] { "Так," }.Concat(Enumerable.Repeat(phrase, 4).SelectMany(p => p)).Append("Сейчас").ToArray();
        var words = texts.Select((t, i) => W(t, i)).ToArray();

        // Act
        var result = Hallucinations.Remove(words);

        // Assert
        Assert.Equal(["Так,", "ты", "гони", "меня,", "я...", "Сейчас"], result.Select(w => w.Text));
    }

    [Fact]
    public void Remove_LiveRepeats_AreKept()
    {
        // Arrange
        var texts = Enumerable.Repeat("Привет.", 5).Concat(["Давай", "только", "спросить.", "Давай", "только", "спросить."])
            .Concat(Enumerable.Repeat(new[] { "Раз,", "два." }, 3).SelectMany(p => p)).ToArray();
        var words = texts.Select((t, i) => W(t, i)).ToArray();

        // Act
        var result = Hallucinations.Remove(words);

        // Assert
        Assert.Equal(texts.Length, result.Count);
    }
}
