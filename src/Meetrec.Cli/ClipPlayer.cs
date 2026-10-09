using System.Diagnostics;
using System.Runtime.InteropServices;
using Meetrec.Core;
using Meetrec.Core.Audio;
using NAudio.Wave;

namespace Meetrec.Cli;

internal sealed class ClipPlayer(Func<string, AudioClip?> load)
{
    public static readonly TimeSpan MaxClip = TimeSpan.FromSeconds(8);

    private static readonly TimeSpan Padding = TimeSpan.FromSeconds(0.3);

    private readonly Dictionary<string, AudioClip?> _audio = [];

    public static bool IsSupported => OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();

    public static ClipPlayer? ForFiles(IReadOnlyList<SourceTrack> tracks) =>
        IsSupported ? new ClipPlayer(id => tracks.FirstOrDefault(t => t.Id == id) is { } t ? AudioLoader.LoadMono16k(t.Path) : null) : null;

    public static ClipPlayer? ForClips(IReadOnlyDictionary<string, AudioClip> clips) =>
        IsSupported ? new ClipPlayer(clips.GetValueOrDefault) : null;

    // Returns the error message, or null when the clip has been played.
    public string? Play(string track, TimeSpan start, TimeSpan end, CancellationToken ct)
    {
        try
        {
            if (!_audio.TryGetValue(track, out var audio))
            {
                _audio[track] = audio = load(track);
            }

            if (audio is null)
            {
                return $"нет звука дорожки {track}";
            }

            var clip = audio.Slice(start - Padding, (end - start > MaxClip ? start + MaxClip : end) + Padding);

            if (OperatingSystem.IsWindows())
            {
                WindowsClipOutput.Play(clip, ct);
            }
            else
            {
                PlayWithAfplay(clip, ct);
            }

            return null;
        }
        // Esc or Ctrl+C during playback: Esc returns to the prompt, Ctrl+C ends the dialog there; nothing to report.
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e) when (e is MeetrecException or IOException or ExternalException or InvalidOperationException
            or NotSupportedException or InvalidDataException or ArgumentException)
        {
            return e.Message;
        }
    }

    private static void PlayWithAfplay(AudioClip clip, CancellationToken ct)
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
