using System.Diagnostics;
using Meetrec.Core.Audio;
using NAudio.Wave;

namespace Meetrec.Cli;

internal sealed class ClipPlayer(string audioPath)
{
    public static readonly TimeSpan MaxClip = TimeSpan.FromSeconds(8);

    private static readonly TimeSpan Padding = TimeSpan.FromSeconds(0.3);

    private AudioClip? _audio;

    public static bool IsSupported => OperatingSystem.IsMacOS();

    public TimeSpan Play(TimeSpan start, TimeSpan end)
    {
        _audio ??= AudioLoader.LoadMono16k(audioPath);
        var clip = _audio.Slice(start - Padding, (end - start > MaxClip ? start + MaxClip : end) + Padding);

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
            return clip.Duration;
        }
        finally
        {
            File.Delete(path);
        }
    }
}
