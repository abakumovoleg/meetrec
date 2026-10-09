using System.CommandLine;
using Meetrec.Core;
using Meetrec.Core.Output;
using Meetrec.Core.Transcript;

namespace Meetrec.Cli;

internal static class NamesCommand
{
    private const string JsonFormat = "json";

    public static bool IsInteractive => !Console.IsInputRedirected && !Console.IsErrorRedirected;

    public static Command Create(Interrupts interrupts)
    {
        var input = new Argument<string>("input")
        {
            Description = "Папка сессии или WAV-файл, для которых уже есть расшифровка",
        };
        var command = new Command("names", "Назвать спикеров в готовой расшифровке") { input };
        command.SetAction(parse => Execute(parse.GetRequiredValue(input), interrupts.Current));
        return command;
    }

    private static int Execute(string path, CancellationToken ct)
    {
        var input = TranscribeInput.Resolve(path);
        var json = SessionLayout.TranscriptPath(input.OutputDirectory, input.OutputName, JsonFormat);
        if (!File.Exists(json))
        {
            throw new MeetrecException($"Нет {Path.GetFileName(json)} — сначала meetrec transcribe {path}");
        }

        if (!IsInteractive)
        {
            throw new MeetrecException("Имена спрашиваются только в интерактивном терминале.");
        }

        var stored = TranscriptReader.Read(json);
        Conversation? named = null;
        if (SpeakerNames.Nameable(stored.Conversation).Count == 0)
        {
            Console.Error.WriteLine("В расшифровке нет спикеров, различённых диаризацией.");
        }
        else
        {
            named = NamesDialog.Run(stored.Conversation, ClipPlayer.ForFiles(input.Tracks), ct);
        }

        if (ct.IsCancellationRequested)
        {
            return 130;
        }

        if (named is null && !stored.IsOutdated)
        {
            return 0;
        }

        // A transcript in an older schema is rewritten in the current one even when no name changed.
        IEnumerable<string> formats = named is null
            ? [JsonFormat]
            : TranscriptWriter.SupportedFormats.Where(f => File.Exists(SessionLayout.TranscriptPath(input.OutputDirectory, input.OutputName, f)));
        foreach (var written in TranscriptWriter.WriteAll(input.OutputDirectory, named ?? stored.Conversation, formats, input.OutputName))
        {
            Console.WriteLine(written);
        }

        return 0;
    }
}
