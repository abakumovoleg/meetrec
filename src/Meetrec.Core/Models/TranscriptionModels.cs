namespace Meetrec.Core.Models;

public static class TranscriptionModels
{
    public static IReadOnlyList<ModelInfo> Required(string whisperName, bool coreMl, DiarizationSettings diarization) =>
    [
        ModelCatalog.WhisperModel(whisperName),
        .. coreMl ? [ModelCatalog.WhisperCoreMlEncoder(whisperName)] : Array.Empty<ModelInfo>(),
        ModelCatalog.SileroVad,
        ModelCatalog.PyannoteSegmentation,
        diarization.Embedding,
    ];
}
