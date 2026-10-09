using SherpaOnnx;

namespace Meetrec.Asr;

public sealed class SileroVad(string modelPath) : IVad
{
    private const int WindowSize = 512;
    private const float MaxSpeechSeconds = 30f;

    public IReadOnlyList<SpeechRegion> Detect(float[] samples, int sampleRate, VadOptions options)
    {
        var config = new VadModelConfig
        {
            SampleRate = sampleRate,
            NumThreads = 1,
            SileroVad = new SileroVadModelConfig
            {
                Model = modelPath,
                Threshold = options.Threshold,
                MinSilenceDuration = (float)options.MinSilence.TotalSeconds,
                MinSpeechDuration = (float)options.MinSpeech.TotalSeconds,
                WindowSize = WindowSize,
                MaxSpeechDuration = MaxSpeechSeconds,
            },
        };

        using var vad = new VoiceActivityDetector(config, bufferSizeInSeconds: 2 * MaxSpeechSeconds);
        var regions = new List<SpeechRegion>();
        var window = new float[WindowSize];
        for (var offset = 0; offset + WindowSize <= samples.Length; offset += WindowSize)
        {
            Array.Copy(samples, offset, window, 0, WindowSize);
            vad.AcceptWaveform(window);
            Drain(vad, regions, sampleRate);
        }

        vad.Flush();
        Drain(vad, regions, sampleRate);
        return regions;
    }

    private static void Drain(VoiceActivityDetector vad, List<SpeechRegion> regions, int sampleRate)
    {
        while (!vad.IsEmpty())
        {
            var segment = vad.Front();
            regions.Add(new SpeechRegion(
                TimeSpan.FromSeconds((double)segment.Start / sampleRate),
                TimeSpan.FromSeconds((double)(segment.Start + segment.Samples.Length) / sampleRate)));
            vad.Pop();
        }
    }
}
