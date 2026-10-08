using System.CommandLine;

namespace Meetrec.Cli;

internal static class CliCommands
{
    public static Command Record()
    {
        var command = new Command("record", "Записать микрофон и системный звук в папку сессии");
        AddRecordOptions(command);
        command.SetAction(_ => NotImplemented("record"));
        return command;
    }

    public static Command Run()
    {
        var command = new Command("run", "Записать, а после остановки расшифровать");
        AddRecordOptions(command);
        new TranscribeOptions().AddTo(command);
        command.SetAction(_ => NotImplemented("run"));
        return command;
    }

    public static Command Devices()
    {
        var command = new Command("devices", "Список микрофонов");
        command.SetAction(_ => NotImplemented("devices"));
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

    private static int NotImplemented(string name)
    {
        Console.Error.WriteLine($"Команда «{name}» пока не реализована.");
        return 1;
    }
}
