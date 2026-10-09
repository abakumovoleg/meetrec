// Service worker: the only part of the extension that may open ws://127.0.0.1 regardless of the page's CSP.
// One call is recorded at a time: the first tab that says hello; others wait until it goes away.
const MEETREC_URL = "ws://127.0.0.1:47823";
const RETRY_MS = 3000;

const ports = [];
const hellos = new Map();
let active = null;
let ws = null;
let retry = null;

const activate = () => {
  if (active && ports.includes(active)) return;
  active = ports.find((p) => hellos.has(p)) ?? null;
  if (!active) return;
  if (ws?.readyState === WebSocket.OPEN) send(hellos.get(active));
  else connect();
};

const connect = () => {
  if (ws || !active) return;
  clearTimeout(retry);
  const socket = new WebSocket(MEETREC_URL);
  ws = socket;
  socket.onopen = () => socket.send(JSON.stringify(hellos.get(active)));
  socket.onmessage = (e) => {
    try {
      active?.postMessage(JSON.parse(e.data));
    } catch {}
  };
  socket.onclose = () => {
    if (ws !== socket) return;
    ws = null;
    active?.postMessage({ t: "stop" });
    if (active) retry = setTimeout(connect, RETRY_MS);
  };
};

const send = (msg) => {
  if (ws?.readyState === WebSocket.OPEN) ws.send(JSON.stringify(msg));
};

chrome.runtime.onConnect.addListener((port) => {
  if (port.name !== "meetzone") return;
  ports.push(port);
  port.onMessage.addListener((msg) => {
    if (msg.t === "ping") {
      if (port === active) connect();
      return;
    }
    if (msg.t === "hello") {
      hellos.set(port, msg);
      if (port === active) send(msg);
      activate();
      return;
    }
    if (port === active) send(msg);
  });
  port.onDisconnect.addListener(() => {
    ports.splice(ports.indexOf(port), 1);
    hellos.delete(port);
    if (port !== active) return;
    send({ t: "closed" });
    active = null;
    activate();
  });
});
