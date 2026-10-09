using System.Diagnostics.CodeAnalysis;

namespace Meetrec.Core.Models;

public static class ModelCatalog
{
    public const string DefaultWhisper = "large-v3-turbo";

    private const string WhisperBase = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";
    private const string SherpaReleases = "https://github.com/k2-fsa/sherpa-onnx/releases/download/";

    private static readonly (string Name, long Bin, long CoreMl)[] WhisperSizes =
    [
        ("tiny", 77_691_713, 15_037_446),
        ("base", 147_951_465, 37_922_638),
        ("small", 487_601_967, 163_083_239),
        ("medium", 1_533_763_059, 567_829_413),
        ("large-v3", 3_095_033_483, 1_175_711_232),
        ("large-v3-turbo", 1_624_555_275, 1_173_393_014),
    ];

    public static readonly ModelInfo SileroVad = new(
        "silero-vad", ModelKind.Vad, new Uri(SherpaReleases + "asr-models/silero_vad.onnx"),
        "silero_vad.onnx", ModelPackaging.File, 643_854);

    public static readonly ModelInfo PyannoteSegmentation = new(
        "pyannote-segmentation-3-0", ModelKind.Segmentation,
        new Uri("https://huggingface.co/csukuangfj/sherpa-onnx-pyannote-segmentation-3-0/resolve/main/model.onnx"),
        "pyannote-segmentation-3-0.onnx", ModelPackaging.File, 5_992_913);

    public static readonly ModelInfo Eres2NetEmbedding = new(
        "3dspeaker-eres2net-base", ModelKind.Embedding,
        new Uri(SherpaReleases + "speaker-recongition-models/3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx"),
        "3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx", ModelPackaging.File, 39_593_761);

    public static readonly ModelInfo WespeakerEmbedding = new(
        "wespeaker-resnet34", ModelKind.Embedding,
        new Uri(SherpaReleases + "speaker-recongition-models/wespeaker_en_voxceleb_resnet34.onnx"),
        "wespeaker_en_voxceleb_resnet34.onnx", ModelPackaging.File, 26_534_365);

    public static IReadOnlyList<string> WhisperNames { get; } = [.. WhisperSizes.Select(s => s.Name)];

    public static IReadOnlyList<ModelInfo> All { get; } =
    [
        .. WhisperSizes.SelectMany(s => new[] { Whisper(s), WhisperCoreMl(s) }),
        SileroVad,
        PyannoteSegmentation,
        Eres2NetEmbedding,
        WespeakerEmbedding,
    ];

    public static bool TryFind(string id, [NotNullWhen(true)] out ModelInfo? model)
    {
        model = All.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        return model is not null;
    }

    public static ModelInfo Find(string id) =>
        TryFind(id, out var model) ? model : throw new MeetrecException($"Неизвестная модель: {id}. Список: meetrec models");

    public static ModelInfo WhisperModel(string name) => Find($"whisper-{name}");

    public static ModelInfo WhisperCoreMlEncoder(string name) => Find($"whisper-{name}-coreml");

    private static ModelInfo Whisper((string Name, long Bin, long CoreMl) size) => new(
        $"whisper-{size.Name}", ModelKind.Whisper, new Uri($"{WhisperBase}ggml-{size.Name}.bin"),
        $"ggml-{size.Name}.bin", ModelPackaging.File, size.Bin);

    private static ModelInfo WhisperCoreMl((string Name, long Bin, long CoreMl) size) => new(
        $"whisper-{size.Name}-coreml", ModelKind.WhisperCoreMlEncoder, new Uri($"{WhisperBase}ggml-{size.Name}-encoder.mlmodelc.zip"),
        $"ggml-{size.Name}-encoder.mlmodelc", ModelPackaging.ZippedDirectory, size.CoreMl);
}
