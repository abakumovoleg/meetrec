// Pure helpers of page.js, kept apart so that `node --test` can check them.
(() => {
  const PROTOCOL = 1;

  // Audio m-sections of a remote SDP → recordable slots. With the videobridge (ssrc rewriting) a slot is
  // "remote-audio-N" and its owner comes later from AudioSourcesMap; peer to peer, the stream id starts with the
  // sender's endpoint. "mixedmslabel" is the bridge's always-silent placeholder.
  const parseSlots = (sdp, pcId) => {
    const slots = [];
    let audio = false;
    let mid = null;
    for (const line of sdp.split(/\r?\n/)) {
      if (line.startsWith("m=")) {
        audio = line.startsWith("m=audio");
        mid = null;
      } else if (line.startsWith("a=mid:")) {
        mid = line.slice(6);
      } else if (audio && mid !== null) {
        const m = /^a=ssrc:(\d+) msid:(\S+)/.exec(line);
        if (!m || slots.some((s) => s.track === `in-${pcId}-${mid}`)) continue;
        const [, ssrc, stream] = m;
        if (stream.startsWith("remote-audio")) {
          slots.push({ track: `in-${pcId}-${mid}`, ssrc: Number(ssrc) });
        } else {
          const p2p = /^([0-9a-f]{8})-audio/.exec(stream);
          if (p2p) slots.push({ track: `in-${pcId}-${mid}`, ssrc: Number(ssrc), owner: p2p[1] });
        }
      }
    }
    return slots;
  };

  // Float samples → base64 of little-endian PCM16, or null for a block of exact zeros: the bridge does not
  // forward silence, so most blocks of a slot are zeros, and meetrec places blocks by time.
  const encodePcm = (samples) => {
    const bytes = new Uint8Array(samples.length * 2);
    const view = new DataView(bytes.buffer);
    let silent = true;
    for (let i = 0; i < samples.length; i++) {
      const s = Math.max(-1, Math.min(1, samples[i]));
      const v = s < 0 ? Math.round(s * 0x8000) : Math.round(s * 0x7fff);
      if (v !== 0) silent = false;
      view.setInt16(i * 2, v, true);
    }
    if (silent) return null;
    let binary = "";
    for (let i = 0; i < bytes.length; i += 0x8000) {
      binary += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
    }
    return btoa(binary);
  };

  // XMPP presence of the conference room → participant, or null for any other stanza. Endpoint is the resource
  // of the room JID; status 110 marks the user's own presence.
  const parsePresence = (doc) => {
    const presence = doc.documentElement;
    if (!presence || presence.localName !== "presence") return null;
    const from = presence.getAttribute("from") || "";
    if (!from.includes("@muc.") || !from.includes("/")) return null;
    const text = (name) => presence.getElementsByTagName(name)[0]?.textContent || null;
    const statuses = [...presence.getElementsByTagName("status")].map((s) => s.getAttribute("code"));
    return {
      t: "presence",
      endpoint: from.slice(from.lastIndexOf("/") + 1),
      name: text("nick"),
      login: text("login"),
      own: statuses.includes("110"),
      left: presence.getAttribute("type") === "unavailable",
    };
  };

  globalThis.__meetrecLib = { PROTOCOL, parseSlots, encodePcm, parsePresence };
})();
