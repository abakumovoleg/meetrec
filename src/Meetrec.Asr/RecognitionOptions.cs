namespace Meetrec.Asr;

// Every value here was chosen by measurement on real calls; see docs/decisions.md before changing one.
public sealed record RecognitionOptions
{
    public static RecognitionOptions Default { get; } = new();

    public VadOptions Vad { get; init; } = new();

    // Regions quieter than this are dropped before Whisper (see SpeechLevel).
    public double MinSpeechDbfs { get; init; } = -60;

    public MaskOptions Mask { get; init; } = new();

    // Whisper started on a long silence hallucinates and may lose everything after it ("Продолжение следует..."
    // over a whole 18-minute track), so the main pass starts shortly before the first speech.
    public TimeSpan Lead { get; init; } = TimeSpan.FromSeconds(1);

    public RepairOptions Repair { get; init; } = new();

    public HallucinationOptions Hallucinations { get; init; } = new();

    public SegmentOptions Segments { get; init; } = new();

    // A prompt sets the style: without it a Russian call can go (and, carried by context, stay) lower case without
    // punctuation, with worse words. A short neutral phrase changed a well-recognised call the least of the
    // prompts measured. Only for Russian: a prompt's language leaks into the text, and there is no English call
    // to measure on.
    public IReadOnlyDictionary<string, string> Prompts { get; init; } =
        new Dictionary<string, string> { ["ru"] = "Да, хорошо. Так, давайте дальше." };
}

public sealed record VadOptions
{
    public float Threshold { get; init; } = 0.35f;

    public TimeSpan MinSilence { get; init; } = TimeSpan.FromSeconds(0.5);

    public TimeSpan MinSpeech { get; init; } = TimeSpan.FromSeconds(0.25);
}

public sealed record MaskOptions
{
    public TimeSpan Padding { get; init; } = TimeSpan.FromSeconds(0.25);

    public TimeSpan MergeGap { get; init; } = TimeSpan.FromSeconds(0.5);

    public TimeSpan MaxSnapDistance { get; init; } = TimeSpan.FromSeconds(1.5);
}

public sealed record RepairOptions
{
    public double MinWordsPerSecond { get; init; } = 0.25;

    public TimeSpan MaxWordLength { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan GroupGap { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan WindowPadding { get; init; } = TimeSpan.FromSeconds(1);
}

// Tracks of one person that are silent most of the time (Meetzone participants) are recognised by speech windows only.
public sealed record SegmentOptions
{
    // Speech regions closer than this are one window.
    public TimeSpan Gap { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan Padding { get; init; } = TimeSpan.FromSeconds(0.5);

    // Windows are joined, with silence between them, into one Whisper call of up to its 30-second chunk; zero — one
    // call per window. On a Meetzone call packs gave better text than separate windows (whose word timestamps
    // drifted enough to interleave two people talking over each other) and were faster.
    public TimeSpan PackLength { get; init; } = TimeSpan.FromSeconds(28);

    public TimeSpan Separator { get; init; } = TimeSpan.FromSeconds(1);
}

public sealed record HallucinationOptions
{
    // Lower case, letters only: words are compared without case and punctuation.
    public IReadOnlyList<IReadOnlyList<string>> Phrases { get; init; } = [["продолжение", "следует"]];

    // Live speech repeats single words ("Привет." x5, "Да," x7) and short phrases twice; a loop repeated a
    // six-word phrase 6-10 times in a row.
    public int LoopMinPhrase { get; init; } = 2;

    public int LoopMaxPhrase { get; init; } = 15;

    public int LoopMinRepeats { get; init; } = 4;
}
