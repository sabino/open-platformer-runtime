import assert from "node:assert/strict";
import { webcrypto } from "node:crypto";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import vm from "node:vm";

const script = readFileSync(new URL("../../web/app.js", import.meta.url), "utf8");

function loader() {
  const elements = new Map();
  const listeners = new Map();
  function element(id) {
    return {
      id, style: {}, hidden: false, src: "", contentWindow: {}, listeners: new Map(),
      classList: { add() {}, remove() {}, toggle() {} },
      setAttribute() {}, removeAttribute(name) { if (name === "src") this.src = ""; },
      addEventListener(name, action) { this.listeners.set(name, action); },
      cloneNode() { return element(id); },
      replaceWith(next) { elements.set(id, next); },
    };
  }
  const get = id => {
    if (!elements.has(id)) elements.set(id, element(id));
    return elements.get(id);
  };
  const window = {
    setTimeout, clearTimeout,
    addEventListener(name, action) {
      if (!listeners.has(name)) listeners.set(name, new Set());
      listeners.get(name).add(action);
    },
    removeEventListener(name, action) { listeners.get(name)?.delete(action); },
  };
  const context = vm.createContext({
    document: { getElementById: get, querySelector: get }, window,
    crypto: webcrypto, Uint8Array, performance, setInterval, clearInterval,
    requestAnimationFrame: callback => queueMicrotask(callback),
  });
  vm.runInContext(script, context, { filename: "web/app.js" });
  return {
    get, value: code => vm.runInContext(code, context),
    call: (name, ...args) => vm.runInContext(name, context)(...args),
    message: (source, data) => {
      for (const action of listeners.get("message") ?? []) action({ source, data });
    },
  };
}

test("unsupported bytes and copier headers remain blocked", async () => {
  const page = loader();
  const rom = await page.call("inspectRom", new Uint8Array(0x80000));
  assert.equal(rom.isSupported, false);
  assert.equal(rom.isExpectedSize, true);
  const headered = await page.call("inspectRom", new Uint8Array(0x80200));
  assert.equal(headered.hasCopierHeader, true);
  assert.equal(headered.canonicalSize, 0x80000);
  assert.equal(headered.sha1, rom.sha1);
  const manifest = page.call("buildBrowserManifest", { name: "test.sfc" }, rom);
  assert.equal(manifest.runtime_status.playable_browser_runtime, false);
  assert.equal(manifest.importer.local_only, true);
});

test("bridge passes original bytes and resolves only the current iframe", async () => {
  const page = loader();
  const frame = page.get("runtimeFrame");
  const bytes = new Uint8Array([1, 2, 3]);
  let request;
  frame.contentWindow.openPlatformerRuntimeGodotCommand = (...args) => { request = args; };
  const pending = page.call("sendRomToGodot", "local.sfc", bytes);
  assert.deepEqual(request, ["rom", bytes, "local.sfc"]);
  assert.equal(page.value("runtimeRomReady"), false);
  page.message({}, { type: "open-platformer-runtime-rom-ready", indexedLevelCount: 512 });
  assert.equal(page.value("runtimeRomReady"), false);
  page.message(frame.contentWindow, {
    type: "open-platformer-runtime-rom-ready", sha1: "test-hash",
    indexedLevelCount: 512, verifiedTerrainCount: 4,
  });
  const result = await pending;
  assert.equal(result.indexedLevelCount, 512);
  assert.equal(result.verifiedTerrainCount, 4);
  assert.equal(result.sha1, "test-hash");
});

test("missing or throwing bridges reject without leaving an orphan promise", async () => {
  const page = loader();
  await assert.rejects(page.call("sendRomToGodot", "test.sfc", new Uint8Array()), /not available/);
  assert.equal(page.value("pendingRomReady"), null);
  page.get("runtimeFrame").contentWindow.openPlatformerRuntimeGodotCommand = () => { throw "marshal failure"; };
  await assert.rejects(page.call("sendRomToGodot", "test.sfc", new Uint8Array()), /marshal failure/);
  assert.equal(page.value("pendingRomReady"), null);
});

test("runtime errors reach the import caller", async () => {
  const page = loader();
  const frame = page.get("runtimeFrame");
  frame.contentWindow.openPlatformerRuntimeGodotCommand = () => {};
  const pending = page.call("sendRomToGodot", "test.sfc", new Uint8Array());
  page.message(frame.contentWindow, { type: "open-platformer-runtime-rom-error", message: "unsupported ROM" });
  await assert.rejects(pending, /unsupported ROM/);
  assert.equal(page.value("pendingRomReady"), null);
});

test("clearing selection releases the old runtime and rejects stale completion", async () => {
  const page = loader();
  const previous = page.get("runtimeFrame");
  previous.contentWindow.openPlatformerRuntimeGodotCommand = () => {};
  const pending = page.call("sendRomToGodot", "test.sfc", new Uint8Array());
  page.call("resetState");
  await assert.rejects(pending, /state reset/);
  const next = page.get("runtimeFrame");
  assert.notEqual(next, previous);
  assert.equal(next.hidden, true);
  assert.equal(next.src, "");
  page.message(previous.contentWindow, { type: "open-platformer-runtime-rom-ready", indexedLevelCount: 512 });
  assert.equal(page.value("runtimeRomReady"), false);
  assert.equal(page.get("playButton").disabled, true);
});

test("a provided local ROM validates identically with and without a copier header", {
  skip: !process.env.SMW_ROM_PATH,
}, async () => {
  const page = loader();
  const bytes = new Uint8Array(readFileSync(process.env.SMW_ROM_PATH));
  const normal = await page.call("inspectRom", bytes);
  assert.equal(normal.isSupported, true);
  const headered = new Uint8Array(bytes.length + 512);
  headered.set(bytes, 512);
  const result = await page.call("inspectRom", headered);
  assert.equal(result.isSupported, true);
  assert.equal(result.sha1, normal.sha1);
  assert.deepEqual(result.importBytes, bytes);
});
