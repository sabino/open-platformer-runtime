#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SMW_ROOT="${SMW_NATIVE_ROOT:-/path/to/native-reference}"
NATIVE_RUNNER="$SMW_ROOT/tools/run-wayland.sh"
OUT_DIR="${SMW_RECORDING_OUT_DIR:-$ROOT/generated/smw/recordings}"
FULL_INPUT="${SMW_RECORDING_FULL_INPUT:-$OUT_DIR/latest-native-full.input}"
LEVEL_INPUT="${SMW_RECORDING_LEVEL_INPUT:-$OUT_DIR/latest-level-start.input}"
LEVEL_FRAME_FILE="${SMW_RECORDING_LEVEL_FRAME:-$OUT_DIR/latest-level-start.frame}"

if [[ ! -x "$NATIVE_RUNNER" ]]; then
  echo "run-native-input-recording-wayland: missing native runner: $NATIVE_RUNNER" >&2
  exit 2
fi

mkdir -p "$OUT_DIR" "$ROOT/generated/smw/native-record-xdg"
rm -f "$FULL_INPUT" "$FULL_INPUT.markers" "$LEVEL_INPUT" "$LEVEL_FRAME_FILE"

cat <<EOF
native_input_recording_schema=1
input=$FULL_INPUT
markers=$FULL_INPUT.markers
level_input=$LEVEL_INPUT
level_frame=$LEVEL_FRAME_FILE

Recording instructions:
  1. Play native SMW from boot/menu normally.
  2. Press K once when level gameplay starts if you want a manual marker.
     The native runtime also writes auto_level_start when it detects level mode.
  3. Close the native window when done; this wrapper will slice the level input.
EOF

XDG_DATA_HOME="$ROOT/generated/smw/native-record-xdg" \
SMW_SWAY_WORKSPACE="${SMW_SWAY_WORKSPACE:-6}" \
"$NATIVE_RUNNER" \
  --native-only \
  --output-method SDL-Software \
  --record-input "$FULL_INPUT" \
  "$@"

if [[ ! -s "$FULL_INPUT" ]]; then
  echo "run-native-input-recording-wayland: native recording did not produce $FULL_INPUT" >&2
  exit 1
fi
if [[ ! -s "$FULL_INPUT.markers" ]]; then
  echo "run-native-input-recording-wayland: native recording did not produce $FULL_INPUT.markers" >&2
  exit 1
fi

level_frame="$("$ROOT/tools/select-input-marker.py" "$FULL_INPUT.markers" --output-frame-file "$LEVEL_FRAME_FILE")"
"$ROOT/tools/slice-input-script.py" "$FULL_INPUT" "$LEVEL_INPUT" --start-frame "$level_frame" >/dev/null

echo "recorded_full_input=$FULL_INPUT"
echo "recorded_markers=$FULL_INPUT.markers"
echo "recorded_level_start_frame=$level_frame"
echo "recorded_level_input=$LEVEL_INPUT"
