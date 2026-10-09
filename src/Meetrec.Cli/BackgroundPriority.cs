using System.ComponentModel;
using System.Diagnostics;
using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;
using Meetrec.Diarization;

namespace Meetrec.Cli;

// Diarization keeps half of the cores busy for minutes, and at normal priority the rest of the machine stutters.
// Below normal it takes the same time on an idle machine but yields to whatever the user is doing. Windows only:
// elsewhere an unprivileged process can lower its priority but not raise it back.
internal sealed class BackgroundPriority : IDisposable
{
    private readonly ProcessPriorityClass? _previous;

    public BackgroundPriority()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            using var process = Process.GetCurrentProcess();
            var previous = process.PriorityClass;
            if (previous == ProcessPriorityClass.Normal)
            {
                process.PriorityClass = ProcessPriorityClass.BelowNormal;
                _previous = previous;
            }
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        if (_previous is not { } previous)
        {
            return;
        }

        try
        {
            using var process = Process.GetCurrentProcess();
            process.PriorityClass = previous;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
        }
    }
}

internal sealed class BackgroundPriorityDiarizer(IDiarizer inner) : IDiarizer
{
    public IReadOnlyList<DiarSegment> Diarize(AudioClip clip, int? numSpeakers, float threshold, IProgress<int>? progress)
    {
        using (new BackgroundPriority())
        {
            return inner.Diarize(clip, numSpeakers, threshold, progress);
        }
    }
}
