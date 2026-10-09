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
        var json = TranscriptWriter.PathOf(input.OutputDirectory, input.OutputName, JsonFormat);
        if (!File.Exists(json))
        {
            throw new MeetrecException($"Нет {Path.GetFileName(json)} — сначала meetrec transcribe {path}");
        }

        if (!IsInteractive)
        {
            throw new MeetrecException("Имена спрашиваются только в интерактивном терминале.");
        }

        var utterances = TranscriptReader.Read(json);
        if (SpeakerNames.Current(utterances).Count == 0)
        {
            Console.Error.WriteLine("В расшифровке нет спикеров системной дорожки.");
            return 0;
        }

        if (NamesDialog.Run(utterances, input.SystemPath, ct) is not { } named)
        {
            return ct.IsCancellationRequested ? 130 : 0;
        }

        var formats = TranscriptWriter.SupportedFormats
            .Where(f => File.Exists(TranscriptWriter.PathOf(input.OutputDirectory, input.OutputName, f)));
        foreach (var written in TranscriptWriter.WriteAll(input.OutputDirectory, named, formats, input.OutputName))
        {
            Console.WriteLine(written);
        }

        return 0;
    }
}
