using System.Text.Json;
using Meetrec.Core.Output;
using Meetrec.Core.Transcript;
using static Meetrec.Core.Tests.TestData;

namespace Meetrec.Core.Tests;

public class JsonFormatterTests
{
    [Fact]
    public void Write_Utterance_SerializesUtteranceAndWordsWithSpeakerAndTrack()
    {
        // Arrange
        var utterance = Utterance(
            Word(" Привет", 1.2346, 1.5, Speakers.Numbered(1), Track.System),
            Word(" всем", 1.6, 2.0, Speakers.Numbered(1), Track.System));

        // Act
        var json = Render(new JsonFormatter(), utterance);

        // Assert
        var u = JsonDocument.Parse(json).RootElement.GetProperty("utterances")[0];
        Assert.Equal("Спикер 1", u.GetProperty("speaker").GetString());
        Assert.Equal("system", u.GetProperty("track").GetString());
        Assert.Equal(1.235, u.GetProperty("start").GetDouble());
        Assert.Equal(2.0, u.GetProperty("end").GetDouble());
        Assert.Equal("Привет всем", u.GetProperty("text").GetString());
        var word = u.GetProperty("words")[0];
        Assert.Equal("Привет", word.GetProperty("text").GetString());
        Assert.Equal(1.235, word.GetProperty("start").GetDouble());
        Assert.Equal(1.5, word.GetProperty("end").GetDouble());
        Assert.Equal("Спикер 1", word.GetProperty("speaker").GetString());
        Assert.Equal("system", word.GetProperty("track").GetString());
    }

    [Fact]
    public void Write_LabelOnlyForRenamedWords()
    {
        // Arrange
        var utterance = Utterance(
            Word("да", 0, 0.5, "Олег", Track.System, Speakers.Numbered(1)),
            Word("ну", 0.6, 1, "Олег", Track.System));

        // Act
        var json = Render(new JsonFormatter(), utterance);

        // Assert
        var words = JsonDocument.Parse(json).RootElement.GetProperty("utterances")[0].GetProperty("words");
        Assert.Equal("Спикер 1", words[0].GetProperty("label").GetString());
        Assert.False(words[1].TryGetProperty("label", out _));
    }

    [Fact]
    public void Write_Cyrillic_IsNotEscaped()
    {
        // Arrange
        var utterance = Utterance(Word("Я", 0, 0.3));

        // Act
        var json = Render(new JsonFormatter(), utterance);

        // Assert
        Assert.Contains("\"speaker\": \"Я\"", json);
        Assert.Contains("\"track\": \"mic\"", json);
        Assert.DoesNotContain("\\u", json);
    }

    [Fact]
    public void Write_NoUtterances_RendersEmptyArray()
    {
        // Arrange
        var formatter = new JsonFormatter();

        // Act
        var json = Render(formatter);

        // Assert
        Assert.Equal(0, JsonDocument.Parse(json).RootElement.GetProperty("utterances").GetArrayLength());
    }
}
