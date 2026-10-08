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
