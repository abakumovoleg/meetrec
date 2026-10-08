using Meetrec.Core.Transcript;

namespace Meetrec.Core.Tests;

internal static class TestData
{
    public static TranscriptWord Word(string text, double start, double end, string speaker = Speakers.Me, Track track = Track.Mic) =>
        new(text, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), speaker, track);

    public static Utterance Utterance(params TranscriptWord[] words) => new(words[0].Speaker, words[0].Track, words);

    public static string Render(Meetrec.Core.Output.ITranscriptFormatter formatter, params Utterance[] utterances)
    {
        using var writer = new StringWriter();
        formatter.Write(utterances, writer);
        return writer.ToString();
    }
}
