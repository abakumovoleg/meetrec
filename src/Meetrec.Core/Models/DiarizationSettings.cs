namespace Meetrec.Core.Models;

public sealed record DiarizationSettings(ModelInfo Embedding, int? NumClusters, float Threshold)
{
    public const float WespeakerThreshold = 0.7f;

    public const float Eres2NetThreshold = 0.5f;

    // Measured on a real Russian call: eres2net honours an explicit cluster count, while wespeaker may return
    // fewer; without a count only wespeaker has a stable threshold (eres2net jumps from 12 speakers to 1).
    // totalSpeakers counts everyone, including the speakers of fixed tracks (the user's mic), who are not diarized.
    public static DiarizationSettings Choose(int? totalSpeakers, int fixedSpeakers, float? threshold)
    {
        if (totalSpeakers is { } total)
        {
            var clusters = Math.Max(1, total - fixedSpeakers);
            return new DiarizationSettings(ModelCatalog.Eres2NetEmbedding, clusters, threshold ?? Eres2NetThreshold);
        }

        return new DiarizationSettings(ModelCatalog.WespeakerEmbedding, null, threshold ?? WespeakerThreshold);
    }
}
