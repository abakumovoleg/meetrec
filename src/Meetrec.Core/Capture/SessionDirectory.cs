namespace Meetrec.Core.Capture;

public static class SessionDirectory
{
    public const string DefaultParent = "meetings";

    public static string Default(DateTime localNow, string currentDirectory) =>
        Path.Combine(currentDirectory, DefaultParent, localNow.ToString("yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture));

    public static bool ContainsRecording(string directory) =>
        File.Exists(Path.Combine(directory, TranscribeInput.SystemFileName))
        || File.Exists(Path.Combine(directory, TranscribeInput.MicFileName));
}
