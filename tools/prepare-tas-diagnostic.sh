#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="${SMW_TAS_OUT_DIR:-$ROOT/generated/smw/tas}"
TAS_ID="${SMW_TAS_ID:-3849S}"
TAS_URL="${SMW_TAS_URL:-https://tasvideos.org/${TAS_ID}?handler=Download}"
SOURCE_ZIP="${SMW_TAS_SOURCE:-$OUT_DIR/${TAS_ID}.zip}"
FULL_INPUT="$OUT_DIR/${TAS_ID}-full.input"
SRAM_OUTPUT="$OUT_DIR/${TAS_ID}.srm"
SLICE_OUTPUT="${SMW_TAS_SLICE_OUTPUT:-$OUT_DIR/${TAS_ID}-diagnostic-slice.input}"
SKIP_FRAMES="${SMW_TAS_SKIP_FRAMES:-${SMW_TAS_YI1_SKIP_FRAMES:-1525}}"
TAKE_FRAMES="${SMW_TAS_FRAMES:-${SMW_TAS_YI1_FRAMES:-6000}}"

mkdir -p "$OUT_DIR"

if [[ ! -s "$SOURCE_ZIP" ]]; then
  if [[ "${SMW_TAS_NO_DOWNLOAD:-0}" == "1" ]]; then
    echo "prepare-tas-diagnostic: missing source and downloads disabled: $SOURCE_ZIP" >&2
    exit 2
  fi
  echo "prepare-tas-diagnostic: downloading $TAS_URL"
  curl -L --fail --silent --show-error -o "$SOURCE_ZIP" "$TAS_URL"
fi

"$ROOT/tools/convert-tas-input.py" "$SOURCE_ZIP" \
  -o "$FULL_INPUT" \
  --sram-output "$SRAM_OUTPUT"

"$ROOT/tools/convert-tas-input.py" "$SOURCE_ZIP" \
  -o "$SLICE_OUTPUT" \
  --skip-frames "$SKIP_FRAMES" \
  --max-frames "$TAKE_FRAMES"

printf 'tas_diagnostic_prepare_schema=1\n'
printf 'tas_id=%s\n' "$TAS_ID"
printf 'source=%s\n' "$SOURCE_ZIP"
printf 'full_input=%s\n' "$FULL_INPUT"
printf 'sram=%s\n' "$SRAM_OUTPUT"
printf 'slice=%s\n' "$SLICE_OUTPUT"
printf 'skip_frames=%s\n' "$SKIP_FRAMES"
printf 'slice_frames=%s\n' "$TAKE_FRAMES"
