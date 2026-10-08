using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using Meetrec.Core.Models;
using Meetrec.Core.Output;

namespace Meetrec.Cli;

internal sealed record TranscribeSettings(int? Speakers, string Language, string Model, IReadOnlyList<string> Formats, float? Threshold);

internal sealed class TranscribeOptions
{
    private static readonly string[] Languages = ["ru", "en", "auto"];

    private readonly Option<int?> _speakers = new("--speakers")
    {
        Description = "Общее число спикеров, включая «Я»",
        Validators = { r => ValidatePositive(r) },
    };

    private readonly Option<string> _language = new Option<string>("--lang")
    {
        Description = "Язык речи",
        DefaultValueFactory = _ => "auto",
    }.AcceptOnlyFromAmong(Languages);

    private readonly Option<string> _model = new Option<string>("--model")
    {
        Description = "Модель Whisper",
        DefaultValueFactory = _ => ModelCatalog.DefaultWhisper,
    }.AcceptOnlyFromAmong([.. ModelCatalog.WhisperNames]);

    private readonly Option<string[]> _formats = new("--format")
    {
        Description = "Форматы вывода через запятую: md,json,srt",
        DefaultValueFactory = _ => [.. TranscriptWriter.SupportedFormats],
        CustomParser = r => r.Tokens.SelectMany(t => t.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray(),
        Validators = { r => ValidateFormats(r) },
    };

    private readonly Option<float?> _threshold = new("--threshold")
    {
        Description = $"Порог кластеризации спикеров, если не задан --speakers (по умолчанию {DiarizationSettings.WespeakerThreshold.ToString(CultureInfo.InvariantCulture)}); больше — меньше спикеров",
    };

    public void AddTo(Command command)
    {
        command.Options.Add(_speakers);
        command.Options.Add(_language);
        command.Options.Add(_model);
        command.Options.Add(_formats);
        command.Options.Add(_threshold);
    }

    public TranscribeSettings Read(ParseResult parse) => new(
        parse.GetValue(_speakers),
        parse.GetRequiredValue(_language),
        parse.GetRequiredValue(_model),
        parse.GetRequiredValue(_formats),
        parse.GetValue(_threshold));

    private static void ValidatePositive(OptionResult result)
    {
        if (result.GetValueOrDefault<int?>() is < 1)
        {
            result.AddError("--speakers должно быть не меньше 1");
        }
    }

    private static void ValidateFormats(OptionResult result)
    {
        var unknown = result.GetValueOrDefault<string[]>().Except(TranscriptWriter.SupportedFormats, StringComparer.OrdinalIgnoreCase).ToArray();
        if (unknown.Length > 0)
        {
            result.AddError($"Неизвестный формат: {string.Join(", ", unknown)}. Допустимо: {string.Join(", ", TranscriptWriter.SupportedFormats)}");
        }
    }
}
