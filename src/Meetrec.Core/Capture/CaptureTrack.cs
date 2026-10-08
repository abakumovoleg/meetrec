using Meetrec.Core.Audio;
using NAudio.Wave;

namespace Meetrec.Core.Capture;

// One recorded track: device buffers in, a 16 kHz mono WAV out, plus the peak for the console meter.
//
// While nothing is playing, WASAPI loopback delivers no buffers at all instead of zeros, so the track would come
// out shorter than the meeting and every timecode after a pause would drift. Each packet is therefore written at
// the position its own timestamp points to, and the gap before it is filled with silence. The microphone has no
// such gaps and goes through the same code, which keeps both tracks on one timeline.
public sealed class CaptureTrack : IDisposable
{
    // Packets arrive in bursts of tens of milliseconds, and their timestamps drift by less than that, so only a
    // clearly larger gap is a real pause.
    public static readonly TimeSpan GapThreshold = TimeSpan.FromMilliseconds(200);

    private readonly Lock _gate = new();
    private readonly Wav16Writer _writer;
    private readonly Mono16kResampler _resampler;
    private float _peak;
    private bool _closed;

    public CaptureTrack(string path, WaveFormat format)
    {
        _resampler = new Mono16kResampler(format);
        _writer = new Wav16Writer(path);
    }

    public static long Samples(TimeSpan position) => (long)Math.Round(position.TotalSeconds * Wav16Writer.SampleRate);

    // `start` is where the first frame of the packet belongs on the recording clock.
    public void Write(ReadOnlySpan<byte> data, bool silent, TimeSpan start)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            PadTo(start);
            var samples = silent ? _resampler.ConvertSilence(data.Length) : _resampler.Convert(data);
            _writer.Append(samples);
            foreach (var sample in samples)
            {
                _peak = Math.Max(_peak, Math.Abs(sample));
            }
        }
    }

    // Called once capture has stopped: no packet can arrive any more, so the whole remaining gap is silence and
    // the track ends up exactly as long as the recording.
    public void Finish(TimeSpan duration)
    {
        lock (_gate)
        {
            if (!_closed)
            {
                _writer.AppendSilence(Samples(duration) - _writer.SampleCount);
            }

            Close();
        }
    }

    public float TakePeak()
    {
        lock (_gate)
        {
            var peak = _peak;
            _peak = 0;
            return peak;
        }
    }

    public void FlushHeader()
    {
        lock (_gate)
        {
            if (!_closed)
            {
                _writer.UpdateHeader();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            Close();
        }
    }

    private void Close()
    {
        _closed = true;
        _writer.Dispose();
    }

    private void PadTo(TimeSpan position)
    {
        var gap = Samples(position) - _writer.SampleCount;
        if (gap >= Samples(GapThreshold))
        {
            _writer.AppendSilence(gap);
        }
    }
}
