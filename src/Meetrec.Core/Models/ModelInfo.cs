namespace Meetrec.Core.Models;

public enum ModelKind
{
    Whisper,
    WhisperCoreMlEncoder,
    Vad,
    Segmentation,
    Embedding,
}

public enum ModelPackaging
{
    File,
    ZippedDirectory,
}

public sealed record ModelInfo(string Id, ModelKind Kind, Uri Url, string InstallName, ModelPackaging Packaging, long SizeBytes);
