#!/usr/bin/env bash
set -euo pipefail

GODOT_BIN="${GODOT_WEB_DOTNET_BIN:-${GODOT_BIN:-}}"
missing=0

if [[ -n "$GODOT_BIN" ]]; then
  if [[ "$GODOT_BIN" == */* ]]; then
    [[ -x "$GODOT_BIN" ]] || { echo "Godot binary is not executable: $GODOT_BIN" >&2; missing=1; }
  else
    command -v "$GODOT_BIN" >/dev/null || { echo "Godot binary not found on PATH: $GODOT_BIN" >&2; missing=1; }
  fi
else
  echo "GODOT_WEB_DOTNET_BIN is not set; export requires a Godot build from PR 106125." >&2
  missing=1
fi

dotnet --list-sdks | awk '{print $1}' | grep -Eq '^9\.' || {
  echo ".NET SDK 9.0+ is required for the Godot web .NET prototype." >&2
  missing=1
}

if ! dotnet workload list | awk 'NR > 4 {print $1}' | grep -qx 'wasm-tools'; then
  echo "The .NET wasm-tools workload is not installed. Run: dotnet workload install wasm-tools" >&2
  missing=1
fi

[[ -f export_presets.cfg ]] || { echo "export_presets.cfg is missing." >&2; exit 1; }

if [[ "$missing" -ne 0 ]]; then
  exit 1
fi

GodotWebDotNetPrototype=true dotnet restore SmwGodotNative.csproj
GodotWebDotNetPrototype=true dotnet build SmwGodotNative.csproj --no-restore

echo "experimental Godot .NET web prerequisites: ok"
