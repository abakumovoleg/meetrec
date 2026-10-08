using System.CommandLine;
using Meetrec.Core.Models;

namespace Meetrec.Cli;

internal static class ModelsCommand
{
    public static Command Create(Interrupts interrupts)
    {
        var command = new Command("models", "Скачанные модели и их пути");
        command.SetAction(_ => List());

        var id = new Argument<string[]>("id")
        {
            Description = "Идентификаторы моделей из списка meetrec models",
            Arity = ArgumentArity.OneOrMore,
        };
        var download = new Command("download", "Скачать модели заранее") { id };
        download.SetAction(parse => DownloadAsync(parse.GetRequiredValue(id), interrupts.Current));
        command.Subcommands.Add(download);

        return command;
    }

    private static int List()
    {
        var store = new ModelStore(ModelDirectory.Resolve());
        Console.WriteLine($"Папка моделей: {store.Root}");
        Console.WriteLine($"Переопределяется переменной {ModelDirectory.OverrideVariable}");
        Console.WriteLine();

        var idWidth = ModelCatalog.All.Max(m => m.Id.Length);
        foreach (var model in ModelCatalog.All)
        {
            var installed = store.IsInstalled(model);
            var status = installed ? store.PathOf(model) : "не скачана";
            Console.WriteLine($"{(installed ? "✓" : " ")} {model.Id.PadRight(idWidth)}  {Formatting.Megabytes(model.SizeBytes),10}  {status}");
        }

        return 0;
    }

    private static async Task<int> DownloadAsync(string[] ids, CancellationToken ct)
    {
        var unknown = ids.Where(id => !ModelCatalog.TryFind(id, out _)).ToList();
        if (unknown.Count > 0)
        {
            Console.Error.WriteLine($"Неизвестные модели: {string.Join(", ", unknown)}. Список: meetrec models");
            return 1;
        }

        var models = ids.Select(ModelCatalog.Find).ToList();

        using var http = ModelHttp.Create();
        var store = new ModelStore(ModelDirectory.Resolve());
        var progress = new ConsoleDownloadProgress();
        foreach (var model in models)
        {
            if (store.IsInstalled(model))
            {
                Console.WriteLine($"{model.Id}: уже скачана — {store.PathOf(model)}");
                continue;
            }

            var path = await store.EnsureAsync(model, http, progress, ct);
            Console.WriteLine($"{model.Id}: {path}");
        }

        return 0;
    }
}
