using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Meetrec.Core;
using Meetrec.Core.Capture;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Meetrec.Capture.Windows;

// Both tracks are recorded in-process through WASAPI: the microphone from its own endpoint, the other
// participants from the default output device in loopback mode. Packets of both carry performance-counter
// timestamps from one clock, so the tracks share a start and session.json needs no offset.
[SupportedOSPlatform("windows")]
public sealed class WindowsCaptureBackend : ICaptureBackend
{
    private const string MicrophoneBlocked =
        "Нет доступа к микрофону: Windows запрещает запись. Параметры → Конфиденциальность и защита → Микрофон: " +
        "включите «Доступ к микрофону» и «Разрешить классическим приложениям доступ к микрофону».";

    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LevelInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StartPoll = TimeSpan.FromMilliseconds(25);

    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Stopwatch _clock = new();
    private readonly Lock _gate = new();
    private readonly List<Stream> _streams = [];
    private long _origin;
    private CaptureTrack? _systemTrack;
    private CaptureTrack? _micTrack;
    private Timer? _levelTimer;
    private Exception? _failure;
    private bool _stopped;

    public event Action<LevelInfo>? Levels;

    public Task Exited => _exited.Task;

    public CaptureHints Hints { get; } = new(
        PermissionUnknown: "Windows сообщает его только при старте записи",
        PermissionPending: null,
        MicrophoneDenied: MicrophoneBlocked,
        SystemAudioDenied: null,
        SystemSilence: "Если собеседники уже говорят, звук идёт не на то устройство вывода, которое выбрано по умолчанию (Параметры → Система → Звук).",
        SlowStart: "Запись ещё не началась. Проверьте, что микрофон и устройство вывода свободны (см. meetrec devices).");

    public Task<IReadOnlyList<MicrophoneDevice>> ListMicrophonesAsync(CancellationToken ct) =>
        Task.FromResult(Microphones());

    // WASAPI loopback needs no permission, and Windows reveals the microphone one only when recording starts.
    public Task<CapturePermissions> CheckPermissionsAsync(CancellationToken ct) =>
        Task.FromResult(new CapturePermissions(PermissionStatus.Unknown, PermissionStatus.Granted));

    public async Task<CaptureStarted> StartAsync(string sessionDir, CaptureOptions options, CancellationToken ct)
    {
        if (_streams.Count > 0)
        {
            throw new InvalidOperationException("Запись уже запущена");
        }

        using var enumerator = new MMDeviceEnumerator();
        var output = DefaultDevice(enumerator, DataFlow.Render)
            ?? throw new CaptureException("Нет устройства вывода по умолчанию: системный звук записать не получится.");
        var microphone = options.NoMic ? null : MicrophoneFor(enumerator, options.MicUid);

        try
        {
            // Loopback stays on polling: WASAPI does not signal the buffer event while the render device is
            // idle, so an event-driven loopback stream can sit silent for the whole meeting.
            _systemTrack = Add(
                Recorder(output).WithLoopbackCapture().WithPollingSync(),
                output,
                Path.Combine(sessionDir, TranscribeInput.SystemFileName));
            if (microphone is not null)
            {
                _micTrack = Add(Recorder(microphone), microphone, Path.Combine(sessionDir, TranscribeInput.MicFileName));
            }

            _clock.Restart();
            _origin = PacketTime.Now();
            foreach (var stream in _streams)
            {
                stream.Recorder.StartRecording();
            }

            await WaitForStartAsync(ct);
        }
        catch (Exception e)
        {
            await QuietStopAsync();
            throw e is COMException or ArgumentException or InvalidOperationException ? new CaptureException(Describe(e)) : e;
        }

        _levelTimer = new Timer(_ => Tick(), null, LevelInterval, LevelInterval);
        return new CaptureStarted(0, $"Системный звук (WASAPI loopback): {output.FriendlyName}");
    }

    public async Task StopAsync()
    {
        if (_streams.Count == 0 || _stopped)
        {
            return;
        }

        _stopped = true;
        if (_levelTimer is { } timer)
        {
            await timer.DisposeAsync();
        }

        foreach (var stream in _streams)
        {
            Quietly(stream.Recorder.StopRecording);
        }

        var stopped = Task.WhenAll(_streams.Select(s => s.Stopped));
        try
        {
            await stopped.WaitAsync(StopTimeout);
        }
        catch (TimeoutException)
        {
        }

        _clock.Stop();
        // A meeting may well end in silence, which loopback simply does not report: both tracks are padded to
        // the recorded length so that their duration matches the real one.
        _systemTrack?.Finish(_clock.Elapsed);
        _micTrack?.Finish(_clock.Elapsed);

        if (Failure() is { } failure)
        {
            throw new CaptureException($"Запись прервалась: {Describe(failure)}");
        }

        if (!stopped.IsCompletedSuccessfully)
        {
            throw new CaptureException("Поток записи не остановился за отведённое время; WAV-файлы могут быть неполными.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await QuietStopAsync();
        foreach (var stream in _streams)
        {
            await stream.Recorder.DisposeAsync().AsTask().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            Quietly(stream.Device.Dispose);
        }

        _streams.Clear();
    }

    private static WasapiRecorderBuilder Recorder(MMDevice device) =>
        new WasapiRecorderBuilder().WithDevice(device).WithMmcssThreadPriority("Capture");

    private static IReadOnlyList<MicrophoneDevice> Microphones()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var defaultId = DefaultId(enumerator, DataFlow.Capture);
            var devices = new List<MicrophoneDevice>();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                using (device)
                {
                    devices.Add(new MicrophoneDevice(device.ID, device.FriendlyName, device.ID == defaultId));
                }
            }

            return devices;
        }
        catch (COMException e)
        {
            throw new CaptureException($"Не удалось получить список микрофонов: {Describe(e)}");
        }
    }

    private static MMDevice MicrophoneFor(MMDeviceEnumerator enumerator, string? uid)
    {
        if (uid is null)
        {
            return DefaultDevice(enumerator, DataFlow.Capture)
                ?? throw new CaptureException("Нет микрофона по умолчанию. Укажите --mic или --no-mic.");
        }

        try
        {
            return enumerator.GetDevice(uid);
        }
        catch (COMException e)
        {
            throw new CaptureException($"Микрофон недоступен: {Describe(e)}");
        }
    }

    private static MMDevice? DefaultDevice(MMDeviceEnumerator enumerator, DataFlow flow) =>
        enumerator.HasDefaultAudioEndpoint(flow, Role.Console) ? enumerator.GetDefaultAudioEndpoint(flow, Role.Console) : null;

    private static string? DefaultId(MMDeviceEnumerator enumerator, DataFlow flow)
    {
        using var device = DefaultDevice(enumerator, flow);
        return device?.ID;
    }

    private static string Describe(Exception e) => e switch
    {
        COMException com => (uint)com.HResult switch
        {
            0x80070005 => MicrophoneBlocked,
            0x88890004 => "Аудиоустройство отключено или недоступно.",
            0x88890008 => "Формат аудиоустройства не поддерживается.",
            0x8889000A => "Аудиоустройство занято другим приложением в монопольном режиме.",
            _ => $"{com.Message} (0x{(uint)com.HResult:X8})",
        },
        _ => e.Message,
    };

    private static void Quietly(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e) when (e is COMException or InvalidOperationException or ObjectDisposedException)
        {
        }
    }

    private CaptureTrack Add(WasapiRecorderBuilder builder, MMDevice device, string path)
    {
        var recorder = builder.Build();
        var track = new CaptureTrack(path, recorder.WaveFormat);
        var stream = new Stream(recorder, device, recorder.WaveFormat.AverageBytesPerSecond);
        recorder.DataAvailable += (data, flags, _, timestamp) => OnData(stream, track, data, flags, timestamp);
        recorder.RecordingStopped += (_, e) => OnStopped(stream, e.Exception);
        _streams.Add(stream);
        return track;
    }

    private void OnData(Stream stream, CaptureTrack track, ReadOnlySpan<byte> data, AudioClientBufferFlags flags, long timestamp)
    {
        try
        {
            var start = PacketTime.Start(
                timestamp,
                _origin,
                !flags.HasFlag(AudioClientBufferFlags.TimestampError),
                _clock.Elapsed,
                TimeSpan.FromSeconds((double)data.Length / stream.BytesPerSecond));
            track.Write(data, flags.HasFlag(AudioClientBufferFlags.Silent), start);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Fail(error);
        }
    }

    private void OnStopped(Stream stream, Exception? error)
    {
        if (error is not null)
        {
            Fail(error);
        }

        stream.Finish();
        _exited.TrySetResult();
    }

    private void Fail(Exception error)
    {
        lock (_gate)
        {
            _failure ??= error;
        }
    }

    private Exception? Failure()
    {
        lock (_gate)
        {
            return _failure;
        }
    }

    private async Task WaitForStartAsync(CancellationToken ct)
    {
        // Each recorder initialises its device on its own thread, so "started" means every stream reached Capturing.
        var waited = Stopwatch.StartNew();
        while (_streams.Any(s => s.Recorder.CaptureState == CaptureState.Starting))
        {
            if (Failure() is { } pending)
            {
                throw new CaptureException(Describe(pending));
            }

            if (waited.Elapsed > StartTimeout)
            {
                throw new CaptureException($"Запись не началась за {StartTimeout.TotalSeconds:0} секунд.");
            }

            await Task.Delay(StartPoll, ct);
        }

        if (Failure() is { } failure)
        {
            throw new CaptureException(Describe(failure));
        }

        if (_streams.Any(s => s.Recorder.CaptureState != CaptureState.Capturing))
        {
            throw new CaptureException("Запись не началась: устройство остановило поток сразу после старта.");
        }
    }

    private void Tick()
    {
        _systemTrack?.FlushHeader();
        _micTrack?.FlushHeader();
        Levels?.Invoke(new LevelInfo(_micTrack?.TakePeak(), _systemTrack?.TakePeak() ?? 0));
    }

    private async Task QuietStopAsync()
    {
        try
        {
            await StopAsync();
        }
        catch (CaptureException)
        {
        }
    }

    private sealed record Stream(WasapiRecorder Recorder, MMDevice Device, int BytesPerSecond)
    {
        private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Stopped => _stopped.Task;

        public void Finish() => _stopped.TrySetResult();
    }
}
