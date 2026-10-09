namespace Meetrec.Cli;

// Cancels its token when Esc is pressed. Other keys pressed meanwhile are swallowed: a stray Enter during playback
// must not accept the suggested name at the next prompt.
internal sealed class EscapeKey : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(30);

    private readonly CancellationTokenSource _pressed;
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread? _watcher;

    private EscapeKey(CancellationToken outer)
    {
        _pressed = CancellationTokenSource.CreateLinkedTokenSource(outer);
        if (Console.IsInputRedirected)
        {
            return;
        }

        _watcher = new Thread(Watch) { IsBackground = true };
        _watcher.Start();
    }

    public CancellationToken Token => _pressed.Token;

    public static EscapeKey Watch(CancellationToken outer) => new(outer);

    public void Dispose()
    {
        _stop.Cancel();
        _watcher?.Join();
        _stop.Dispose();
        _pressed.Dispose();
    }

    private void Watch()
    {
        while (!_stop.IsCancellationRequested && !_pressed.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                _stop.Token.WaitHandle.WaitOne(PollInterval);
                continue;
            }

            if (Console.ReadKey(intercept: true).Key == ConsoleKey.Escape)
            {
                _pressed.Cancel();
            }
        }
    }
}
