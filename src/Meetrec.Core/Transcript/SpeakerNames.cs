namespace Meetrec.Core.Transcript;

public static class SpeakerNames
{
    public static IReadOnlyList<TranscriptSpeaker> Nameable(Conversation conversation) => [.. conversation.Speakers.Where(s => s.IsNameable)];

    // Speakers given the same name end up in one utterance when they speak in a row.
    public static Conversation Apply(Conversation conversation, IReadOnlyDictionary<SpeakerId, string> names)
    {
        var speakers = conversation.Speakers
            .Select(s => s.IsNameable && names.TryGetValue(s.Id, out var name) ? s with { Name = name } : s)
            .ToList();
        return new Conversation(speakers, TranscriptMerger.Group([.. conversation.Utterances.SelectMany(u => u.Words)], speakers));
    }

    // Back to "Спикер N", so that speakers merged under one name are shown separately again.
    public static Conversation Reset(Conversation conversation) =>
        Apply(conversation, Nameable(conversation).ToDictionary(s => s.Id, s => Speakers.DefaultName(s.Id)));
}
