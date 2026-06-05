#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SMW_ROOT="${SMW_NATIVE_ROOT:-}"
NATIVE_RUNNER="$SMW_ROOT/tools/run-wayland.sh"
NATIVE_BINARY="${SMW_NATIVE_BINARY:-$SMW_ROOT/build/smw_native}"
NATIVE_ASSET_BUNDLE="${SMW_NATIVE_ASSET_BUNDLE:-$SMW_ROOT/smw_assets.dat}"
GODOT_BIN="${GODOT_BIN:-$(command -v godot4-mono || command -v godot-mono || command -v godot4 || command -v godot || true)}"
INPUT_SCRIPT="${SMW_RECORDING_INPUT:-$ROOT/generated/smw/recordings/latest-native-full.input}"
GODOT_INPUT=""
OUT_DIR="${SMW_TRACE_OUT_DIR:-$ROOT/generated/smw/traces}"
FRAMES="${SMW_TRACE_FRAMES:-600}"
TRACE_EVERY="${SMW_TRACE_EVERY:-1}"
TOLERANCE="${SMW_TRACE_TOLERANCE:-1.0}"
NATIVE_Y_OFFSET="${SMW_TRACE_NATIVE_Y_OFFSET:--64.0}"
GODOT_INPUT_DELAY="${SMW_TRACE_GODOT_INPUT_DELAY:-2}"
GODOT_TRACE_TIMEOUT_SECONDS="${SMW_TRACE_GODOT_TIMEOUT_SECONDS:-120}"
LEVEL_START_FRAME=""
NATIVE_START_LEVEL="${SMW_TRACE_NATIVE_START_LEVEL:-41}"
NATIVE_START_GAME_MODE="${SMW_TRACE_NATIVE_START_GAME_MODE:-20}"
HEADLESS="${SMW_TRACE_HEADLESS:-0}"
NATIVE_XDG_HOME="${SMW_NATIVE_XDG_HOME:-$ROOT/generated/smw/native-xdg}"

usage() {
  cat <<'EOF'
Usage: tools/run-recording-trace-compare.sh [options]

Runs native smw/ from cold boot with --input-script in an isolated generated
XDG data directory, runs Godot with the matching level-start input, normalizes
both traces, and reports the first position/power-up divergence. This path does
not replay, copy, or overwrite native save-state slots.

Options:
  --frames COUNT              Number of frames to run and trace.
  --trace-every COUNT         Native state trace cadence.
  --input FILE                Full native/game-start .input script.
  --godot-input FILE          Godot/level-start .input script.
  --level-start-frame N       Slice --input at frame N for Godot.
  --out-dir DIR               Output directory for logs/jsonl.
  --tolerance PX              Position tolerance in pixels.
  --native-y-offset PX        Native player_y offset before comparing to Godot.
  --godot-input-delay N       Neutral frames prepended to sliced Godot input. Default 2.
  --native-start-level ID     First native level id to compare. Default 41.
  --native-start-game-mode ID First native game_mode to compare. Default 20.
  --headless                  Run native through SDL dummy and Godot --headless.
  --help                      Print this text.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --frames)
      shift
      FRAMES="${1:?--frames expects a value}"
      ;;
    --trace-every)
      shift
      TRACE_EVERY="${1:?--trace-every expects a value}"
      ;;
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
    --out-dir)
      shift
      OUT_DIR="${1:?--out-dir expects a path}"
      ;;
    --tolerance)
      shift
      TOLERANCE="${1:?--tolerance expects a value}"
      ;;
    --native-y-offset)
      shift
      NATIVE_Y_OFFSET="${1:?--native-y-offset expects a value}"
      ;;
    --godot-input-delay)
      shift
      GODOT_INPUT_DELAY="${1:?--godot-input-delay expects a value}"
      ;;
    --native-start-level)
      shift
      NATIVE_START_LEVEL="${1:?--native-start-level expects a value}"
      ;;
    --native-start-game-mode)
      shift
      NATIVE_START_GAME_MODE="${1:?--native-start-game-mode expects a value}"
      ;;
    --headless)
      HEADLESS=1
      ;;
    --help)
      usage
      exit 0
      ;;
    *)
      echo "run-recording-trace-compare: unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
  shift
done

if [[ -z "$SMW_ROOT" ]]; then
  echo "run-recording-trace-compare: set SMW_NATIVE_ROOT to the native reference checkout" >&2
  exit 2
fi

if [[ "$HEADLESS" == "1" ]]; then
  if [[ ! -x "$NATIVE_BINARY" ]]; then
    echo "run-recording-trace-compare: missing native binary: $NATIVE_BINARY" >&2
    exit 2
  fi
  if [[ ! -s "$NATIVE_ASSET_BUNDLE" ]]; then
    echo "run-recording-trace-compare: missing native asset bundle: $NATIVE_ASSET_BUNDLE" >&2
    exit 2
  fi
  if [[ ! -x "$GODOT_BIN" ]]; then
    echo "run-recording-trace-compare: missing Godot binary: $GODOT_BIN" >&2
    exit 2
  fi
else
  if [[ ! -x "$NATIVE_RUNNER" ]]; then
    echo "run-recording-trace-compare: missing native runner: $NATIVE_RUNNER" >&2
    exit 2
  fi
fi
if [[ ! -s "$INPUT_SCRIPT" ]]; then
  echo "run-recording-trace-compare: missing input script: $INPUT_SCRIPT" >&2
  exit 2
fi
if ! [[ "$GODOT_INPUT_DELAY" =~ ^[0-9]+$ ]]; then
  echo "run-recording-trace-compare: --godot-input-delay must be a non-negative integer" >&2
  exit 2
fi
if [[ "$OUT_DIR" != /* ]]; then
  OUT_DIR="$ROOT/$OUT_DIR"
fi
if [[ -n "$GODOT_INPUT" && "$GODOT_INPUT" != /* ]]; then
  GODOT_INPUT="$ROOT/$GODOT_INPUT"
fi

mkdir -p "$OUT_DIR" "$NATIVE_XDG_HOME"
config_file="$NATIVE_XDG_HOME/smw-clean-trace.ini"
video_driver="Wayland"
if [[ "$HEADLESS" == "1" ]]; then
  video_driver="dummy"
fi
cat >"$config_file" <<EOF
[General]
Autosave = 0
SavePlaythrough = 0
DisableFrameDelay = 1
AllowOpposingDirections = 0
RunMode = Native

[Graphics]
Fullscreen = 0
WindowScale = 3
OutputMethod = SDL-Software
VideoDriver = $video_driver
NoSpriteLimits = 1

[Sound]
EnableAudio = 0

[GamepadMap]
EnableGamepad1 = false
EnableGamepad2 = false
EOF
rm -f "$NATIVE_XDG_HOME/snesrev/smw_native/save0.sav"

if [[ -z "$GODOT_INPUT" ]]; then
  if [[ -n "$LEVEL_START_FRAME" ]]; then
    GODOT_INPUT="$OUT_DIR/godot-level-start.input"
    "$ROOT/tools/slice-input-script.py" "$INPUT_SCRIPT" "$GODOT_INPUT" --start-frame "$LEVEL_START_FRAME" --frames "$FRAMES" --prepad-frames "$GODOT_INPUT_DELAY" >/dev/null
  elif [[ -s "$INPUT_SCRIPT.markers" ]]; then
    LEVEL_START_FRAME="$("$ROOT/tools/select-input-marker.py" "$INPUT_SCRIPT.markers")"
    GODOT_INPUT="$OUT_DIR/godot-level-start.input"
    "$ROOT/tools/slice-input-script.py" "$INPUT_SCRIPT" "$GODOT_INPUT" --start-frame "$LEVEL_START_FRAME" --frames "$FRAMES" --prepad-frames "$GODOT_INPUT_DELAY" >/dev/null
  else
    GODOT_INPUT="$INPUT_SCRIPT"
  fi
fi
if [[ ! -s "$GODOT_INPUT" ]]; then
  echo "run-recording-trace-compare: missing Godot input script: $GODOT_INPUT" >&2
  exit 2
fi

NATIVE_FRAMES="$FRAMES"
if [[ -n "$LEVEL_START_FRAME" ]]; then
  NATIVE_FRAMES=$((LEVEL_START_FRAME + FRAMES))
fi

godot_commands="$OUT_DIR/godot-trace-live.commands"
{
  printf 'trace_live %s tag=recording quit_when_done\n' "$FRAMES"
} >"$godot_commands"

native_log="$OUT_DIR/native.log"
godot_log="$OUT_DIR/godot.log"
native_jsonl="$OUT_DIR/native.jsonl"
godot_jsonl="$OUT_DIR/godot.jsonl"

if [[ "$HEADLESS" == "1" ]]; then
  XDG_DATA_HOME="$NATIVE_XDG_HOME" \
  SDL_VIDEODRIVER=dummy \
  SDL_AUDIODRIVER=dummy \
  "$NATIVE_BINARY" \
    --config "$config_file" \
    --asset-bundle "$NATIVE_ASSET_BUNDLE" \
    --native-only \
    --output-method SDL-Software \
    --video-driver dummy \
    --no-audio \
    --disable-frame-delay \
    --input-script "$INPUT_SCRIPT" \
    --state-trace-every "$TRACE_EVERY" \
    --frames "$NATIVE_FRAMES" \
    >"$native_log" 2>&1

  if [[ "${SMW_SKIP_DOTNET_BUILD:-0}" != "1" ]]; then
    (cd "$ROOT" && dotnet build SmwGodotNative.csproj --no-restore >/dev/null)
  fi
  (
    cd "$ROOT"
    "$GODOT_BIN" \
    --headless \
    --audio-driver Dummy \
    --path . \
    --quit-after "$FRAMES" \
    --smw-test-autostart \
    --smw-test-powerup=small \
    --smw-no-audio \
    --smw-input-script="$GODOT_INPUT" \
    --smw-debug-command-file="$godot_commands" \
      >"$godot_log" 2>&1
  ) &
else
  XDG_DATA_HOME="$NATIVE_XDG_HOME" \
  SMW_NATIVE_CONFIG="$config_file" \
  "$NATIVE_RUNNER" \
    --native-only \
    --output-method SDL-Software \
    --no-audio \
    --disable-frame-delay \
    --input-script "$INPUT_SCRIPT" \
    --state-trace-every "$TRACE_EVERY" \
    --frames "$NATIVE_FRAMES" \
    >"$native_log" 2>&1

  SMW_SWAY_WORKSPACE="${SMW_SWAY_WORKSPACE:-6}" \
  "$ROOT/tools/run-wayland.sh" \
    --quit-after "$FRAMES" \
    --smw-test-autostart \
    --smw-test-powerup=small \
    --smw-no-audio \
    --smw-input-script="$GODOT_INPUT" \
    --smw-debug-command-file="$godot_commands" \
    >"$godot_log" 2>&1 &
fi
godot_runner_pid=$!
godot_completed=0
for _ in $(seq 1 $((GODOT_TRACE_TIMEOUT_SECONDS * 5))); do
  if ! kill -0 "$godot_runner_pid" 2>/dev/null; then
    wait "$godot_runner_pid"
    godot_completed=1
    break
  fi
  if rg -q 'smw-debug-state: tag=recording_done' "$godot_log" 2>/dev/null; then
    godot_completed=1
    pkill -TERM -P "$godot_runner_pid" 2>/dev/null || true
    kill -TERM "$godot_runner_pid" 2>/dev/null || true
    wait "$godot_runner_pid" 2>/dev/null || true
    break
  fi
  sleep 0.2
done
if [[ "$godot_completed" != "1" ]]; then
  pkill -TERM -P "$godot_runner_pid" 2>/dev/null || true
  kill -TERM "$godot_runner_pid" 2>/dev/null || true
  wait "$godot_runner_pid" 2>/dev/null || true
  echo "run-recording-trace-compare: Godot trace did not finish within ${GODOT_TRACE_TIMEOUT_SECONDS}s" >&2
  exit 1
fi

set +e
compare_args=(
  --native-log "$native_log" \
  --godot-log "$godot_log" \
  --native-jsonl "$native_jsonl" \
  --godot-jsonl "$godot_jsonl" \
  --expected-comparable-records "$FRAMES" \
  --tolerance "$TOLERANCE" \
  --native-y-offset "$NATIVE_Y_OFFSET"
)
if [[ -n "$LEVEL_START_FRAME" ]]; then
  compare_args+=(--native-start-frame "$LEVEL_START_FRAME")
else
  compare_args+=(
    --native-start-level "$NATIVE_START_LEVEL"
    --native-start-game-mode "$NATIVE_START_GAME_MODE"
  )
fi
"$ROOT/tools/compare-recording-traces.py" "${compare_args[@]}"
status=$?
set -e

printf 'native_log=%s\n' "$native_log"
printf 'godot_log=%s\n' "$godot_log"
printf 'native_jsonl=%s\n' "$native_jsonl"
printf 'godot_jsonl=%s\n' "$godot_jsonl"
printf 'godot_input_delay=%s\n' "$GODOT_INPUT_DELAY"
printf 'headless=%s\n' "$HEADLESS"
exit "$status"
