using System.Text;
using Meetrec.Capture.Mac;
using Meetrec.Capture.Windows;
using Meetrec.Core.Audio;
using Meetrec.Core.Capture;

namespace Meetrec.Cli;

// Everything that differs between macOS and Windows; commands do not check the OS themselves.
internal static class Platform
{
    // The CoreML runtime of Whisper.net (macOS) needs the encoder model next to the ggml one.
    public static bool NeedsCoreMlEncoder => OperatingSystem.IsMacOS();

    public static bool CanPlayClips => OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();

    // Every message is in Russian, and a Windows console left on its OEM code page prints them as question marks —
    // including output redirected to a file.
    public static void ConfigureConsole()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            Console.OutputEncoding = Encoding.UTF8;

            // Input has the same problem in reverse: typed names arrive through the OEM code page, and Cyrillic that
            // it lacks turns into question marks. UTF-16 makes .NET read the console with ReadConsoleW; UTF-8 would
            // not do, since conhost returns zeros for non-ASCII there. Piped input is bytes and is left alone.
            if (!Console.IsInputRedirected)
            {
                Console.InputEncoding = Encoding.Unicode;
            }
        }
        catch (IOException)
        {
        }
    }

    public static ICaptureBackend CreateCaptureBackend()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsCaptureBackend();
        }

        if (!OperatingSystem.IsMacOS())
        {
            throw new CaptureException("Запись поддерживается на macOS и Windows.");
        }

        var helper = MacCaptureBackend.Locate(AppContext.BaseDirectory)
            ?? throw new CaptureException($"Не найден {MacCaptureBackend.HelperFileName} рядом с meetrec ({AppContext.BaseDirectory}).");
        return new MacCaptureBackend(helper);
    }

    public static void PlayClip(AudioClip clip, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsClipOutput.Play(clip, ct);
        }
        else if (OperatingSystem.IsMacOS())
        {
            MacClipOutput.Play(clip, ct);
        }
        else
        {
            throw new NotSupportedException("Проигрывание фрагментов поддерживается на macOS и Windows.");
        }
    }

    // Diarization keeps half of the cores busy for minutes, and at normal priority the rest of the machine stutters.
    // Below normal it takes the same time on an idle machine but yields to whatever the user is doing. Windows only:
    // elsewhere an unprivileged process can lower its priority but not raise it back.
    public static IDisposable LowerPriority() => OperatingSystem.IsWindows() ? new BackgroundPriority() : NoPriorityChange.Instance;

    private sealed class NoPriorityChange : IDisposable
    {
        public static readonly NoPriorityChange Instance = new();

        public void Dispose()
        {
        }
    }
}
