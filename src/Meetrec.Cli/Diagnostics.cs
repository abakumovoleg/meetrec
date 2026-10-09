using System.Globalization;
using Meetrec.Asr;
using Meetrec.Core;
using Meetrec.Core.Models;
using Meetrec.Core.Transcript;

namespace Meetrec.Cli;

internal static class Diagnostics
{
    public static string Input(TranscribeInput input)
    {
        if (input.Session is not { } session)
        {
            return input.OutputName == SessionLayout.TranscriptName ? $"Вход: {input.OutputDirectory}" : $"Вход: {input.Tracks[0].Path}";
        }

        var mic = session.Tracks.FirstOrDefault(t => t.Source == TrackSource.Mic);
        var system = session.Tracks.FirstOrDefault(t => t.Source == TrackSource.System);
        var parts = new List<string>();
        if (mic is not null || system is not null)
        {
            parts.Add(mic is null ? "без микрофона" : $"микрофон «{mic.Device ?? "?"}»");
            if (system is not null)
            {
                parts.Add($"системный звук «{system.Device ?? "?"}»");
            }

            parts.Add($"сдвиг {mic?.OffsetMs ?? 0} мс");
        }

        parts.AddRange(session.Tracks.Where(t => t.Source == TrackSource.Participant).Select(t => $"{Name(t)} со сдвигом {t.OffsetMs} мс"));
        return $"Вход: {input.OutputDirectory} — {string.Join(", ", parts)}, записано meetrec {session.Version}";
    }

    // "одна дорожка" for a lone file; otherwise what the tracks are: "микрофон и системный звук".
    public static string Tracks(TranscribeInput input)
    {
        if (input.Tracks.Count == 1)
        {
            return "одна дорожка";
        }

        var sources = input.Session?.Tracks.Select(Describe).ToList() ?? [.. input.Tracks.Select(t => t.Speakers is FixedSpeaker ? "микрофон" : "системный звук")];
        return string.Join(", ", sources[..^1]) + " и " + sources[^1];
    }

    public static string Settings(TranscribeSettings settings, TranscribeInput input, DiarizationSettings diarization)
    {
        var threshold = diarization.Threshold.ToString("0.0#", CultureInfo.CurrentCulture);
        var speakers = !input.Tracks.Any(t => t.Speakers is DiarizedSpeakers) ? "без диаризации: у каждой дорожки свой спикер"
            : diarization.NumClusters is { } clusters ? $"спикеров {settings.Speakers} (кластеров диаризации {clusters}, {diarization.Embedding.Id})"
            : $"спикеры автоматически ({diarization.Embedding.Id}, порог {threshold})";
        return $"Параметры: язык {settings.Language}, модель {settings.Model}, {speakers}, форматы {string.Join(", ", settings.Formats)}";
    }

    public static string Summary(Conversation conversation, TimeSpan elapsed)
    {
        var speakers = conversation.Utterances
            .Where(u => u.Speaker != SpeakerId.Unknown)
            .Select(u => conversation.NameOf(u.Speaker))
            .Distinct()
            .Count();
        return $"Готово за {Formatting.Duration(elapsed)}: {conversation.Utterances.Count} реплик, спикеров {speakers}";
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
        if (stats.SpeechWindows > 0)
        {
            parts.Add($"Whisper только по {Formatting.Count(stats.SpeechWindows, "окну", "окнам", "окнам")} речи");
        }

        if (stats.RepairWindows > 0)
        {
            parts.Add($"повторно распознано окон {stats.RepairWindows}");
        }

        return "  " + string.Join("; ", parts);
    }

    private static string Describe(SessionTrack track) => track.Source switch
    {
        TrackSource.Mic => "микрофон",
        TrackSource.System => "системный звук",
        _ => Name(track),
    };

    private static string Name(SessionTrack track) => $"«{track.Speaker?.Name ?? track.File}»";
}
