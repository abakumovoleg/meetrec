using System.CommandLine;

namespace Meetrec.Cli;

internal sealed record RecordSettings(string? Output, string? Mic, bool NoMic);

internal sealed class RecordOptions
{
    private readonly Option<DirectoryInfo> _output = new("--output", "-o")
    {
        Description = "Папка сессии (по умолчанию ./meetings/yyyy-MM-dd_HH-mm-ss)",
    };

    private readonly Option<string> _mic = new("--mic")
    {
        Description = "Микрофон: имя или uid (см. meetrec devices)",
    };

    private readonly Option<bool> _noMic = new("--no-mic")
    {
        Description = "Не записывать микрофон",
    };

    public void AddTo(Command command)
    {
        command.Options.Add(_output);
        command.Options.Add(_mic);
        command.Options.Add(_noMic);
        command.Validators.Add(r =>
        {
            if (r.GetValue(_noMic) && r.GetValue(_mic) is not null)
            {
                r.AddError("--mic и --no-mic несовместимы");
            }
        });
    }

    public RecordSettings Read(ParseResult parse) =>
        new(parse.GetValue(_output)?.FullName, parse.GetValue(_mic), parse.GetValue(_noMic));
}
