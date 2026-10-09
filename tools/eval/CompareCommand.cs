namespace Meetrec.Eval;

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
            var same = File.Exists(other) && File.ReadAllBytes(expected).AsSpan().SequenceEqual(File.ReadAllBytes(other));
            Console.WriteLine($"{(same ? "same" : "DIFF")}  {relative}");
            differ += same ? 0 : 1;
        }

        Console.WriteLine(differ == 0 ? "Совпало." : $"Расхождений: {differ}");
        return differ == 0 ? 0 : 1;
    }
}
