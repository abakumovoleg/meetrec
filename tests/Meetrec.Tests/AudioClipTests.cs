using Meetrec.Core.Audio;

namespace Meetrec.Tests;

public class AudioClipTests
{
    [Fact]
    public void Slice_InsideClip_ReturnsSamplesOfRange()
    {
        // Arrange
        var clip = new AudioClip([0, 1, 2, 3, 4, 5, 6, 7, 8, 9], 10);

        // Act
        var slice = clip.Slice(TimeSpan.FromSeconds(0.2), TimeSpan.FromSeconds(0.5));

        // Assert
        Assert.Equal([2f, 3f, 4f], slice.Samples);
        Assert.Equal(10, slice.SampleRate);
    }

    [Fact]
    public void Slice_BeyondBounds_IsClamped()
    {
        // Arrange
        var clip = new AudioClip([0, 1, 2], 10);

        // Act
        var slice = clip.Slice(TimeSpan.FromSeconds(-1), TimeSpan.FromSeconds(5));
        var empty = clip.Slice(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal([0f, 1f, 2f], slice.Samples);
        Assert.Empty(empty.Samples);
    }
}
