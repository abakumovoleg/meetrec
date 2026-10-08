namespace Meetrec.Core.Capture;

public interface ICaptureBackend : IAsyncDisposable
{
    event Action<LevelInfo>? Levels;

    CaptureHints Hints { get; }

    // Completes when the capture process ends, whether stopped or failed on its own.
    Task Exited { get; }

    Task<IReadOnlyList<MicrophoneDevice>> ListMicrophonesAsync(CancellationToken ct);

    Task<CapturePermissions> CheckPermissionsAsync(CancellationToken ct);

    Task<CaptureStarted> StartAsync(string sessionDir, CaptureOptions options, CancellationToken ct);

    Task StopAsync();
}

public sealed record CaptureOptions(string? MicUid, bool NoMic);

public sealed record CaptureStarted(int OffsetMs, string SystemDevice);

public enum PermissionStatus
{
    Unknown,
    Granted,
    Denied,
}

public sealed record CapturePermissions(PermissionStatus Microphone, PermissionStatus SystemAudio);

public readonly record struct LevelInfo(float? Mic, float System);

public sealed class CaptureException(string message) : Exception(message);
