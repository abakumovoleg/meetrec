using Meetrec.Core.Transcript;

namespace Meetrec.Tests;

internal static class TestData
{
    public static TranscriptWord Word(string text, double start, double end, string speaker = Speakers.Me, Track track = Track.Mic, string? label = null) =>
        new(text, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), speaker, track, label);

    public static Utterance Said(string speaker, double start, string text, Track track = Track.System)
    {
        var words = text.Split(' ');
        return Utterance([.. words.Select((w, i) => Word(w, start + i * 0.4, start + i * 0.4 + 0.3, speaker, track))]);
    }

    public static Utterance Utterance(params TranscriptWord[] words) => new(words[0].Speaker, words[0].Track, words);

    public static string Render(Meetrec.Core.Output.ITranscriptFormatter formatter, params Utterance[] utterances)
    {
        using var writer = new StringWriter();
        formatter.Write(utterances, writer);
        return writer.ToString();
    }
}
