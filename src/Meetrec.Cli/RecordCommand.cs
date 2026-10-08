using System.CommandLine;
using Meetrec.Capture.Mac;
using Meetrec.Core;
using Meetrec.Core.Capture;

namespace Meetrec.Cli;

internal static class RecordCommand
{
    private const string SystemDeviceName = "Системный звук (Core Audio Process Tap)";
    private static readonly TimeSpan PermissionHintDelay = TimeSpan.FromSeconds(5);

    public static Command Record(Interrupts interrupts)
    {
        var options = new RecordOptions();
        var command = new Command("record", "Записать микрофон и системный звук в папку сессии");
        options.AddTo(command);
        command.SetAction(async parse => await ExecuteAsync(options.Read(parse), interrupts) is null ? 1 : 0);
        return command;
    }

    public static Command Run(Interrupts interrupts)
    {
        var recordOptions = new RecordOptions();
        var transcribeOptions = new TranscribeOptions();
        var command = new Command("run", "Записать, а после остановки расшифровать");
        recordOptions.AddTo(command);
        transcribeOptions.AddTo(command);
        command.SetAction(async parse =>
        {
            var directory = await ExecuteAsync(recordOptions.Read(parse), interrupts);
            if (directory is null)
            {
                return 1;
            }

            return await TranscribeCommand.ExecuteAsync(directory, transcribeOptions.Read(parse), interrupts.NextPhase());
        });
        return command;
    }

    public static Command Devices(Interrupts interrupts)
    {
        var command = new Command("devices", "Список микрофонов");
        command.SetAction(async _ =>
        {
            try
            {
                await using var backend = CreateBackend();
                foreach (var device in await backend.ListMicrophonesAsync(interrupts.Current))
                {
                    Console.WriteLine($"{(device.IsDefault ? "*" : " ")} {device.Name}  ({device.Uid})");
                }

                var permissions = await backend.CheckPermissionsAsync(interrupts.Current);
                Console.WriteLine();
                Console.WriteLine($"Разрешение на микрофон: {Describe(permissions.Microphone)}");
                Console.WriteLine($"Разрешение на системный звук: {Describe(permissions.SystemAudio)}");

                return 0;
            }
            catch (CaptureException e)
            {
                Console.Error.WriteLine($"Ошибка: {e.Message}");
                return 1;
            }
        });
        return command;
    }

    private static async Task<string?> ExecuteAsync(RecordSettings settings, Interrupts interrupts)
    {
        var directory = settings.Output ?? SessionDirectory.Default(DateTime.Now, Environment.CurrentDirectory);
        try
        {
            if (Directory.Exists(directory) && SessionDirectory.ContainsRecording(directory))
            {
                throw new CaptureException($"В папке уже есть запись: {directory}");
            }

            await using var backend = CreateBackend();
            var stop = interrupts.Current;
            ReportPermissions(await backend.CheckPermissionsAsync(stop), settings.NoMic);
            var mic = settings.NoMic ? null : await ResolveMicrophoneAsync(backend, settings.Mic, stop);

            Directory.CreateDirectory(directory);
            var status = new RecordingStatus();
            backend.Levels += status.OnLevels;

            var startedAt = DateTimeOffset.UtcNow;
            var start = backend.StartAsync(directory, new CaptureOptions(mic?.Uid, settings.NoMic), stop);
            if (await Task.WhenAny(start, Task.Delay(PermissionHintDelay, CancellationToken.None)) != start)
            {
                status.WriteLine("Запись ещё не началась. Если macOS спрашивает разрешение на микрофон или запись звука — ответьте на запрос; иначе выбранный микрофон не отдаёт звук (см. meetrec devices).");
            }

            var started = await start;
            status.MarkStarted();
            SessionFile.Write(
                Path.Combine(directory, SessionFile.FileName),
                new Session(startedAt, mic?.Name, SystemDeviceName, started.OffsetMs, AppVersion.Current));
            status.WriteLine($"Запись в {directory}{(mic is null ? "" : $", микрофон «{mic.Name}»")}. Ctrl+C — остановить.");

            await Task.WhenAny(backend.Exited, Task.Delay(Timeout.Infinite, stop));
            await backend.StopAsync();
            status.WriteLine($"Записано {Formatting.Duration(status.Elapsed)}: {directory}");
            return directory;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Запись отменена до начала.");
            DeleteIfEmpty(directory);
            return null;
        }
        catch (Exception e) when (e is CaptureException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Ошибка: {e.Message}");
            DeleteIfEmpty(directory);
            return null;
        }
    }

    private const string SettingsHint =
        "Выдайте его приложению терминала: Системные настройки → Конфиденциальность и безопасность → {0}. После этого перезапустите терминал.";

    private static void ReportPermissions(CapturePermissions permissions, bool noMic)
    {
        if (permissions.SystemAudio == PermissionStatus.Denied)
        {
            throw new CaptureException(
                "Нет разрешения на запись системного звука — собеседники не запишутся. " +
                string.Format(SettingsHint, "Запись звука и экрана → «Только системный звук»"));
        }

        if (!noMic && permissions.Microphone == PermissionStatus.Denied)
        {
            throw new CaptureException(
                "Нет разрешения на микрофон. " + string.Format(SettingsHint, "Микрофон") + " Или запишите без него: --no-mic.");
        }

        if (permissions.SystemAudio == PermissionStatus.Unknown || (!noMic && permissions.Microphone == PermissionStatus.Unknown))
        {
            Console.Error.WriteLine("macOS спросит разрешение на запись звука — ответьте «Разрешить», запись начнётся после этого.");
        }
    }

    private static string Describe(PermissionStatus status) => status switch
    {
        PermissionStatus.Granted => "есть",
        PermissionStatus.Denied => "запрещено",
        _ => "ещё не запрашивалось (macOS спросит при первой записи)",
    };

    private static async Task<MicrophoneDevice> ResolveMicrophoneAsync(ICaptureBackend backend, string? query, CancellationToken ct)
    {
        var devices = await backend.ListMicrophonesAsync(ct);
        return MicrophoneDevice.Resolve(devices, query)
            ?? throw new CaptureException(query is null
                ? "Нет микрофона по умолчанию. Укажите --mic или --no-mic."
                : $"Микрофон «{query}» не найден или найдено несколько. Доступны: {string.Join(", ", devices.Select(d => d.Name))}");
    }

    private static ICaptureBackend CreateBackend()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new CaptureException("Запись пока поддерживается только на macOS.");
        }

        var helper = MacCaptureBackend.Locate(AppContext.BaseDirectory)
            ?? throw new CaptureException($"Не найден {MacCaptureBackend.HelperFileName} рядом с meetrec ({AppContext.BaseDirectory}).");
        return new MacCaptureBackend(helper);
    }

    private static void DeleteIfEmpty(string directory)
    {
        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }
}
