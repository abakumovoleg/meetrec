using System.CommandLine;
using System.Diagnostics;
using Meetrec.Core;
using Meetrec.Core.Models;
using Meetrec.Core.Output;
using Meetrec.Pipeline;

namespace Meetrec.Cli;

internal static class TranscribeCommand
{
    public static Command Create(Interrupts interrupts)
    {
        var input = new Argument<string>("input")
        {
            Description = "Папка сессии или одиночный WAV-файл",
        };
        var options = new TranscribeOptions();
        var command = new Command("transcribe", "Расшифровать готовую запись") { input };
        options.AddTo(command);
        command.SetAction(parse => ExecuteAsync(parse.GetRequiredValue(input), options.Read(parse), interrupts.Current));
        return command;
    }

    public static async Task<int> ExecuteAsync(string path, TranscribeSettings settings, CancellationToken ct)
    {
        var total = Stopwatch.StartNew();
        var input = TranscribeInput.Resolve(path);
        var options = settings.ToOptions(input.FixedSpeakers);
        var store = new ModelStore(ModelDirectory.Resolve());
        Console.Error.WriteLine(Diagnostics.Input(input));
        Console.Error.WriteLine(Diagnostics.Settings(settings, input, options.Diarization));
        Console.Error.WriteLine($"Модели: {store.Root}");
        using (var http = ModelHttp.Create())
        {
            var models = TranscriptionModels.Required(settings.Model, OperatingSystem.IsMacOS(), options.Diarization);
            await store.EnsureAllAsync(models, http, new ConsoleDownloadProgress(), ct);
        }

        var engines = TranscriptionEngines.FromModels(store, options);
        engines = engines with { Diarizer = new BackgroundPriorityDiarizer(engines.Diarizer) };
        var result = await new TranscriptionPipeline(engines).RunAsync(input, options, new ConsoleTranscriptionObserver(settings, input), ct);

        var paths = TranscriptWriter.WriteAll(input.OutputDirectory, result.Conversation, settings.Formats, input.OutputName);
        Console.Error.WriteLine(Diagnostics.Summary(result.Conversation, total.Elapsed));
        if (settings.AskNames && NamesCommand.IsInteractive
            && NamesDialog.Run(result.Conversation, ClipPlayer.ForClips(result.DiarizedAudio), ct) is { } named)
        {
            paths = TranscriptWriter.WriteAll(input.OutputDirectory, named, settings.Formats, input.OutputName);
        }

        foreach (var written in paths)
        {
            Console.WriteLine(written);
        }

        return 0;
    }
}
