using System.Diagnostics;

namespace Meetrec.Cli;

internal sealed class ConsoleStageProgress(string stage) : IProgress<int>
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly Lock _gate = new();
    private int _shown = -1;

    public void Report(int value)
    {
        lock (_gate)
        {
            value = Math.Clamp(value, 0, 100);
            if (value == _shown || Console.IsErrorRedirected)
            {
                return;
            }

            _shown = value;
            Console.Error.Write($"\r{stage}: {value,3}%\u001b[K");
        }
    }

    public void Complete()
    {
        lock (_gate)
        {
            var line = $"{stage}: готово за {Formatting.Duration(_elapsed.Elapsed)}";
            Console.Error.WriteLine(Console.IsErrorRedirected ? line : $"\r{line}\u001b[K");
        }
    }
}
