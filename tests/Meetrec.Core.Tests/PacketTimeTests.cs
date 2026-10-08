using Meetrec.Capture.Windows;

namespace Meetrec.Core.Tests;

public class PacketTimeTests
{
    private static readonly TimeSpan Arrival = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Length = TimeSpan.FromMilliseconds(100);

    [Fact]
    public void Start_ReliableTimestamp_IsMeasuredFromTheRecordingStart()
    {
        // Arrange
        var origin = TimeSpan.FromHours(3).Ticks;
        var timestamp = origin + TimeSpan.FromSeconds(9.5).Ticks;

        // Act
        var start = PacketTime.Start(timestamp, origin, reliable: true, Arrival, Length);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(9.5), start);
    }

    [Fact]
    public void Start_UnreliableTimestamp_FallsBackToArrivalMinusLength()
    {
        // Arrange
        var origin = TimeSpan.FromHours(3).Ticks;
        var timestamp = origin + TimeSpan.FromSeconds(9.5).Ticks;

        // Act
        var start = PacketTime.Start(timestamp, origin, reliable: false, Arrival, Length);

        // Assert
        Assert.Equal(Arrival - Length, start);
    }

    // The first packet can carry a timestamp from just before recording started.
    [Fact]
    public void Start_TimestampBeforeTheRecordingStart_FallsBackToArrival()
    {
        // Arrange
        var origin = TimeSpan.FromHours(3).Ticks;

        // Act
        var start = PacketTime.Start(origin - 100, origin, reliable: true, Arrival, Length);

        // Assert
        Assert.Equal(Arrival - Length, start);
    }

    [Fact]
    public void Now_CountsInTheSameUnitsAsTimeSpan()
    {
        // Arrange
        var before = PacketTime.Now();

        // Act
        Thread.Sleep(50);
        var elapsed = TimeSpan.FromTicks(PacketTime.Now() - before);

        // Assert
        Assert.InRange(elapsed, TimeSpan.FromMilliseconds(40), TimeSpan.FromSeconds(5));
    }
}
