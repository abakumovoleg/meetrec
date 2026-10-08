using System.Diagnostics;
using Meetrec.Core.Models;

namespace Meetrec.Cli;

internal sealed class ConsoleDownloadProgress : IProgress<DownloadProgress>
{
    private static readonly TimeSpan RedrawInterval = TimeSpan.FromMilliseconds(200);

    private readonly Stopwatch _sinceRedraw = Stopwatch.StartNew();
    private string? _currentModel;

    public void Report(DownloadProgress value)
    {
        var finished = value.TotalBytes is { } total && value.BytesReceived >= total;
        var started = value.BytesReceived == 0;
        if (!started && !finished && _sinceRedraw.Elapsed < RedrawInterval)
        {
            return;
        }

        _sinceRedraw.Restart();
        _currentModel = value.Model.Id;

        var line = value.TotalBytes is { } size && size > 0
            ? $"Скачивание {value.Model.Id}: {100.0 * value.BytesReceived / size,5:0.0}%  {Formatting.Megabytes(value.BytesReceived)} / {Formatting.Megabytes(size)}"
            : $"Скачивание {value.Model.Id}: {Formatting.Megabytes(value.BytesReceived)}";

        if (Console.IsErrorRedirected)
        {
            if (started || finished)
            {
                Console.Error.WriteLine(line);
            }

            return;
        }

        Console.Error.Write($"\r{line}\u001b[K");
        if (finished)
        {
            Console.Error.WriteLine();
        }
    }
}
