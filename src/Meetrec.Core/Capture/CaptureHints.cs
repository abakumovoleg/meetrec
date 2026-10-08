namespace Meetrec.Core.Capture;

// Wording that differs between the backends: on macOS a silent system track almost always means a missing TCC
// permission, on Windows it means nothing is playing or the wrong output device is the default one.
public sealed record CaptureHints(
    // How an unknown permission status is described by `meetrec devices`.
    string PermissionUnknown,
    // Printed before recording starts when a permission status is unknown; null — nothing to warn about.
    string? PermissionPending,
    string? MicrophoneDenied,
    string? SystemAudioDenied,
    // Appended to the warning about ten seconds of zeros on the system track.
    string SystemSilence,
    // Printed when recording has not started within a few seconds.
    string SlowStart);
