using System.CommandLine;
using System.Text;
using Meetrec.Cli;

// Every message here is in Russian, and a Windows console left on its OEM code page prints them as question
// marks — including output redirected to a file.
if (OperatingSystem.IsWindows())
{
    try
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Input has the same problem in reverse: typed names arrive through the OEM code page, and Cyrillic that
        // it lacks turns into question marks. UTF-16 makes .NET read the console with ReadConsoleW; UTF-8 would
        // not do, since conhost returns zeros for non-ASCII there. Piped input is bytes and is left alone.
        if (!Console.IsInputRedirected)
        {
            Console.InputEncoding = Encoding.Unicode;
        }
    }
    catch (IOException)
    {
    }
}

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

// Ctrl+C is handled by Interrupts, so that `run` can stop recording without cancelling the transcription after it.
return await root.Parse(args).InvokeAsync(new InvocationConfiguration { ProcessTerminationTimeout = null });
