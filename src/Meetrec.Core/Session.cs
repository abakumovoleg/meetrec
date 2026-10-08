namespace Meetrec.Core;

public sealed record Session(
    DateTimeOffset StartedAtUtc,
    string? MicDevice,
    string? SystemDevice,
    // Delay of mic.wav start relative to system.wav start; mic time + OffsetMs = session time.
    int OffsetMs,
    string Version);
