# Расширение meetrec для Meetzone

Отдаёт `meetrec record --meetzone` звук созвона Meetzone дорожкой на каждого участника. Устройство Meetzone и замеры — `docs/decisions.md`, разделы «Meetzone».

- `page.js` (+ `lib.js`) — в мире страницы с `document_start`: подменяет `RTCPeerConnection` и `WebSocket`, чтобы видеть presence из XMPP, `AudioSourcesMap` из data channel и SDP. Состояние созвона собирает всегда, звук пишет только между `start` и `stop` от meetrec: `AudioContext` на 16 кГц, `ScriptProcessorNode` на каждую входящую дорожку-слот и на свой исходящий трек.
- `bridge.js` — изолированный content script: окно ↔ порт service worker.
- `worker.js` — service worker: единственный, кому CSP страницы не мешает открыть `ws://127.0.0.1:47823`. Пишется один созвон — первая вкладка, приславшая `hello`.

Тесты чистых функций: `node --test 'browser/meetzone/test/*.test.js'`.

## Протокол (версия 1)

JSON-сообщения по WebSocket. Расширение → meetrec:

| `t` | Поля | |
| --- | --- | --- |
| `hello` | `protocol`, `version`, `url` | первое сообщение; meetrec отвечает `start` или `stop` |
| `presence` | `endpoint`, `name`, `login`, `own`, `left` | участник из XMPP presence; `own` — свой (status 110) |
| `sources` | `sources: [{ssrc, owner}]` | `AudioSourcesMap`: чей голос сейчас идёт через ssrc слота |
| `slots` | `slots: [{track, ssrc, owner?}]` | входящие аудиодорожки `in-<pc>-<mid>` из удалённого SDP; `owner` — только для P2P, где msid начинается с endpoint |
| `started` | `context`, `at`, `wall` | запись началась: время `AudioContext` и соответствующее ему wall clock, мс |
| `audio` | `track`, `at`, `pcm` | блок 4096 сэмплов, 16 кГц, PCM16 LE в base64, начинается в `at` секунд `AudioContext`; `track` — слот или `out` (свой исходящий трек). Блоки из одних нулей не шлются |
| `error` | `message` | ошибка на странице |
| `closed` | | вкладка созвона закрылась |

meetrec → расширение: `start` (прислать состояние и писать звук), `stop`.

meetrec принимает соединение только с `Origin: chrome-extension://…`. Номер протокола меняется только при несовместимом изменении; неизвестные типы сообщений meetrec пропускает.
