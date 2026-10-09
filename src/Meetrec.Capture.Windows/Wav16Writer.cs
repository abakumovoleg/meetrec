using System.Buffers.Binary;
using System.Text;
using Meetrec.Core.Audio;

namespace Meetrec.Capture.Windows;

// Streaming writer for the recorded tracks: 16-bit PCM, mono, 16 kHz — the format the rest of the pipeline reads.
internal sealed class Wav16Writer : IDisposable
{
    public const int SampleRate = AudioLoader.TargetSampleRate;

    private const int HeaderSize = 44;
    private const int SilenceChunkSamples = SampleRate;

    private readonly FileStream _file;
    private byte[] _bytes = [];
    private byte[]? _silence;
    private bool _closed;

    public Wav16Writer(string path)
    {
        _file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        _file.Write(Header(0));
    }

    public long SampleCount { get; private set; }

    public void Append(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0)
        {
            return;
        }

        if (_bytes.Length < samples.Length * 2)
        {
            _bytes = new byte[samples.Length * 2];
        }

        for (var i = 0; i < samples.Length; i++)
        {
            var value = (short)Math.Clamp(MathF.Round(samples[i] * 32767f), short.MinValue, short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(_bytes.AsSpan(i * 2), value);
        }

        _file.Write(_bytes.AsSpan(0, samples.Length * 2));
        SampleCount += samples.Length;
    }

    public void AppendSilence(long samples)
    {
        if (samples <= 0)
        {
            return;
        }

        _silence ??= new byte[SilenceChunkSamples * 2];
        for (var left = samples; left > 0;)
        {
            var chunk = (int)Math.Min(left, SilenceChunkSamples);
            _file.Write(_silence.AsSpan(0, chunk * 2));
            left -= chunk;
        }

        SampleCount += samples;
    }

    // Called periodically as well as on close, so that a killed process still leaves a readable file.
    public void UpdateHeader()
    {
        if (_closed)
        {
            return;
        }

        var end = _file.Position;
        _file.Position = 0;
        _file.Write(Header(SampleCount * 2));
        _file.Position = end;
        _file.Flush();
    }

    public void Dispose()
    {
        if (_closed)
        {
            return;
        }

        UpdateHeader();
        _closed = true;
        _file.Dispose();
    }

    internal static byte[] Header(long dataBytes)
    {
        var header = new byte[HeaderSize];
        var span = header.AsSpan();
        Encoding.ASCII.GetBytes("RIFF", span);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], (uint)(36 + dataBytes));
        Encoding.ASCII.GetBytes("WAVEfmt ", span[8..]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[22..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..], SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], SampleRate * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(span[32..], 2);
        BinaryPrimitives.WriteUInt16LittleEndian(span[34..], 16);
        Encoding.ASCII.GetBytes("data", span[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[40..], (uint)dataBytes);
        return header;
    }
}
