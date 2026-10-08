using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Meetrec.Core.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Meetrec.Cli;

// Playback of a 16 kHz mono clip through WASAPI in shared mode: the audio engine converts the format itself,
// so the clip goes to the device as is. The same default output device the loopback track was recorded from.
[SupportedOSPlatform("windows")]
internal static class WindowsClipOutput
{
    private static readonly TimeSpan Tail = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);
    private const int LatencyMs = 200;

    public static void Play(AudioClip clip, CancellationToken ct)
    {
        using var enumerator = new MMDeviceEnumerator();
        if (!enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Console))
        {
            throw new IOException("Нет устройства вывода по умолчанию");
        }

        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
        using var player = new WasapiPlayerBuilder()
            .WithDevice(device)
            .WithSharedMode()
            .WithLatency(LatencyMs)
            .Build();

        using var finished = new ManualResetEventSlim(false);
        Exception? failure = null;
        player.PlaybackStopped += (_, e) =>
        {
            failure = e.Exception;
            finished.Set();
        };

        player.Init(Source(clip));
        player.Play();
        try
        {
            if (!finished.Wait(clip.Duration + Tail + StopTimeout, ct))
            {
                throw new IOException("Устройство вывода не ответило");
            }
        }
        finally
        {
            player.Stop();
        }

        if (failure is not null)
        {
            throw new IOException(failure.Message, failure);
        }
    }

    // Silence after the clip: WASAPI stops as soon as the source ends, which would cut off the last buffer.
    private static IWaveProvider Source(AudioClip clip)
    {
        var samples = new float[clip.Samples.Length + (int)(clip.SampleRate * Tail.TotalSeconds)];
        clip.Samples.CopyTo(samples, 0);
        var bytes = MemoryMarshal.AsBytes(samples.AsSpan()).ToArray();
        return new RawSourceWaveStream(new MemoryStream(bytes), WaveFormat.CreateIeeeFloatWaveFormat(clip.SampleRate, 1));
    }
}
