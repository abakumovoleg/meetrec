using System.CommandLine;
using Meetrec.Core;
using Meetrec.Core.Audio;
using Meetrec.Core.Capture;
using Meetrec.Core.Transcript;

namespace Meetrec.Cli;

internal static class RecordCommand
{
    private static readonly TimeSpan PermissionHintDelay = TimeSpan.FromSeconds(5);

    public static Command Record(Interrupts interrupts)
    {
        var options = new RecordOptions();
        var command = new Command("record", "Записать микрофон и системный звук в папку сессии");
        options.AddTo(command);
        command.SetAction(async parse =>
        {
            await ExecuteAsync(options.Read(parse), interrupts, keepAfterStopFailure: false);
            return 0;
        });
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
            var directory = await ExecuteAsync(recordOptions.Read(parse), interrupts, keepAfterStopFailure: true);
            return await TranscribeCommand.ExecuteAsync(directory, transcribeOptions.Read(parse), interrupts.NextPhase());
        });
        return command;
    }

    public static Command Devices(Interrupts interrupts)
    {
        var command = new Command("devices", "Список микрофонов");
        command.SetAction(async _ =>
        {
            await using var backend = Platform.CreateCaptureBackend();
            foreach (var device in await backend.ListMicrophonesAsync(interrupts.Current))
            {
                Console.WriteLine($"{(device.IsDefault ? "*" : " ")} {device.Name}  ({device.Uid})");
            }

            var permissions = await backend.CheckPermissionsAsync(interrupts.Current);
            Console.WriteLine();
            Console.WriteLine($"Разрешение на микрофон: {Describe(permissions.Microphone, backend.Hints)}");
            Console.WriteLine($"Разрешение на системный звук: {Describe(permissions.SystemAudio, backend.Hints)}");
        });
        return command;
    }

    // keepAfterStopFailure: in `run` a recording whose stop failed is still transcribed if its WAV files are readable —
    // an hour of a call is not thrown away because the helper did not exit in time.
    private static async Task<string> ExecuteAsync(RecordSettings settings, Interrupts interrupts, bool keepAfterStopFailure)
    {
        var directory = settings.Output ?? SessionDirectory.Default(DateTime.Now, Environment.CurrentDirectory);
        try
        {
            if (Directory.Exists(directory) && SessionLayout.ContainsRecording(directory))
            {
                throw new CaptureException($"В папке уже есть запись: {directory}");
            }

            await using var backend = Platform.CreateCaptureBackend();
            var stop = interrupts.Current;
            ReportPermissions(await backend.CheckPermissionsAsync(stop), backend.Hints, settings.NoMic);
            var mic = settings.NoMic ? null : await ResolveMicrophoneAsync(backend, settings.Mic, stop);

            CreateDirectory(directory);
            var status = new RecordingStatus(backend.Hints.SystemSilence);
            backend.Levels += status.OnLevels;

            var startedAt = DateTimeOffset.UtcNow;
            var start = backend.StartAsync(directory, new CaptureOptions(mic?.Uid, settings.NoMic), stop);
            if (await Task.WhenAny(start, Task.Delay(PermissionHintDelay, CancellationToken.None)) != start)
            {
                status.WriteLine(backend.Hints.SlowStart);
            }

            var started = await start;
            status.MarkStarted();
            var session = NewSession(startedAt, mic, started);
            SessionFile.Write(Path.Combine(directory, SessionLayout.SessionFileName), session);
            status.WriteLine($"Запись в {directory}{(mic is null ? "" : $", микрофон «{mic.Name}»")}. Ctrl+C — остановить.");

            await Task.WhenAny(backend.Exited, Task.Delay(Timeout.Infinite, stop));
            try
            {
                await backend.StopAsync();
            }
            catch (CaptureException e) when (keepAfterStopFailure
                && session.Tracks.All(t => AudioLoader.IsReadable(Path.Combine(directory, t.File))))
            {
                status.WriteLine($"Внимание: запись остановилась с ошибкой: {e.Message}");
                status.WriteLine("WAV-файлы читаются — расшифровываю то, что записалось.");
            }

            status.WriteLine($"Записано {Formatting.Duration(status.Elapsed)}: {directory}");
            return directory;
        }
        catch
        {
            DeleteIfEmpty(directory);
            throw;
        }
    }

    // Mic first: on equal timestamps its words come first, as they always have.
    private static Session NewSession(DateTimeOffset startedAt, MicrophoneDevice? mic, CaptureStarted started)
    {
        SessionTrack[] micTrack = mic is null
            ? []
            : [new SessionTrack(SessionLayout.MicFile, TrackSource.Mic, started.OffsetMs, mic.Name, new SessionSpeaker(SpeakerId.Me.Value, Speakers.Me))];
        return new Session(startedAt, AppVersion.Current, [.. micTrack, new SessionTrack(SessionLayout.SystemFile, TrackSource.System, 0, started.SystemDevice)]);
    }

    private static void ReportPermissions(CapturePermissions permissions, CaptureHints hints, bool noMic)
    {
        if (permissions.SystemAudio == PermissionStatus.Denied)
        {
            throw new CaptureException(hints.SystemAudioDenied ?? "Нет доступа к системному звуку — собеседники не запишутся.");
        }

        if (!noMic && permissions.Microphone == PermissionStatus.Denied)
        {
            throw new CaptureException(
                (hints.MicrophoneDenied ?? "Нет доступа к микрофону.") + " Или запишите без него: --no-mic.");
        }

        if (hints.PermissionPending is { } pending
            && (permissions.SystemAudio == PermissionStatus.Unknown || (!noMic && permissions.Microphone == PermissionStatus.Unknown)))
        {
            Console.Error.WriteLine(pending);
        }
    }

    private static string Describe(PermissionStatus status, CaptureHints hints) => status switch
    {
        PermissionStatus.Granted => "есть",
        PermissionStatus.Denied => "запрещено",
        _ => hints.PermissionUnknown,
    };

    private static async Task<MicrophoneDevice> ResolveMicrophoneAsync(ICaptureBackend backend, string? query, CancellationToken ct)
    {
        var devices = await backend.ListMicrophonesAsync(ct);
        return MicrophoneDevice.Resolve(devices, query)
            ?? throw new CaptureException(query is null
                ? "Нет микрофона по умолчанию. Укажите --mic или --no-mic."
                : $"Микрофон «{query}» не найден или найдено несколько. Доступны: {string.Join(", ", devices.Select(d => d.Name))}");
    }

    private static void CreateDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MeetrecException($"Не удалось создать папку {directory}: {e.Message}", e);
        }
    }

    private static void DeleteIfEmpty(string directory)
    {
        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }
}
