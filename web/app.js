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

let currentManifest = null;
let currentRomBytes = null;
let currentRomIsSupported = false;
let currentLevelIndex = [];
let pyodidePromise = null;
let importerSourcePromise = null;
let runtimeImportPromise = null;

romFile.addEventListener("change", async () => {
  const file = romFile.files?.[0];
  if (!file) {
    resetState();
    return;
  }

  setBusy(file.name);

  try {
    const buffer = await file.arrayBuffer();
    const bytes = new Uint8Array(buffer);
    const inspection = await inspectRom(bytes);
    currentRomBytes = inspection.importBytes;
    currentRomIsSupported = inspection.isSupported;
    currentManifest = buildBrowserManifest(file, inspection);
    renderInspection(currentManifest);
    manifestButton.disabled = false;
    playButton.disabled = true;

    if (inspection.isSupported) {
      statusEl.textContent = "Reading level names from ROM...";
      const levelIndex = await buildLevelIndex(currentRomBytes);
      currentManifest.level_index = levelIndex;
      currentLevelIndex = levelIndex.levels;
      playButton.disabled = currentLevelIndex.length === 0;
      statusEl.textContent = currentLevelIndex.length > 0
        ? `ROM validated locally. ${currentLevelIndex.length} levels will be searchable inside the game.`
        : "ROM validated, but no valid levels were found.";
    }
  } catch (error) {
    currentManifest = null;
    currentRomBytes = null;
    currentRomIsSupported = false;
    currentLevelIndex = [];
    manifestButton.disabled = true;
    playButton.disabled = true;
    statusEl.textContent = error instanceof Error ? error.message : "ROM processing failed.";
    detailsEl.innerHTML = detailsMarkup([
      ["ROM", file.name],
      ["Import", "Failed"],
      ["Runtime", "Unavailable"],
    ]);
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
  if (!currentManifest) {
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
  if (!currentRomBytes || !currentRomIsSupported) {
    return;
  }

  const levelId = initialLevelId();
  playButton.disabled = true;
  manifestButton.disabled = true;

  try {
    await importAndSendLevel(levelId, { autoStart: true });
    playButton.disabled = false;
  } catch (error) {
    const message = error instanceof Error ? error.message : "Browser play failed.";
    statusEl.textContent = message;
    updateDetails("Failed", "Runtime unavailable");
    playButton.disabled = false;
  }
});

async function handleRuntimeLevelImport(levelId) {
  if (!currentRomBytes || !currentRomIsSupported || runtimeImportPromise) {
    return;
  }

  const normalizedLevelId = normalizeLevelId(levelId || "105");
  playButton.disabled = true;
  manifestButton.disabled = true;

  runtimeImportPromise = importAndSendLevel(normalizedLevelId, { autoStart: true })
    .catch((error) => {
      const message = error instanceof Error ? error.message : "Browser import failed.";
      statusEl.textContent = message;
      updateDetails("Failed", `Level ${normalizedLevelId}`);
      throw error;
    })
    .finally(() => {
      runtimeImportPromise = null;
      playButton.disabled = false;
      manifestButton.disabled = false;
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
  statusEl.textContent = `Generating level ${levelId} from the local ROM...`;
  const [assetPack] = await Promise.all([
    buildAssetPack(currentRomBytes, levelId),
    ensureRuntimeFrame(),
  ]);

  currentManifest = assetPack.manifest;
  manifestButton.disabled = false;
  statusEl.textContent = `Streaming ${assetPack.files.length} generated files into the runtime...`;
  await sendAssetPackToGodot(assetPack, levelId, autoStart);
  screenEl?.classList.add("is-playing");
  runtimeFrame.hidden = false;
  statusEl.textContent = "Runtime started.";
  updateDetails("Complete", `Level ${levelId}`);
  return assetPack;
}

function resetState() {
  currentManifest = null;
  currentRomBytes = null;
  currentRomIsSupported = false;
  currentLevelIndex = [];
  manifestButton.disabled = true;
  playButton.disabled = true;
  screenEl?.classList.remove("is-playing");
  runtimeFrame.hidden = true;
  statusEl.textContent = "Waiting for a local ROM file.";
  detailsEl.innerHTML = detailsMarkup([
    ["ROM", "Not selected"],
    ["Import", "Idle"],
    ["Runtime", "Pending"],
  ]);
}

function setBusy(fileName) {
  statusEl.textContent = "Reading local file...";
  detailsEl.innerHTML = detailsMarkup([
    ["ROM", fileName],
    ["Import", "Reading"],
    ["Runtime", "Pending"],
  ]);
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
  const pyodide = await getPyodide();
  const importerSource = await getImporterSource();
  statusEl.textContent = `Generating level ${levelId} from the local ROM...`;
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

  const files = collectFiles(pyodide, "/out");
  const manifestFile = files.find((file) => file.path === "manifest.json");
  if (!manifestFile) {
    throw new Error("Importer did not produce a manifest.");
  }

  return {
    files,
    manifest: JSON.parse(new TextDecoder().decode(manifestFile.bytes)),
  };
}

async function buildLevelIndex(bytes) {
  const pyodide = await getPyodide();
  const importerSource = await getImporterSource();

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
