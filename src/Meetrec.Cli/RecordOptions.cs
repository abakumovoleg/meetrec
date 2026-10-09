using System.CommandLine;

namespace Meetrec.Cli;

internal sealed record RecordSettings(string? Output, string? Mic, bool NoMic, bool Meetzone);

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

    private readonly Option<bool> _meetzone = new("--meetzone")
    {
        Description = "Созвон в Meetzone: дорожка на участника из браузера через расширение meetrec, вместо микрофона и системного звука",
    };

    public void AddTo(Command command)
    {
        command.Options.Add(_output);
        command.Options.Add(_mic);
        command.Options.Add(_noMic);
        command.Options.Add(_meetzone);
        command.Validators.Add(r =>
        {
            if (r.GetValue(_noMic) && r.GetValue(_mic) is not null)
            {
                r.AddError("--mic и --no-mic несовместимы");
            }

            if (r.GetValue(_meetzone) && (r.GetValue(_noMic) || r.GetValue(_mic) is not null))
            {
                r.AddError("--meetzone записывает звук из браузера, --mic и --no-mic к нему не относятся");
            }
        });
    }

    public RecordSettings Read(ParseResult parse) =>
        new(parse.GetValue(_output)?.FullName, parse.GetValue(_mic), parse.GetValue(_noMic), parse.GetValue(_meetzone));
}
