using System.CommandLine;
using Meetrec.Cli;

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
