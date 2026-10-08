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

68 юнит-тестов в `tests/Meetrec.Core.Tests`, все зелёные (`dotnet test`).

**Ещё не подключено к CLI:** команды `transcribe`, `record`, `run`, `devices` — заглушки. Работают только `models` и `models download`. Распознавание и диаризация пока вызываются только из стенда замеров.

## Осталось

Предлагаемый порядок: 9 → 11–12 → 14–16 → 13.

**Этапы 1–2 спеки — расшифровка готовой записи**

- [ ] **9. `meetrec transcribe` end-to-end.** Вход: одиночный WAV (= системная дорожка, без «Я») или папка сессии с `mic.wav` + `system.wav` + `session.json` (`offsetMs`). Автоскачивание нужных моделей (Whisper, CoreML-энкодер на Mac, Silero, pyannote, эмбеддинг по `DiarizationSettings`); прогресс в консоли; оба трека распознаются одним экземпляром Whisper последовательно; диаризация только `system.wav`; `Merge`; запись `transcript.*` в папку сессии. Решить, куда писать вывод для одиночного файла. Чтение/запись `session.json` (модель `Session` уже есть).
- [ ] **10. Приёмка этапа 1:** ≥ 85 % слов верному спикеру на 10-минутной записи 3 спикеров с `--speakers 3`, таймкоды реплик ±0,5 с. Нужна запись с известной разметкой — у пользователя её пока нет.

**Этап 4 — запись на macOS**

- [ ] **11. Swift-хелпер `native/meetrec-capture`** (Swift Package, `swift build -c release --arch arm64`): Core Audio Process Tap (глобальный моно-тап, приватный агрегат только с тапом — иначе эхо), микрофон через `AVAudioEngine`, `AVAudioConverter` → 16 кГц моно, два WAV; stdout — JSON-строки `{"t":"level",...}` и `{"t":"started","offsetMs":N}`; SIGINT → корректное закрытие; коды выхода 2 (нет разрешения) и 3 (нет устройства). Референс — AudioCap, Meetily `core_audio.rs`.
- [ ] **12. `Meetrec.Capture.Mac` + CLI:** `ICaptureBackend` (запуск хелпера, протокол), команды `record`, `run`, `devices`; индикатор уровней раз в секунду; предупреждение, если системный канал — нули первые 10 с; Ctrl+C всегда даёт валидные WAV; `session.json`. Проверить знак `offsetMs` против трактовки в `Merge` (см. decisions).

**Этап 3 — запись на Windows**

- [ ] **13. `Meetrec.Capture.Windows`:** NAudio `WasapiCapture` + `WasapiLoopbackCapture`, дописывание тишины в loopback по `Stopwatch`, ресемплинг при записи, `devices`. Проверка возможна только на Windows-машине — уточнить у пользователя, есть ли она.

**Этап 5 — релиз**

- [ ] **14. Публикация** single-file self-contained под `osx-arm64` и `win-x64`; решить с нативными библиотеками whisper.cpp/sherpa-onnx (`IncludeNativeLibrariesForSelfExtract` или папка); `meetrec-capture` рядом с `meetrec`, поиск через `AppContext.BaseDirectory`; запуск на чистой машине без .NET.
- [ ] **15. CI** `.github/workflows/release.yml` по тегу `v*`: тесты, сборка Swift, publish, архивы `meetrec-<ver>-win-x64.zip` / `meetrec-<ver>-osx-arm64.tar.gz`, GitHub Release.
- [ ] **16. README:** установка, разрешения macOS (выдаются терминалу), `xattr -d com.apple.quarantine`, Windows 10 vs 11 (Whisper.net требует 11 + VC++ Redist 2022), рекомендация `--model small` без GPU, наушники против эха.

**Открытые вопросы**

- Реплики, где вопрос и ответ разных людей склеены — смена спикера без паузы не ловится.
- Размер пакета `Whisper.net.Runtime.Cuda` на Windows и его влияние на архив.
- CoreML-рантайм без энкодера не стартует — в шаге 9 обязательно скачивать энкодер (или откатываться на другой рантайм).
- SHA256 моделей (сейчас только проверка размера).
- Стенд замеров живёт в `/tmp/meetrec-eval` и может пропасть после перезагрузки; при необходимости перенести в `tools/eval`.
