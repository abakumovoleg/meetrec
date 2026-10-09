# meetrec-eval — стенд замеров

Консольный проект в решении, не публикуется. Решения о параметрах распознавания принимаются по его замерам (см. `docs/decisions.md`).

Данные — в `tools/eval/data/` (в `.gitignore`): реальные записи, эталоны и сгенерированные фикстуры в репозиторий не попадают.

```sh
dotnet build tools/eval
E=tools/eval/bin/Debug/net10.0/meetrec-eval

$E options > opts.json                         # параметры по умолчанию; в файле можно оставить только изменяемые
$E run tools/eval/data/cases.json out [--options opts.json]
$E compare tools/eval/data/baseline out        # md/json/srt побайтно, код 1 при расхождении
$E recognize meeting.wav words.json [--options opts.json] [--lang ru] [--model large-v3-turbo]
$E timing tools/eval/data/fixtures [--options opts.json]   # таймкоды слов на синтетике, фикстуры создаются через say
$E meetzone meetzone/<probe>.json tools/eval/data/inputs/meetzone   # сессия участников из записи отладочного расширения
$E meetzone-replay meetzone/<probe>.json   # проиграть ту же запись в запущенный `meetrec record --meetzone`, как расширение
```

`cases.json` — список случаев, пути относительно файла:

```json
[
  { "name": "meeting", "input": "inputs/meeting.wav" },
  { "name": "meeting-s5", "input": "inputs/meeting.wav", "speakers": 5 },
  { "name": "session", "input": "inputs/session" }
]
```

Необязательные поля: `speakers`, `threshold`, `language` (`ru`), `model` (`large-v3-turbo`). Вывод случая — `out/<name>/` с теми же именами файлов, что у `meetrec transcribe`, и `stats.txt` (статистика дорожек, время этапов).

Эталон — вывод `run` (или `meetrec transcribe`) на коде до изменения: проверка «поведение не изменилось» — `run` + `compare`. Конвейер детерминирован: два прогона совпадают побайтно.
