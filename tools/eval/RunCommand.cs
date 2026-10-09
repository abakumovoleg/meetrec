using System.Text.Json;
using Meetrec.Asr;
using Meetrec.Core;
using Meetrec.Core.Models;
using Meetrec.Core.Output;
using Meetrec.Pipeline;

namespace Meetrec.Eval;

internal static class RunCommand
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<int> ExecuteAsync(Args args)
    {
        var casesPath = Path.GetFullPath(args.At(0, "cases.json"));
        var output = Path.GetFullPath(args.At(1, "out"));
        var recognition = OptionsFile.Read(args);
        var cases = JsonSerializer.Deserialize<EvalCase[]>(File.ReadAllText(casesPath), Json)
            ?? throw new MeetrecException($"Пустой {casesPath}");

        foreach (var evalCase in cases)
        {
            var input = TranscribeInput.Resolve(Path.Combine(Path.GetDirectoryName(casesPath)!, evalCase.Input));
            var diarization = DiarizationSettings.Choose(evalCase.Speakers, input.HasMicTrack, evalCase.Threshold);
            var options = new TranscriptionOptions(evalCase.Language, evalCase.Model, recognition, diarization);
            var store = await Models.EnsureAsync(TranscriptionModels.Required(evalCase.Model, OperatingSystem.IsMacOS(), diarization));

            Console.WriteLine($"== {evalCase.Name}");
            var result = await new TranscriptionPipeline(TranscriptionEngines.FromModels(store, options))
                .RunAsync(input, options, new EvalObserver(), CancellationToken.None);

            var directory = Directory.CreateDirectory(Path.Combine(output, evalCase.Name)).FullName;
            TranscriptWriter.WriteAll(directory, result.Utterances, TranscriptWriter.SupportedFormats, input.OutputName);
            File.WriteAllLines(Path.Combine(directory, "stats.txt"), Stats(result));
            Console.WriteLine($"   {result.Utterances.Count} реплик → {directory}");
        }

        return 0;
    }

    private static IEnumerable<string> Stats(TranscriptionResult result) =>
    [
        .. result.Tracks.Select(t => $"{t.Track}: {t.Stats}"),
        .. result.Stages.Select(s => $"{s.Stage.Kind} {s.Stage.Track}: {s.Elapsed.TotalSeconds:0.0} s"),
        $"utterances: {result.Utterances.Count}",
    ];

    private sealed record EvalCase(string Name, string Input, int? Speakers, float? Threshold, string Language = "ru", string Model = ModelCatalog.DefaultWhisper);

    private sealed class EvalObserver : ITranscriptionObserver
    {
        public void Warning(string message) => Console.WriteLine($"   внимание: {message}");

        public void AudioLoaded(TimeSpan duration, int tracks)
        {
        }

        public void TranscriberLoaded()
        {
        }

        public void StageStarted(PipelineStage stage)
        {
        }

        public void StageProgress(PipelineStage stage, int percent)
        {
        }

        public void StageCompleted(PipelineStage stage, TimeSpan elapsed) =>
            Console.WriteLine($"   {stage.Kind} {stage.Track}: {elapsed.TotalSeconds:0.0} с");

        public void TrackRecognized(string track, RecognitionStats stats) => Console.WriteLine($"   {stats}");
    }
}
