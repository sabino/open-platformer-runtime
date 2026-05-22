#!/usr/bin/env bash
set -euo pipefail

ROM_PATH="${1:-${SMW_ROM_PATH:-}}"
OUT_DIR="${2:-generated/smw}"

if [[ -z "$ROM_PATH" ]]; then
  echo "usage: tools/import-smw.sh /path/to/compatible-rom.sfc [out-dir]" >&2
  echo "or set SMW_ROM_PATH" >&2
  exit 2
fi

python3 tools/smw_import.py --rom "$ROM_PATH" --out "$OUT_DIR" --level 105 --include-exit-targets --exit-depth 1
