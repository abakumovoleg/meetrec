# План рефакторинга

По итогам архитектурного аудита (2026-10-09). Цель — подготовить код к шагу 18 (Meetzone: N дорожек участников с известными именами) и убрать костыли. Каждая фаза — отдельный шаг: один коммит, обновление `docs/progress.md` и, если были решения, `docs/decisions.md`. Фазы R2–R4 не меняют поведение: результат на эталонной записи должен совпасть с базовым.

Отметки: `[ ]` — не начато, `[~]` — в работе, `[x]` — сделано (с хешем коммита в строке фазы).

## Эталон для проверки

- [x] Снять базовый результат до R2: `transcribe` реальной 8-минутной записи (`meeting.wav`, см. `CLAUDE.md`) и папки сессии `out-new/` с фиксированными параметрами (`--lang ru --model large-v3-turbo`, без имён, плюс один прогон с `--speakers 5`). Сохранить `transcript.md`/`json` в `/tmp/meetrec-eval/baseline/` (а после R3 — в `tools/eval/baseline/` вне git, если там голоса).
- Проверка после R2, R3, R4: `transcript.md` совпадает побайтно; `transcript.json` совпадает по содержимому с поправкой на новый формат (R4).
- Снято на R1 (`9ae091b`): `out-new/` не нашлась, вместо неё — сессия `/tmp/meetrec-eval/rec1009` (19 мин, две дорожки). Эталон и копии входов — `/tmp/meetrec-eval/baseline/`, сверка — `baseline/check.sh <meetrec>`; с R3 — `tools/eval/data/` (`cases.json`, `inputs/`, `baseline/`), сверка — `meetrec-eval run` + `compare`. Два прогона совпали побайтно — эталон детерминирован.

---

## R0. Мелочи — коммит: `6ed8d78`

- [x] `.gitignore`: `out*/` в корне.
- [x] `Hallucinations.IsReadableScript`: латиница целиком (`é`, `ü`, `ß`), а не только `a–z`; кириллица целиком (`і`, `ї`, `є`). Тест: «café», «naïve», «Київ» не выбрасываются, «그래요» выбрасывается.
- [x] Дубли: `MarkdownFormatter.FormatClock` → общий формат времени в Core; `ModelHttp` берёт версию из `AppVersion.Current`.
- [x] `WindowsCaptureBackend.Stream` → `CaptureStream`.

Готово, когда: `dotnet build`, `dotnet test` зелёные.

## R1. Единая обработка ошибок — коммит: `1dfbb03`

- [x] `MeetrecException` в `Meetrec.Core` — ошибка для пользователя (текст без стека); `CaptureException` наследует её.
- [x] Ошибки ввода бросают `MeetrecException` вместо `ArgumentException`/`FileNotFoundException`: `TranscribeInput.Resolve`, `ModelCatalog.Find`, `NamesCommand` («нет transcript.json»), `SpeakerDiarizer` (частота).
- [x] Ошибки загрузки моделей (`HttpRequestException`, обрыв в `ModelStore`) и чтения файлов (`JsonException`, `InvalidDataException`, `IOException`) оборачиваются в `MeetrecException` там, где известен контекст (какой файл, какая модель).
- [x] Один обработчик в `Program`: `MeetrecException` → «Ошибка: …», код 1; `OperationCanceledException` при Ctrl+C → «Прервано.», 130; прочее → сообщение и стек в stderr, код 70. Убрать списки `catch … when (e is …)` из команд.
- [x] Тесты на перевод исключений, где он в Core.

Готово, когда: в командах нет фильтров по типам исключений; ошибка Whisper.net/sherpa-onnx не роняет процесс без сообщения.

## R2. Распознавание: место, параметры, тесты — коммит: `bdc1089`

- [x] Перенести `SpeechMask`, `SpeechRepair`, `SpeechLevel`, `Hallucinations`, `SpeechRegion` из `Meetrec.Core.Audio` в `Meetrec.Asr` (namespace `Meetrec.Asr.Heuristics` или плоско); тесты — следом.
- [x] Тестовый проект ссылается на `Meetrec.Asr` и `Meetrec.Diarization` (решить: добавить ссылки в `Meetrec.Core.Tests` или переименовать в `Meetrec.Tests` — записать в decisions).
- [x] Интерфейсы `ITranscriber` (окно → слова + язык) и `IVad` (сэмплы → регионы); `WhisperTranscriber` и `SileroVad` их реализуют, `SpeechRecognizer` принимает интерфейсы.
- [x] `RecognitionOptions` — все пороги из `Default*` (громкость, маска, ремонт, отступ старта, параметры VAD, фильтр галлюцинаций) и подсказка по языку (вместо `language == "ru"` в `WhisperTranscriber`). Значения по умолчанию — текущие, с теми же комментариями-обоснованиями.
- [x] Тесты `SpeechRecognizer` на фейках:
  - [x] тишина / тихие регионы → Whisper не вызывается;
  - [x] основной проход стартует за `Lead` до первой речи, таймкоды окна сдвигаются обратно;
  - [x] регион без слов распознаётся повторно отдельным окном и заменяет пустоту;
  - [x] результат повторного окна тоже проходит фильтр галлюцинаций;
  - [x] слова вне речи отбрасываются, статистика (`RecognitionStats`) сходится.
- [x] Окно для Whisper без копии всей дорожки (`clip.Samples[from..to]`): проверить, принимает ли Whisper.net `ReadOnlyMemory<float>`; если нет — оставить и записать в decisions.

Готово, когда: эталон совпал; оркестрация покрыта тестами; в `Meetrec.Core` нет логики, специфичной для Whisper.

## R3. Конвейер расшифровки из CLI в библиотеку — коммит: `c7b5330`

- [x] Проект `Meetrec.Pipeline` (ссылается на Core, Asr, Diarization): `TranscriptionPipeline.RunAsync(input, TranscriptionOptions, ITranscriptionObserver, ct)` → `TranscriptionResult` (реплики, статистика по дорожкам, время этапов).
- [x] `TranscriptionOptions` = язык, модель Whisper, `RecognitionOptions`, `DiarizationSettings`.
- [x] `ITranscriptionObserver`: начало/прогресс/конец этапа, статистика дорожки, предупреждения (нет `session.json`). Скачивание моделей — отдельный вызов до конвейера с `IProgress<DownloadProgress>`.
- [x] `TranscribeCommand` — только разбор опций, вызов конвейера, отрисовка (`Diagnostics`, `ConsoleStageProgress`), запись файлов, диалог имён.
- [x] Диалог имён получает уже загруженную системную дорожку вместо повторного `AudioLoader.LoadMono16k` в `ClipPlayer` (для `names` — загрузка как сейчас).
- [x] `AudioLoader.ReadAll`: массив точного размера вместо `List<float>` + `ToArray()` (пик памяти ×2 на часовой записи).
- [x] Стенд замеров в репозиторий: `tools/eval` (консольный проект в `Meetrec.slnx`, не публикуется) — прогон конвейера с `RecognitionOptions` из аргументов/файла, сравнение с эталоном. Данные и результаты — вне git.
- [x] Тест конвейера на фейках `ITranscriber`/`IVad`/диаризатора: две дорожки со сдвигом → ожидаемые реплики.

Готово, когда: эталон совпал; `TranscribeCommand` < 60 строк; стенд из `/tmp/meetrec-eval/harness` воспроизводится через `tools/eval`.

## R4. Модель дорожек и спикеров — коммит: —

Главная фаза: снимает допущение «микрофон + системный звук» и разделяет идентификатор и имя спикера.

**Формат и раскладка сессии**

- [x] `SessionLayout` — единственное место с именами файлов (`mic.wav`, `system.wav`, `session.json`, `transcript.*`, `<имя>.transcript.*`); убрать константы из `TranscribeInput`, `TranscriptWriter`, `SessionDirectory`.
- [x] `session.json` v2: `schemaVersion: 2`, `tracks: [{ file, offsetMs, source: mic|system|participant, speaker?: { id, name } }]`. Чтение v1 (без `schemaVersion`) → две дорожки, `offsetMs` микрофона из старого поля. Запись — только v2. Тесты на оба.
- [x] `TranscribeInput` → список `SourceTrack(Id, Path, Offset, SpeakerSource)`, где `SpeakerSource` = `Fixed(SpeakerId, Name)` | `Diarized`. Одиночный WAV — одна `Diarized`-дорожка; папка сессии — `mic` как `Fixed("me", "Я")` + `system` как `Diarized`.

**Спикеры**

- [x] `SpeakerId` — стабильный идентификатор: `me`, `sys:1`, `sys:2`, `unknown`, для Meetzone — `mz:<login>`. В `TranscriptWord` — `SpeakerId` и `TrackId` (строка) вместо `Speaker`/`Track`/`Label`; удалить `SpeakerLabel`.
- [x] Имена — отдельная таблица `SpeakerId → Name` у расшифровки (`Transcript { Speakers, Utterances }`); `Speakers.Numbered`/`Me`/`Unknown` — только имена по умолчанию.
- [x] `TranscriptMerger.Merge(IReadOnlyList<TrackWords>, сегменты диаризации по дорожке)`: сдвиг каждой дорожки — свой; `Fixed` → его id, `Diarized` → `trackId:N` по сегментам; нумерация спикеров — по первому появлению, как сейчас.
- [x] Группировка реплик по `SpeakerId` + дорожке; переименование в одно имя по-прежнему склеивает соседние реплики (как сейчас) — проверить тестом.
- [x] `SpeakerNames`, `SpeakerProfiles`, `NameHints`: «можно назвать» = спикер из `Diarized`-дорожки и не `unknown` (вместо `Track.System`).
- [x] `DiarizationSettings.Choose(totalSpeakers, fixedSpeakers, threshold)`: кластеров = всего − число `Fixed`-спикеров (сейчас `hasMicTrack ? total − 1`).
- [x] Форматтеры md/srt берут имя из таблицы; вывод md/srt не меняется.

**`transcript.json` v2**

- [x] `schemaVersion: 2`, `speakers: [{ id, name, track }]`, в репликах и словах — `speakerId`, `track` (строка). Отображаемое имя в реплике оставить для читаемости (`speaker`), но читать только `speakerId` + таблицу.
- [x] `TranscriptReader` читает v1: `label ?? speaker` → id (`Я` → `me`, `Спикер N` → `sys:N`, `Спикер ?` → `unknown`), имя → таблица. Тест на json, записанном текущей версией (положить фикстуру).
- [x] `meetrec names` на старой расшифровке пересохраняет её в v2.

**Проверка**

- [x] Эталон: md/srt совпали побайтно, json эквивалентен (скрипт сравнения в `tools/eval`).
- [x] Синтетическая сессия с тремя `Fixed`-дорожками (имитация Meetzone, `say -v Milena`) склеивается по времени с именами без диаризации — тест конвейера на фейках.
- [x] README: раздел про формат `session.json`/`transcript.json`.

Готово, когда: в коде нет `Track.Mic`/`Track.System` вне чтения v1 и записи сессии; шаг 18 сводится к новому источнику дорожек.

## R5. Платформа и протокол хелпера — коммит: —

- [ ] `Platform` в CLI — одна фабрика: бэкенд захвата, вывод фрагментов (`afplay`/WASAPI), нужен ли CoreML-энкодер, понижение приоритета. Убрать разбросанные `OperatingSystem.Is*` из команд.
- [ ] Протокол хелпера: первое сообщение `{"t":"hello","protocol":1,"version":"…"}`; CLI отказывает с понятным текстом при несовпадении версии протокола. Swift + тест в `CaptureKit`, C# + тест `HelperMessage`.
- [ ] Неизвестные/битые строки протокола не теряются молча: копятся и попадают в текст ошибки при сбое хелпера.
- [ ] Перенести `CaptureTrack` и `Mono16kResampler` в `Meetrec.Capture.Windows` (нужны только WASAPI); `LevelBar` — в CLI. В `Meetrec.Core.Capture` остаются контракты (`ICaptureBackend`, типы, `CaptureHints`).
- [ ] `run`: если `StopAsync` упал, но WAV-файлы есть и читаются — предупредить и всё равно расшифровать. **Решение за пользователем** — спросить перед реализацией.

Готово, когда: `Meetrec.Core` не ссылается на NAudio (кроме `AudioLoader`) — либо записано в decisions, почему ссылается.

---

## Вне плана (отмечено при аудите, не делаем без повода)

- Квадратичные проходы (`TranscriptMerger.FindSpeaker`, `SpeechMask.Apply`, `SpeechRepair.FindSparse`) — на часовой записи это десятки миллионов сравнений, незаметно. Вернуться, если N дорожек Meetzone даст заметное время.
- Консольный ввод-вывод в `NamesDialog`/`RecordingStatus` напрямую через `Console` — тестировать диалог через `TextReader`/`TextWriter`, если он ещё вырастет.
- `Interrupts.NextPhase` не освобождает прошлый `CancellationTokenSource` — живёт до конца процесса, утечки нет.
