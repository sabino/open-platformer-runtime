const EXPECTED_SIZE = 0x80000;
const COPIER_HEADER_SIZE = 0x200;
const EXPECTED_SHA1 = "6B47BB75D16514B6A476AA0C73A683A2A4C18765";

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

let currentManifest = null;

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
    currentManifest = buildBrowserManifest(file, inspection);
    renderInspection(currentManifest);
    manifestButton.disabled = !inspection.isSupported;
    playButton.disabled = true;
  } catch (error) {
    currentManifest = null;
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

function resetState() {
  currentManifest = null;
  manifestButton.disabled = true;
  playButton.disabled = true;
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
  const isSupported = !hasCopierHeader && isExpectedSize && isExpectedSha1;
  const probes = PROBES.map((probe) => inspectProbe(canonicalBytes, probe));

  return {
    selectedSize: bytes.length,
    canonicalSize: canonicalBytes.length,
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
      playable_browser_runtime: false,
      reason: "The current Godot 4 .NET runtime is not connected to this browser loader yet.",
    },
  };
}

function renderInspection(manifest) {
  const rom = manifest.source_rom;
  const readableProbeCount = manifest.importer.probes.filter((probe) => probe.readable).length;

  if (rom.is_supported) {
    statusEl.textContent = "ROM validated locally. Browser play is waiting on the web runtime bridge.";
  } else if (rom.has_copier_header) {
    statusEl.textContent = "Headered dumps are detected but not accepted by the current importer.";
  } else if (!rom.is_expected_size) {
    statusEl.textContent = "Unsupported ROM size.";
  } else {
    statusEl.textContent = "Unsupported ROM hash.";
  }

  detailsEl.innerHTML = detailsMarkup([
    ["ROM", rom.is_supported ? "Supported" : "Unsupported"],
    ["SHA-1", shortHash(rom.sha1)],
    ["Tables", `${readableProbeCount}/${manifest.importer.probes.length} readable`],
    ["Runtime", "Bridge pending"],
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
