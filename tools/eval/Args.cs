using Meetrec.Core;

namespace Meetrec.Eval;

internal sealed record Args(string? Command, IReadOnlyList<string> Positional, IReadOnlyDictionary<string, string> Named)
{
    public static Args Parse(string[] args)
    {
        var positional = new List<string>();
        var named = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                if (i + 1 >= args.Length)
                {
                    throw new MeetrecException($"Нет значения для {args[i]}");
                }

                named[args[i][2..]] = args[++i];
            }
            else
            {
                positional.Add(args[i]);
            }
        }

        return new Args(positional.FirstOrDefault(), [.. positional.Skip(1)], named);
    }

    public string At(int index, string name) =>
        index < Positional.Count ? Positional[index] : throw new MeetrecException($"Не указан аргумент {name}");

    public string Get(string name, string fallback) => Named.GetValueOrDefault(name, fallback);
}
