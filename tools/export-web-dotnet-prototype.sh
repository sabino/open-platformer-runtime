#!/usr/bin/env bash
set -euo pipefail

OUT_DIR="${1:-web-export/out}"
GODOT_BIN="${GODOT_WEB_DOTNET_BIN:-${GODOT_BIN:-godot}}"
PROJECT_FILE="SmwGodotNative.csproj"
PRESET_FILE="export_presets.cfg"
WEB_SDK_VERSION="${GODOT_WEB_DOTNET_SDK_VERSION:-4.5.0-dev}"
WEB_NUGET_SOURCE="${GODOT_WEB_DOTNET_NUGET_SOURCE:-}"
WEB_RELEASE_TEMPLATE="${GODOT_WEB_DOTNET_RELEASE_TEMPLATE:-}"
WEB_DEBUG_TEMPLATE="${GODOT_WEB_DOTNET_DEBUG_TEMPLATE:-}"

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

if ! dotnet workload list | awk 'NR > 4 {print $1}' | grep -qx 'wasm-tools-net9'; then
  echo "The .NET wasm-tools-net9 workload is not installed. Run: dotnet workload install wasm-tools-net9" >&2
  exit 1
fi

mkdir -p "$OUT_DIR"
find "$OUT_DIR" -mindepth 1 -maxdepth 1 -exec rm -rf {} +

project_backup="$(mktemp)"
preset_backup="$(mktemp)"
nuget_backup=""
nuget_existed=0
cp "$PROJECT_FILE" "$project_backup"
cp "$PRESET_FILE" "$preset_backup"
restore_project_files() {
  cp "$project_backup" "$PROJECT_FILE"
  cp "$preset_backup" "$PRESET_FILE"
  rm -f "$project_backup"
  rm -f "$preset_backup"
  if [[ -n "$nuget_backup" ]]; then
    if [[ "$nuget_existed" -eq 1 ]]; then
      cp "$nuget_backup" NuGet.config
    else
      rm -f NuGet.config
    fi
    rm -f "$nuget_backup"
  fi
}
trap restore_project_files EXIT

python3 - "$PROJECT_FILE" "$WEB_SDK_VERSION" <<'PY'
from pathlib import Path
import re
import sys

path = Path(sys.argv[1])
sdk_version = sys.argv[2]
text = path.read_text()
text = re.sub(r'<Project Sdk="Godot\.NET\.Sdk/[^"]+">', f'<Project Sdk="Godot.NET.Sdk/{sdk_version}">', text, count=1)
text = re.sub(r'\s*<TargetFramework Condition="[^"]+">[^<]+</TargetFramework>\n', '\n', text)
if '<TargetFramework>' in text:
    text = re.sub(r'<TargetFramework>[^<]+</TargetFramework>', '<TargetFramework>net9.0</TargetFramework>', text, count=1)
else:
    text = text.replace('<PropertyGroup>\n', '<PropertyGroup>\n    <TargetFramework>net9.0</TargetFramework>\n', 1)
path.write_text(text)
PY

if [[ -n "$WEB_NUGET_SOURCE" ]]; then
  [[ -d "$WEB_NUGET_SOURCE" ]] || { echo "Godot .NET NuGet source does not exist: $WEB_NUGET_SOURCE" >&2; exit 1; }
  nuget_backup="$(mktemp)"
  if [[ -f NuGet.config ]]; then
    nuget_existed=1
    cp NuGet.config "$nuget_backup"
  fi
  cat > NuGet.config <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="godot-web-dotnet" value="$WEB_NUGET_SOURCE" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF
fi

template_args=()
if [[ -n "$WEB_RELEASE_TEMPLATE" ]]; then
  template_args+=(--release "$WEB_RELEASE_TEMPLATE")
fi
if [[ -n "$WEB_DEBUG_TEMPLATE" ]]; then
  template_args+=(--debug "$WEB_DEBUG_TEMPLATE")
fi
if [[ "${#template_args[@]}" -gt 0 ]]; then
  python3 tools/configure-web-export-templates.py "${template_args[@]}"
fi

GodotWebDotNetPrototype=true "$GODOT_BIN" --headless --export-release "Web" "$OUT_DIR/index.html"
if [[ "${GODOT_WEB_DOTNET_PATCH_INDEX:-1}" != "0" ]]; then
  python3 tools/patch-godot-web-dotnet-index.py "$OUT_DIR/index.js" --allow-missing
fi

echo "experimental Godot .NET web export written to $OUT_DIR"
