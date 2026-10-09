using Meetrec.Core.Output;
using Meetrec.Core.Transcript;

namespace Meetrec.Cli;

internal static class NamesDialog
{
    private const int SampleLength = 110;

    // Returns the renamed conversation, or null if nothing changed or the dialog was interrupted.
    public static Conversation? Run(Conversation conversation, ClipPlayer? player, CancellationToken ct)
    {
        var current = SpeakerNames.Nameable(conversation);
        if (current.Count == 0)
        {
            return null;
        }

        var byDefault = SpeakerNames.Reset(conversation);
        var hints = NameHints.Find(byDefault);
        var names = current.ToDictionary(s => s.Id, s => s.Name);

        var err = Console.Error;
        err.WriteLine();
        err.WriteLine($"Кто есть кто — {Formatting.Count(current.Count, "спикер", "спикера", "спикеров")}. "
            + $"Enter — принять предложенное, «-» — оставить номер{(player is null ? "" : ", p — послушать (Esc — остановить)")}, q — закончить.");

        foreach (var profile in SpeakerProfiles.Build(byDefault))
        {
            var name = Ask(profile, names[profile.Id], hints.GetValueOrDefault(profile.Id), player, ct);
            if (ct.IsCancellationRequested)
            {
                err.WriteLine("Имена не сохранены.");
                return null;
            }

            if (name is null)
            {
                break;
            }

            names[profile.Id] = name;
        }

        if (current.All(s => names[s.Id] == s.Name))
        {
            err.WriteLine("Имена не изменились.");
            return null;
        }

        err.WriteLine();
        foreach (var group in current.GroupBy(s => names[s.Id]).Where(g => g.Count() > 1 || g.Key != Speakers.DefaultName(g.First().Id)))
        {
            err.WriteLine($"{string.Join(" + ", group.Select(s => Speakers.DefaultName(s.Id)))} → {group.Key}");
        }

        return SpeakerNames.Apply(conversation, names);
    }

    private static string? Ask(SpeakerProfile profile, string currentName, NameHint? hint, ClipPlayer? player, CancellationToken ct)
    {
        var err = Console.Error;
        err.WriteLine();
        err.WriteLine($"{profile.Label}{(currentName == profile.Label ? "" : $" ({currentName})")} — "
            + $"{profile.TalkShare * 100:0} % речи, {Formatting.Count(profile.UtteranceCount, "реплика", "реплики", "реплик")}");
        foreach (var sample in profile.Samples)
        {
            err.WriteLine($"  [{TimeFormat.Clock(sample.Start)}] {Shorten(sample.Text)}");
        }

        if (hint is not null)
        {
            err.WriteLine($"  Подсказка: {hint.Name} — так обратились перед репликой этого спикера "
                + $"({Formatting.Count(hint.Mentions, "раз", "раза", "раз")}), [{TimeFormat.Clock(hint.At)}] «{hint.Quote}»");
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
                    err.WriteLine($"  ▶ [{TimeFormat.Clock(utterance.Start)}] {Shorten(utterance.Text)}");
                    using (var escape = EscapeKey.Watch(ct))
                    {
                        if (player.Play(utterance.Track, utterance.Start, utterance.End, escape.Token) is { } error)
                        {
                            err.WriteLine($"  Не удалось проиграть: {error}");
                        }
                    }

                    break;
                default:
                    return line;
            }
        }
    }

    private static string Shorten(string text) => text.Length <= SampleLength ? text : text[..SampleLength].TrimEnd() + "…";
}
