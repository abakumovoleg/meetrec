using System.Text.Json;
using Meetrec.Core.Transcript;
using static Meetrec.Core.Output.TranscriptJson;

namespace Meetrec.Core.Output;

public sealed record StoredTranscript(Conversation Conversation, int SchemaVersion)
{
    public bool IsOutdated => SchemaVersion < TranscriptJson.SchemaVersion;
}

public static class TranscriptReader
{
    private const string MicTrack = "mic";

    public static StoredTranscript Read(string path)
    {
        var dto = ReadDto(path);
        try
        {
            return dto.SchemaVersion switch
            {
                null => new StoredTranscript(FromV1(dto), 1),
                SchemaVersion => new StoredTranscript(FromV2(dto), SchemaVersion),
                _ => throw new MeetrecException($"{path} записан более новой версией meetrec (схема {dto.SchemaVersion})"),
            };
        }
        catch (KeyNotFoundException e)
        {
            throw new MeetrecException($"Не удалось прочитать {path}: {e.Message}", e);
        }
    }

    private static TranscriptDto ReadDto(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<TranscriptDto>(File.ReadAllText(path), Options)
                ?? throw new MeetrecException($"Пустая расшифровка: {path}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new MeetrecException($"Не удалось прочитать {path}: {e.Message}", e);
        }
    }

    private static Conversation FromV2(TranscriptDto dto)
    {
        var speakers = (dto.Speakers ?? []).Select(s => new TranscriptSpeaker(new SpeakerId(s.Id), s.Name, s.Track, s.Diarized)).ToList();
        var known = speakers.Select(s => s.Id).ToHashSet();
        var utterances = dto.Utterances
            .Where(u => u.Words.Count > 0)
            .Select(u => new Utterance(Known(u.SpeakerId), u.Track, [.. u.Words.Select(w => Word(w, Known(w.SpeakerId)))]))
            .ToList();
        return new Conversation(speakers, utterances);

        SpeakerId Known(string? id) =>
            id is not null && known.Contains(new SpeakerId(id)) ? new SpeakerId(id) : throw new KeyNotFoundException($"нет спикера «{id}» в таблице");
    }

    // The speaker table is rebuilt from words in order of appearance, as the merger builds it.
    private static Conversation FromV1(TranscriptDto dto)
    {
        var speakers = new List<TranscriptSpeaker>();
        var known = new HashSet<SpeakerId>();
        var utterances = new List<Utterance>();
        foreach (var u in dto.Utterances.Where(u => u.Words.Count > 0))
        {
            var words = new List<TranscriptWord>();
            foreach (var w in u.Words)
            {
                var name = w.Speaker ?? u.Speaker;
                var id = V1Id(w.Label ?? name, w.Track);
                if (known.Add(id))
                {
                    speakers.Add(new TranscriptSpeaker(id, name, w.Track, Diarized: w.Track != MicTrack));
                }

                words.Add(Word(w, id));
            }

            utterances.Add(new Utterance(words[0].Speaker, u.Track, words));
        }

        return new Conversation(speakers, utterances);
    }

    private static SpeakerId V1Id(string label, string track)
    {
        if (track == MicTrack)
        {
            return SpeakerId.Me;
        }

        if (label == Speakers.Unknown)
        {
            return SpeakerId.Unknown;
        }

        return Speakers.TryParseNumbered(label, out var number) ? SpeakerId.Diarized(track, number) : new SpeakerId($"{track}:{label}");
    }

    private static TranscriptWord Word(WordDto w, SpeakerId speaker) =>
        new(w.Text, TimeSpan.FromSeconds(w.Start), TimeSpan.FromSeconds(w.End), speaker, w.Track);
}
