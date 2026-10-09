using Meetrec.Core.Transcript;

namespace Meetrec.Core;

public sealed record SourceTrack(string Id, string Path, TimeSpan Offset, SpeakerSource Speakers, TrackSource Source);

public sealed record TranscribeInput(IReadOnlyList<SourceTrack> Tracks, Session? Session, string OutputDirectory, string OutputName)
{
    public int FixedSpeakers => Tracks.Select(t => t.Speakers).OfType<FixedSpeaker>().DistinctBy(s => s.Id).Count();

    public static TranscribeInput Resolve(string path)
    {
        var full = Path.GetFullPath(path);
        if (Directory.Exists(full))
        {
            return FromSessionDirectory(full);
        }

        if (!File.Exists(full))
        {
            throw new MeetrecException($"Нет такого файла или папки: {path}");
        }

        if (!Path.GetExtension(full).Equals(".wav", StringComparison.OrdinalIgnoreCase))
        {
            throw new MeetrecException($"Ожидается WAV-файл или папка сессии: {path}");
        }

        // A lone file is treated as system audio of a session without a mic, and gets its own output names so that
        // several recordings in one folder do not overwrite each other's transcripts.
        var track = new SourceTrack(SessionLayout.TrackId(SessionLayout.SystemFile), full, TimeSpan.Zero, DiarizedSpeakers.Instance, TrackSource.System);
        return new TranscribeInput([track], null, Path.GetDirectoryName(full)!, SessionLayout.TranscriptNameFor(full));
    }

    private static TranscribeInput FromSessionDirectory(string directory)
    {
        var sessionPath = Path.Combine(directory, SessionLayout.SessionFileName);
        var session = File.Exists(sessionPath) ? SessionFile.Read(sessionPath) : null;
        var tracks = session?.Tracks ?? Guess(directory);
        if (tracks.FirstOrDefault(t => !File.Exists(Path.Combine(directory, t.File))) is { } missing)
        {
            throw new MeetrecException($"В папке сессии нет {missing.File}: {directory}");
        }

        if (tracks.Count == 0)
        {
            throw new MeetrecException($"В папке сессии нет дорожек: {directory}");
        }

        return new TranscribeInput([.. tracks.Select(t => ToSource(t, directory))], session, directory, SessionLayout.TranscriptName);
    }

    // Without session.json the tracks are taken from the files present, with no offsets.
    private static IReadOnlyList<SessionTrack> Guess(string directory)
    {
        var me = new SessionSpeaker(SpeakerId.Me.Value, Speakers.Me);
        SessionTrack[] mic = File.Exists(Path.Combine(directory, SessionLayout.MicFile))
            ? [new SessionTrack(SessionLayout.MicFile, TrackSource.Mic, 0, Speaker: me)]
            : [];
        return [.. mic, new SessionTrack(SessionLayout.SystemFile, TrackSource.System, 0)];
    }

    private static SourceTrack ToSource(SessionTrack track, string directory) => new(
        SessionLayout.TrackId(track.File),
        Path.Combine(directory, track.File),
        TimeSpan.FromMilliseconds(track.OffsetMs),
        track.Speaker is { } speaker ? new FixedSpeaker(new SpeakerId(speaker.Id), speaker.Name) : DiarizedSpeakers.Instance,
        track.Source);
}
