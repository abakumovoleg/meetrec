using Meetrec.Core.Transcript;

namespace Meetrec.Cli;

internal static class NamesDialog
{
    private const int SampleLength = 110;

    // Returns the renamed transcript, or null if nothing changed or the dialog was interrupted.
    public static IReadOnlyList<Utterance>? Run(IReadOnlyList<Utterance> utterances, string? audioPath, CancellationToken ct)
    {
        var current = SpeakerNames.Current(utterances);
        if (current.Count == 0)
        {
            return null;
        }

        var byLabel = SpeakerNames.Reset(utterances);
        var hints = NameHints.Find(byLabel);
        var player = ClipPlayer.IsSupported && File.Exists(audioPath) ? new ClipPlayer(audioPath) : null;
        var names = current.ToDictionary(s => s.Label, s => s.Name);

        var err = Console.Error;
        err.WriteLine();
        err.WriteLine($"Кто есть кто — {Formatting.Count(current.Count, "спикер", "спикера", "спикеров")}. "
            + $"Enter — принять предложенное, «-» — оставить номер{(player is null ? "" : ", p — послушать")}, q — закончить.");

        foreach (var profile in SpeakerProfiles.Build(byLabel))
        {
            var name = Ask(profile, names[profile.Label], hints.GetValueOrDefault(profile.Label), player, ct);
            if (ct.IsCancellationRequested)
            {
                err.WriteLine("Имена не сохранены.");
                return null;
            }

            if (name is null)
            {
                break;
            }

            names[profile.Label] = name;
        }

        if (current.All(s => names[s.Label] == s.Name))
        {
            err.WriteLine("Имена не изменились.");
            return null;
        }

        err.WriteLine();
        foreach (var group in current.GroupBy(s => names[s.Label]).Where(g => g.Count() > 1 || g.Key != g.First().Label))
        {
            err.WriteLine($"{string.Join(" + ", group.Select(s => s.Label))} → {group.Key}");
        }

        return SpeakerNames.Apply(utterances, names);
    }

    private static string? Ask(SpeakerProfile profile, string currentName, NameHint? hint, ClipPlayer? player, CancellationToken ct)
    {
        var err = Console.Error;
        err.WriteLine();
        err.WriteLine($"{profile.Label}{(currentName == profile.Label ? "" : $" ({currentName})")} — "
            + $"{profile.TalkShare * 100:0} % речи, {Formatting.Count(profile.UtteranceCount, "реплика", "реплики", "реплик")}");
        foreach (var sample in profile.Samples)
        {
            err.WriteLine($"  [{Formatting.Clock(sample.Start)}] {Shorten(sample.Text)}");
        }

        if (hint is not null)
        {
            err.WriteLine($"  Подсказка: {hint.Name} — так обратились перед репликой этого спикера "
                + $"({Formatting.Count(hint.Mentions, "раз", "раза", "раз")}), [{Formatting.Clock(hint.At)}] «{hint.Quote}»");
        }

        var suggested = currentName != profile.Label ? currentName : hint?.Name;
        var played = 0;
        while (true)
        {
            err.Write(suggested is null ? "Имя: " : $"Имя [{suggested}]: ");
            var line = Console.ReadLine()?.Trim();
            switch (line?.ToLowerInvariant())
            {
                case null or "q" or "й":
                    return null;
                case "":
                    return suggested ?? profile.Label;
                case "-":
                    return profile.Label;
                case "p" or "з" when player is not null:
                    var utterance = profile.LongestUtterances[played++ % profile.LongestUtterances.Count];
                    err.WriteLine($"  ▶ [{Formatting.Clock(utterance.Start)}] {Shorten(utterance.Text)}");
                    if (player.Play(utterance.Start, utterance.End, ct) is { } error)
                    {
                        err.WriteLine($"  Не удалось проиграть: {error}");
                    }

                    break;
                default:
                    return line;
            }
        }
    }

    private static string Shorten(string text) => text.Length <= SampleLength ? text : text[..SampleLength].TrimEnd() + "…";
}
