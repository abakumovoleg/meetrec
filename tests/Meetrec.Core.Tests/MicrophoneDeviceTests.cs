using Meetrec.Core.Capture;

namespace Meetrec.Core.Tests;

public class MicrophoneDeviceTests
{
    private static readonly MicrophoneDevice BuiltIn = new("BuiltInMicrophoneDevice", "Микрофон MacBook Pro", true);
    private static readonly MicrophoneDevice Webcam = new("AppleUSBAudioEngine:C922", "C922 Pro Stream Webcam", false);
    private static readonly MicrophoneDevice Headset = new("BT:AirPods", "AirPods Pro", false);
    private static readonly MicrophoneDevice[] Devices = [BuiltIn, Webcam, Headset];

    [Fact]
    public void Resolve_NoQuery_ReturnsDefault()
    {
        // Arrange
        string? query = null;

        // Act
        var device = MicrophoneDevice.Resolve(Devices, query);

        // Assert
        Assert.Equal(BuiltIn, device);
    }

    [Theory]
    [InlineData("AppleUSBAudioEngine:C922")]
    [InlineData("c922 pro stream webcam")]
    [InlineData("webcam")]
    public void Resolve_UidNameOrUniquePart_FindsDevice(string query)
    {
        // Arrange
        var devices = Devices;

        // Act
        var device = MicrophoneDevice.Resolve(devices, query);

        // Assert
        Assert.Equal(Webcam, device);
    }

    [Theory]
    [InlineData("Pro")]
    [InlineData("Нет такого")]
    public void Resolve_AmbiguousOrUnknown_ReturnsNull(string query)
    {
        // Arrange
        var devices = Devices;

        // Act
        var device = MicrophoneDevice.Resolve(devices, query);

        // Assert
        Assert.Null(device);
    }
}
