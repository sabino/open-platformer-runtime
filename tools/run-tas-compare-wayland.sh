#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SMW_ROOT="${SMW_NATIVE_ROOT:-/path/to/native-reference}"
NATIVE_RUNNER="$SMW_ROOT/tools/run-wayland.sh"
NATIVE_FRAMES="${SMW_COMPARE_NATIVE_FRAMES:-9000}"
GODOT_FRAMES="${SMW_COMPARE_GODOT_FRAMES:-7200}"
SKIP_FRAMES="${SMW_TAS_SKIP_FRAMES:-${SMW_TAS_YI1_SKIP_FRAMES:-1525}}"
SLICE_FRAMES="${SMW_TAS_FRAMES:-${SMW_TAS_YI1_FRAMES:-6000}}"
DELAY_SECONDS="${SMW_COMPARE_GODOT_DELAY_SECONDS:-}"
OVERLAYS=1

usage() {
  cat <<'EOF'
Usage: tools/run-tas-compare-wayland.sh [options]

Launches the native smw reference movie and a Godot diagnostic TAS slice on
Wayland so desync is visible side by side.

Options:
  --no-overlays              Start the Godot window without debug gizmos.
  --native-frames COUNT      Native reference exit frame count.
  --godot-frames COUNT       Godot visible run exit frame count.
  --skip-frames COUNT        Source movie frames skipped for Godot level slice.
  --slice-frames COUNT       Source movie frames converted for Godot.
  --delay-seconds SECONDS    Delay before launching Godot after native.
  --help                     Print this text.

The default skip is 1525 because the native trace reaches active gameplay there
for TASVideos 3849S. That movie enters Yoshi's Island 2 first, so this is a
comparison aid, not a Yoshi Island 1 sync claim.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-overlays)
      OVERLAYS=0
      ;;
    --native-frames)
      shift
      NATIVE_FRAMES="${1:?--native-frames expects a value}"
      ;;
    --godot-frames)
      shift
      GODOT_FRAMES="${1:?--godot-frames expects a value}"
      ;;
    --skip-frames)
      shift
      SKIP_FRAMES="${1:?--skip-frames expects a value}"
      ;;
    --slice-frames)
      shift
      SLICE_FRAMES="${1:?--slice-frames expects a value}"
      ;;
    --delay-seconds)
      shift
      DELAY_SECONDS="${1:?--delay-seconds expects a value}"
      ;;
    --help)
      usage
      exit 0
      ;;
    *)
      echo "run-tas-compare-wayland: unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
  shift
done

if [[ ! -x "$NATIVE_RUNNER" ]]; then
  echo "run-tas-compare-wayland: missing native Wayland runner: $NATIVE_RUNNER" >&2
  exit 2
fi

prepare_output="$(
  SMW_TAS_SKIP_FRAMES="$SKIP_FRAMES" \
  SMW_TAS_FRAMES="$SLICE_FRAMES" \
    "$ROOT/tools/prepare-tas-diagnostic.sh"
)"
printf '%s\n' "$prepare_output"
full_input="$(printf '%s\n' "$prepare_output" | awk -F= '$1 == "full_input" { print $2 }')"
sram="$(printf '%s\n' "$prepare_output" | awk -F= '$1 == "sram" { print $2 }')"
slice="$(printf '%s\n' "$prepare_output" | awk -F= '$1 == "slice" { print $2 }')"

if [[ -z "$full_input" || -z "$sram" || -z "$slice" || ! -s "$full_input" || ! -s "$sram" || ! -s "$slice" ]]; then
  echo "run-tas-compare-wayland: prepared TAS artifacts are missing" >&2
  exit 2
fi

if [[ -z "$DELAY_SECONDS" ]]; then
  DELAY_SECONDS="$(python3 - "$SKIP_FRAMES" <<'PY'
import sys
print(f"{int(sys.argv[1]) / 60.0:.3f}")
PY
)"
fi

echo "run-tas-compare-wayland: native full movie input=$full_input frames=$NATIVE_FRAMES"
echo "run-tas-compare-wayland: godot slice input=$slice frames=$GODOT_FRAMES delay=${DELAY_SECONDS}s"

SMW_SWAY_WORKSPACE="${SMW_SWAY_WORKSPACE:-6}" "$NATIVE_RUNNER" \
  --native-only \
  --output-method SDL-Software \
  --no-audio \
  --allow-opposing-directions \
  --sram "$sram" \
  --input-script "$full_input" \
  --frames "$NATIVE_FRAMES" &
native_pid=$!

sleep "$DELAY_SECONDS"

godot_args=(
  --smw-test-autostart
  --smw-test-powerup=small
  --smw-input-script="$slice"
  --smw-no-audio
  --quit-after "$GODOT_FRAMES"
)
if [[ "$OVERLAYS" == "1" ]]; then
  godot_args+=(--smw-debug-overlays)
fi

SMW_SWAY_WORKSPACE="${SMW_SWAY_WORKSPACE:-6}" "$ROOT/tools/run-wayland.sh" "${godot_args[@]}" &
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
