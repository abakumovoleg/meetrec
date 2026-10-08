using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;
using SherpaOnnx;

namespace Meetrec.Diarization;

public sealed class SpeakerDiarizer(string segmentationModelPath, string embeddingModelPath)
{
    private const float WindowShiftRatio = 0.1f;

    public IReadOnlyList<DiarSegment> Diarize(AudioClip clip, int? numSpeakers, float threshold, IProgress<int>? progress)
    {
        var config = new OfflineSpeakerDiarizationConfig();
        config.Segmentation.Pyannote.Model = segmentationModelPath;
        config.Segmentation.Pyannote.WindowShiftRatio = WindowShiftRatio;
        config.Segmentation.NumThreads = ThreadCount;
        config.Embedding.Model = embeddingModelPath;
        config.Embedding.NumThreads = ThreadCount;
        config.Clustering.NumClusters = numSpeakers ?? -1;
        config.Clustering.Threshold = threshold;

        using var diarization = new OfflineSpeakerDiarization(config);
        if (diarization.SampleRate != clip.SampleRate)
        {
            throw new ArgumentException($"Диаризация ждёт {diarization.SampleRate} Гц, получено {clip.SampleRate} Гц", nameof(clip));
        }

        var callback = new OfflineSpeakerDiarizationProgressCallback((processed, total, _) =>
        {
            progress?.Report(total > 0 ? 100 * processed / total : 100);
            return 0;
        });

        var segments = diarization.ProcessWithCallback(clip.Samples, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        return [.. segments.Select(s => new DiarSegment(TimeSpan.FromSeconds(s.Start), TimeSpan.FromSeconds(s.End), s.Speaker))];
    }

    private static int ThreadCount => Math.Max(1, Environment.ProcessorCount / 2);
}
