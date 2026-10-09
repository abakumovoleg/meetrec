using System.Runtime.InteropServices;
using Meetrec.Core;
using Meetrec.Core.Audio;

namespace Meetrec.Cli;

internal sealed class ClipPlayer(Func<string, AudioClip?> load)
{
    public static readonly TimeSpan MaxClip = TimeSpan.FromSeconds(8);

    private static readonly TimeSpan Padding = TimeSpan.FromSeconds(0.3);

    private readonly Dictionary<string, AudioClip?> _audio = [];

    public static ClipPlayer? ForFiles(IReadOnlyList<SourceTrack> tracks) =>
        Platform.CanPlayClips ? new ClipPlayer(id => tracks.FirstOrDefault(t => t.Id == id) is { } t ? AudioLoader.LoadMono16k(t.Path) : null) : null;

    public static ClipPlayer? ForClips(IReadOnlyDictionary<string, AudioClip> clips) =>
        Platform.CanPlayClips ? new ClipPlayer(clips.GetValueOrDefault) : null;

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

            Platform.PlayClip(clip, ct);

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
}
