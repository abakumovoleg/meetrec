# meetrec — спецификация CLI-рекордера встреч

Oct 8, 2026 · @oleg

## Цель и границы

`meetrec` — консольная утилита на .NET для macOS и Windows: записывает созвон из любого приложения двумя дорожками (микрофон и системный звук), расшифровывает локально через Whisper и размечает собеседников по голосу. Всё работает офлайн, после скачивания моделей сеть не нужна.

**Входит в объём:**

- Запись микрофона и системного звука в два отдельных WAV-файла с общим началом отсчёта.
- Локальное распознавание речи (whisper.cpp через Whisper.net) с пословными таймкодами.
- Диаризация системной дорожки (sherpa-onnx); все реплики с микрофонной дорожки помечаются как «Я».
- Склейка двух дорожек в один транскрипт, отсортированный по времени.
- Вывод в Markdown, JSON и SRT.
- Сборка self-contained бинарников под `osx-arm64` и `win-x64`.

**Не входит в объём:**

- Графический интерфейс, установщик, автообновление.
- Саммари, LLM, любая обработка через облако.
- Расшифровка в реальном времени: распознавание запускается после остановки записи.
- Автоопределение начала созвона, интеграции с Zoom, Teams и календарём.
- Подавление эха. Рекомендованный сценарий — запись в наушниках.

## Стек и зависимости

Одно консольное приложение на .NET 8 (LTS) или новее плюс маленький нативный Swift-хелпер для захвата звука на macOS. Версии пакетов агент берёт последние стабильные на момент начала работы.

| Компонент | Пакет / технология | Платформа | Назначение |
| --- | --- | --- | --- |
| CLI-парсинг | `System.CommandLine` | обе | Команды и флаги |
| Распознавание | `Whisper.net` + `Whisper.net.Runtime` | обе | Обёртка над whisper.cpp |
| Ускорение на Mac | `Whisper.net.Runtime.CoreML` | macOS | Apple Neural Engine / GPU |
| Ускорение на Windows | `Whisper.net.Runtime.Cuda`, `Whisper.net.Runtime.Vulkan` | Windows | NVIDIA / прочие GPU, с откатом на CPU |
| Диаризация | `org.k2fsa.sherpa.onnx` | обе | Сегментация pyannote + эмбеддинги голоса в ONNX |
| Захват на Windows | `NAudio` (`WasapiCapture`, `WasapiLoopbackCapture`) | Windows | Микрофон и системный звук |
| Захват на macOS | Swift-хелпер `meetrec-capture` (Core Audio Process Tap + AVAudioEngine) | macOS 14.4+ | Микрофон и системный звук в два WAV |
| Ресемплинг | NAudio `WdlResamplingSampleProvider` или собственный | обе | Приведение к 16 кГц моно |

**Ограничения платформ:**

- macOS: только Apple Silicon (`osx-arm64`), версия 14.4 и новее (нужна для Process Tap).
- Windows: `win-x64`. Whisper.net в README заявляет Windows 11 как минимальную версию и требует Visual C++ Redistributable 2022. Поддержку Windows 10 агент проверяет отдельно и фиксирует в README.
- На x64 CPU нужны AVX2/FMA; для старых машин есть `Whisper.net.Runtime.NoAvx`.

## CLI-интерфейс

Три команды: `record` пишет звук, `transcribe` обрабатывает готовую запись, `run` делает обе вещи подряд. Каждая запись — папка сессии, а не один файл.

```
meetrec record   [-o <dir>] [--mic <device>] [--no-mic]
meetrec transcribe <dir|file.wav> [--speakers N] [--lang ru|en|auto]
                   [--model tiny|base|small|medium|large-v3|large-v3-turbo]
                   [--format md,json,srt] [--threshold 0.5]
meetrec run      [те же флаги, что у record и transcribe]
meetrec devices  # список микрофонов
meetrec models   # скачанные модели и их пути
```

**Поведение `record`:**

- По умолчанию создаёт папку `./meetings/yyyy-MM-dd_HH-mm-ss/`.
- Остановка по Ctrl+C: файлы корректно закрываются, WAV-заголовки дописываются.
- В консоли раз в секунду: длительность и индикатор уровня обоих каналов, чтобы сразу было видно, что системный звук реально пишется.
- Если за первые 10 секунд системный канал — сплошные нули, выводится предупреждение (на macOS это почти всегда отсутствие разрешения).

**Папка сессии:**

| Файл | Содержимое |
| --- | --- |
| `mic.wav` | Микрофон, 16 кГц, моно, PCM16 |
| `system.wav` | Системный звук, 16 кГц, моно, PCM16 |
| `session.json` | Время старта по UTC, устройства, смещение дорожек в мс, версия утилиты |
| `transcript.md` | Реплики вида `[00:01:23] Спикер 2: текст` |
| `transcript.json` | Реплики и слова с таймкодами, спикером и дорожкой-источником |
| `transcript.srt` | Субтитры с именем спикера в каждой строке |

**`transcribe` по одиночному файлу.** Если передан один WAV (например, запись из другого рекордера), он считается системной дорожкой: меток «Я» не будет, диаризация идёт по всему файлу. Входной файл любой частоты и числа каналов приводится к 16 кГц моно.

**Модели** скачиваются при первом запуске в `~/.cache/meetrec/models` (macOS) или `%LOCALAPPDATA%\meetrec\models` (Windows) с прогрессом в консоли. По умолчанию — `large-v3-turbo`. Переопределение пути — переменная `MEETREC_MODELS`.

## Архитектура и пайплайн

Микрофон и системный звук остаются отдельными дорожками от захвата до склейки: микрофон даёт реплики «Я» без всякой диаризации, а модель голоса работает только на системной дорожке, где меньше спикеров.

&#91;embedded content: пайплайн meetrec · две дорожки, одна склейка\]

Запись (`record`) и обработка (`transcribe`) разделены: обработку можно повторить на той же папке с другой моделью или другим числом спикеров, не перезаписывая созвон.

## Захват звука

Оба бэкенда реализуют один контракт и пишут две отдельные дорожки с общим началом отсчёта. Это главное отличие от Meetily: там микрофон и системный звук смешиваются в один поток ещё до VAD и распознавания (`AudioMixerRingBuffer` и `ProfessionalAudioMixer` в `pipeline.rs`), а на диск сохраняется уже смешанный `audio.mp4`. Из-за этого разделение «Я / собеседники» в Meetily теряется. Логику смешивания **не переносить**.

```csharp
interface ICaptureBackend : IAsyncDisposable
{
    Task StartAsync(string sessionDir, CaptureOptions options, CancellationToken ct);
    Task StopAsync();                  // закрывает mic.wav и system.wav
    event Action<LevelInfo> Levels;    // уровни для индикатора в консоли
}
```

### Windows: NAudio, в процессе

- Микрофон: `WasapiCapture` на выбранном или дефолтном устройстве ввода.
- Системный звук: `WasapiLoopbackCapture` на дефолтном устройстве вывода.
- **Тишина в loopback.** Когда в системе ничего не играет, WASAPI loopback не присылает буферы вообще, а не присылает нули. Без компенсации дорожка «сжимается» и таймкоды уезжают. Нужно вести часы по `Stopwatch` и дописывать нули за пропущенное время (или параллельно проигрывать тишину на устройство вывода).
- Оба потока приводятся к 16 кГц моно сразу при записи.
- Референс в Meetily: `devices/platform/windows.rs` (выбор WASAPI-хоста и устройств вывода в режиме loopback) и `stream.rs` (`build_input_stream` для разных форматов сэмплов).

### macOS: Swift-хелпер `meetrec-capture`

Отдельный бинарник рядом с основным. .NET запускает его как дочерний процесс и останавливает по SIGINT.

```
meetrec-capture --out <sessionDir> [--mic <uid>] [--no-mic]
  stdout: JSON-строки {"t":"level","mic":0.12,"sys":0.40} и {"t":"started","offsetMs":12}
  exit 0 после корректного закрытия обоих WAV; коды ошибок: 2 — нет разрешения, 3 — нет устройства
```

- Системный звук: Core Audio Process Tap (`CATapDescription`, глобальный моно-тап без исключений), `AudioHardwareCreateProcessTap`, затем приватное агрегатное устройство, в котором **только** тап, без самого устройства вывода, и IOProc для чтения.
- Это ровно то, что делает Meetily в `capture/core_audio.rs` через Rust-крейт `cidre`: `TapDesc::with_mono_global_tap_excluding_processes`, словарь агрегатного устройства с `is_private`, `tap_auto_start`, `tap_list`. Комментарии там объясняют грабли: если добавить в агрегат и устройство вывода, и тап, звук дублируется (эхо). Обработка смены частоты дискретизации — в `audio_proc` того же файла.
- Микрофон: `AVAudioEngine.inputNode` (проще всего) или второй IOProc.
- Обе дорожки ресемплируются в 16 кГц моно через `AVAudioConverter` и пишутся в WAV.
- Разрешения: тап требует `NSAudioCaptureUsageDescription`, микрофон — `NSMicrophoneUsageDescription` (см. `Info.plist` Meetily). Для CLI-бинарника разрешение выдаётся терминалу, из которого его запустили. Без разрешения тап не падает, а молча отдаёт нули — отсюда проверка в `record`.
- Сборка: Swift Package, `swift build -c release --arch arm64`, без Xcode-проекта.

### Синхронизация дорожек

Обе дорожки стартуют в одной точке; фактическая разница старта в миллисекундах пишется в `session.json` (`offsetMs`) и учитывается при склейке. Дрейф часов за час записи несущественен для транскрипта, компенсировать его не нужно.

## Распознавание и диаризация

Каждая дорожка распознаётся отдельно с пословными таймкодами; диаризация запускается только на `system.wav`; затем слова получают спикера по перекрытию во времени.

### Распознавание (Whisper.net)

- Модели GGML с Hugging Face (`ggerganov/whisper.cpp`), по умолчанию `ggml-large-v3-turbo.bin`. Для CoreML на Mac рядом нужна папка `*-encoder.mlmodelc` той же модели.
- Процессор: `WithLanguage(lang)`, `WithTokenTimestamps()`, `SplitOnWord()`, `WithMaxSegmentLength(1)` — так сегменты получаются по одному слову с точными границами. Альтернатива: собирать слова из токенов при включённых `WithProbabilities()`. Агент выбирает вариант, дающий корректные границы слов на русском (токены Whisper не совпадают со словами).
- Перед распознаванием — VAD (`sherpa-onnx` содержит Silero VAD): длинные паузы вырезаются, а результирующие таймкоды пересчитываются обратно в шкалу исходного файла. Это заметно снижает галлюцинации Whisper на тишине («Продолжение следует…», «Субтитры сделал…»). Meetily делает то же (`audio/vad.rs`, Silero, 30 мс-окна на 16 кГц) — ориентир по параметрам.
- Обе дорожки распознаются последовательно одним загруженным экземпляром модели, чтобы не держать в памяти две копии.
- Референс по параметрам whisper.cpp: `whisper_engine/whisper_engine.rs` в Meetily (около строк 561–602 и 678–719: beam search, `set_token_timestamps(true)`, язык).

### Диаризация (sherpa-onnx)

- `OfflineSpeakerDiarization` со следующей конфигурацией:
  - сегментация: `sherpa-onnx-pyannote-segmentation-3-0/model.onnx`, `WindowShiftRatio = 0.1`;
  - эмбеддинги: модель из релиза `speaker-recongition-models` (так, с опечаткой, называется тег). Кандидаты: `3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx` (из официального примера) и `wespeaker_en_voxceleb_resnet34.onnx`. Агент сравнивает их на тестовой записи с русской речью и фиксирует лучшую по умолчанию;
  - кластеризация: `NumClusters = speakers − 1` (минус «Я»), если передан `--speakers`; иначе `Threshold` (по умолчанию 0.5, флаг `--threshold`).
- На вход — `system.wav` целиком, 16 кГц моно, через `ProcessWithCallback` с прогрессом в консоли.
- Готовый пример: `dotnet-examples/offline-speaker-diarization/Program.cs` в репозитории sherpa-onnx.

### Склейка

1. Слова с `mic.wav` получают спикера `Я`.
2. Для каждого слова с `system.wav` выбирается сегмент диаризации с максимальным перекрытием по времени. Если перекрытия нет — ближайший сегмент не дальше 1 с, иначе `Спикер ?`.
3. Таймкоды микрофонной дорожки сдвигаются на `offsetMs` из `session.json`.
4. Все слова сортируются по времени начала.
5. Подряд идущие слова одного спикера объединяются в реплику; новая реплика начинается при смене спикера или паузе дольше 1,5 с.
6. Спикеры системной дорожки нумеруются по порядку первого появления: `Спикер 1`, `Спикер 2` и т. д.

Это чистая функция без зависимостей от ОС и моделей: `Merge(IReadOnlyList<Word> mic, IReadOnlyList<Word> sys, IReadOnlyList<DiarSegment> segs, int offsetMs) → IReadOnlyList<Utterance>`. Покрыть юнит-тестами в первую очередь.

**Не использовать** `frontend/src-tauri/src/audio/stt.rs` из Meetily как образец диаризации: файл ссылается на модуль `pyannote`, которого в дереве нет, и не подключён в `audio/mod.rs` — это мёртвый код, унаследованный от Screenpipe.

## Структура решения, сборка и распространение

Ядро (распознавание, диаризация, склейка, вывод) не зависит от ОС; платформенный код изолирован в двух проектах захвата и подключается условно по RID.

```
meetrec/
  src/
    Meetrec.Cli/            # точка входа, System.CommandLine, консольный вывод
    Meetrec.Core/           # Session, Word, DiarSegment, Utterance, Merge, форматтеры md/json/srt
    Meetrec.Asr/            # Whisper.net, VAD, загрузка моделей
    Meetrec.Diarization/    # sherpa-onnx
    Meetrec.Capture.Windows/# NAudio, ICaptureBackend
    Meetrec.Capture.Mac/    # запуск и протокол meetrec-capture, ICaptureBackend
  native/
    meetrec-capture/        # Swift Package (macOS)
  tests/
    Meetrec.Core.Tests/     # Merge, форматтеры, пересчёт таймкодов после VAD
    fixtures/               # короткие WAV с известным числом спикеров
  .github/workflows/release.yml
```

**Публикация:**

```
dotnet publish src/Meetrec.Cli -c Release -r win-x64   --self-contained -p:PublishSingleFile=true
dotnet publish src/Meetrec.Cli -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
```

- Нативные библиотеки whisper.cpp и sherpa-onnx в single-file не встраиваются: `IncludeNativeLibrariesForSelfExtract=true` либо раздача папкой. Агент выбирает рабочий вариант и проверяет запуск на чистой машине без .NET.
- На macOS `meetrec-capture` кладётся рядом с `meetrec`; путь ищется относительно `AppContext.BaseDirectory`.
- Модели в архив не входят.

**CI (GitHub Actions)** по тегу `v*`: матрица `windows-latest` и `macos-latest` (arm64). Шаги: тесты `Meetrec.Core.Tests`, сборка Swift-хелпера на macOS, `dotnet publish`, упаковка в `meetrec-<ver>-win-x64.zip` и `meetrec-<ver>-osx-arm64.tar.gz`, публикация в GitHub Release.

**Подпись — вне первой версии.** Неподписанный бинарник на macOS после скачивания блокируется Gatekeeper; в README описать `xattr -d com.apple.quarantine meetrec meetrec-capture`. На Windows — предупреждение SmartScreen. Подпись и нотаризацию (Apple Developer, $99/год) добавить позже отдельной задачей.

## Этапы, приёмка, риски

Работу вести по этапам: каждый заканчивается чем-то запускаемым, и платформенный захват начинается только после того, как ядро проверено на готовых файлах.

| Этап | Результат | Критерий приёмки |
| --- | --- | --- |
| 1. Ядро | `meetrec transcribe file.wav` на обеих ОС | На записи 3 спикеров по 10 минут при `--speakers 3` не менее 85% слов отнесены верному спикеру; таймкоды реплик расходятся с реальностью не более чем на 0,5 с |
| 2. Две дорожки | `transcribe <dir>` с `mic.wav` + `system.wav` | Все слова с микрофона помечены «Я»; со сдвигом `offsetMs` реплики идут в верном порядке; юнит-тесты `Merge` зелёные |
| 3. Захват Windows | `meetrec record` / `run` | Часовая запись созвона в Teams или Zoom: длительность обоих WAV совпадает с реальной в пределах 1 с, в том числе при паузах без звука |
| 4. Захват macOS | `meetrec-capture` + `ICaptureBackend` | То же на macOS 14.4+; без разрешения — понятная ошибка, а не тишина; Ctrl+C всегда даёт валидные WAV |
| 5. Релиз | CI и архивы в GitHub Release | Архив запускается на чистой машине без .NET и без среды разработки; README описывает установку, разрешения и `xattr` |

**Нефункциональные требования:**

- Обработка часовой записи на M1/M2 с 16 ГБ и `large-v3-turbo` — не дольше 15 минут. На Windows без GPU допустимо медленнее; в README указать рекомендацию `--model small`.
- Пиковое потребление памяти — до 6 ГБ.
- Никаких сетевых запросов, кроме скачивания моделей.

**Риски:**

- **Эхо без наушников.** Голоса собеседников попадают в микрофон и дублируются репликами «Я». В v1 не решается; README рекомендует наушники. Кандидат на v2: удалять с микрофонной дорожки слова, совпадающие по тексту и времени с системной.
- **Качество эмбеддингов на русском.** Модели голоса обучены в основном на китайском или английском. Митигация — сравнение двух моделей на этапе 1 и флаги `--speakers` / `--threshold`.
- **Галлюцинации Whisper на тишине.** Митигация — VAD перед распознаванием.
- **Windows 10.** Whisper.net официально требует Windows 11; проверить и задокументировать.
- **Разрешения macOS для CLI.** Разрешение выдаётся терминалу; при запуске из IDE или другого терминала запрос появится заново.

## Референсы

Пути в Meetily указаны относительно `frontend/src-tauri/src/` на ветке `main` (состояние на сентябрь 2026). Код Meetily под MIT — его можно читать и переносить идеи, но копировать фрагменты только с сохранением уведомления о лицензии.

| Что смотреть | Где | Зачем |
| --- | --- | --- |
| [Захват системного звука macOS](https://github.com/Zackriya-Solutions/meetily/blob/main/frontend/src-tauri/src/audio/capture/core_audio.rs) | Meetily `audio/capture/core_audio.rs` | Process Tap + приватный агрегат, обход эха, IOProc, смена частоты |
| [Выбор бэкенда захвата](https://github.com/Zackriya-Solutions/meetily/blob/main/frontend/src-tauri/src/audio/capture/system.rs) | Meetily `audio/capture/system.rs` | Как macOS-тап и cpal разведены по платформам |
| [Устройства и loopback Windows](https://github.com/Zackriya-Solutions/meetily/blob/main/frontend/src-tauri/src/audio/devices/platform/windows.rs) | Meetily `audio/devices/platform/windows.rs` | Выбор WASAPI-хоста, устройства вывода в режиме loopback |
| [Потоки и форматы сэмплов](https://github.com/Zackriya-Solutions/meetily/blob/main/frontend/src-tauri/src/audio/stream.rs) | Meetily `audio/stream.rs` | Конвертация I16/I32/F32 при захвате |
| [Разрешения macOS](https://github.com/Zackriya-Solutions/meetily/blob/main/frontend/src-tauri/src/audio/permissions.rs) | Meetily `audio/permissions.rs`, `frontend/src-tauri/Info.plist` | Проверка и запрос разрешений, ключи `NS*UsageDescription` |
| [VAD](https://github.com/Zackriya-Solutions/meetily/blob/main/frontend/src-tauri/src/audio/vad.rs) | Meetily `audio/vad.rs` | Параметры Silero: 16 кГц, окна 30 мс, redemption time |
| [Параметры whisper.cpp](https://github.com/Zackriya-Solutions/meetily/blob/main/frontend/src-tauri/src/whisper_engine/whisper_engine.rs) | Meetily `whisper_engine/whisper_engine.rs` | Beam search, token timestamps, язык |
| [Смешивание дорожек](https://github.com/Zackriya-Solutions/meetily/blob/main/frontend/src-tauri/src/audio/pipeline.rs) | Meetily `audio/pipeline.rs` | Антипример: здесь дорожки сливаются, у нас — нет |
| [Whisper.net](https://github.com/sandrohanea/whisper.net) | README, `examples/` | Runtime-пакеты, порядок выбора бэкенда, `WhisperProcessorBuilder` |
| [Пример канальной диаризации](https://github.com/sandrohanea/whisper.net/blob/main/examples/Diarization/Program.cs) | Whisper.net `examples/Diarization` | Привязка сегментов к каналам по энергии — запасной вариант для «Я / они» |
| [Диаризация на C#](https://github.com/k2-fsa/sherpa-onnx/blob/master/dotnet-examples/offline-speaker-diarization/Program.cs) | sherpa-onnx `dotnet-examples/offline-speaker-diarization` | Готовый код `OfflineSpeakerDiarization` |
| [Модели сегментации](https://github.com/k2-fsa/sherpa-onnx/releases/tag/speaker-segmentation-models) | sherpa-onnx releases | `sherpa-onnx-pyannote-segmentation-3-0` |
| [Модели эмбеддингов](https://github.com/k2-fsa/sherpa-onnx/releases/tag/speaker-recongition-models) | sherpa-onnx releases | Кандидаты для сравнения на русском |
| [Модели Whisper](https://huggingface.co/ggerganov/whisper.cpp) | Hugging Face | GGML и CoreML-энкодеры |
| [AudioCap](https://github.com/insidegui/AudioCap) | GitHub | Пример Process Tap на Swift — основа для `meetrec-capture` |
| [NAudio](https://github.com/naudio/NAudio) | GitHub | `WasapiCapture`, `WasapiLoopbackCapture`, ресемплинг |
