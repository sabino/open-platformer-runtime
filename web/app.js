const EXPECTED_SIZE = 0x80000;
const COPIER_HEADER_SIZE = 0x200;
const EXPECTED_SHA1 = "6B47BB75D16514B6A476AA0C73A683A2A4C18765";
const PYODIDE_INDEX_URL = "https://cdn.jsdelivr.net/pyodide/v0.27.7/full/";
const PYODIDE_MODULE_URL = `${PYODIDE_INDEX_URL}pyodide.mjs`;
const IMPORTER_URL = "import/smw_import.py";
const RUNTIME_URL = "experimental-godot/";
const DEFAULT_LEVEL_ID = "105";

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
let currentLevelIndex = [];
let isBusy = false;
let pyodidePromise = null;
let importerSourcePromise = null;
let runtimeImportPromise = null;

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
  currentLevelIndex = [];
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
      const levelIndex = await buildLevelIndex(currentRomBytes);
      currentManifest.level_index = levelIndex;
      currentLevelIndex = levelIndex.levels;
      const readyMessage = currentLevelIndex.length > 0
        ? `ROM validated locally. ${currentLevelIndex.length} levels will be searchable inside the game.`
        : "ROM validated, but no valid levels were found.";
      await progressStep("Ready", 100, readyMessage);
    } else {
      showProgress("Unsupported", 100, statusEl.textContent);
    }
  } catch (error) {
    currentManifest = null;
    currentRomBytes = null;
    currentRomIsSupported = false;
    currentLevelIndex = [];
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
  if (event.source !== runtimeFrame.contentWindow ||
      event.data?.type !== "open-platformer-runtime-import-level") {
    return;
  }

  void handleRuntimeLevelImport(event.data.levelId);
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
  if (isBusy || !currentRomBytes || !currentRomIsSupported) {
    return;
  }

  const levelId = initialLevelId();
  beginBusy(`Preparing ${levelId}`, 4, `Preparing level ${levelId}...`);

  try {
    await importAndSendLevel(levelId, { autoStart: true });
  } catch (error) {
    const message = error instanceof Error ? error.message : "Browser play failed.";
    showProgress("Failed", 100, message);
    updateDetails("Failed", "Runtime unavailable");
  } finally {
    isBusy = false;
    refreshControls();
  }
});

async function handleRuntimeLevelImport(levelId) {
  if (isBusy || !currentRomBytes || !currentRomIsSupported || runtimeImportPromise) {
    return;
  }

  const normalizedLevelId = normalizeLevelId(levelId || "105");
  beginBusy(`Preparing ${normalizedLevelId}`, 4, `Preparing level ${normalizedLevelId}...`);

  runtimeImportPromise = importAndSendLevel(normalizedLevelId, { autoStart: true })
    .catch((error) => {
      const message = error instanceof Error ? error.message : "Browser import failed.";
      showProgress("Failed", 100, message);
      updateDetails("Failed", `Level ${normalizedLevelId}`);
      throw error;
    })
    .finally(() => {
      runtimeImportPromise = null;
      isBusy = false;
      refreshControls();
    });

  try {
    await runtimeImportPromise;
  } catch {
    // The status line above is the user-facing error surface.
  }
}

async function importAndSendLevel(levelId, options = {}) {
  const autoStart = options.autoStart ?? true;
  updateDetails("Importing", "Starting");
  await progressStep("Starting import", 8, `Generating level ${levelId} from the local ROM...`);
  const runtimeReadyPromise = ensureRuntimeFrame();
  const assetPack = await buildAssetPack(currentRomBytes, levelId);
  await progressStep("Loading runtime", 82, "Waiting for the Godot runtime...");
  await runtimeReadyPromise;

  currentManifest = assetPack.manifest;
  await progressStep("Streaming files", 86, `Streaming ${assetPack.files.length} generated files into the runtime...`);
  await sendAssetPackToGodot(assetPack, levelId, autoStart);
  screenEl?.classList.add("is-playing");
  runtimeFrame.hidden = false;
  showProgress("Runtime ready", 100, "Runtime started.");
  updateDetails("Complete", `Level ${levelId}`);
  return assetPack;
}

function resetState() {
  currentManifest = null;
  currentRomBytes = null;
  currentRomIsSupported = false;
  currentLevelIndex = [];
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
  return Boolean(currentRomBytes && currentRomIsSupported && currentLevelIndex.length > 0);
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

async function buildAssetPack(bytes, levelId) {
  await progressStep("Loading Python", 18, "Loading browser Python runtime...");
  const pyodide = await getPyodide();
  await progressStep("Loading importer", 32, "Loading ROM importer...");
  const importerSource = await getImporterSource();
  await progressStep("Generating assets", 50, `Generating level ${levelId} from the local ROM...`);
  updateDetails("Running", "Pyodide");

  pyodide.FS.writeFile("/input.sfc", bytes);
  pyodide.FS.writeFile("/smw_import.py", importerSource);
  pyodide.globals.set("opr_level_id", levelId);
  pyodide.runPython(`
import argparse
import importlib.util
import os
import shutil
import sys

shutil.rmtree("/out", ignore_errors=True)
os.makedirs("/out", exist_ok=True)

spec = importlib.util.spec_from_file_location("smw_import", "/smw_import.py")
smw_import = importlib.util.module_from_spec(spec)
assert spec.loader is not None
sys.modules["smw_import"] = smw_import
spec.loader.exec_module(smw_import)
smw_import.import_rom(argparse.Namespace(
    rom="/input.sfc",
    out="/out",
    level=[opr_level_id],
    include_exit_targets=True,
    exit_depth=1,
))
`);
  pyodide.globals.delete("opr_level_id");
  await progressStep("Collecting files", 72, "Collecting generated runtime files...");

  const files = collectFiles(pyodide, "/out");
  const manifestFile = files.find((file) => file.path === "manifest.json");
  if (!manifestFile) {
    throw new Error("Importer did not produce a manifest.");
  }

  const manifest = JSON.parse(new TextDecoder().decode(manifestFile.bytes));
  if (currentLevelIndex.length > 0) {
    manifest.level_index = {
      source: "browser_rom_index",
      count: currentLevelIndex.length,
      levels: currentLevelIndex,
    };
    const manifestIndex = files.findIndex((file) => file.path === "manifest.json");
    files[manifestIndex] = {
      ...manifestFile,
      bytes: new TextEncoder().encode(JSON.stringify(manifest, null, 2)),
    };
  }

  return {
    files,
    manifest,
  };
}

async function buildLevelIndex(bytes) {
  await progressStep("Loading Python", 38, "Loading browser Python runtime...");
  const pyodide = await getPyodide();
  await progressStep("Loading importer", 54, "Loading ROM importer...");
  const importerSource = await getImporterSource();
  await progressStep("Reading level names", 72, "Reading level names from ROM...");

  pyodide.FS.writeFile("/input.sfc", bytes);
  pyodide.FS.writeFile("/smw_import.py", importerSource);
  const indexJson = pyodide.runPython(`
import importlib.util
import json
import sys
from pathlib import Path

spec = importlib.util.spec_from_file_location("smw_import", "/smw_import.py")
smw_import = importlib.util.module_from_spec(spec)
assert spec.loader is not None
sys.modules["smw_import"] = smw_import
spec.loader.exec_module(smw_import)

rom = smw_import.Rom.load(Path("/input.sfc"))
try:
    titles, title_source = smw_import.load_overworld_level_titles(rom)
    title_error = None
except Exception as exc:
    titles = {}
    title_source = "unavailable"
    title_error = str(exc)

levels = []
invalid = []
for level_id in range(smw_import.EDITOR_LEVEL_TITLE_COUNT):
    try:
        layer1_addr = rom.get_24(0x05E000 + level_id * 3)
        layer1_len = smw_import.calc_level_len(rom, layer1_addr)
        header = smw_import.decode_level_header(rom.get_bytes(layer1_addr, 5))
        title = titles.get(level_id, "")
        key = f"{level_id:03X}"
        levels.append({
            "id": key,
            "name": title,
            "display_name": title or f"Level {key}",
            "title_source": title_source if title else "none",
            "layer1_addr": f"0x{layer1_addr:06X}",
            "layer1_length": layer1_len,
            "screens": header["screens"],
            "vertical": bool(header["vertical"]),
        })
    except Exception as exc:
        invalid.append({"id": f"{level_id:03X}", "error": str(exc)})

json.dumps({
    "source": title_source,
    "status": "ok" if title_error is None else "partial",
    "error": title_error,
    "count": len(levels),
    "invalid_count": len(invalid),
    "levels": levels,
})
`);
  await progressStep("Preparing levels", 92, "Preparing searchable level list...");
  const parsed = JSON.parse(indexJson);
  parsed.levels.sort((left, right) => levelSortKey(left.id) - levelSortKey(right.id));
  return parsed;
}

async function getPyodide() {
  if (!pyodidePromise) {
    pyodidePromise = import(PYODIDE_MODULE_URL)
      .then(({ loadPyodide }) => loadPyodide({ indexURL: PYODIDE_INDEX_URL }));
  }
  return pyodidePromise;
}

async function getImporterSource() {
  if (!importerSourcePromise) {
    importerSourcePromise = fetch(IMPORTER_URL, { cache: "no-cache" }).then((response) => {
      if (!response.ok) {
        throw new Error(`Could not load browser importer (${response.status}).`);
      }
      return response.text();
    });
  }
  return importerSourcePromise;
}

function collectFiles(pyodide, root) {
  const files = [];
  const visit = (directory) => {
    for (const entry of pyodide.FS.readdir(directory)) {
      if (entry === "." || entry === "..") {
        continue;
      }
      const fullPath = `${directory}/${entry}`;
      const stat = pyodide.FS.stat(fullPath);
      if (pyodide.FS.isDir(stat.mode)) {
        visit(fullPath);
        continue;
      }
      const relativePath = fullPath.slice(root.length + 1);
      files.push({
        path: relativePath,
        bytes: pyodide.FS.readFile(fullPath),
      });
    }
  };
  visit(root);
  files.sort((left, right) => left.path.localeCompare(right.path));
  return files;
}

async function ensureRuntimeFrame() {
  if (!runtimeFrame.src) {
    runtimeFrame.src = RUNTIME_URL;
  }
  runtimeFrame.hidden = false;
  await waitForGodotCommand();
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

async function sendAssetPackToGodot(assetPack, levelId, autoStart = true) {
  const command = runtimeFrame.contentWindow?.openPlatformerRuntimeGodotCommand;
  if (typeof command !== "function") {
    throw new Error("Godot runtime bridge is not available.");
  }

  sendLevelIndexToGodot(command);
  command("begin");
  for (let index = 0; index < assetPack.files.length; index += 1) {
    const file = assetPack.files[index];
    command("file", file.path, file.bytes);
    if (index % 4 === 0 || index + 1 === assetPack.files.length) {
      const percent = 86 + Math.round(((index + 1) / assetPack.files.length) * 12);
      showProgress(`Streaming ${index + 1}/${assetPack.files.length}`, percent, `Streaming ${assetPack.files.length} generated files into the runtime...`);
      updateDetails(`${index + 1}/${assetPack.files.length}`, "Streaming");
      await nextFrame();
    }
  }
  command("complete", levelId, autoStart);
}

function sendLevelIndexToGodot(command) {
  if (currentLevelIndex.length === 0) {
    return;
  }

  command("level_index", JSON.stringify({
    source: "browser_rom_index",
    count: currentLevelIndex.length,
    levels: currentLevelIndex,
  }));
}

function nextFrame() {
  return new Promise((resolve) => requestAnimationFrame(resolve));
}

function updateDetails(importState, runtimeState) {
  const rom = currentManifest?.source_rom ?? currentManifest?.rom;
  const romState = rom?.is_supported || rom?.sha1 ? "Supported" : "Selected";
  const hash = rom?.sha1 ? shortHash(rom.sha1) : "Pending";
  detailsEl.innerHTML = detailsMarkup([
    ["ROM", romState],
    ["SHA-1", hash],
    ["Import", importState],
    ["Runtime", runtimeState],
  ]);
}

function initialLevelId() {
  if (currentLevelIndex.some((level) => level.id === DEFAULT_LEVEL_ID)) {
    return DEFAULT_LEVEL_ID;
  }

  return currentLevelIndex[0]?.id ?? DEFAULT_LEVEL_ID;
}

function levelSortKey(levelId) {
  return Number.parseInt(levelId, 16);
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

function normalizeLevelId(value) {
  const trimmed = String(value).trim().replace(/^0x/i, "");
  const parsed = Number.parseInt(trimmed || "105", 16);
  if (!Number.isFinite(parsed) || parsed < 0 || parsed >= 0x200) {
    throw new Error(`Invalid level id: ${value}`);
  }
  return parsed.toString(16).toUpperCase().padStart(3, "0");
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
