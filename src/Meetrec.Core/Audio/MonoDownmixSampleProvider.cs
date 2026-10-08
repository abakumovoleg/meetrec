using NAudio.Wave;

namespace Meetrec.Core.Audio;

internal sealed class MonoDownmixSampleProvider(ISampleProvider source) : ISampleProvider
{
    private readonly int _channels = source.WaveFormat.Channels;
    private float[] _buffer = [];

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);

    public int Read(Span<float> buffer)
    {
        var needed = buffer.Length * _channels;
        if (_buffer.Length < needed)
        {
            _buffer = new float[needed];
        }

        var read = source.Read(_buffer.AsSpan(0, needed));
        var frames = read / _channels;
        for (var frame = 0; frame < frames; frame++)
        {
            var sum = 0f;
            var start = frame * _channels;
            for (var ch = 0; ch < _channels; ch++)
            {
                sum += _buffer[start + ch];
            }

            buffer[frame] = sum / _channels;
        }

        return frames;
    }
}
