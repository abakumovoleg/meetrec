namespace Meetrec.Core;

public sealed record TranscribeInput(string? MicPath, string SystemPath, Session? Session, string OutputDirectory, string OutputName)
{
    public const string MicFileName = "mic.wav";

    public const string SystemFileName = "system.wav";

    public const string SessionOutputName = "transcript";

    public const string SingleFileOutputSuffix = ".transcript";

    public bool HasMicTrack => MicPath is not null;

    public int OffsetMs => Session?.OffsetMs ?? 0;

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

        // A lone file gets its own output names so that several recordings in one folder do not overwrite
        // each other's transcript.* files.
        return new TranscribeInput(
            null, full, null, Path.GetDirectoryName(full)!, Path.GetFileNameWithoutExtension(full) + SingleFileOutputSuffix);
    }

    private static TranscribeInput FromSessionDirectory(string directory)
    {
        var system = Path.Combine(directory, SystemFileName);
        if (!File.Exists(system))
        {
            throw new MeetrecException($"В папке сессии нет {SystemFileName}: {directory}");
        }

        var mic = Path.Combine(directory, MicFileName);
        var sessionPath = Path.Combine(directory, SessionFile.FileName);
        var session = File.Exists(sessionPath) ? SessionFile.Read(sessionPath) : null;

        return new TranscribeInput(File.Exists(mic) ? mic : null, system, session, directory, SessionOutputName);
    }
}
