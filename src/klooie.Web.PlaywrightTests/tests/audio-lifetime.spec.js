import { test, expect } from "@playwright/test";
import { openFresh } from "../lib/klooieWeb.js";

test.use({ serviceWorkers: "block" });

test("late-loading loop does not start after its playback id has been stopped", async ({ page }) => {
  const delayedAudio = createSilentWav();
  const soundUrl = `/slow-lifetime-expired-${Date.now()}.mp3`;
  let releaseAudioResponse;
  const audioResponseReleased = new Promise(resolve => releaseAudioResponse = resolve);
  let audioRequestStarted;
  const audioRequestHasStarted = new Promise(resolve => audioRequestStarted = resolve);

  await page.addInitScript(() => {
    window.__klooieAudioStarts = [];
    class FakeAudioNode {
      connect() { }
      disconnect() { }
    }

    class FakeBufferSource extends FakeAudioNode {
      constructor(context) {
        super();
        this.context = context;
        this.loop = false;
        this.onended = undefined;
      }

      start(when, offset) {
        window.__klooieAudioStarts.push({ when, offset, loop: this.loop });
      }

      stop() {
        this.onended?.();
      }
    }

    class FakeAudioContext {
      constructor() {
        this.currentTime = 0;
        this.destination = new FakeAudioNode();
        this.state = "running";
      }

      createBufferSource() {
        return new FakeBufferSource(this);
      }

      createGain() {
        const node = new FakeAudioNode();
        node.gain = { value: 1 };
        return node;
      }

      createStereoPanner() {
        const node = new FakeAudioNode();
        node.pan = { value: 0 };
        return node;
      }

      decodeAudioData() {
        return Promise.resolve({ duration: 1 });
      }

      resume() {
        this.state = "running";
        return Promise.resolve();
      }
    }

    window.AudioContext = FakeAudioContext;
    window.webkitAudioContext = FakeAudioContext;
  });

  await page.route("**/slow-lifetime-expired-*.mp3", async route => {
    audioRequestStarted();
    await audioResponseReleased;
    await route.fulfill({
      status: 200,
      contentType: "audio/wav",
      body: delayedAudio
    });
  });

  await openFresh(page);
  await expect.poll(async () => await page.evaluate(() => !!window.klooieAssets?.play)).toBe(true);

  await page.evaluate(() => {
    window.__klooieAudioStarts.length = 0;
    window.klooieAssets.clearAudioCache();
  });
  await page.evaluate(url => window.klooieAssets.play(987654, url, 1, 0, true, true, false, null), soundUrl);

  await Promise.race([
    audioRequestHasStarted,
    new Promise((_, reject) => setTimeout(() => reject(new Error("Timed out waiting for delayed audio request")), 10_000))
  ]);
  await page.evaluate(() => window.klooieAssets.stop(987654));
  releaseAudioResponse();

  await expect.poll(async () => await page.evaluate(() => ({
    active: window.klooieAssets.active.has("987654"),
    music: window.klooieAssets.music.has("987654"),
    starts: window.__klooieAudioStarts.length
  })), {
    message: "stopped playback should stay stopped after delayed audio load completes"
  }).toEqual({ active: false, music: false, starts: 0 });
});

function createSilentWav() {
  const sampleRate = 8000;
  const sampleCount = 800;
  const dataSize = sampleCount * 2;
  const buffer = Buffer.alloc(44 + dataSize);
  buffer.write("RIFF", 0);
  buffer.writeUInt32LE(36 + dataSize, 4);
  buffer.write("WAVE", 8);
  buffer.write("fmt ", 12);
  buffer.writeUInt32LE(16, 16);
  buffer.writeUInt16LE(1, 20);
  buffer.writeUInt16LE(1, 22);
  buffer.writeUInt32LE(sampleRate, 24);
  buffer.writeUInt32LE(sampleRate * 2, 28);
  buffer.writeUInt16LE(2, 32);
  buffer.writeUInt16LE(16, 34);
  buffer.write("data", 36);
  buffer.writeUInt32LE(dataSize, 40);
  return buffer;
}
