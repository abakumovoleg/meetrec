using Meetrec.Capture.Meetzone;
using Meetrec.Core;
using Meetrec.Core.Capture;

namespace Meetrec.Cli;

// `record --meetzone`: tracks come from the browser extension, not from a capture helper. The page may go away and
// come back (reload, restarted service worker), so the recording ends only on Ctrl+C.
internal static class MeetzoneRecording
{
    public const string ExtensionFolder = "meetzone-extension";

    private static readonly TimeSpan ExtensionHintDelay = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan StatusInterval = TimeSpan.FromSeconds(1);

    public static string ExtensionDirectory => Path.Combine(AppContext.BaseDirectory, ExtensionFolder);

    public static async Task RecordAsync(string directory, CancellationToken stop)
    {
        var status = new RecordingStatus();
        var connected = false;
        var started = false;
        var recorder = new MeetzoneRecorder(directory);
        try
        {
            recorder.ParticipantAdded += name =>
            {
                status.WriteLine($"Участник: {name}");
                WriteSession(directory, recorder);
            };

            await using var server = new MeetzoneServer(MeetzoneServer.DefaultPort, message =>
            {
                switch (message)
                {
                    case Started when !started:
                        started = true;
                        status.MarkStarted();
                        status.WriteLine($"Запись Meetzone в {directory}. Ctrl+C — остановить.");
                        break;
                    case PageError error:
                        status.WriteLine($"Внимание: ошибка на странице созвона: {error.Message}");
                        break;
                    case PageClosed:
                        status.WriteLine("Страница созвона закрыта — жду, пока она откроется снова. Ctrl+C — остановить.");
                        break;
                }

                recorder.Handle(message);
            });
            server.Connected += hello =>
            {
                connected = true;
                status.WriteLine($"Расширение meetrec {hello.Version} подключено{(hello.Url is { } url ? $": {url}" : "")}");
            };
            server.Disconnected += () => status.WriteLine("Расширение отключилось — жду переподключения. Ctrl+C — остановить.");
            server.Problem += problem => status.WriteLine($"Внимание: {problem}");
            server.Start();
            status.WriteLine("Жду созвон Meetzone: откройте его в Chrome с расширением meetrec. Ctrl+C — остановить.");

            var hinted = false;
            using var ticks = new PeriodicTimer(StatusInterval);
            try
            {
                while (await ticks.WaitForNextTickAsync(stop))
                {
                    if (!connected && !hinted && status.Elapsed >= ExtensionHintDelay)
                    {
                        hinted = true;
                        status.WriteLine(
                            $"Расширение не подключилось. Установите его в Chrome: chrome://extensions → «Режим разработчика» → «Загрузить распакованное» → {ExtensionDirectory}; затем обновите страницу созвона.");
                    }

                    if (started)
                    {
                        var snapshot = recorder.Snapshot();
                        var speaking = snapshot.Speaking.Count > 0 ? $"  говорит: {string.Join(", ", snapshot.Speaking)}" : "";
                        status.Show($"  Meetzone: участников {snapshot.Participants}{speaking}");
                    }
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
            }

            await server.StopAsync();
        }
        finally
        {
            recorder.Finish();
        }

        var tracks = recorder.Tracks();
        if (tracks.Count == 0)
        {
            throw new CaptureException(started
                ? "Никто не говорил — записывать нечего."
                : "Расширение meetrec не прислало звук: созвон не был открыт или расширение не установлено.");
        }

        WriteSession(directory, recorder);
        if (recorder.DroppedBlocks > 0)
        {
            status.WriteLine($"Внимание: {recorder.DroppedBlocks} блоков звука без известного участника отброшено.");
        }

        status.WriteLine($"Записано {Formatting.Duration(status.Elapsed)}, участников {tracks.Count}: {directory}");
    }

    // Rewritten whenever someone new is heard, so that a recording cut short by a crash can still be transcribed.
    private static void WriteSession(string directory, MeetzoneRecorder recorder)
    {
        if (recorder.StartedAt is { } startedAt && recorder.Tracks() is { Count: > 0 } tracks)
        {
            SessionFile.Write(Path.Combine(directory, SessionLayout.SessionFileName), new Session(startedAt, AppVersion.Current, tracks));
        }
    }
}
