using System.Globalization;
using Meetrec.Asr;
using Meetrec.Core;
using Meetrec.Core.Models;

namespace Meetrec.Cli;

internal static class Diagnostics
{
    public static string Input(TranscribeInput input)
    {
        if (input.Session is not { } session)
        {
            return input.OutputName == TranscribeInput.SessionOutputName ? $"Вход: {input.OutputDirectory}" : $"Вход: {input.SystemPath}";
        }

        var mic = input.HasMicTrack ? $"микрофон «{session.MicDevice ?? "?"}»" : "без микрофона";
        return $"Вход: {input.OutputDirectory} — {mic}, системный звук «{session.SystemDevice ?? "?"}», сдвиг {session.OffsetMs} мс, записано meetrec {session.Version}";
    }

    public static string Settings(TranscribeSettings settings, DiarizationSettings diarization)
    {
        var threshold = diarization.Threshold.ToString("0.0#", CultureInfo.CurrentCulture);
        var speakers = diarization.NumClusters is { } clusters
            ? $"спикеров {settings.Speakers} (кластеров диаризации {clusters}, {diarization.Embedding.Id})"
            : $"спикеры автоматически ({diarization.Embedding.Id}, порог {threshold})";
        return $"Параметры: язык {settings.Language}, модель {settings.Model}, {speakers}, форматы {string.Join(", ", settings.Formats)}";
    }

    public static string Recognition(RecognitionStats stats, string language)
    {
        if (stats.SpeechRegions == 0)
        {
            return $"  речи нет (тихих регионов отброшено {stats.QuietRegions}), Whisper не запускался";
        }

        var parts = new List<string>();
        if (language == WhisperTranscriber.AutoLanguage)
        {
            parts.Add($"язык определён: {stats.Language ?? "?"}");
        }

        parts.Add($"речь {Formatting.Duration(stats.Speech)} в {Formatting.Count(stats.SpeechRegions, "регионе", "регионах", "регионах")}, тихих отброшено {stats.QuietRegions}");
        parts.Add($"слов от Whisper {stats.WhisperWords}, галлюцинаций отброшено {stats.Hallucinations}, вне речи отброшено {stats.DroppedWords}");
        if (stats.RepairWindows > 0)
        {
            parts.Add($"повторно распознано окон {stats.RepairWindows}");
        }

        return "  " + string.Join("; ", parts);
    }
}
