using System.CommandLine;
using Meetrec.Cli;

var root = new RootCommand("meetrec — запись созвонов и локальная расшифровка")
{
    CliCommands.Record(),
    TranscribeCommand.Create(),
    CliCommands.Run(),
    CliCommands.Devices(),
    ModelsCommand.Create(),
};

return await root.Parse(args).InvokeAsync();
