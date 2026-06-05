#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${GODOT_BIN:-$(command -v godot4-mono || command -v godot-mono || command -v godot4 || command -v godot || true)}"
ROM_PATH="${SMW_ROM_PATH:-}"
IMPORT_MODE=auto
ALL_LEVELS=0
CLEAN=1
QUIT_AFTER="${SMW_LEVEL_SMOKE_QUIT_AFTER:-1}"
LEVELS=()

usage() {
  cat >&2 <<'EOF'
usage:
  tools/check-level-smoke.sh [--rom rom.sfc] [--level ID] [--levels A,B,C] [--all-levels] [--no-import]

options:
  --rom PATH        compatible unheadered ROM path; defaults to SMW_ROM_PATH
  --level ID        import/smoke one requested level; may be repeated
  --levels A,B,C    import/smoke comma-separated level IDs
  --all-levels      import/smoke the full vanilla level ID range 000..1FF
  --no-import       smoke levels already present in generated/smw/manifest.json
  --no-clean        keep existing generated files before importing
  --quit-after N    Godot frames/seconds argument for each smoke launch; default: 1
  -h, --help        show this help

examples:
  SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/check-level-smoke.sh --level 106
  SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/check-level-smoke.sh --all-levels
  tools/check-level-smoke.sh --no-import
EOF
}

append_levels() {
  local value="$1"
  local part
  value="${value//,/ }"
  for part in $value; do
    if [[ -n "$part" ]]; then
      LEVELS+=("$part")
    fi
  done
}

while (($#)); do
  case "$1" in
    --rom)
      [[ $# -ge 2 ]] || { echo "check-level-smoke: --rom requires a path" >&2; exit 2; }
      ROM_PATH="$2"
      shift 2
      ;;
    --rom=*)
      ROM_PATH="${1#--rom=}"
      shift
      ;;
    --level)
      [[ $# -ge 2 ]] || { echo "check-level-smoke: --level requires an id" >&2; exit 2; }
      append_levels "$2"
      shift 2
      ;;
    --level=*)
      append_levels "${1#--level=}"
      shift
      ;;
    --levels)
      [[ $# -ge 2 ]] || { echo "check-level-smoke: --levels requires ids" >&2; exit 2; }
      append_levels "$2"
      shift 2
      ;;
    --levels=*)
      append_levels "${1#--levels=}"
      shift
      ;;
    --all-levels)
      ALL_LEVELS=1
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
    --quit-after)
      [[ $# -ge 2 ]] || { echo "check-level-smoke: --quit-after requires a value" >&2; exit 2; }
      QUIT_AFTER="$2"
      shift 2
      ;;
    --quit-after=*)
      QUIT_AFTER="${1#--quit-after=}"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "check-level-smoke: unknown argument: $1" >&2
      usage
      exit 2
      ;;
  esac
done

cd "$ROOT"

if [[ "$IMPORT_MODE" != "off" ]]; then
  if [[ -z "$ROM_PATH" ]]; then
    usage
    echo "check-level-smoke: set SMW_ROM_PATH, pass --rom, or use --no-import" >&2
    exit 2
  fi

  import_args=(--rom "$ROM_PATH")
  if ((ALL_LEVELS)); then
    import_args+=(--all-levels)
  elif ((${#LEVELS[@]} > 0)); then
    for level in "${LEVELS[@]}"; do
      import_args+=(--level "$level")
    done
  else
    import_args+=(--all-levels)
  fi
  if ((CLEAN)); then
    import_args+=(--clean)
  fi
  tools/import-smw.sh "${import_args[@]}"
fi

if [[ ! -x "$GODOT_BIN" ]]; then
  echo "check-level-smoke: Godot executable not found or not executable: $GODOT_BIN" >&2
  exit 2
fi

if [[ "${SMW_SKIP_DOTNET_BUILD:-0}" != "1" ]]; then
  dotnet build SmwGodotNative.csproj --no-restore >/dev/null
fi

mapfile -t manifest_levels < <(python3 - <<'PY'
import json
from pathlib import Path
manifest = Path("generated/smw/manifest.json")
if not manifest.exists():
    raise SystemExit("check-level-smoke: generated/smw/manifest.json is missing")
levels = json.loads(manifest.read_text()).get("levels", {})
for level in sorted(levels, key=lambda value: int(value, 16)):
    print(level)
PY
)

if ((${#manifest_levels[@]} == 0)); then
  echo "check-level-smoke: manifest contains no levels" >&2
  exit 1
fi

failed=0
passed=0
for level in "${manifest_levels[@]}"; do
  log_file="$(mktemp)"
  if "$GODOT_BIN" \
    --headless \
    --audio-driver Dummy \
    --path "$ROOT" \
    --quit-after "$QUIT_AFTER" \
    --smw-test-level="$level" >"$log_file" 2>&1 &&
    grep -q "smw-runtime: level=$level " "$log_file"; then
    echo "check-level-smoke: ok level=$level"
    passed=$((passed + 1))
  else
    echo "check-level-smoke: failed level=$level" >&2
    sed -n '1,120p' "$log_file" >&2
    failed=$((failed + 1))
  fi
  rm -f "$log_file"
done

echo "check-level-smoke: passed=$passed failed=$failed total=${#manifest_levels[@]}"
if ((failed > 0)); then
  exit 1
fi
