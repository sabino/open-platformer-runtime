#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INPUT_SCRIPT="${SMW_RECORDING_INPUT:-$ROOT/generated/smw/recordings/latest-native-recording.input}"
GODOT_INPUT=""
LEVEL_START_FRAME=""
FRAMES=""
GODOT_EXTRA=()
NATIVE_EXTRA=()

usage() {
  cat <<'EOF'
Usage: tools/run-recording-compare-wayland.sh [options]

Launch native smw/ from cold boot with a frame-counted input script and launch
Godot with the matching level-start input slice. This path never replays or
overwrites native save-state slots.

Options:
  --input FILE             Full native/game-start .input script.
  --godot-input FILE       Godot/level-start .input script. Overrides slicing.
  --level-start-frame N    Slice --input at frame N for Godot.
  --frames COUNT           Limit sliced Godot input length.
  --godot-arg ARG          Extra argument passed to Godot.
  --native-arg ARG         Extra argument passed to smw_native.
  --help                   Print this text.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --input)
      shift
      INPUT_SCRIPT="${1:?--input expects a path}"
      ;;
    --godot-input)
      shift
      GODOT_INPUT="${1:?--godot-input expects a path}"
      ;;
    --level-start-frame)
      shift
      LEVEL_START_FRAME="${1:?--level-start-frame expects a value}"
      ;;
    --frames)
      shift
      FRAMES="${1:?--frames expects a value}"
      ;;
    --godot-arg)
      shift
      GODOT_EXTRA+=("${1:?--godot-arg expects a value}")
      ;;
    --native-arg)
      shift
      NATIVE_EXTRA+=("${1:?--native-arg expects a value}")
      ;;
    --help)
      usage
      exit 0
      ;;
    *)
      echo "run-recording-compare-wayland: unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
  shift
done

if [[ ! -s "$INPUT_SCRIPT" ]]; then
  echo "run-recording-compare-wayland: missing input script: $INPUT_SCRIPT" >&2
  exit 2
fi

if [[ -z "$GODOT_INPUT" ]]; then
  if [[ -n "$LEVEL_START_FRAME" ]]; then
    GODOT_INPUT="$ROOT/generated/smw/recordings/latest-level-start-slice.input"
    slice_args=("--start-frame" "$LEVEL_START_FRAME")
    if [[ -n "$FRAMES" ]]; then
      slice_args+=("--frames" "$FRAMES")
    fi
    "$ROOT/tools/slice-input-script.py" "$INPUT_SCRIPT" "$GODOT_INPUT" "${slice_args[@]}"
  else
    GODOT_INPUT="$INPUT_SCRIPT"
  fi
fi
if [[ ! -s "$GODOT_INPUT" ]]; then
  echo "run-recording-compare-wayland: missing Godot input script: $GODOT_INPUT" >&2
  exit 2
fi

SMW_SWAY_WORKSPACE="${SMW_SWAY_WORKSPACE:-6}" \
  "$ROOT/tools/run-native-input-wayland.sh" "$INPUT_SCRIPT" --no-audio "${NATIVE_EXTRA[@]}" &
native_pid=$!

sleep "${SMW_RECORDING_COMPARE_GODOT_DELAY_SECONDS:-0.5}"

SMW_SWAY_WORKSPACE="${SMW_SWAY_WORKSPACE:-6}" \
  "$ROOT/tools/run-recorded-input-wayland.sh" "$GODOT_INPUT" --smw-no-audio "${GODOT_EXTRA[@]}" &
godot_pid=$!

if command -v swaymsg >/dev/null; then
  sleep 1
  swaymsg "workspace ${SMW_SWAY_WORKSPACE:-6}" >/dev/null || true
  swaymsg "[pid=\"$native_pid\"] move left" >/dev/null || true
  swaymsg "[pid=\"$godot_pid\"] move right" >/dev/null || true
fi

wait "$native_pid"
native_status=$?
wait "$godot_pid"
godot_status=$?
exit $(( native_status != 0 ? native_status : godot_status ))
