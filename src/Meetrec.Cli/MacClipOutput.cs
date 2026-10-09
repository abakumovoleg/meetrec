using System.Diagnostics;
using System.Runtime.Versioning;
using Meetrec.Core.Audio;
using NAudio.Wave;

namespace Meetrec.Cli;

[SupportedOSPlatform("macos")]
internal static class MacClipOutput
{
    public static void Play(AudioClip clip, CancellationToken ct)
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
            try
            {
                player.WaitForExitAsync(ct).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                player.Kill();
                player.WaitForExit();
                throw;
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
