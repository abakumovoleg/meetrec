namespace Meetrec.Core.Transcript;

public sealed record Conversation(IReadOnlyList<TranscriptSpeaker> Speakers, IReadOnlyList<Utterance> Utterances)
{
    public TranscriptSpeaker Speaker(SpeakerId id) =>
        Speakers.FirstOrDefault(s => s.Id == id) ?? throw new KeyNotFoundException($"Нет спикера {id} в таблице спикеров");

    public string NameOf(SpeakerId id) => Speaker(id).Name;
}

public sealed record TranscriptSpeaker(SpeakerId Id, string Name, string Track, bool Diarized)
{
    // Fixed speakers (the user, Meetzone participants) come with their names; only diarized ones are named by the user.
    public bool IsNameable => Diarized && Id != SpeakerId.Unknown;
}
