// Isolated content script: relays between page.js (window messages) and the service worker (a runtime port).
// The worker is restarted by the browser now and then, which breaks the port; then the page is asked to say
// hello again so that meetrec gets the full state anew.
(() => {
  const PING_MS = 20000;
  let port = null;
  let ping = null;

  const toPage = (msg) => window.postMessage({ __meetrec: "extension", msg }, location.origin);

  // After the extension is reloaded the old content script has no runtime left; it stays silent until the page
  // is reloaded too.
  const connect = () => {
    try {
      port = chrome.runtime.connect({ name: "meetzone" });
    } catch {
      return;
    }
    port.onMessage.addListener(toPage);
    port.onDisconnect.addListener(() => {
      port = null;
      clearInterval(ping);
      setTimeout(() => {
        connect();
        toPage({ t: "reconnect" });
      }, 1000);
    });
    // Keeps the worker alive and lets it retry meetrec while nobody is recording.
    ping = setInterval(() => port?.postMessage({ t: "ping" }), PING_MS);
  };

  window.addEventListener("message", (e) => {
    if (e.source !== window || e.data?.__meetrec !== "page") return;
    if (!port) connect();
    try {
      port?.postMessage(e.data.msg);
    } catch {}
  });
})();
