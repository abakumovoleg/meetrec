using System.Runtime.InteropServices;

namespace Meetrec.Cli;

// The first Ctrl+C cancels only the current phase (e.g. stops recording in `run` so transcription can follow);
// a second one within the same phase falls through to the default handler and terminates the process.
internal sealed class Interrupts : IDisposable
{
    private readonly Lock _gate = new();
    private readonly PosixSignalRegistration[] _registrations;
    private CancellationTokenSource _phase = new();
    private bool _interrupted;

    // SIGQUIT is Ctrl+Break in a Windows console: without a handler it kills the process outright, leaving the
    // WAV files of a recording unclosed.
    public Interrupts() =>
        _registrations =
        [
            PosixSignalRegistration.Create(PosixSignal.SIGINT, Handle),
            PosixSignalRegistration.Create(PosixSignal.SIGQUIT, Handle),
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, Handle),
        ];

    public CancellationToken Current
    {
        get
        {
            lock (_gate)
            {
                return _phase.Token;
            }
        }
    }

    public bool Interrupted
    {
        get
        {
            lock (_gate)
            {
                return _interrupted;
            }
        }
    }

    public CancellationToken NextPhase()
    {
        lock (_gate)
        {
            _phase = new CancellationTokenSource();
            return _phase.Token;
        }
    }

    public void Dispose()
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }
    }

    private void Handle(PosixSignalContext context)
    {
        CancellationTokenSource phase;
        lock (_gate)
        {
            phase = _phase;
            _interrupted = true;
        }

        if (phase.IsCancellationRequested)
        {
            return;
        }

        context.Cancel = true;
        phase.Cancel();
    }
}
