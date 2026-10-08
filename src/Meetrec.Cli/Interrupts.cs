using System.Runtime.InteropServices;

namespace Meetrec.Cli;

// The first Ctrl+C cancels only the current phase (e.g. stops recording in `run` so transcription can follow);
// a second one within the same phase falls through to the default handler and terminates the process.
internal sealed class Interrupts : IDisposable
{
    private readonly Lock _gate = new();
    private readonly PosixSignalRegistration[] _registrations;
    private CancellationTokenSource _phase = new();

    public Interrupts() =>
        _registrations = [PosixSignalRegistration.Create(PosixSignal.SIGINT, Handle), PosixSignalRegistration.Create(PosixSignal.SIGTERM, Handle)];

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
        }

        if (phase.IsCancellationRequested)
        {
            return;
        }

        context.Cancel = true;
        phase.Cancel();
    }
}
