using Meetrec.Core.Models;

namespace Meetrec.Eval;

internal static class Models
{
    public static async Task<ModelStore> EnsureAsync(IEnumerable<ModelInfo> models)
    {
        var store = new ModelStore(ModelDirectory.Resolve());
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        await store.EnsureAllAsync(models, http, null, CancellationToken.None);
        return store;
    }

    public static IReadOnlyList<ModelInfo> ForRecognition(string whisper) =>
    [
        ModelCatalog.WhisperModel(whisper),
        .. OperatingSystem.IsMacOS() ? [ModelCatalog.WhisperCoreMlEncoder(whisper)] : Array.Empty<ModelInfo>(),
        ModelCatalog.SileroVad,
    ];
}
