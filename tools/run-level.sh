#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${GODOT_BIN:-$(command -v godot4-mono || command -v godot-mono || command -v godot4 || command -v godot || true)}"
ROM_PATH="${SMW_ROM_PATH:-}"
IMPORT_MODE=auto
HEADLESS=0
CLEAN=1
LEVEL_ID=""
GODOT_ARGS=()

usage() {
  cat >&2 <<'EOF'
usage:
  tools/run-level.sh LEVEL [--rom rom.sfc] [--no-import] [--headless] [-- GODOT_ARGS...]

options:
  --rom PATH      compatible unheadered ROM path; defaults to SMW_ROM_PATH
  --no-import     run the level from the existing generated/smw manifest
  --no-clean      keep existing generated files before importing
  --headless      launch Godot headlessly
  -h, --help      show this help

examples:
  SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/run-level.sh 106
  tools/run-level.sh 1CB --no-import --headless -- --quit-after 2
EOF
}

normalize_level_id() {
  python3 - "$1" <<'PY'
import sys
text = sys.argv[1].strip()
if text.lower().startswith("0x"):
    text = text[2:]
try:
    value = int(text, 16)
except ValueError:
    print(text.upper())
else:
    if not 0 <= value < 0x200:
        raise SystemExit(f"run-level: level id out of range: {sys.argv[1]}")
    print(f"{value:03X}")
PY
}

manifest_has_level() {
  python3 - "$1" <<'PY'
import json
import sys
from pathlib import Path
level = sys.argv[1]
manifest = Path("generated/smw/manifest.json")
if not manifest.exists():
    raise SystemExit("run-level: generated/smw/manifest.json is missing")
levels = json.loads(manifest.read_text()).get("levels", {})
if level not in levels:
    available = " ".join(sorted(levels)) or "(none)"
    raise SystemExit(f"run-level: level {level} is not imported; available: {available}")
print(f"run-level: using imported level {level}")
PY
}

while (($#)); do
  case "$1" in
    --rom)
      [[ $# -ge 2 ]] || { echo "run-level: --rom requires a path" >&2; exit 2; }
      ROM_PATH="$2"
      shift 2
      ;;
    --rom=*)
      ROM_PATH="${1#--rom=}"
      shift
      ;;
    --no-import)
      IMPORT_MODE=off
      shift
      ;;
    --no-clean)
      CLEAN=0
      shift
      ;;
    --headless)
      HEADLESS=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --)
      shift
      GODOT_ARGS=("$@")
      break
      ;;
    -*)
      GODOT_ARGS+=("$1")
      shift
      ;;
    *)
      if [[ -z "$LEVEL_ID" ]]; then
        LEVEL_ID="$(normalize_level_id "$1")"
      else
        GODOT_ARGS+=("$1")
      fi
      shift
      ;;
  esac
done

if [[ -z "$LEVEL_ID" ]]; then
  usage
  echo "run-level: pass a level id such as 105, 106, or 1CB" >&2
  exit 2
fi

cd "$ROOT"

if [[ "$IMPORT_MODE" != "off" && -n "$ROM_PATH" ]]; then
  import_args=(--rom "$ROM_PATH" --level "$LEVEL_ID")
  if ((CLEAN)); then
    import_args+=(--clean)
  fi
  tools/import-smw.sh "${import_args[@]}"
else
  if [[ "$IMPORT_MODE" != "off" ]]; then
    echo "run-level: SMW_ROM_PATH is not set; using existing generated/smw manifest" >&2
  fi
  manifest_has_level "$LEVEL_ID"
fi

if [[ ! -x "$GODOT_BIN" ]]; then
  echo "run-level: Godot executable not found or not executable: $GODOT_BIN" >&2
  exit 2
fi

if [[ "${SMW_SKIP_DOTNET_BUILD:-0}" != "1" ]]; then
  dotnet build SmwGodotNative.csproj --no-restore >/dev/null
fi

display_args=()
if ((HEADLESS)); then
  display_args+=(--headless)
elif [[ "${XDG_SESSION_TYPE:-}" == "wayland" || -n "${WAYLAND_DISPLAY:-}" ]]; then
  display_args+=(--display-driver wayland --rendering-driver opengl3)
  export GDK_BACKEND=wayland
  export SDL_VIDEODRIVER=wayland
  export QT_QPA_PLATFORM=wayland
else
  display_args+=(--display-driver x11 --rendering-driver opengl3)
fi

exec "$GODOT_BIN" \
  "${display_args[@]}" \
  --audio-driver Dummy \
  --path "$ROOT" \
  --smw-test-level="$LEVEL_ID" \
  "${GODOT_ARGS[@]}"
