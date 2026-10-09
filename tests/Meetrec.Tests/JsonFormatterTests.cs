using System.Text.Json;
using Meetrec.Core.Output;
using Meetrec.Core.Transcript;
using static Meetrec.Tests.TestData;

namespace Meetrec.Tests;

public class JsonFormatterTests
{
    [Fact]
    public void Write_Conversation_SerializesSchemaAndSpeakerTable()
    {
        // Arrange
        var conversation = Conversation([Said(SpeakerId.Me, 0, "привет"), Said(S1, 1, "да")], (S1, "Олег"));

        // Act
        var json = Render(new JsonFormatter(), conversation);

        // Assert
        var root = JsonDocument.Parse(json).RootElement;
        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
        var speakers = root.GetProperty("speakers");
        Assert.Equal(["me", "system:1"], speakers.EnumerateArray().Select(s => s.GetProperty("id").GetString()));
        Assert.Equal("Олег", speakers[1].GetProperty("name").GetString());
        Assert.Equal("system", speakers[1].GetProperty("track").GetString());
        Assert.True(speakers[1].GetProperty("diarized").GetBoolean());
        Assert.False(speakers[0].GetProperty("diarized").GetBoolean());
    }

    [Fact]
    public void Write_Utterance_SerializesSpeakerIdNameAndWords()
    {
        // Arrange
        var utterance = Utterance(Word(" Привет", 1.2346, 1.5, S1), Word(" всем", 1.6, 2.0, S1));

        // Act
        var json = Render(new JsonFormatter(), Conversation([utterance], (S1, "Олег")));

        // Assert
        var u = JsonDocument.Parse(json).RootElement.GetProperty("utterances")[0];
        Assert.Equal("system:1", u.GetProperty("speakerId").GetString());
        Assert.Equal("Олег", u.GetProperty("speaker").GetString());
        Assert.Equal("system", u.GetProperty("track").GetString());
        Assert.Equal(1.235, u.GetProperty("start").GetDouble());
        Assert.Equal(2.0, u.GetProperty("end").GetDouble());
        Assert.Equal("Привет всем", u.GetProperty("text").GetString());
        var word = u.GetProperty("words")[0];
        Assert.Equal("Привет", word.GetProperty("text").GetString());
        Assert.Equal(1.235, word.GetProperty("start").GetDouble());
        Assert.Equal(1.5, word.GetProperty("end").GetDouble());
        Assert.Equal("system:1", word.GetProperty("speakerId").GetString());
        Assert.Equal("system", word.GetProperty("track").GetString());
        Assert.False(word.TryGetProperty("speaker", out _));
        Assert.False(word.TryGetProperty("label", out _));
    }

    [Fact]
    public void Write_Cyrillic_IsNotEscaped()
    {
        // Arrange
        var conversation = Conversation([Utterance(Word("Я", 0, 0.3))]);

        // Act
        var json = Render(new JsonFormatter(), conversation);

        // Assert
        Assert.Contains("\"speaker\": \"Я\"", json);
        Assert.DoesNotContain("\\u", json);
    }

    [Fact]
    public void Write_NoUtterances_RendersEmptyArrays()
    {
        // Arrange
        var formatter = new JsonFormatter();

        // Act
        var json = Render(formatter, Conversation([]));

        // Assert
        var root = JsonDocument.Parse(json).RootElement;
        Assert.Equal(0, root.GetProperty("utterances").GetArrayLength());
        Assert.Equal(0, root.GetProperty("speakers").GetArrayLength());
    }
}
