const EXPECTED_SIZE = 0x80000;
const COPIER_HEADER_SIZE = 0x200;
const EXPECTED_SHA1 = "6B47BB75D16514B6A476AA0C73A683A2A4C18765";
const RUNTIME_URL = "experimental-godot/";

const PROBES = [
  { name: "global palettes", address: 0x00b0a0, length: 0x180 },
  { name: "player metadata", address: 0x00dcec, length: 70 },
  { name: "entrance tables", address: 0x05f000, length: 0x800 },
];

const romFile = document.getElementById("romFile");
const statusEl = document.getElementById("status");
const detailsEl = document.getElementById("details");
const manifestButton = document.getElementById("manifestButton");
const playButton = document.getElementById("playButton");
const runtimeFrame = document.getElementById("runtimeFrame");
const screenEl = document.querySelector(".screen");
const controlsEl = document.querySelector(".controls");
const fileButton = document.querySelector(".file-button");
const progressWrap = document.getElementById("progressWrap");
const progressLabel = document.getElementById("progressLabel");
const progressValue = document.getElementById("progressValue");
const progressBar = document.getElementById("progressBar");

let currentManifest = null;
let currentRomBytes = null;
let currentRomIsSupported = false;
let currentIndexedLevelCount = 0;
let runtimeRomReady = false;
let isBusy = false;
let pendingRomReady = null;

romFile.addEventListener("change", async () => {
  if (isBusy) {
    return;
  }

  const file = romFile.files?.[0];
  if (!file) {
    resetState();
    return;
  }

  currentManifest = null;
  currentRomBytes = null;
  currentRomIsSupported = false;
  currentIndexedLevelCount = 0;
  runtimeRomReady = false;
  rejectPendingRomReady("ROM selection changed.");
  screenEl?.classList.remove("is-playing");
  runtimeFrame.hidden = true;
  setBusy(file.name);

  try {
    await nextFrame();
    const buffer = await file.arrayBuffer();
    const bytes = new Uint8Array(buffer);
    await progressStep("Checking ROM", 14, "Checking ROM hash...");
    const inspection = await inspectRom(bytes);
    await progressStep("Validating tables", 24, "Validating ROM tables...");
    currentRomBytes = inspection.importBytes;
    currentRomIsSupported = inspection.isSupported;
    currentManifest = buildBrowserManifest(file, inspection);
    renderInspection(currentManifest);

    if (inspection.isSupported) {
      await progressStep("Loading runtime", 34, "Loading the Godot runtime...");
      await ensureRuntimeFrame({ reveal: false });
      await progressStep("Sending ROM", 48, "Sending ROM bytes into the runtime...");
      const result = await sendRomToGodot(file.name, currentRomBytes);
      currentIndexedLevelCount = result.indexedLevelCount ?? 0;
      runtimeRomReady = currentIndexedLevelCount > 0;
      currentManifest.runtime_status.indexed_level_count = currentIndexedLevelCount;
      currentManifest.runtime_status.native_importer = "ready";
      const readyMessage = runtimeRomReady
        ? `ROM loaded. ${currentIndexedLevelCount} levels are searchable inside the game.`
        : "ROM loaded, but no valid levels were indexed.";
      await progressStep("Ready", 100, readyMessage);
      updateDetails("Complete", `${currentIndexedLevelCount} levels`);
    } else {
      showProgress("Unsupported", 100, statusEl.textContent);
    }
  } catch (error) {
    currentManifest = null;
    currentRomBytes = null;
    currentRomIsSupported = false;
    currentIndexedLevelCount = 0;
    runtimeRomReady = false;
    rejectPendingRomReady("ROM processing failed.");
    const message = error instanceof Error ? error.message : "ROM processing failed.";
    showProgress("Failed", 100, message);
    detailsEl.innerHTML = detailsMarkup([
      ["ROM", file.name],
      ["Import", "Failed"],
      ["Runtime", "Unavailable"],
    ]);
  } finally {
    isBusy = false;
    refreshControls();
  }
});

window.addEventListener("message", (event) => {
  if (event.source !== runtimeFrame.contentWindow || !event.data?.type) {
    return;
  }

  handleRuntimeMessage(event.data);
});

manifestButton.addEventListener("click", () => {
  if (isBusy || !currentManifest) {
    return;
  }

  const blob = new Blob([JSON.stringify(currentManifest, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = "browser-rom-manifest.json";
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
});

playButton.addEventListener("click", async () => {
  if (isBusy || !runtimeRomReady) {
    return;
  }

  beginBusy("Opening runtime", 98, "Opening the in-game course selector...");

  try {
    await ensureRuntimeFrame({ reveal: true });
    screenEl?.classList.add("is-playing");
    runtimeFrame.hidden = false;
    showProgress("Runtime ready", 100, "Use the in-game selector to search and start a level.");
    updateDetails("Complete", `${currentIndexedLevelCount} levels`);
  } catch (error) {
    const message = error instanceof Error ? error.message : "Browser play failed.";
    showProgress("Failed", 100, message);
    updateDetails("Failed", "Runtime unavailable");
  } finally {
    isBusy = false;
    refreshControls();
  }
});

function resetState() {
  currentManifest = null;
  currentRomBytes = null;
  currentRomIsSupported = false;
  currentIndexedLevelCount = 0;
  runtimeRomReady = false;
  rejectPendingRomReady("ROM state reset.");
  isBusy = false;
  screenEl?.classList.remove("is-playing");
  runtimeFrame.hidden = true;
  resetProgress();
  statusEl.textContent = "Waiting for a local ROM file.";
  detailsEl.innerHTML = detailsMarkup([
    ["ROM", "Not selected"],
    ["Import", "Idle"],
    ["Runtime", "Pending"],
  ]);
  refreshControls();
}

function setBusy(fileName) {
  beginBusy("Reading file", 4, "Reading local file...");
  detailsEl.innerHTML = detailsMarkup([
    ["ROM", fileName],
    ["Import", "Reading"],
    ["Runtime", "Pending"],
  ]);
}

function beginBusy(label, value, status) {
  isBusy = true;
  showProgress(label, value, status);
  refreshControls();
}

async function progressStep(label, value, status) {
  showProgress(label, value, status);
  await nextFrame();
}

function showProgress(label, value, status) {
  const clampedValue = Math.max(0, Math.min(100, Math.round(value)));
  if (status) {
    statusEl.textContent = status;
  }
  if (progressWrap) {
    progressWrap.hidden = false;
    progressWrap.setAttribute("aria-valuenow", String(clampedValue));
  }
  if (progressLabel) {
    progressLabel.textContent = label;
  }
  if (progressValue) {
    progressValue.textContent = `${clampedValue}%`;
  }
  if (progressBar) {
    progressBar.style.width = `${clampedValue}%`;
  }
}

function resetProgress() {
  if (progressWrap) {
    progressWrap.hidden = true;
    progressWrap.setAttribute("aria-valuenow", "0");
  }
  if (progressLabel) {
    progressLabel.textContent = "Idle";
  }
  if (progressValue) {
    progressValue.textContent = "0%";
  }
  if (progressBar) {
    progressBar.style.width = "0%";
  }
}

function refreshControls() {
  romFile.disabled = isBusy;
  manifestButton.disabled = isBusy || !currentManifest;
  playButton.disabled = isBusy || !canPlay();
  controlsEl?.setAttribute("aria-busy", isBusy ? "true" : "false");
  fileButton?.classList.toggle("is-disabled", isBusy);
  fileButton?.setAttribute("aria-disabled", isBusy ? "true" : "false");
}

function canPlay() {
  return Boolean(currentRomBytes && currentRomIsSupported && runtimeRomReady);
}

async function inspectRom(bytes) {
  const hasCopierHeader = bytes.length > COPIER_HEADER_SIZE && bytes.length % 0x8000 === COPIER_HEADER_SIZE;
  const canonicalBytes = hasCopierHeader ? bytes.slice(COPIER_HEADER_SIZE) : bytes;
  const sha1 = await sha1Hex(canonicalBytes);
  const isExpectedSize = canonicalBytes.length === EXPECTED_SIZE;
  const isExpectedSha1 = sha1 === EXPECTED_SHA1;
  const isSupported = isExpectedSize && isExpectedSha1;
  const probes = PROBES.map((probe) => inspectProbe(canonicalBytes, probe));

  return {
    selectedSize: bytes.length,
    canonicalSize: canonicalBytes.length,
    importBytes: canonicalBytes,
    hasCopierHeader,
    sha1,
    isExpectedSize,
    isExpectedSha1,
    isSupported,
    probes,
  };
}

function inspectProbe(bytes, probe) {
  try {
    const start = loRomIndex(probe.address, bytes.length);
    const end = start + probe.length;
    return {
      name: probe.name,
      source_address: `0x${probe.address.toString(16).padStart(6, "0").toUpperCase()}`,
      length: probe.length,
      readable: end <= bytes.length,
    };
  } catch {
    return {
      name: probe.name,
      source_address: `0x${probe.address.toString(16).padStart(6, "0").toUpperCase()}`,
      length: probe.length,
      readable: false,
    };
  }
}

function buildBrowserManifest(file, inspection) {
  return {
    schema_version: 1,
    runtime: "browser-loader-preview",
    source_rom: {
      file_name: file.name,
      selected_size: inspection.selectedSize,
      canonical_size: inspection.canonicalSize,
      sha1: inspection.sha1,
      has_copier_header: inspection.hasCopierHeader,
      is_expected_size: inspection.isExpectedSize,
      is_expected_sha1: inspection.isExpectedSha1,
      is_supported: inspection.isSupported,
      expected_size: EXPECTED_SIZE,
      expected_sha1: EXPECTED_SHA1,
    },
    importer: {
      local_only: true,
      probes: inspection.probes,
    },
    runtime_status: {
      playable_browser_runtime: inspection.isSupported,
      reason: inspection.isSupported
        ? "Ready to import locally in the browser."
        : "The runtime only accepts the compatible USA ROM.",
    },
  };
}

function renderInspection(manifest) {
  const rom = manifest.source_rom;
  const readableProbeCount = manifest.importer.probes.filter((probe) => probe.readable).length;

  if (rom.is_supported && rom.has_copier_header) {
    statusEl.textContent = "Headered ROM validated. The copier header will be stripped in memory.";
  } else if (rom.is_supported) {
    statusEl.textContent = "ROM validated locally. Choose a level and press Play.";
  } else if (rom.has_copier_header) {
    statusEl.textContent = "Headered dump detected, but its canonical ROM data is unsupported.";
  } else if (!rom.is_expected_size) {
    statusEl.textContent = "Unsupported ROM size.";
  } else {
    statusEl.textContent = "Unsupported ROM hash.";
  }

  detailsEl.innerHTML = detailsMarkup([
    ["ROM", rom.is_supported ? "Supported" : "Unsupported"],
    ["SHA-1", shortHash(rom.sha1)],
    ["Tables", `${readableProbeCount}/${manifest.importer.probes.length} readable`],
    ["Runtime", rom.is_supported ? "Ready" : "Blocked"],
  ]);
}

async function ensureRuntimeFrame(options = {}) {
  if (!runtimeFrame.src) {
    runtimeFrame.src = RUNTIME_URL;
  }
  if (options.reveal) {
    runtimeFrame.hidden = false;
  }
  await waitForGodotCommand();
}

async function sendRomToGodot(fileName, bytes) {
  const readyPromise = waitForRuntimeRomReady();
  const command = runtimeFrame.contentWindow?.openPlatformerRuntimeGodotCommand;
  if (typeof command !== "function") {
    rejectPendingRomReady("Godot runtime bridge is not available.");
    throw new Error("Godot runtime bridge is not available.");
  }

  command("rom", bytes, fileName);
  return readyPromise;
}

function waitForRuntimeRomReady() {
  rejectPendingRomReady("Superseded by a new ROM import.");
  return new Promise((resolve, reject) => {
    let timeout = 0;
    const entry = {
      resolve: (value) => {
        window.clearTimeout(timeout);
        if (pendingRomReady === entry) {
          pendingRomReady = null;
        }
        resolve(value);
      },
      reject: (message) => {
        window.clearTimeout(timeout);
        if (pendingRomReady === entry) {
          pendingRomReady = null;
        }
        reject(new Error(message));
      },
    };
    timeout = window.setTimeout(() => {
      if (pendingRomReady === entry) {
        pendingRomReady = null;
      }
      reject(new Error("Timed out waiting for the native ROM importer."));
    }, 120000);
    pendingRomReady = entry;
  });
}

function rejectPendingRomReady(message) {
  if (!pendingRomReady) {
    return;
  }

  pendingRomReady.reject(message);
}

function handleRuntimeMessage(data) {
  switch (data.type) {
    case "open-platformer-runtime-import-status": {
      const total = Math.max(1, Number(data.total) || 1);
      const completed = Math.max(0, Math.min(total, Number(data.completed) || 0));
      const percent = 48 + Math.round((completed / total) * 46);
      const stage = data.levelId ? `${data.stage} ${data.levelId}` : data.stage;
      showProgress(stage || "Importing", percent, stage || "Importing ROM...");
      updateDetails("Running", stage || "Native importer");
      break;
    }
    case "open-platformer-runtime-rom-ready": {
      const count = Number(data.indexedLevelCount) || 0;
      runtimeRomReady = count > 0;
      currentIndexedLevelCount = count;
      pendingRomReady?.resolve({
        sha1: data.sha1,
        indexedLevelCount: count,
        generatedLevelCount: Number(data.generatedLevelCount) || 0,
      });
      break;
    }
    case "open-platformer-runtime-rom-error": {
      const message = data.message || "Native ROM import failed.";
      runtimeRomReady = false;
      pendingRomReady?.reject(message);
      break;
    }
  }
}

async function waitForGodotCommand() {
  const startedAt = performance.now();
  const timeoutMs = 120000;

  return new Promise((resolve, reject) => {
    const check = () => {
      if (typeof runtimeFrame.contentWindow?.openPlatformerRuntimeGodotCommand === "function") {
        cleanup();
        resolve();
        return;
      }
      if (performance.now() - startedAt > timeoutMs) {
        cleanup();
        reject(new Error("Timed out waiting for the Godot web runtime."));
      }
    };
    const onMessage = (event) => {
      if (event.source === runtimeFrame.contentWindow &&
          event.data?.type === "open-platformer-runtime-godot-ready") {
        check();
      }
    };
    const timer = setInterval(check, 250);
    const cleanup = () => {
      clearInterval(timer);
      window.removeEventListener("message", onMessage);
    };
    window.addEventListener("message", onMessage);
    check();
  });
}

function nextFrame() {
  return new Promise((resolve) => requestAnimationFrame(resolve));
}

function updateDetails(importState, runtimeState) {
  const rom = currentManifest?.source_rom ?? currentManifest?.rom;
  const romState = rom?.is_supported ? "Supported" : rom?.sha1 ? "Unsupported" : "Selected";
  const hash = rom?.sha1 ? shortHash(rom.sha1) : "Pending";
  detailsEl.innerHTML = detailsMarkup([
    ["ROM", romState],
    ["SHA-1", hash],
    ["Import", importState],
    ["Runtime", runtimeState],
  ]);
}

function detailsMarkup(rows) {
  return rows
    .map(([label, value]) => `<div><dt>${escapeHtml(label)}</dt><dd>${escapeHtml(value)}</dd></div>`)
    .join("");
}

function loRomIndex(address, romByteLength) {
  if ((address & 0x8000) === 0) {
    throw new RangeError(`LoROM address must have bit 0x8000 set: 0x${address.toString(16)}`);
  }

  const index = (((address >> 16) & 0x7f) * 0x8000) + (address & 0x7fff);
  if (index < 0 || index >= romByteLength) {
    throw new RangeError(`LoROM address out of range: 0x${address.toString(16)}`);
  }

  return index;
}

async function sha1Hex(bytes) {
  if (!globalThis.crypto?.subtle) {
    throw new Error("SHA-1 is unavailable. Serve this page from localhost or HTTPS.");
  }

  const digest = await crypto.subtle.digest("SHA-1", bytes);
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, "0")).join("").toUpperCase();
}

function shortHash(hash) {
  return `${hash.slice(0, 8)}...${hash.slice(-8)}`;
}

function escapeHtml(value) {
  return String(value).replace(/[&<>"']/g, (char) => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    "\"": "&quot;",
    "'": "&#39;",
  })[char]);
}
