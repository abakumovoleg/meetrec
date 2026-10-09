using System.CommandLine;
using Meetrec.Cli;
using Meetrec.Core;

Platform.ConfigureConsole();

using var interrupts = new Interrupts();

var root = new RootCommand("meetrec — запись созвонов и локальная расшифровка")
{
    RecordCommand.Record(interrupts),
    TranscribeCommand.Create(interrupts),
    NamesCommand.Create(interrupts),
    RecordCommand.Run(interrupts),
    RecordCommand.Devices(interrupts),
    ModelsCommand.Create(interrupts),
};

try
{
    // Ctrl+C is handled by Interrupts, so that `run` can stop recording without cancelling the transcription after it.
    return await root.Parse(args).InvokeAsync(
        new InvocationConfiguration { ProcessTerminationTimeout = null, EnableDefaultExceptionHandler = false });
}
catch (MeetrecException e)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Ошибка: {e.Message}");
    return 1;
}
catch (OperationCanceledException) when (interrupts.Interrupted)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Прервано.");
    return 130;
}
catch (Exception e)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Внутренняя ошибка meetrec: {e}");
    return 70;
}
