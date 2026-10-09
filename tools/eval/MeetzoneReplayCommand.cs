using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Meetrec.Core;
using NAudio.Wave;

namespace Meetrec.Eval;

// Plays a recording of the debug extension into `meetrec record --meetzone` the way the meetrec extension would:
// hello, wait for start, conference state, then non-silent 4096-sample blocks of every track in time order.
internal static class MeetzoneReplayCommand
{
    private const int Block = 4096;
    private const int SampleRate = 16_000;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<int> ExecuteAsync(Args args)
    {
        var probePath = Path.GetFullPath(args.At(0, "probe.json"));
        var port = int.Parse(args.Get("port", "47823"), CultureInfo.InvariantCulture);
        using var json = JsonDocument.Parse(File.ReadAllText(probePath));
        var root = json.RootElement;
        var events = root.GetProperty("events").EnumerateArray().ToList();

        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Origin", "chrome-extension://meetrec-eval");
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}"), CancellationToken.None);
        await SendAsync(socket, new { t = "hello", protocol = 1, version = "meetrec-eval", url = root.GetProperty("url").GetString() });
        var reply = await ReceiveAsync(socket);
        if (reply != """{"t":"start"}""")
        {
            throw new MeetrecException($"meetrec ответил не start: {reply}");
        }

        foreach (var (endpoint, person) in MeetzoneCommand.Presences(events))
        {
            await SendAsync(socket, new { t = "presence", endpoint, name = person.Name, login = person.Login, own = person.Own, left = false });
        }

        var sources = events.Where(e => e.GetProperty("k").GetString() == "dc.in")
            .Select(e => JsonDocument.Parse(e.GetProperty("data").GetString()!).RootElement)
            .Where(m => m.TryGetProperty("colibriClass", out var c) && c.GetString() == "AudioSourcesMap")
            .SelectMany(m => m.GetProperty("mappedSources").EnumerateArray())
            .Select(s => new { ssrc = s.GetProperty("ssrc").GetInt64(), owner = s.GetProperty("owner").GetString() })
            .ToList();
        await SendAsync(socket, new { t = "sources", sources });

        var sdp = events.Last(e => e.GetProperty("k").GetString() == "sdp.remote");
        var pc = sdp.GetProperty("pc").GetInt32();
        var slots = Slots(sdp.GetProperty("sdp").GetString()!, pc);
        await SendAsync(socket, new { t = "slots", slots = slots.Select(s => new { track = s.Track, ssrc = s.Ssrc }) });
        await SendAsync(socket, new { t = "started", context = "replay", at = 0.0, wall = root.GetProperty("timeOrigin").GetDouble() });

        var blocks = new List<(double At, string Track, byte[] Pcm)>();
        foreach (var track in root.GetProperty("tracks").EnumerateArray())
        {
            var key = track.GetProperty("dir").GetString() == "out"
                ? "out"
                : $"in-{track.GetProperty("pc").GetInt32()}-{track.GetProperty("mid").GetString()}";
            if (key != "out" && slots.All(s => s.Track != key))
            {
                continue;
            }

            var first = track.GetProperty("firstPlaybackTime").GetDouble();
            using var reader = new WaveFileReader(Path.Combine(Path.GetDirectoryName(probePath)!, track.GetProperty("file").GetString()!));
            var bytes = new byte[reader.Length];
            reader.ReadExactly(bytes);
            for (var offset = 0; offset < bytes.Length; offset += Block * 2)
            {
                var pcm = bytes.AsSpan(offset, Math.Min(Block * 2, bytes.Length - offset));
                if (pcm.ContainsAnyExcept((byte)0))
                {
                    blocks.Add((first + (double)offset / 2 / SampleRate, key, pcm.ToArray()));
                }
            }
        }

        foreach (var (at, track, pcm) in blocks.OrderBy(b => b.At))
        {
            await SendAsync(socket, new { t = "audio", track, at, pcm = Convert.ToBase64String(pcm) });
        }

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        Console.WriteLine($"   отправлено блоков: {blocks.Count}, дорожек: {blocks.Select(b => b.Track).Distinct().Count()}");
        return 0;
    }

    private static List<(string Track, long Ssrc)> Slots(string sdp, int pc)
    {
        var slots = new List<(string Track, long Ssrc)>();
        string? mid = null;
        foreach (var line in sdp.Split("\r\n"))
        {
            if (line.StartsWith("a=mid:", StringComparison.Ordinal))
            {
                mid = line["a=mid:".Length..];
            }
            else if (line.StartsWith("a=ssrc:", StringComparison.Ordinal) && line.Contains(" msid:remote-audio", StringComparison.Ordinal) && mid is not null)
            {
                slots.Add(($"in-{pc}-{mid}", long.Parse(line["a=ssrc:".Length..line.IndexOf(' ')], CultureInfo.InvariantCulture)));
            }
        }

        return slots;
    }

    private static Task SendAsync(ClientWebSocket socket, object message) =>
        socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message, Json), WebSocketMessageType.Text, true, CancellationToken.None);

    private static async Task<string> ReceiveAsync(ClientWebSocket socket)
    {
        var buffer = new byte[4096];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }
}
