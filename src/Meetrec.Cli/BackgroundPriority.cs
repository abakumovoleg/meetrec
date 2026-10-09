using System.ComponentModel;
using System.Diagnostics;
using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;
using Meetrec.Diarization;

namespace Meetrec.Cli;

// See Platform.LowerPriority.
internal sealed class BackgroundPriority : IDisposable
{
    private readonly ProcessPriorityClass? _previous;

    public BackgroundPriority()
    {
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
        using (Platform.LowerPriority())
        {
            return inner.Diarize(clip, numSpeakers, threshold, progress);
        }
    }
}
