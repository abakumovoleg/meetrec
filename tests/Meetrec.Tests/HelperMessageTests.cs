using Meetrec.Capture.Mac;
using Meetrec.Core.Capture;

namespace Meetrec.Tests;

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
    [InlineData("  ")]
    public void Parse_Blank_ReturnsNull(string? line)
    {
        // Arrange
        var input = line;

        // Act
        var message = HelperMessage.Parse(input);

        // Assert
        Assert.Null(message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"t":"unknown"}""")]
    [InlineData("""{"t":"started"}""")]
    [InlineData("""{"t":"level","sys":"loud"}""")]
    public void Parse_UnknownOrBroken_KeepsLineAsUnrecognized(string line)
    {
        // Arrange
        var input = line;

        // Act
        var message = HelperMessage.Parse(input);

        // Assert
        Assert.Equal(new HelperMessage.Unrecognized(line), message);
    }

    [Fact]
    public void Parse_Hello_ReadsProtocolAndVersion()
    {
        // Arrange
        var line = """{"t":"hello","protocol":1,"version":"0.1.0"}""";

        // Act
        var message = HelperMessage.Parse(line);

        // Assert
        Assert.Equal(new HelperMessage.Hello(1, "0.1.0"), message);
    }
}

public class HelperPermissionMessageTests
{
    [Fact]
    public void Parse_Permissions_ReadsBothStatuses()
    {
        // Arrange
        var line = """{"t":"permissions","mic":"granted","system":"denied"}""";

        // Act
        var message = HelperMessage.Parse(line);

        // Assert
        Assert.Equal(
            new HelperMessage.Permissions(new CapturePermissions(PermissionStatus.Granted, PermissionStatus.Denied)),
            message);
    }

    [Fact]
    public void Parse_PermissionsWithUnexpectedValue_TreatsAsUnknown()
    {
        // Arrange
        var line = """{"t":"permissions","mic":"restricted","system":"unknown"}""";

        // Act
        var message = HelperMessage.Parse(line);

        // Assert
        Assert.Equal(
            new HelperMessage.Permissions(new CapturePermissions(PermissionStatus.Unknown, PermissionStatus.Unknown)),
            message);
    }
}
