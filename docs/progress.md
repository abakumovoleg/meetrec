# Прогресс meetrec

Спека: `meetrec — спецификация CLI-рекордера встреч.md` в корне. Почему сделано так — `docs/decisions.md`. Этот файл — что сделано и что дальше; обновлять в конце каждого шага.

## Сделано

| Шаг | Что | Коммит |
| --- | --- | --- |
| 1 | Каркас: `Meetrec.slnx`, проекты Cli/Core/Asr/Diarization/Core.Tests, CLI-заглушки команд и флагов (System.CommandLine 2.0) | `dcd4c67` |
| 2 | `TranscriptMerger.Merge` + модели `Word`, `DiarSegment`, `Utterance`, `TranscriptWord`, `Session` | `7871e8f` |
| 3 | Форматтеры md/json/srt, `TranscriptWriter.WriteAll` | `3ec266f` |
| 4 | `AudioLoader.LoadMono16k` — WAV любой частоты/каналов → 16 кГц моно (NAudio.Core 3) | `8c5fa5e` |
| 5 | `ModelCatalog`, `ModelDirectory` (`MEETREC_MODELS`), `ModelStore` (скачивание через `.part`, распаковка CoreML zip); команды `meetrec models` и `meetrec models download <id>` | `df4bfbc` |
| 6 | `WhisperTranscriber` (Whisper.net 1.9.1, `SplitOnWord`, CoreML на Mac) | `fa222a6` |
| 7 | `SileroVad` + `SpeechMask` — VAD после распознавания (отступление от спеки, см. decisions); `SpeechRecognizer` связывает VAD и Whisper | `a793360` |
| 8 | `SpeakerDiarizer` (sherpa-onnx), `DiarizationSettings.Choose` — eres2net при `--speakers`, wespeaker с порогом 0,6 без него | `77755c1` |
| 9 | `meetrec transcribe` end-to-end: `TranscribeInput` (папка сессии или WAV), `SessionFile` (чтение/запись `session.json`), `TranscriptionModels` (автоскачивание), прогресс по этапам, вывод `transcript.*` / `<имя>.transcript.*` | `089486b` |
| 11 | Swift-хелпер `native/meetrec-capture`: Process Tap + приватный агрегат только с тапом, микрофон через `AVAudioEngine`, `AVAudioConverter` → 16 кГц моно, протокол stdout, SIGINT/SIGTERM, коды 2/3, `--list-devices`; библиотека `CaptureKit` + 13 тестов swift-testing | `7c21e53` |
| 12 | `Meetrec.Capture.Mac` (`MacCaptureBackend`: запуск хелпера, протокол, остановка через SIGINT), `ICaptureBackend` и помощники в `Meetrec.Core/Capture`; команды `record`, `run`, `devices`; индикатор уровней, предупреждение о 10 с нулей, `session.json`; свой обработчик Ctrl+C (`Interrupts`); `dotnet build` на Mac собирает хелпер. Микрофон в хелпере переведён с `AVAudioEngine` на IOProc | `e6bf67a` |
| 7б | `SpeechRepair`: регионы речи, где Whisper почти не дал слов (< 0,25 коротких слов/с), распознаются повторно отдельными окнами; итог `transcribe` не считает «Спикер ?» спикером | `1cd8c52` |
| 12+ | Разрешение на системный звук: проверка (`TCCAccessPreflight`) и явный запрос (`TCCAccessRequest`) в хелпере, `--check-permissions`, отказ до старта записи, статус в `meetrec devices`. Порог громкости −60 дБFS для регионов VAD (`SpeechLevel`) — убирает «Thank you.» на молчащем микрофоне | — |

113 юнит-тестов в `tests/Meetrec.Core.Tests` (`dotnet test`) и 15 в `native/meetrec-capture` (`swift test`), все зелёные.

Все команды спеки работают на macOS; на Windows `record`, `run`, `devices` пока сообщают, что запись не поддерживается.

Проверка шага 9 на реальной 8-минутной записи: одиночный файл — 1 мин 10 с (из них ≈ 20 с — первая компиляция CoreML), 5 спикеров; папка сессии с синтетическим `mic.wav` (44,1 кГц стерео) и `offsetMs = 20000` — 44 с, «Я» на 00:00:20, при `--speakers 6` ровно 5 спикеров + «Я».

## Осталось

Предлагаемый порядок: 14–16 → 13.

**Этапы 1–2 спеки — расшифровка готовой записи**

- [ ] **10. Приёмка этапа 1:** ≥ 85 % слов верному спикеру на 10-минутной записи 3 спикеров с `--speakers 3`, таймкоды реплик ±0,5 с. Нужна запись с известной разметкой — у пользователя её пока нет.

**Этап 4 — запись на macOS** — готов (шаги 11–12).

**Этап 3 — запись на Windows**

- [ ] **13. `Meetrec.Capture.Windows`:** NAudio `WasapiCapture` + `WasapiLoopbackCapture`, дописывание тишины в loopback по `Stopwatch`, ресемплинг при записи, `devices`. Проверка возможна только на Windows-машине — уточнить у пользователя, есть ли она.

**Этап 5 — релиз**

- [ ] **14. Публикация** single-file self-contained под `osx-arm64` и `win-x64`; решить с нативными библиотеками whisper.cpp/sherpa-onnx (`IncludeNativeLibrariesForSelfExtract` или папка); `meetrec-capture` рядом с `meetrec`, поиск через `AppContext.BaseDirectory`; запуск на чистой машине без .NET.
- [ ] **15. CI** `.github/workflows/release.yml` по тегу `v*`: тесты, сборка Swift, publish, архивы `meetrec-<ver>-win-x64.zip` / `meetrec-<ver>-osx-arm64.tar.gz`, GitHub Release.
- [ ] **16. README:** установка, разрешения macOS (выдаются терминалу), `xattr -d com.apple.quarantine`, Windows 10 vs 11 (Whisper.net требует 11 + VC++ Redist 2022), рекомендация `--model small` без GPU, наушники против эха.

**Открытые вопросы**

- Частичные потери после долгих пауз внутри записи (синтетика «острова через 20 с»: 22/32 слов). `SpeechRepair` с порогом 0,25 их не ловит — плотность в таких регионах ≈ 0,7 слова/с; порог 1,0 даёт 27/32, но уже меняет нормальный текст созвона (817 из 821 общих). Нужна запись с разметкой, чтобы решить. Стенд: `/tmp/meetrec-eval/gap`.

- Хелпер не переживает смену устройства во время записи (`AVAudioEngineConfigurationChange`, отключение наушников): микрофон остановится, системная дорожка продолжит писаться. Ресемплер смену частоты уже переносит.
- Запись микрофона с живой речью человека не проверена — только уровни (веб-камера, встроенный, Bluetooth-наушники). Проверить на настоящем созвоне.
- Ctrl+C в интерактивном терминале не проверен вживую (в тестах — SIGTERM, у фоновых процессов неинтерактивного shell SIGINT игнорируется). Терминал шлёт SIGINT всей группе, хелпер получит его сам и закроет файлы — путь штатный.

- Реплики, где вопрос и ответ разных людей склеены — смена спикера без паузы не ловится.
- Размер пакета `Whisper.net.Runtime.Cuda` на Windows и его влияние на архив.
- CoreML-энкодер скачивается на любом macOS (`OperatingSystem.IsMacOS()`); на Intel-Mac CoreML-рантайм не проверялся.
- `record` (шаг 12) должен писать `session.json` через `SessionFile.Write`; `run` — переиспользовать `TranscribeCommand.RunAsync` и `TranscribeOptions`.
- SHA256 моделей (сейчас только проверка размера).
- Стенд замеров живёт в `/tmp/meetrec-eval` и может пропасть после перезагрузки; при необходимости перенести в `tools/eval`.
