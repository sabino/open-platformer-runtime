#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${GODOT_BIN:-godot4-mono}"
LOG_FILE="$(mktemp)"
cleanup() {
  local code=$?
  if [[ "$code" -ne 0 ]]; then
    sed -n '1,240p' "$LOG_FILE" >&2 || true
  fi
  rm -f "$LOG_FILE"
  exit "$code"
}
trap cleanup EXIT

"$GODOT_BIN" --headless --path "$ROOT" --build-solutions --quit >/dev/null

SMW_TAS_FRAMES="${SMW_TAS_FRAMES:-900}" \
SMW_TAS_RUN_FRAMES="${SMW_TAS_RUN_FRAMES:-180}" \
  "$ROOT/tools/run-tas-diagnostic.sh" --headless --smw-no-audio >"$LOG_FILE" 2>&1

grep -q "tas_diagnostic_prepare_schema=1" "$LOG_FILE"
grep -q "tas_id=3849S" "$LOG_FILE"
grep -q "smw-input-script: loaded" "$LOG_FILE"
grep -q "smw-runtime: level=105" "$LOG_FILE"
grep -q "segments=" "$LOG_FILE"
grep -q "frames=900" "$LOG_FILE"

printf 'tas_diagnostic_check_schema=1\n'
printf 'status=diagnostic-input-load-ok\n'
printf 'log=%s\n' "$LOG_FILE"
