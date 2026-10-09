// Runs in the page (world MAIN, document_start) to see what Meetzone's own code sees: peer connections, the XMPP
// signalling socket and the videobridge data channel. Talks to bridge.js through window messages. The conference
// state is collected from the start; audio is recorded only between "start" and "stop" from meetrec.
(() => {
  if (window.__meetrecPage) return;
  window.__meetrecPage = true;

  const { PROTOCOL, parseSlots, encodePcm, parsePresence } = globalThis.__meetrecLib;
  const VERSION = "0.1.0";
  const SAMPLE_RATE = 16000;
  const BLOCK = 4096;
  const POLL_MS = 250;

  const pcs = new Map();
  const people = new Map();
  const owners = new Map();
  const slots = new Map();
  let pcSeq = 0;
  let announced = false;
  let rec = null;

  const post = (msg) => window.postMessage({ __meetrec: "page", msg }, location.origin);

  // The bridge connects to the extension only once there is a call in this frame.
  const announce = () => {
    if (announced) return;
    announced = true;
    post({ t: "hello", protocol: PROTOCOL, version: VERSION, url: location.href });
  };

  const snapshot = () => {
    for (const p of people.values()) post(p);
    if (owners.size) post({ t: "sources", sources: [...owners].map(([ssrc, owner]) => ({ ssrc, owner })) });
    if (slots.size) post({ t: "slots", slots: [...slots.values()] });
  };

  const onPresence = (data) => {
    if (typeof data !== "string" || !data.startsWith("<presence")) return;
    let presence;
    try {
      presence = parsePresence(new DOMParser().parseFromString(data, "text/xml"));
    } catch {
      return;
    }
    if (!presence) return;
    people.set(presence.endpoint, presence);
    if (rec) post(presence);
  };

  const onBridgeMessage = (data) => {
    if (typeof data !== "string" || !data.includes("AudioSourcesMap")) return;
    try {
      const message = JSON.parse(data);
      if (message.colibriClass !== "AudioSourcesMap") return;
      const sources = message.mappedSources.map((s) => ({ ssrc: s.ssrc, owner: s.owner }));
      for (const s of sources) owners.set(s.ssrc, s.owner);
      if (rec) post({ t: "sources", sources });
    } catch {}
  };

  const onRemoteSdp = (pcId, sdp) => {
    const found = parseSlots(sdp || "", pcId);
    for (const s of found) slots.set(s.track, s);
    if (found.length && rec) post({ t: "slots", slots: found });
  };

  const OrigPC = window.RTCPeerConnection;
  class MeetrecPC extends OrigPC {
    constructor(...args) {
      super(...args);
      this.__meetrecId = ++pcSeq;
      pcs.set(this.__meetrecId, this);
      this.addEventListener("datachannel", (e) =>
        e.channel.addEventListener("message", (m) => onBridgeMessage(m.data)),
      );
    }
    createDataChannel(...args) {
      const channel = super.createDataChannel(...args);
      channel.addEventListener("message", (m) => onBridgeMessage(m.data));
      return channel;
    }
    setRemoteDescription(desc, ...rest) {
      announce();
      onRemoteSdp(this.__meetrecId, desc?.sdp);
      return super.setRemoteDescription(desc, ...rest);
    }
  }
  window.RTCPeerConnection = MeetrecPC;

  const OrigWS = window.WebSocket;
  class MeetrecWS extends OrigWS {
    constructor(...args) {
      super(...args);
      this.addEventListener("message", (m) => onPresence(m.data));
    }
  }
  window.WebSocket = MeetrecWS;

  // Sent on every start: a meetrec started anew while the page kept recording needs the clock too.
  const announceStart = () => {
    // Right after creation the output timestamp is still zero; the current time is then close enough.
    const ts = rec.ctx.getOutputTimestamp();
    const [at, perf] = ts.performanceTime > 0 ? [ts.contextTime, ts.performanceTime] : [rec.ctx.currentTime, performance.now()];
    post({ t: "started", context: rec.context, at, wall: performance.timeOrigin + perf });
  };

  const start = async () => {
    if (rec) {
      snapshot();
      announceStart();
      return;
    }
    const ctx = new AudioContext({ sampleRate: SAMPLE_RATE });
    const sink = ctx.createGain();
    sink.gain.value = 0;
    sink.connect(ctx.destination);
    rec = { ctx, sink, tracks: new Map(), timer: null, context: crypto.randomUUID() };
    if (ctx.state !== "running") {
      post({ t: "error", message: "звук страницы приостановлен браузером — кликните по странице созвона" });
      const resume = () => ctx.resume();
      document.addEventListener("click", resume, { capture: true, once: true });
      await ctx.resume().catch(() => {});
      while (rec?.ctx === ctx && ctx.state !== "running") await new Promise((r) => setTimeout(r, 200));
      if (rec?.ctx !== ctx) return;
    }
    snapshot();
    announceStart();
    rec.timer = setInterval(poll, POLL_MS);
    poll();
  };

  const stop = () => {
    if (!rec) return;
    clearInterval(rec.timer);
    for (const t of rec.tracks.values()) {
      t.source?.disconnect();
      t.processor.disconnect();
    }
    rec.ctx.close();
    rec = null;
  };

  // A slot keeps its MediaStreamTrack, but the own outgoing track changes with the microphone (replaceTrack),
  // so tracks are re-attached whenever they differ.
  const attach = (key, track) => {
    let t = rec.tracks.get(key);
    if (t && t.track === track) return;
    if (!t) {
      const processor = rec.ctx.createScriptProcessor(BLOCK, 1, 1);
      processor.onaudioprocess = (e) => {
        if (!rec) return;
        const pcm = encodePcm(e.inputBuffer.getChannelData(0));
        if (pcm) post({ t: "audio", track: key, at: e.playbackTime, pcm });
      };
      processor.connect(rec.sink);
      t = { processor, source: null, track: null };
      rec.tracks.set(key, t);
    }
    t.source?.disconnect();
    t.track = track;
    t.source = null;
    if (!track) return;
    try {
      t.source = rec.ctx.createMediaStreamSource(new MediaStream([track]));
      t.source.connect(t.processor);
    } catch (err) {
      post({ t: "error", message: `не удалось записать дорожку ${key}: ${err}` });
    }
  };

  const poll = () => {
    if (!rec) return;
    let outgoing = null;
    for (const [id, pc] of pcs) {
      if (pc.connectionState === "closed") {
        pcs.delete(id);
        continue;
      }
      for (const t of pc.getTransceivers()) {
        if (t.mid !== null && slots.has(`in-${id}-${t.mid}`) && t.receiver.track?.kind === "audio") {
          attach(`in-${id}-${t.mid}`, t.receiver.track);
        }
        if (t.sender.track?.kind === "audio" && t.sender.track.readyState === "live") outgoing ??= t.sender.track;
      }
    }
    attach("out", outgoing);
  };

  window.addEventListener("message", (e) => {
    if (e.source !== window || e.data?.__meetrec !== "extension") return;
    const msg = e.data.msg;
    if (msg.t === "start") start().catch((err) => post({ t: "error", message: String(err) }));
    else if (msg.t === "stop") stop();
    else if (msg.t === "reconnect" && announced) {
      stop();
      post({ t: "hello", protocol: PROTOCOL, version: VERSION, url: location.href });
    }
  });
})();
