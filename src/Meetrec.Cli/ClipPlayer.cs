using System.Diagnostics;
using System.Runtime.InteropServices;
using Meetrec.Core.Audio;
using NAudio.Wave;

namespace Meetrec.Cli;

internal sealed class ClipPlayer(string audioPath)
{
    public static readonly TimeSpan MaxClip = TimeSpan.FromSeconds(8);

    private static readonly TimeSpan Padding = TimeSpan.FromSeconds(0.3);

    private AudioClip? _audio;

    public static bool IsSupported => OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();

    // Returns the error message, or null when the clip has been played.
    public string? Play(TimeSpan start, TimeSpan end, CancellationToken ct)
    {
        try
        {
            _audio ??= AudioLoader.LoadMono16k(audioPath);
            var clip = _audio.Slice(start - Padding, (end - start > MaxClip ? start + MaxClip : end) + Padding);

            if (OperatingSystem.IsWindows())
            {
                WindowsClipOutput.Play(clip, ct);
            }
            else
            {
                PlayWithAfplay(clip);
            }

            return null;
        }
        // Ctrl+C during playback: the dialog ends on the next prompt, nothing to report here.
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e) when (e is IOException or ExternalException or InvalidOperationException
            or NotSupportedException or InvalidDataException or ArgumentException)
        {
            return e.Message;
        }
    }

    private static void PlayWithAfplay(AudioClip clip)
    {
        var path = Path.Combine(Path.GetTempPath(), $"meetrec-{Guid.NewGuid():N}.wav");
        try
        {
            using (var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(clip.SampleRate, 1)))
            {
                writer.WriteSamples(clip.Samples, 0, clip.Samples.Length);
            }

            using var player = Process.Start(new ProcessStartInfo("afplay", [path]) { UseShellExecute = false })
                ?? throw new IOException("Не удалось запустить afplay");
            player.WaitForExit();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
