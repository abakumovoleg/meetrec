using Meetrec.Capture.Mac;
using Meetrec.Core.Capture;

namespace Meetrec.Core.Tests;

public class HelperMessageTests
{
    [Fact]
    public void Parse_Started_ReadsOffset()
    {
        // Arrange
        var line = """{"t":"started","offsetMs":125}""";

        // Act
        var message = HelperMessage.Parse(line);

        // Assert
        Assert.Equal(new HelperMessage.Started(125), message);
    }

    [Fact]
    public void Parse_LevelWithAndWithoutMic_ReadsLevels()
    {
        // Arrange
        var withMic = """{"t":"level","mic":0.12,"sys":0.400}""";
        var withoutMic = """{"t":"level","sys":0.5}""";

        // Act
        var first = HelperMessage.Parse(withMic);
        var second = HelperMessage.Parse(withoutMic);

        // Assert
        Assert.Equal(new HelperMessage.Level(new LevelInfo(0.12f, 0.4f)), first);
        Assert.Equal(new HelperMessage.Level(new LevelInfo(null, 0.5f)), second);
    }

    [Fact]
    public void Parse_Device_ReadsMicrophone()
    {
        // Arrange
        var line = """{"t":"device","uid":"BuiltInMicrophoneDevice","name":"Микрофон \"MacBook\"","default":true}""";

        // Act
        var message = HelperMessage.Parse(line);

        // Assert
        Assert.Equal(new HelperMessage.Device(new MicrophoneDevice("BuiltInMicrophoneDevice", "Микрофон \"MacBook\"", true)), message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"t":"unknown"}""")]
    [InlineData("""{"t":"started"}""")]
    [InlineData("""{"t":"level","sys":"loud"}""")]
    public void Parse_UnknownOrBroken_ReturnsNull(string? line)
    {
        // Arrange
        var input = line;

        // Act
        var message = HelperMessage.Parse(input);

        // Assert
        Assert.Null(message);
    }
}
