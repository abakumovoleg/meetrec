using Meetrec.Core;
using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;

namespace Meetrec.Capture.Meetzone;

// Lays the audio of the page out into one track per participant login. A slot (incoming track) belongs to whoever
// the videobridge maps its ssrc to at the moment; a reconnected participant gets a new endpoint and slot but the
// same login, so the same track. Samples are placed by AudioContext time, and only non-silent blocks arrive, so
// gaps are filled with silence.
public sealed class MeetzoneRecorder(string directory) : IDisposable
{
    private const int SampleRate = AudioLoader.TargetSampleRate;

    // Audio that arrives before its owner is known waits this long; a minute covers a late presence.
    private static readonly TimeSpan MaxPending = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan HeaderInterval = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan SpeakingTail = TimeSpan.FromSeconds(0.7);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, Slot> _slots = [];
    private readonly Dictionary<long, string> _owners = [];
    private readonly Dictionary<string, Person> _people = [];
    private readonly Dictionary<string, TimeSpan> _contexts = [];
    private readonly Dictionary<string, Participant> _participants = [];
    private readonly Dictionary<string, Queue<Audio>> _pending = [];
    private string? _ownEndpoint;
    private string? _context;
    private TimeSpan _latest;

    public DateTimeOffset? StartedAt { get; private set; }

    public int DroppedBlocks { get; private set; }

    public event Action<string>? ParticipantAdded;

    public void Handle(MeetzoneMessage message)
    {
        string[] added;
        lock (_gate)
        {
            var before = _participants.Count;
            switch (message)
            {
                case Started started:
                    Start(started);
                    break;
                case Presence presence:
                    Update(presence);
                    break;
                case Sources sources:
                    foreach (var owner in sources.Owners)
                    {
                        _owners[owner.Ssrc] = owner.Owner;
                    }

                    break;
                case Slots slots:
                    foreach (var slot in slots.Tracks)
                    {
                        _slots[slot.Track] = slot;
                    }

                    break;
                case Audio audio when _context is not null:
                    Enqueue(audio with { At = audio.At + _contexts[_context] });
                    break;
            }

            if (message is Presence or Sources or Slots)
            {
                FlushPending();
            }

            added = [.. _participants.Values.Skip(before).Select(p => p.Name)];
        }

        foreach (var name in added)
        {
            ParticipantAdded?.Invoke(name);
        }
    }

    public RecordingSnapshot Snapshot()
    {
        lock (_gate)
        {
            var speaking = _participants.Values.Where(p => p.HeardUntil >= _latest - SpeakingTail).Select(p => p.Name);
            return new RecordingSnapshot(_participants.Count, [.. speaking]);
        }
    }

    // Own track first: on equal timestamps its words come first, as the mic's always have.
    public IReadOnlyList<SessionTrack> Tracks()
    {
        lock (_gate)
        {
            return
            [
                .. _participants.Values.OrderByDescending(p => p.Own).ThenBy(p => p.Start).Select(p => new SessionTrack(
                    p.File,
                    TrackSource.Participant,
                    (int)Math.Round(p.Start.TotalMilliseconds),
                    Speaker: new SessionSpeaker(SpeakerId.Meetzone(p.Login).Value, p.Name))),
            ];
        }
    }

    // Audio whose owner never became known is kept under the slot it came from rather than lost.
    public void Finish()
    {
        lock (_gate)
        {
            foreach (var (track, queue) in _pending)
            {
                var person = Resolve(track) ?? new Person(track, Speakers.Unknown, false);
                while (queue.TryDequeue(out var audio))
                {
                    Write(person, audio);
                }
            }

            _pending.Clear();
            foreach (var participant in _participants.Values)
            {
                participant.Writer.Dispose();
            }
        }
    }

    public void Dispose() => Finish();

    // A reloaded page starts a new AudioContext with its own clock; it is joined to the session by wall time.
    private void Start(Started started)
    {
        StartedAt ??= started.Wall;
        _contexts.TryAdd(started.Context, started.Wall - StartedAt.Value - started.At);
        _context = started.Context;
    }

    private void Update(Presence presence)
    {
        if (presence.Own)
        {
            _ownEndpoint = presence.Endpoint;
        }

        if (presence.Left)
        {
            return;
        }

        var login = presence.Login ?? presence.Endpoint;
        var person = new Person(login, presence.Name ?? login, presence.Endpoint == _ownEndpoint);
        _people[presence.Endpoint] = person;
        if (_participants.TryGetValue(login, out var participant))
        {
            participant.Name = person.Name;
            participant.Own |= person.Own;
        }
    }

    private void Enqueue(Audio audio)
    {
        if (Resolve(audio.Track) is { } person && !_pending.ContainsKey(audio.Track))
        {
            Write(person, audio);
            return;
        }

        if (!_pending.TryGetValue(audio.Track, out var queue))
        {
            _pending[audio.Track] = queue = new Queue<Audio>();
        }

        queue.Enqueue(audio);
        while (queue.Peek().At < audio.At - MaxPending)
        {
            queue.Dequeue();
            DroppedBlocks++;
        }
    }

    private void FlushPending()
    {
        foreach (var track in _pending.Keys.ToList())
        {
            if (Resolve(track) is not { } person)
            {
                continue;
            }

            foreach (var audio in _pending[track])
            {
                Write(person, audio);
            }

            _pending.Remove(track);
        }
    }

    private Person? Resolve(string track)
    {
        var endpoint = track == Audio.Outgoing
            ? _ownEndpoint
            : _slots.TryGetValue(track, out var slot) ? slot.Owner ?? _owners.GetValueOrDefault(slot.Ssrc) : null;
        return endpoint is null ? null : _people.GetValueOrDefault(endpoint);
    }

    private void Write(Person person, Audio audio)
    {
        if (!_participants.TryGetValue(person.Login, out var participant))
        {
            var file = SessionLayout.ParticipantFile(person.Login);
            participant = new Participant(person.Login, file, audio.At, new Wav16Writer(Path.Combine(directory, file)))
            {
                Name = person.Name,
                Own = person.Own,
            };
            _participants[person.Login] = participant;
        }

        participant.Append(audio);
        _latest = audio.At + Duration(audio.Pcm) > _latest ? audio.At + Duration(audio.Pcm) : _latest;
    }

    private static TimeSpan Duration(byte[] pcm) => TimeSpan.FromSeconds((double)pcm.Length / 2 / SampleRate);

    private sealed record Person(string Login, string Name, bool Own);

    private sealed class Participant(string login, string file, TimeSpan start, Wav16Writer writer)
    {
        private long _headerAt;

        public string Login => login;

        public string File => file;

        public TimeSpan Start => start;

        public Wav16Writer Writer => writer;

        public required string Name { get; set; }

        public required bool Own { get; set; }

        public TimeSpan HeardUntil { get; private set; }

        // A block that overlaps what is already written (two slots of one login at once) keeps only its new part.
        public void Append(Audio audio)
        {
            var position = (long)Math.Round((audio.At - start).TotalSeconds * SampleRate);
            var skip = writer.SampleCount - position;
            if (skip < 0)
            {
                writer.AppendSilence(-skip);
                skip = 0;
            }

            if (skip * 2 < audio.Pcm.Length)
            {
                writer.AppendPcm16(audio.Pcm.AsSpan((int)skip * 2));
            }

            HeardUntil = start + TimeSpan.FromSeconds((double)writer.SampleCount / SampleRate);
            if (writer.SampleCount - _headerAt >= HeaderInterval.TotalSeconds * SampleRate)
            {
                writer.UpdateHeader();
                _headerAt = writer.SampleCount;
            }
        }
    }
}

public sealed record RecordingSnapshot(int Participants, IReadOnlyList<string> Speaking);
