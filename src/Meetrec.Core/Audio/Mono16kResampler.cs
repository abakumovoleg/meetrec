using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Meetrec.Core.Audio;

// Push counterpart of AudioLoader: capture hands over buffers in the device format, the tracks are written at
// 16 kHz mono. The pipeline keeps its resampling state between calls, so buffer boundaries add no clicks.
public sealed class Mono16kResampler
{
    private readonly BufferedWaveProvider _source;
    private readonly ISampleProvider _pipeline;
    private float[] _samples = new float[AudioLoader.TargetSampleRate];
    private byte[] _zeros = [];

    public Mono16kResampler(WaveFormat format)
    {
        // WASAPI reports its mix format as WaveFormatExtensible, which the sample providers below do not accept.
        var standard = format is WaveFormatExtensible extensible ? extensible.ToStandardWaveFormat() : format;
        _source = new BufferedWaveProvider(standard) { ReadFully = false };
        ISampleProvider provider = _source.ToSampleProvider();
        if (provider.WaveFormat.Channels > 1)
        {
            provider = new MonoDownmixSampleProvider(provider);
        }

        if (provider.WaveFormat.SampleRate != AudioLoader.TargetSampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, AudioLoader.TargetSampleRate);
        }

        _pipeline = provider;
    }

    // The returned span is valid until the next call.
    public ReadOnlySpan<float> Convert(ReadOnlySpan<byte> data)
    {
        _source.AddSamples(data);
        return Drain();
    }

    // WASAPI may flag a packet as silent and hand over a buffer whose contents are undefined; its length still
    // belongs in the track, so the same number of zero bytes goes through the pipeline instead.
    public ReadOnlySpan<float> ConvertSilence(int bytes)
    {
        if (_zeros.Length < bytes)
        {
            _zeros = new byte[bytes];
        }

        _source.AddSamples(_zeros.AsSpan(0, bytes));
        return Drain();
    }

    private ReadOnlySpan<float> Drain()
    {
        var written = 0;
        while (true)
        {
            if (written == _samples.Length)
            {
                Array.Resize(ref _samples, _samples.Length * 2);
            }

            var free = _samples.Length - written;
            var read = _pipeline.Read(_samples.AsSpan(written, free));
            written += read;
            if (read < free)
            {
                return _samples.AsSpan(0, written);
            }
        }
    }
}
