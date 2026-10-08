using Meetrec.Core.Models;

namespace Meetrec.Core.Tests;

public class TranscriptionModelsTests
{
    [Fact]
    public void Required_CoreMl_IncludesEncoderAndChosenEmbedding()
    {
        // Arrange
        var diarization = DiarizationSettings.Choose(3, hasMicTrack: true, threshold: null);

        // Act
        var models = TranscriptionModels.Required("tiny", coreMl: true, diarization);

        // Assert
        Assert.Equal(
            ["whisper-tiny", "whisper-tiny-coreml", "silero-vad", "pyannote-segmentation-3-0", "3dspeaker-eres2net-base"],
            models.Select(m => m.Id));
    }

    [Fact]
    public void Required_WithoutCoreMl_SkipsEncoder()
    {
        // Arrange
        var diarization = DiarizationSettings.Choose(null, hasMicTrack: false, threshold: null);

        // Act
        var models = TranscriptionModels.Required("small", coreMl: false, diarization);

        // Assert
        Assert.Equal(
            ["whisper-small", "silero-vad", "pyannote-segmentation-3-0", "wespeaker-resnet34"],
            models.Select(m => m.Id));
    }
}
