using Meetrec.Core.Capture;

namespace Meetrec.Core.Tests;

public class CaptureHelpersTests
{
    [Theory]
    [InlineData(0f, "··········")]
    [InlineData(0.0005f, "··········")]
    [InlineData(0.01f, "███·······")]
    [InlineData(0.1f, "███████···")]
    [InlineData(1f, "██████████")]
    [InlineData(2f, "██████████")]
    public void LevelBar_Render_UsesDecibelScale(float peak, string expected)
    {
        // Arrange
        var level = peak;

        // Act
        var bar = LevelBar.Render(level);

        // Assert
        Assert.Equal(expected, bar);
    }

    [Fact]
    public void SilenceWatch_ZerosForWholeWindow_WarnsOnce()
    {
        // Arrange
        var watch = new SilenceWatch(TimeSpan.FromSeconds(10));

        // Act
        var early = watch.ShouldWarn(TimeSpan.FromSeconds(9), 0);
        var due = watch.ShouldWarn(TimeSpan.FromSeconds(10), 0);
        var again = watch.ShouldWarn(TimeSpan.FromSeconds(11), 0);

        // Assert
        Assert.False(early);
        Assert.True(due);
        Assert.False(again);
    }

    [Fact]
    public void SilenceWatch_AnySoundBeforeWindowEnds_NeverWarns()
    {
        // Arrange
        var watch = new SilenceWatch(TimeSpan.FromSeconds(10));
        watch.ShouldWarn(TimeSpan.FromSeconds(3), 0.001f);

        // Act
        var warned = watch.ShouldWarn(TimeSpan.FromSeconds(12), 0);

        // Assert
        Assert.False(warned);
    }

    [Fact]
    public void SessionDirectory_Default_UsesMeetingsFolderAndLocalTimestamp()
    {
        // Arrange
        var now = new DateTime(2026, 10, 8, 15, 4, 5);

        // Act
        var path = SessionDirectory.Default(now, "/work");

        // Assert
        Assert.Equal(Path.Combine("/work", "meetings", "2026-10-08_15-04-05"), path);
    }
}
