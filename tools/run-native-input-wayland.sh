#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SMW_ROOT="${SMW_NATIVE_ROOT:-/path/to/native-reference}"
NATIVE_RUNNER="$SMW_ROOT/tools/run-wayland.sh"
INPUT_SCRIPT="${1:-${SMW_RECORDING_INPUT:-$ROOT/generated/smw/recordings/latest-native-full.input}}"
if [[ $# -gt 0 ]]; then
  shift
fi

if [[ ! -x "$NATIVE_RUNNER" ]]; then
  echo "run-native-input-wayland: missing native runner: $NATIVE_RUNNER" >&2
  exit 2
fi
if [[ ! -s "$INPUT_SCRIPT" ]]; then
  echo "run-native-input-wayland: missing input script: $INPUT_SCRIPT" >&2
  exit 2
fi

mkdir -p "$ROOT/generated/smw/native-xdg"

echo "native_input_schema=1"
echo "input=$INPUT_SCRIPT"
echo "xdg_data_home=$ROOT/generated/smw/native-xdg"

XDG_DATA_HOME="$ROOT/generated/smw/native-xdg" \
SMW_SWAY_WORKSPACE="${SMW_SWAY_WORKSPACE:-6}" \
exec "$NATIVE_RUNNER" \
  --native-only \
  --output-method SDL-Software \
  --input-script "$INPUT_SCRIPT" \
  "$@"
