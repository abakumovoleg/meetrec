using System.CommandLine;
using Meetrec.Core.Output;

namespace Meetrec.Cli;

internal static class CliCommands
{
    private static readonly string[] ModelNames =
        ["tiny", "base", "small", "medium", "large-v3", "large-v3-turbo"];

    private static readonly string[] Languages = ["ru", "en", "auto"];

    public static Command Record()
    {
        var command = new Command("record", "Записать микрофон и системный звук в папку сессии");
        AddRecordOptions(command);
        command.SetAction(_ => NotImplemented("record"));
        return command;
    }

    public static Command Transcribe()
    {
        var input = new Argument<string>("input")
        {
            Description = "Папка сессии или одиночный WAV-файл",
        };
        var command = new Command("transcribe", "Расшифровать готовую запись") { input };
        AddTranscribeOptions(command);
        command.SetAction(_ => NotImplemented("transcribe"));
        return command;
    }

    public static Command Run()
    {
        var command = new Command("run", "Записать, а после остановки расшифровать");
        AddRecordOptions(command);
        AddTranscribeOptions(command);
        command.SetAction(_ => NotImplemented("run"));
        return command;
    }

    public static Command Devices()
    {
        var command = new Command("devices", "Список микрофонов");
        command.SetAction(_ => NotImplemented("devices"));
        return command;
    }

    public static Command Models()
    {
        var command = new Command("models", "Скачанные модели и их пути");
        command.SetAction(_ => NotImplemented("models"));
        return command;
    }

    private static void AddRecordOptions(Command command)
    {
        command.Options.Add(new Option<DirectoryInfo>("--output", "-o")
        {
            Description = "Папка сессии (по умолчанию ./meetings/yyyy-MM-dd_HH-mm-ss)",
        });
        command.Options.Add(new Option<string>("--mic")
        {
            Description = "Микрофон (см. meetrec devices)",
        });
        command.Options.Add(new Option<bool>("--no-mic")
        {
            Description = "Не записывать микрофон",
        });
    }

    private static void AddTranscribeOptions(Command command)
    {
        command.Options.Add(new Option<int?>("--speakers")
        {
            Description = "Общее число спикеров, включая «Я»",
        });
        command.Options.Add(new Option<string>("--lang")
        {
            Description = "Язык речи",
            DefaultValueFactory = _ => "auto",
        }.AcceptOnlyFromAmong(Languages));
        command.Options.Add(new Option<string>("--model")
        {
            Description = "Модель Whisper",
            DefaultValueFactory = _ => "large-v3-turbo",
        }.AcceptOnlyFromAmong(ModelNames));
        command.Options.Add(new Option<string[]>("--format")
        {
            Description = "Форматы вывода через запятую: md,json,srt",
            DefaultValueFactory = _ => [.. TranscriptWriter.SupportedFormats],
            CustomParser = r => r.Tokens.SelectMany(t => t.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray(),
            Validators = { r => ValidateFormats(r) },
        });
        command.Options.Add(new Option<double>("--threshold")
        {
            Description = "Порог кластеризации спикеров, если не задан --speakers",
            DefaultValueFactory = _ => 0.5,
        });
    }

    private static void ValidateFormats(System.CommandLine.Parsing.OptionResult result)
    {
        var unknown = result.GetValueOrDefault<string[]>().Except(TranscriptWriter.SupportedFormats, StringComparer.OrdinalIgnoreCase).ToArray();
        if (unknown.Length > 0)
        {
            result.AddError($"Неизвестный формат: {string.Join(", ", unknown)}. Допустимо: {string.Join(", ", TranscriptWriter.SupportedFormats)}");
        }
    }

    private static int NotImplemented(string name)
    {
        Console.Error.WriteLine($"Команда «{name}» пока не реализована.");
        return 1;
    }
}
