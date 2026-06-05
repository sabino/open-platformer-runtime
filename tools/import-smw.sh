#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROM_PATH="${SMW_ROM_PATH:-}"
OUT_DIR="${SMW_IMPORT_OUT_DIR:-generated/smw}"
EXIT_DEPTH="${SMW_IMPORT_EXIT_DEPTH:-1}"
INCLUDE_EXIT_TARGETS=1
CLEAN_OUT=0
ALL_LEVELS=0
LEVELS=()
POSITIONALS=()

usage() {
  cat >&2 <<'EOF'
usage:
  tools/import-smw.sh [rom.sfc] [out-dir]
  tools/import-smw.sh --rom rom.sfc --out generated/smw --level 105 [--level 1CB]

options:
  --rom PATH             compatible unheadered ROM path; defaults to SMW_ROM_PATH
  --out DIR              generated asset output dir; defaults to generated/smw
  --level ID             import one requested level; may be repeated; default: 105
  --levels A,B,C         import comma-separated level IDs
  --all-levels           import the full vanilla level ID range 000..1FF
  --include-exit-targets import direct screen-exit destination levels; default
  --no-exit-targets      import only requested levels
  --exit-depth N         exit-target traversal depth; default: 1
  --clean                remove the output dir before import
  -h, --help             show this help

examples:
  SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/import-smw.sh --level 106 --clean
  tools/import-smw.sh --rom /path/to/compatible-rom.sfc --levels 105,106,1CB
  tools/import-smw.sh --rom /path/to/compatible-rom.sfc --all-levels --clean
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
      [[ $# -ge 2 ]] || { echo "import-smw: --rom requires a path" >&2; exit 2; }
      ROM_PATH="$2"
      shift 2
      ;;
    --rom=*)
      ROM_PATH="${1#--rom=}"
      shift
      ;;
    --out)
      [[ $# -ge 2 ]] || { echo "import-smw: --out requires a path" >&2; exit 2; }
      OUT_DIR="$2"
      shift 2
      ;;
    --out=*)
      OUT_DIR="${1#--out=}"
      shift
      ;;
    --level)
      [[ $# -ge 2 ]] || { echo "import-smw: --level requires an id" >&2; exit 2; }
      append_levels "$2"
      shift 2
      ;;
    --level=*)
      append_levels "${1#--level=}"
      shift
      ;;
    --levels)
      [[ $# -ge 2 ]] || { echo "import-smw: --levels requires ids" >&2; exit 2; }
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
    --include-exit-targets)
      INCLUDE_EXIT_TARGETS=1
      shift
      ;;
    --no-exit-targets)
      INCLUDE_EXIT_TARGETS=0
      shift
      ;;
    --exit-depth)
      [[ $# -ge 2 ]] || { echo "import-smw: --exit-depth requires a number" >&2; exit 2; }
      EXIT_DEPTH="$2"
      shift 2
      ;;
    --exit-depth=*)
      EXIT_DEPTH="${1#--exit-depth=}"
      shift
      ;;
    --clean)
      CLEAN_OUT=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --)
      shift
      while (($#)); do
        POSITIONALS+=("$1")
        shift
      done
      ;;
    -*)
      echo "import-smw: unknown option: $1" >&2
      usage
      exit 2
      ;;
    *)
      POSITIONALS+=("$1")
      shift
      ;;
  esac
done

if ((${#POSITIONALS[@]} > 0)); then
  ROM_PATH="${POSITIONALS[0]}"
fi
if ((${#POSITIONALS[@]} > 1)); then
  OUT_DIR="${POSITIONALS[1]}"
fi
if ((${#POSITIONALS[@]} > 2)); then
  echo "import-smw: extra positional arguments are ambiguous; use --level for level IDs" >&2
  usage
  exit 2
fi

if ((ALL_LEVELS)); then
  LEVELS=()
  for ((level = 0; level < 0x200; level++)); do
    printf -v level_id "%03X" "$level"
    LEVELS+=("$level_id")
  done
elif ((${#LEVELS[@]} == 0)); then
  append_levels "${SMW_IMPORT_LEVELS:-105}"
fi

if [[ -z "$ROM_PATH" ]]; then
  usage
  echo "import-smw: set SMW_ROM_PATH or pass --rom" >&2
  exit 2
fi

if ! [[ "$EXIT_DEPTH" =~ ^[0-9]+$ ]]; then
  echo "import-smw: --exit-depth must be a non-negative integer" >&2
  exit 2
fi

if ((CLEAN_OUT)); then
  case "$OUT_DIR" in
    ""|"/"|".")
      echo "import-smw: refusing to clean unsafe output dir: '$OUT_DIR'" >&2
      exit 2
      ;;
  esac
  rm -rf -- "$OUT_DIR"
fi

args=(python3 "$ROOT/tools/smw_import.py" --rom "$ROM_PATH" --out "$OUT_DIR")
for level in "${LEVELS[@]}"; do
  args+=(--level "$level")
done
if ((INCLUDE_EXIT_TARGETS)); then
  args+=(--include-exit-targets --exit-depth "$EXIT_DEPTH")
fi

printf 'import-smw: rom=%s out=%s levels=%s exit_targets=%s exit_depth=%s\n' \
  "$ROM_PATH" "$OUT_DIR" "${LEVELS[*]}" "$INCLUDE_EXIT_TARGETS" "$EXIT_DEPTH" >&2
"${args[@]}"
