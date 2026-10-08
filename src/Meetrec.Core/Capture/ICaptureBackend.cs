namespace Meetrec.Core.Capture;

public interface ICaptureBackend : IAsyncDisposable
{
    event Action<LevelInfo>? Levels;

    // Completes when the capture process ends, whether stopped or failed on its own.
    Task Exited { get; }

    Task<IReadOnlyList<MicrophoneDevice>> ListMicrophonesAsync(CancellationToken ct);

    Task<CaptureStarted> StartAsync(string sessionDir, CaptureOptions options, CancellationToken ct);

    Task StopAsync();
}

public sealed record CaptureOptions(string? MicUid, bool NoMic);

public sealed record CaptureStarted(int OffsetMs);

public readonly record struct LevelInfo(float? Mic, float System);

public sealed class CaptureException(string message) : Exception(message);
