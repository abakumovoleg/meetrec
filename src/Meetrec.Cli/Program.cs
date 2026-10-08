using System.CommandLine;
using Meetrec.Cli;

var root = new RootCommand("meetrec — запись созвонов и локальная расшифровка")
{
    CliCommands.Record(),
    CliCommands.Transcribe(),
    CliCommands.Run(),
    CliCommands.Devices(),
    ModelsCommand.Create(),
};

return await root.Parse(args).InvokeAsync();
