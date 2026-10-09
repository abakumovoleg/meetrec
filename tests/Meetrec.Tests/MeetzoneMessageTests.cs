using Meetrec.Capture.Meetzone;
using Meetrec.Core.Capture;

namespace Meetrec.Tests;

public class MeetzoneMessageTests
{
    [Fact]
    public void Parse_Audio_DecodesPcmAndTime()
    {
        // Arrange
        var json = """{"t":"audio","track":"in-2-3","at":12.5,"pcm":"AQACAA=="}""";

        // Act
        var message = MeetzoneMessage.Parse(json);

        // Assert
        var audio = Assert.IsType<Audio>(message);
        Assert.Equal(("in-2-3", TimeSpan.FromSeconds(12.5)), (audio.Track, audio.At));
        Assert.Equal([1, 0, 2, 0], audio.Pcm);
    }

    [Fact]
    public void Parse_Presence_ReadsParticipant()
    {
        // Arrange
        var json = """{"t":"presence","endpoint":"e158c45b","name":"Oleg Abakumov","login":"olabakumov","own":true,"left":false}""";

        // Act
        var message = MeetzoneMessage.Parse(json);

        // Assert
        Assert.Equal(new Presence("e158c45b", "Oleg Abakumov", "olabakumov", true, false), message);
    }

    [Fact]
    public void Parse_SlotsAndSources_ReadLists()
    {
        // Arrange
        var slots = """{"t":"slots","slots":[{"track":"in-2-3","ssrc":912173308},{"track":"in-1-0","ssrc":42,"owner":"6db2446f"}]}""";
        var sources = """{"t":"sources","sources":[{"ssrc":912173308,"owner":"6db2446f"}]}""";

        // Act
        var parsedSlots = Assert.IsType<Slots>(MeetzoneMessage.Parse(slots));
        var parsedSources = Assert.IsType<Sources>(MeetzoneMessage.Parse(sources));

        // Assert
        Assert.Equal([new Slot("in-2-3", 912173308, null), new Slot("in-1-0", 42, "6db2446f")], parsedSlots.Tracks);
        Assert.Equal([new SourceOwner(912173308, "6db2446f")], parsedSources.Owners);
    }

    [Fact]
    public void Parse_Started_ReadsContextClock()
    {
        // Arrange
        var json = """{"t":"started","context":"c1","at":0.75,"wall":1791533729198.4}""";

        // Act
        var message = MeetzoneMessage.Parse(json);

        // Assert
        Assert.Equal(new Started("c1", TimeSpan.FromSeconds(0.75), DateTimeOffset.FromUnixTimeMilliseconds(1791533729198)), message);
    }

    [Fact]
    public void Parse_UnknownType_IsIgnored()
    {
        // Act
        var message = MeetzoneMessage.Parse("""{"t":"levels","x":1}""");

        // Assert
        Assert.Null(message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"t":"audio","track":"in-2-3"}""")]
    [InlineData("""{"t":"audio","track":"in-2-3","at":1,"pcm":"%%%"}""")]
    public void Parse_Broken_ThrowsCaptureException(string json)
    {
        // Act
        var error = Record.Exception(() => MeetzoneMessage.Parse(json));

        // Assert
        Assert.IsType<CaptureException>(error);
    }
}
