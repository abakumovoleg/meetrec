using System.Diagnostics;
using Meetrec.Core.Capture;

namespace Meetrec.Cli;

internal sealed class RecordingStatus
{
    private static readonly TimeSpan RedirectedInterval = TimeSpan.FromSeconds(30);

    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly SilenceWatch _silence = new(SilenceWatch.DefaultWindow);
    private readonly Lock _gate = new();
    private TimeSpan _lastRedirectedLine = TimeSpan.Zero;

    public TimeSpan Elapsed => _elapsed.Elapsed;

    public void MarkStarted()
    {
        lock (_gate)
        {
            _elapsed.Restart();
        }
    }

    public void OnLevels(LevelInfo levels)
    {
        lock (_gate)
        {
            var elapsed = _elapsed.Elapsed;
            if (_silence.ShouldWarn(elapsed, levels.System))
            {
                WriteLine(
                    $"Внимание: системный звук — сплошные нули {SilenceWatch.DefaultWindow.TotalSeconds:0} с. Если собеседники уже говорят, " +
                    "у терминала нет разрешения «Запись звука и экрана» (Системные настройки → Конфиденциальность и безопасность).");
            }

            var mic = levels.Mic is { } m ? $"  Я {LevelBar.Render(m)}" : "";
            var line = $"● {elapsed:hh\\:mm\\:ss}{mic}  Собеседники {LevelBar.Render(levels.System)}";
            if (!Console.IsErrorRedirected)
            {
                Console.Error.Write($"\r{line}\u001b[K");
            }
            else if (elapsed - _lastRedirectedLine >= RedirectedInterval)
            {
                _lastRedirectedLine = elapsed;
                Console.Error.WriteLine(line);
            }
        }
    }

    public void WriteLine(string message)
    {
        lock (_gate)
        {
            Console.Error.WriteLine(Console.IsErrorRedirected ? message : $"\r\u001b[K{message}");
        }
    }
}
