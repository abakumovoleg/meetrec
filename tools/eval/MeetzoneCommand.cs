using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Meetrec.Core;
using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;
using NAudio.Wave;

namespace Meetrec.Eval;

// Turns a recording of the debug extension (meetzone/probe) into a session of participant tracks, the way
// meetrec will record Meetzone: one track per login, slots of a reconnected participant joined, own outgoing track.
internal static class MeetzoneCommand
{
    private static readonly XNamespace Client = "jabber:client";

    private static readonly XNamespace Nick = "http://jabber.org/protocol/nick";

    private static readonly XNamespace Muc = "http://jabber.org/protocol/muc#user";

    public static int Execute(Args args)
    {
        var probePath = Path.GetFullPath(args.At(0, "probe.json"));
        var output = Path.GetFullPath(args.At(1, "папка сессии"));
        if (SessionLayout.ContainsRecording(output) || File.Exists(Path.Combine(output, SessionLayout.SessionFileName)))
        {
            throw new MeetrecException($"В папке уже есть сессия: {output}");
        }

        using var json = JsonDocument.Parse(File.ReadAllText(probePath));
        var root = json.RootElement;
        var events = root.GetProperty("events").EnumerateArray().ToList();
        var people = Presences(events);
        var own = people.Values.SingleOrDefault(p => p.Own) ?? throw new MeetrecException("Нет своего presence (status 110)");
        var owners = SlotOwners(events);

        var slots = new List<Slot>();
        foreach (var track in root.GetProperty("tracks").EnumerateArray())
        {
            var mid = track.GetProperty("mid").GetString()!;
            var outgoing = track.GetProperty("dir").GetString() == "out";
            var person = outgoing ? own : owners.TryGetValue(mid, out var endpoint) ? people.GetValueOrDefault(endpoint) : null;
            if (person is null)
            {
                Console.WriteLine($"   пропущен слот {track.GetProperty("key").GetString()}: нет владельца");
                continue;
            }

            var file = Path.Combine(Path.GetDirectoryName(probePath)!, track.GetProperty("file").GetString()!);
            var start = TimeSpan.FromSeconds(track.GetProperty("firstPlaybackTime").GetDouble());
            slots.Add(new Slot(person, start, AudioLoader.LoadMono16k(file)));
        }

        var origin = slots.Min(s => s.Start);
        var startedAt = WallClock(events, root.GetProperty("timeOrigin").GetDouble(), origin);
        Directory.CreateDirectory(output);
        var tracks = new List<SessionTrack>();
        foreach (var group in slots.GroupBy(s => s.Person.Login).OrderByDescending(g => g.First().Person.Own))
        {
            var person = group.First().Person;
            var start = group.Min(s => s.Start);
            var file = SessionLayout.ParticipantFile(person.Login);
            WriteJoined(Path.Combine(output, file), group, start);
            tracks.Add(new SessionTrack(
                file, TrackSource.Participant, (int)Math.Round((start - origin).TotalMilliseconds), Speaker: new SessionSpeaker(SpeakerId.Meetzone(person.Login).Value, person.Name)));
            Console.WriteLine($"   {file}: {person.Name}, слотов {group.Count()}, сдвиг {(start - origin).TotalSeconds:0.000} с");
        }

        SessionFile.Write(Path.Combine(output, SessionLayout.SessionFileName), new Session(startedAt, "meetzone-probe", tracks));
        return 0;
    }

    internal static Dictionary<string, Person> Presences(List<JsonElement> events)
    {
        var people = new Dictionary<string, Person>();
        foreach (var e in events.Where(e => e.GetProperty("k").GetString() == "ws.in"))
        {
            var data = e.GetProperty("data").GetString()!;
            if (!data.StartsWith("<presence", StringComparison.Ordinal))
            {
                continue;
            }

            var presence = XElement.Parse(data);
            var from = (string?)presence.Attribute("from") ?? "";
            var login = presence.Descendants(Client + "login").FirstOrDefault()?.Value;
            var nick = presence.Element(Nick + "nick")?.Value;
            if (!from.Contains("@muc.", StringComparison.Ordinal) || login is null)
            {
                continue;
            }

            var endpoint = from[(from.LastIndexOf('/') + 1)..];
            var own = presence.Elements(Muc + "x").Elements(Muc + "status").Any(s => (string?)s.Attribute("code") == "110");
            people[endpoint] = new Person(login, nick ?? login, own || (people.TryGetValue(endpoint, out var known) && known.Own));
        }

        return people;
    }

    // mid → endpoint: the slot's ssrc comes from the remote SDP, its owner from AudioSourcesMap.
    private static Dictionary<string, string> SlotOwners(List<JsonElement> events)
    {
        var ssrcOwner = new Dictionary<long, string>();
        foreach (var e in events.Where(e => e.GetProperty("k").GetString() == "dc.in"))
        {
            using var message = JsonDocument.Parse(e.GetProperty("data").GetString()!);
            if (message.RootElement.TryGetProperty("colibriClass", out var type) && type.GetString() == "AudioSourcesMap")
            {
                foreach (var source in message.RootElement.GetProperty("mappedSources").EnumerateArray())
                {
                    var ssrc = source.GetProperty("ssrc").GetInt64();
                    var owner = source.GetProperty("owner").GetString()!;
                    if (ssrcOwner.TryGetValue(ssrc, out var previous) && previous != owner)
                    {
                        throw new MeetrecException($"Слот {ssrc} сменил владельца {previous} → {owner}: стенд этого не умеет");
                    }

                    ssrcOwner[ssrc] = owner;
                }
            }
        }

        var sdp = events.Last(e => e.GetProperty("k").GetString() == "sdp.remote").GetProperty("sdp").GetString()!;
        var owners = new Dictionary<string, string>();
        string? mid = null;
        foreach (var line in sdp.Split("\r\n"))
        {
            if (line.StartsWith("a=mid:", StringComparison.Ordinal))
            {
                mid = line["a=mid:".Length..];
            }
            else if (line.StartsWith("a=ssrc:", StringComparison.Ordinal) && line.Contains(" msid:remote-audio", StringComparison.Ordinal)
                && mid is not null && ssrcOwner.TryGetValue(long.Parse(line["a=ssrc:".Length..line.IndexOf(' ')], CultureInfo.InvariantCulture), out var owner))
            {
                owners[mid] = owner;
            }
        }

        return owners;
    }

    // The probe logs context time against wall time every 5 s; the first pair is enough for a start time.
    private static DateTimeOffset WallClock(List<JsonElement> events, double timeOrigin, TimeSpan contextTime)
    {
        var clock = events.FirstOrDefault(e => e.GetProperty("k").GetString() == "clock");
        var wallMs = clock.ValueKind == JsonValueKind.Undefined
            ? timeOrigin
            : clock.GetProperty("wall").GetDouble() + (contextTime.TotalSeconds - clock.GetProperty("ctxTime").GetDouble()) * 1000;
        return DateTimeOffset.FromUnixTimeMilliseconds((long)wallMs);
    }

    private static void WriteJoined(string path, IEnumerable<Slot> slots, TimeSpan start)
    {
        var placed = slots.Select(s => (From: (int)Math.Round((s.Start - start).TotalSeconds * AudioLoader.TargetSampleRate), s.Clip.Samples)).ToList();
        var samples = new float[placed.Max(p => p.From + p.Samples.Length)];
        foreach (var (from, source) in placed)
        {
            for (var i = 0; i < source.Length; i++)
            {
                samples[from + i] += source[i];
            }
        }

        using var writer = new WaveFileWriter(path, new WaveFormat(AudioLoader.TargetSampleRate, 16, 1));
        foreach (var sample in samples)
        {
            writer.WriteSample(Math.Clamp(sample, -1f, 1f));
        }
    }

    internal sealed record Person(string Login, string Name, bool Own);

    private sealed record Slot(Person Person, TimeSpan Start, AudioClip Clip);
}
