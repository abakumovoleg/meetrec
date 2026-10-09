using Meetrec.Core.Output;
using Meetrec.Core.Transcript;

namespace Meetrec.Tests;

internal static class TestData
{
    public const string MicTrack = "mic";
    public const string SystemTrack = "system";

    public static readonly SpeakerId S1 = SpeakerId.Diarized(SystemTrack, 1);
    public static readonly SpeakerId S2 = SpeakerId.Diarized(SystemTrack, 2);
    public static readonly SpeakerId S3 = SpeakerId.Diarized(SystemTrack, 3);

    // The user speaks on the mic track, everyone else on the system track.
    public static TranscriptWord Word(string text, double start, double end, SpeakerId? speaker = null) =>
        new(text, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), speaker ?? SpeakerId.Me, TrackOf(speaker ?? SpeakerId.Me));

    public static Utterance Said(SpeakerId speaker, double start, string text)
    {
        var words = text.Split(' ');
        return Utterance([.. words.Select((w, i) => Word(w, start + i * 0.4, start + i * 0.4 + 0.3, speaker))]);
    }

    public static Utterance Utterance(params TranscriptWord[] words) => new(words[0].Speaker, words[0].Track, words);

    // Utterances are kept as given; the speaker table gets default names, overridden by `names`.
    public static Conversation Conversation(Utterance[] utterances, params (SpeakerId Id, string Name)[] names)
    {
        var speakers = utterances
            .SelectMany(u => u.Words)
            .Select(w => w.Speaker)
            .Distinct()
            .Select(id => new TranscriptSpeaker(
                id,
                names.Where(n => n.Id == id).Select(n => n.Name).FirstOrDefault() ?? DefaultName(id),
                TrackOf(id),
                Diarized: id != SpeakerId.Me))
            .ToList();
        return new Conversation(speakers, utterances);
    }

    public static string Render(ITranscriptFormatter formatter, Conversation conversation)
    {
        using var writer = new StringWriter();
        formatter.Write(conversation, writer);
        return writer.ToString();
    }

    private static string DefaultName(SpeakerId id) => id == SpeakerId.Me ? Speakers.Me : Speakers.DefaultName(id);

    private static string TrackOf(SpeakerId id) => id == SpeakerId.Me ? MicTrack : SystemTrack;
}
