const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const context = { btoa };
vm.runInNewContext(fs.readFileSync(path.join(__dirname, "..", "lib.js"), "utf8"), context);
const { parseSlots, encodePcm } = context.__meetrecLib;

const sdp = [
  "v=0",
  "m=audio 1 UDP/TLS/RTP/SAVPF 111",
  "a=mid:0",
  "a=ssrc:514247122 msid:mixedmslabel mixedlabelaudio0",
  "m=video 1 UDP/TLS/RTP/SAVPF 100",
  "a=mid:1",
  "a=ssrc:2396547951 msid:remote-video-1 remote-video-1",
  "m=audio 1 UDP/TLS/RTP/SAVPF 111",
  "a=mid:3",
  "a=ssrc:912173308 cname:x",
  "a=ssrc:912173308 msid:remote-audio-1 remote-audio-1",
  "m=audio 1 UDP/TLS/RTP/SAVPF 111",
  "a=mid:4",
  "a=ssrc:920311732 msid:remote-audio-2 remote-audio-2",
  "",
].join("\r\n");

test("parseSlots: bridge slots by mid, without the placeholder and video", () => {
  // Act
  const slots = parseSlots(sdp, 2);

  // Assert
  assert.deepEqual(JSON.parse(JSON.stringify(slots)), [
    { track: "in-2-3", ssrc: 912173308 },
    { track: "in-2-4", ssrc: 920311732 },
  ]);
});

test("parseSlots: peer to peer, the owner is the endpoint in the stream id", () => {
  // Arrange
  const p2p = "m=audio 1 UDP/TLS/RTP/SAVPF 111\r\na=mid:0\r\na=ssrc:42 msid:6db2446f-audio-0-1 6db2446f-audio-0-1\r\n";

  // Act
  const slots = parseSlots(p2p, 1);

  // Assert
  assert.deepEqual(JSON.parse(JSON.stringify(slots)), [{ track: "in-1-0", ssrc: 42, owner: "6db2446f" }]);
});

test("encodePcm: zeros give null", () => {
  // Act
  const pcm = encodePcm(new Float32Array(4096));

  // Assert
  assert.equal(pcm, null);
});

test("encodePcm: little-endian PCM16 in base64, clipped", () => {
  // Act
  const pcm = encodePcm(Float32Array.from([0, 1, -1, 2, 0.5]));

  // Assert
  const bytes = Buffer.from(pcm, "base64");
  assert.deepEqual([0, 2, 4, 6, 8].map((i) => bytes.readInt16LE(i)), [0, 32767, -32768, 32767, 16384]);
});
