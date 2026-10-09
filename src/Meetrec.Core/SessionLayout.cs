namespace Meetrec.Core;

// The only place that knows file names inside a session folder and next to a lone recording.
public static class SessionLayout
{
    public const string MicFile = "mic.wav";

    public const string SystemFile = "system.wav";

    public const string SessionFileName = "session.json";

    public const string TranscriptName = "transcript";

    public static string TranscriptNameFor(string wavPath) => Path.GetFileNameWithoutExtension(wavPath) + "." + TranscriptName;

    public static string TranscriptPath(string directory, string name, string format) => Path.Combine(directory, $"{name}.{format}");

    public static bool ContainsRecording(string directory) =>
        File.Exists(Path.Combine(directory, SystemFile)) || File.Exists(Path.Combine(directory, MicFile));

    public static string TrackId(string file) => Path.GetFileNameWithoutExtension(file);
}
