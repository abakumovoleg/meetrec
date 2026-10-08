using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using Meetrec.Asr;
using Meetrec.Core;
using Meetrec.Core.Audio;
using Meetrec.Core.Models;
using Meetrec.Core.Output;
using Meetrec.Core.Transcript;
using Meetrec.Diarization;

namespace Meetrec.Cli;

internal static class TranscribeCommand
{
    public static Command Create(Interrupts interrupts)
    {
        var input = new Argument<string>("input")
        {
            Description = "Папка сессии или одиночный WAV-файл",
        };
        var options = new TranscribeOptions();
        var command = new Command("transcribe", "Расшифровать готовую запись") { input };
        options.AddTo(command);
        command.SetAction(parse => ExecuteAsync(parse.GetRequiredValue(input), options.Read(parse), interrupts.Current));
        return command;
    }

    public static async Task<int> ExecuteAsync(string path, TranscribeSettings settings, CancellationToken ct)
    {
        try
        {
            var input = TranscribeInput.Resolve(path);
            await RunAsync(input, settings, ct);
            return 0;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Прервано.");
            return 130;
        }
        catch (Exception e) when (e is IOException or ArgumentException or HttpRequestException or JsonException or InvalidDataException or FormatException)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Ошибка: {e.Message}");
            return 1;
        }
    }

    private static async Task RunAsync(TranscribeInput input, TranscribeSettings settings, CancellationToken ct)
    {
        var total = Stopwatch.StartNew();
        if (input.HasMicTrack && input.Session is null)
        {
            Console.Error.WriteLine($"Внимание: нет {SessionFile.FileName}, дорожки склеиваются без сдвига.");
        }

        var diarization = DiarizationSettings.Choose(settings.Speakers, input.HasMicTrack, settings.Threshold);
        var store = new ModelStore(ModelDirectory.Resolve());
        await EnsureModelsAsync(store, TranscriptionModels.Required(settings.Model, OperatingSystem.IsMacOS(), diarization), ct);

        var mic = input.MicPath is null ? null : AudioLoader.LoadMono16k(input.MicPath);
        var system = AudioLoader.LoadMono16k(input.SystemPath);
        Console.Error.WriteLine(mic is null
            ? $"Запись: {Formatting.Duration(system.Duration)}, одна дорожка"
            : $"Запись: {Formatting.Duration(system.Duration)}, микрофон и системный звук");

        IReadOnlyList<Word> micWords = [];
        IReadOnlyList<Word> systemWords;
        // Both tracks share one Whisper instance, and it is released before diarization to keep peak memory down.
        using (var whisper = WhisperTranscriber.Load(store.PathOf(ModelCatalog.WhisperModel(settings.Model))))
        {
            var recognizer = new SpeechRecognizer(whisper, new SileroVad(store.PathOf(ModelCatalog.SileroVad)));
            if (mic is not null)
            {
                micWords = await RecognizeAsync(recognizer, mic, TranscribeInput.MicFileName, settings.Language, ct);
            }

            systemWords = await RecognizeAsync(recognizer, system, Path.GetFileName(input.SystemPath), settings.Language, ct);
            Console.Error.WriteLine($"Whisper: {settings.Model}, рантайм {WhisperTranscriber.LoadedRuntime ?? "?"}");
        }

        ct.ThrowIfCancellationRequested();
        var diarizationProgress = new ConsoleStageProgress("Диаризация");
        var segments = new SpeakerDiarizer(store.PathOf(ModelCatalog.PyannoteSegmentation), store.PathOf(diarization.Embedding))
            .Diarize(system, diarization.NumClusters, diarization.Threshold, diarizationProgress);
        diarizationProgress.Complete();

        var utterances = TranscriptMerger.Merge(micWords, systemWords, segments, input.OffsetMs);
        var paths = TranscriptWriter.WriteAll(input.OutputDirectory, utterances, settings.Formats, input.OutputName);

        var speakers = utterances.Select(u => u.Speaker).Distinct().Count();
        Console.Error.WriteLine($"Готово за {Formatting.Duration(total.Elapsed)}: {utterances.Count} реплик, спикеров {speakers}");
        foreach (var path in paths)
        {
            Console.WriteLine(path);
        }
    }

    private static async Task EnsureModelsAsync(ModelStore store, IReadOnlyList<ModelInfo> models, CancellationToken ct)
    {
        var missing = models.Where(m => !store.IsInstalled(m)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        using var http = ModelHttp.Create();
        var progress = new ConsoleDownloadProgress();
        foreach (var model in missing)
        {
            await store.EnsureAsync(model, http, progress, ct);
        }
    }

    private static async Task<IReadOnlyList<Word>> RecognizeAsync(
        SpeechRecognizer recognizer, AudioClip clip, string name, string language, CancellationToken ct)
    {
        var progress = new ConsoleStageProgress($"Распознавание {name}");
        var words = await recognizer.RecognizeAsync(clip, language, progress, ct);
        progress.Complete();
        return words;
    }
}
