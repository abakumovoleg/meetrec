using Meetrec.Core.Audio;
using Meetrec.Core.Transcript;

namespace Meetrec.Diarization;

public interface IDiarizer
{
    IReadOnlyList<DiarSegment> Diarize(AudioClip clip, int? numSpeakers, float threshold, IProgress<int>? progress);
}
