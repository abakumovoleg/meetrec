using Meetrec.Core.Models;

namespace Meetrec.Core.Tests;

public class DiarizationSettingsTests
{
    [Fact]
    public void Choose_SpeakersGivenWithMicTrack_ExcludesMeFromClusters()
    {
        // Arrange
        int? speakers = 4;

        // Act
        var settings = DiarizationSettings.Choose(speakers, hasMicTrack: true, threshold: null);

        // Assert
        Assert.Equal(3, settings.NumClusters);
        Assert.Equal(ModelCatalog.Eres2NetEmbedding, settings.Embedding);
    }

    [Fact]
    public void Choose_SpeakersGivenForSingleFile_CountsEveryoneOnSystemTrack()
    {
        // Arrange
        int? speakers = 4;

        // Act
        var settings = DiarizationSettings.Choose(speakers, hasMicTrack: false, threshold: null);

        // Assert
        Assert.Equal(4, settings.NumClusters);
    }

    [Fact]
    public void Choose_OnlyMeWithMicTrack_KeepsAtLeastOneCluster()
    {
        // Arrange
        int? speakers = 1;

        // Act
        var settings = DiarizationSettings.Choose(speakers, hasMicTrack: true, threshold: null);

        // Assert
        Assert.Equal(1, settings.NumClusters);
    }

    [Fact]
    public void Choose_NoSpeakers_UsesWespeakerWithItsThreshold()
    {
        // Arrange
        int? speakers = null;

        // Act
        var settings = DiarizationSettings.Choose(speakers, hasMicTrack: true, threshold: null);

        // Assert
        Assert.Null(settings.NumClusters);
        Assert.Equal(ModelCatalog.WespeakerEmbedding, settings.Embedding);
        Assert.Equal(DiarizationSettings.WespeakerThreshold, settings.Threshold);
    }

    [Fact]
    public void Choose_ExplicitThreshold_OverridesModelDefault()
    {
        // Arrange
        float? threshold = 0.75f;

        // Act
        var settings = DiarizationSettings.Choose(null, hasMicTrack: false, threshold);

        // Assert
        Assert.Equal(0.75f, settings.Threshold);
    }
}
