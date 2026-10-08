using System.Diagnostics;

namespace Meetrec.Capture.Windows;

// WASAPI timestamps every packet with the performance counter at its first frame, in the same 100-nanosecond
// units TimeSpan counts in. That is where the packet belongs on the recording clock — far more reliable than the
// moment the capture thread got around to handing it over, which the OS can delay. When the device reports the
// timestamp as unreliable, the packet is placed by arrival time minus its own length instead.
internal static class PacketTime
{
    private const long UnitsPerSecond = 10_000_000;

    public static long Now() => (long)(Stopwatch.GetTimestamp() * ((double)UnitsPerSecond / Stopwatch.Frequency));

    public static TimeSpan Start(long timestamp, long origin, bool reliable, TimeSpan arrival, TimeSpan length) =>
        reliable && timestamp > origin ? TimeSpan.FromTicks(timestamp - origin) : arrival - length;
}
