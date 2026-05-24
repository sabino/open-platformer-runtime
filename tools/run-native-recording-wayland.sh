#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SMW_ROOT="${SMW_NATIVE_ROOT:-/path/to/native-reference}"
NATIVE_RUNNER="$SMW_ROOT/tools/run-wayland.sh"
SWAY_WORKSPACE="${SMW_SWAY_WORKSPACE:-6}"
SAVE_DIR="${SMW_NATIVE_SAVE_DIR:-${XDG_DATA_HOME:-$HOME/.local/share}/snesrev/smw_native}"
OUT_DIR="${SMW_NATIVE_RECORD_OUT_DIR:-$ROOT/generated/smw/recordings}"
OUT_SCRIPT="${SMW_NATIVE_RECORD_OUTPUT:-$OUT_DIR/latest-native-recording.input}"
EXTRA_ARGS=()

usage() {
  cat <<'EOF'
Usage: tools/run-native-recording-wayland.sh [--output FILE] [--save-dir DIR] [--] [native args...]

Launches /path/to/native-reference visibly on Wayland, then converts the newest
manual or playthrough snapshot into a Godot input script after the window exits.

During the native run:
  K           clear the input history at the point you want the recording to start
  Shift+F1   save a manual snapshot to save1.sav
  finish map auto-saves a playthrough snapshot when SavePlaythrough is enabled

Keyboard controls in native smw/: arrows move, Z=B/jump, X=A/spin,
A=Y/run, S=X/run, Enter=Start.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --output)
      shift
      OUT_SCRIPT="${1:?--output expects a path}"
      ;;
    --save-dir)
      shift
      SAVE_DIR="${1:?--save-dir expects a path}"
      ;;
    --help)
      usage
      exit 0
      ;;
    --)
      shift
      EXTRA_ARGS+=("$@")
      break
      ;;
    *)
      EXTRA_ARGS+=("$1")
      ;;
  esac
  shift
done

if [[ ! -x "$NATIVE_RUNNER" ]]; then
  echo "run-native-recording-wayland: missing native runner: $NATIVE_RUNNER" >&2
  exit 2
fi

mkdir -p "$SAVE_DIR/playthrough" "$OUT_DIR"
marker="$(mktemp)"
cleanup() {
  rm -f "$marker"
}
trap cleanup EXIT

echo "native_record_schema=1"
echo "native_root=$SMW_ROOT"
echo "save_dir=$SAVE_DIR"
echo "output=$OUT_SCRIPT"
echo "workspace=$SWAY_WORKSPACE"
echo
echo "Recording workflow:"
echo "  1. Press K when the part you want to record begins."
echo "  2. Play normally."
echo "  3. Press Shift+F1 to save save1.sav, or finish a level to create playthrough/*.sav."
echo "  4. Close the native window; this wrapper will convert the newest snapshot."

if command -v swaymsg >/dev/null; then
  swaymsg "workspace $SWAY_WORKSPACE" >/dev/null || true
fi

SMW_SWAY_WORKSPACE="$SWAY_WORKSPACE" "$NATIVE_RUNNER" \
  --native-only \
  --output-method SDL-Software \
  "${EXTRA_ARGS[@]}" &
native_pid=$!

if command -v swaymsg >/dev/null; then
  for _ in $(seq 1 80); do
    if ! kill -0 "$native_pid" 2>/dev/null; then
      break
    fi
    if swaymsg "[pid=\"$native_pid\"] move container to workspace $SWAY_WORKSPACE" >/dev/null 2>&1; then
      swaymsg "[pid=\"$native_pid\"] floating enable" >/dev/null || true
      swaymsg "[pid=\"$native_pid\"] move position center" >/dev/null || true
      swaymsg "workspace $SWAY_WORKSPACE" >/dev/null || true
      break
    fi
    sleep 0.1
  done
fi

set +e
wait "$native_pid"
native_status=$?
set -e
if [[ "$native_status" -ne 0 ]]; then
  echo "run-native-recording-wayland: native run exited with status $native_status" >&2
fi

snapshot="$(
  find "$SAVE_DIR" "$SAVE_DIR/playthrough" -maxdepth 1 -type f -name '*.sav' -newer "$marker" -printf '%T@ %p\n' 2>/dev/null |
    sort -nr |
    awk 'NR == 1 { $1=""; sub(/^ /, ""); print; exit }'
)"

if [[ -z "$snapshot" ]]; then
  echo "run-native-recording-wayland: no new snapshot found in $SAVE_DIR or $SAVE_DIR/playthrough" >&2
  echo "Press K, play, press Shift+F1, then close the native window to capture a manual recording." >&2
  if [[ "$native_status" -ne 0 ]]; then
    exit "$native_status"
  fi
  exit 2
fi

"$ROOT/tools/convert-native-snapshot-input.py" "$snapshot" -o "$OUT_SCRIPT"
echo "recorded_snapshot=$snapshot"
echo "recorded_input=$OUT_SCRIPT"
exit "$native_status"
