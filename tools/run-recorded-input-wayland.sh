#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INPUT_SCRIPT="${1:-$ROOT/generated/smw/recordings/latest-native-recording.input}"
shift $(( $# > 0 ? 1 : 0 ))

if [[ ! -s "$INPUT_SCRIPT" ]]; then
  echo "run-recorded-input-wayland: missing input script: $INPUT_SCRIPT" >&2
  echo "Record one first with tools/run-native-recording-wayland.sh" >&2
  exit 2
fi

exec "$ROOT/tools/run-wayland.sh" \
  --smw-test-autostart \
  --smw-debug-overlays \
  --smw-input-script="$INPUT_SCRIPT" \
  "$@"
