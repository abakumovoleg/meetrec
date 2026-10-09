using Meetrec.Core.Output;

namespace Meetrec.Eval;

// md and srt must match byte for byte. json in an older schema is compared by content: both files are read and
// written again in the current schema.
internal static class CompareCommand
{
    private static readonly string[] Extensions = [".md", ".json", ".srt"];

    public static int Execute(Args args)
    {
        var baseline = args.At(0, "baseline");
        var actual = args.At(1, "out");
        var differ = 0;
        foreach (var expected in Directory.EnumerateFiles(baseline, "*", SearchOption.AllDirectories)
                     .Where(f => Extensions.Contains(Path.GetExtension(f)))
                     .Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(baseline, expected);
            var other = Path.Combine(actual, relative);
            var result = !File.Exists(other) ? "нет файла"
                : File.ReadAllBytes(expected).AsSpan().SequenceEqual(File.ReadAllBytes(other)) ? "same"
                : Path.GetExtension(expected) == ".json" && Normalized(expected) == Normalized(other) ? "same по содержанию"
                : "DIFF";
            Console.WriteLine($"{result}  {relative}");
            differ += result.StartsWith("same", StringComparison.Ordinal) ? 0 : 1;
        }

        Console.WriteLine(differ == 0 ? "Совпало." : $"Расхождений: {differ}");
        return differ == 0 ? 0 : 1;
    }

    private static string Normalized(string path)
    {
        using var writer = new StringWriter();
        new JsonFormatter().Write(TranscriptReader.Read(path).Conversation, writer);
        return writer.ToString();
    }
}
