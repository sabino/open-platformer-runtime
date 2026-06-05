#!/usr/bin/env bash
set -euo pipefail

OUT_DIR="${1:-web-export/out}"
GODOT_BIN="${GODOT_WEB_DOTNET_BIN:-${GODOT_BIN:-godot}}"

if [[ "$GODOT_BIN" == */* ]]; then
  [[ -x "$GODOT_BIN" ]] || { echo "Godot binary is not executable: $GODOT_BIN" >&2; exit 1; }
else
  command -v "$GODOT_BIN" >/dev/null || { echo "Godot binary not found on PATH: $GODOT_BIN" >&2; exit 1; }
fi

dotnet --list-sdks | awk '{print $1}' | grep -Eq '^9\.' || {
  echo ".NET SDK 9.0+ is required for the Godot web .NET prototype." >&2
  exit 1
}

if ! dotnet workload list | awk 'NR > 4 {print $1}' | grep -qx 'wasm-tools'; then
  echo "The .NET wasm-tools workload is not installed. Run: dotnet workload install wasm-tools" >&2
  exit 1
fi

mkdir -p "$OUT_DIR"
find "$OUT_DIR" -mindepth 1 -maxdepth 1 -exec rm -rf {} +

GodotWebDotNetPrototype=true "$GODOT_BIN" --headless --export-release "Web" "$OUT_DIR/index.html"
python3 tools/patch-godot-web-dotnet-index.py "$OUT_DIR/index.js" --allow-missing

echo "experimental Godot .NET web export written to $OUT_DIR"
