using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Meetrec.Core.Capture;

namespace Meetrec.Capture.Mac;

public sealed partial class MacCaptureBackend(string helperPath) : ICaptureBackend
{
    public const string HelperFileName = "meetrec-capture";

    public const string SystemDeviceName = "Системный звук (Core Audio Process Tap)";

    private const string SettingsHint =
        "Выдайте его приложению терминала: Системные настройки → Конфиденциальность и безопасность → {0}. После этого перезапустите терминал.";

    private const int SigInt = 2;
    private const int NoPermissionExitCode = 2;
    private const int NoDeviceExitCode = 3;
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);

    private readonly TaskCompletionSource<CaptureStarted> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly StringBuilder _stderr = new();
    private Process? _process;
    private Task<int>? _exitCode;

    public event Action<LevelInfo>? Levels;

    public CaptureHints Hints { get; } = new(
        PermissionUnknown: "ещё не запрашивалось (macOS спросит при первой записи)",
        PermissionPending: "macOS спросит разрешение на запись звука — ответьте «Разрешить», запись начнётся после этого.",
        MicrophoneDenied: "Нет разрешения на микрофон. " + string.Format(SettingsHint, "Микрофон"),
        SystemAudioDenied: "Нет разрешения на запись системного звука — собеседники не запишутся. "
            + string.Format(SettingsHint, "Запись звука и экрана → «Только системный звук»"),
        SystemSilence: "Если собеседники уже говорят, у терминала нет разрешения «Запись звука и экрана» (Системные настройки → Конфиденциальность и безопасность).",
        SlowStart: "Запись ещё не началась. Если macOS спрашивает разрешение на микрофон или запись звука — ответьте на запрос; "
            + "иначе выбранный микрофон не отдаёт звук (см. meetrec devices).");

    public Task Exited => _exitCode ?? Task.CompletedTask;

    public static string? Locate(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, HelperFileName);
        return File.Exists(path) ? path : null;
    }

    public async Task<IReadOnlyList<MicrophoneDevice>> ListMicrophonesAsync(CancellationToken ct) =>
        [.. (await QueryAsync("--list-devices", "список микрофонов", ct)).OfType<HelperMessage.Device>().Select(d => d.Microphone)];

    public async Task<CapturePermissions> CheckPermissionsAsync(CancellationToken ct) =>
        (await QueryAsync("--check-permissions", "статус разрешений", ct)).OfType<HelperMessage.Permissions>().FirstOrDefault()?.Status
        ?? new CapturePermissions(PermissionStatus.Unknown, PermissionStatus.Unknown);

    public async Task<CaptureStarted> StartAsync(string sessionDir, CaptureOptions options, CancellationToken ct)
    {
        if (_process is not null)
        {
            throw new InvalidOperationException("Запись уже запущена");
        }

        List<string> args = ["--out", sessionDir];
        if (options.NoMic)
        {
            args.Add("--no-mic");
        }
        else if (options.MicUid is { } uid)
        {
            args.AddRange(["--mic", uid]);
        }

        var process = new Process { StartInfo = CreateStartInfo(args) };
        process.OutputDataReceived += (_, e) => OnOutput(e.Data);
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (_stderr)
                {
                    _stderr.AppendLine(e.Data);
                }
            }
        };

        process.Start();
        _process = process;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _exitCode = WaitForExitAsync(process);

        try
        {
            return await _started.Task.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Before "started" the helper may be blocked on a macOS permission dialog and is not listening for
            // SIGINT yet, so it is killed outright.
            KillQuietly(process);
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_process is not { } process || _exitCode is not { } exitCode)
        {
            return;
        }

        if (!exitCode.IsCompleted)
        {
            _ = Kill(process.Id, SigInt);
        }

        int code;
        try
        {
            code = await exitCode.WaitAsync(StopTimeout);
        }
        catch (TimeoutException)
        {
            KillQuietly(process);
            throw new CaptureException("Хелпер записи не завершился за отведённое время и был остановлен принудительно; WAV-файлы могут быть неполными.");
        }

        if (code != 0)
        {
            throw Failure(code);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is { } process)
        {
            KillQuietly(process);
            if (_exitCode is { } exitCode)
            {
                await ((Task)exitCode).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }

            process.Dispose();
        }
    }

    private async Task<IReadOnlyList<HelperMessage>> QueryAsync(string flag, string what, CancellationToken ct)
    {
        using var process = Process.Start(CreateStartInfo([flag]))!;
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
        {
            throw new CaptureException($"Не удалось получить {what}: {(await stderr).Trim()}");
        }

        return [.. (await stdout).Split('\n').Select(HelperMessage.Parse).OfType<HelperMessage>()];
    }

    private ProcessStartInfo CreateStartInfo(IEnumerable<string> args)
    {
        if (!File.Exists(helperPath))
        {
            throw new CaptureException($"Не найден хелпер записи: {helperPath}");
        }

        var info = new ProcessStartInfo(helperPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        return info;
    }

    private void OnOutput(string? line)
    {
        switch (HelperMessage.Parse(line))
        {
            case HelperMessage.Started started:
                _started.TrySetResult(new CaptureStarted(started.OffsetMs, SystemDeviceName));
                break;
            case HelperMessage.Level level:
                Levels?.Invoke(level.Info);
                break;
        }
    }

    private async Task<int> WaitForExitAsync(Process process)
    {
        await process.WaitForExitAsync();
        var code = process.ExitCode;
        _started.TrySetException(code == 0
            ? new CaptureException("Хелпер записи завершился до начала записи")
            : Failure(code));
        return code;
    }

    private CaptureException Failure(int code)
    {
        string details;
        lock (_stderr)
        {
            details = _stderr.ToString().Trim();
        }

        var reason = code switch
        {
            NoPermissionExitCode => "Нет разрешения на запись.",
            NoDeviceExitCode => "Микрофон не найден или недоступен (см. meetrec devices).",
            _ => $"Хелпер записи завершился с кодом {code}.",
        };

        return new CaptureException(details.Length > 0 ? $"{reason}\n{details}" : reason);
    }

    private static void KillQuietly(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int Kill(int pid, int signal);
}
