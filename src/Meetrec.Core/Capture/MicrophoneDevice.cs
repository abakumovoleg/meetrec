namespace Meetrec.Core.Capture;

public sealed record MicrophoneDevice(string Uid, string Name, bool IsDefault)
{
    public static MicrophoneDevice? Resolve(IReadOnlyList<MicrophoneDevice> devices, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return devices.FirstOrDefault(d => d.IsDefault);
        }

        return devices.FirstOrDefault(d => d.Uid == query)
            ?? devices.FirstOrDefault(d => d.Name.Equals(query, StringComparison.CurrentCultureIgnoreCase))
            ?? Single(devices.Where(d => d.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
    }

    private static MicrophoneDevice? Single(IEnumerable<MicrophoneDevice> matches)
    {
        var list = matches.Take(2).ToList();
        return list.Count == 1 ? list[0] : null;
    }
}
