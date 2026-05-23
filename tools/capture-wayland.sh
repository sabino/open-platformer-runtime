#!/usr/bin/env bash
set -euo pipefail

GODOT_BIN="${GODOT_BIN:-godot4-mono}"
CAPTURE_PATH="${1:-generated/smw/captures/level_105_viewport.png}"
LEVEL_ID="${2:-105}"
SWAY_WORKSPACE="${SMW_SWAY_WORKSPACE:-6}"
CAPTURE_DELAY="${SMW_CAPTURE_DELAY:-1.0}"
LOG_FILE="$(mktemp)"
PID_FILE="$(mktemp)"
EXTRA_GODOT_ARGS=()

if [[ $# -gt 0 ]]; then
  shift
fi
if [[ $# -gt 0 ]]; then
  shift
fi
if [[ $# -gt 0 ]]; then
  EXTRA_GODOT_ARGS=("$@")
fi

cleanup() {
  if [[ -s "$PID_FILE" ]]; then
    local pid
    pid="$(cat "$PID_FILE")"
    if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null || true
      wait "$pid" 2>/dev/null || true
    fi
  fi
  rm -f "$LOG_FILE" "$PID_FILE"
}
trap cleanup EXIT

if [[ ! -x "$GODOT_BIN" ]]; then
  echo "Godot executable not found or not executable: $GODOT_BIN" >&2
  exit 2
fi

if [[ "${XDG_SESSION_TYPE:-}" != "wayland" && -z "${WAYLAND_DISPLAY:-}" ]]; then
  echo "No Wayland session detected. Refusing graphical capture run." >&2
  exit 2
fi

command -v swaymsg >/dev/null
command -v grim >/dev/null
command -v jq >/dev/null

dotnet build SmwGodotNative.csproj

swaymsg "workspace $SWAY_WORKSPACE" >/dev/null

export GDK_BACKEND=wayland
export SDL_VIDEODRIVER=wayland
export QT_QPA_PLATFORM=wayland

"$GODOT_BIN" \
  --display-driver wayland \
  --rendering-driver opengl3 \
  --audio-driver Dummy \
  --path . \
  --smw-test-level="$LEVEL_ID" \
  "${EXTRA_GODOT_ARGS[@]}" >"$LOG_FILE" 2>&1 &
echo "$!" >"$PID_FILE"
godot_pid="$(cat "$PID_FILE")"

find_godot_rect() {
  swaymsg -t get_tree | jq -r --argjson pid "$godot_pid" '
    [
      .. | objects
      | select(.pid? == $pid)
      | select(.rect? and .rect.width > 0 and .rect.height > 0)
      | "\(.rect.x),\(.rect.y) \(.rect.width)x\(.rect.height)"
    ][0] // ""
  '
}

place_godot_window() {
  swaymsg "[pid=\"$godot_pid\"] move container to workspace $SWAY_WORKSPACE" >/dev/null || true
  swaymsg "[pid=\"$godot_pid\"] floating enable" >/dev/null || true
  swaymsg "[pid=\"$godot_pid\"] resize set width 768 px height 672 px" >/dev/null || true
  swaymsg "[pid=\"$godot_pid\"] move position center" >/dev/null || true
  swaymsg "workspace $SWAY_WORKSPACE" >/dev/null || true
}

rect=""
for _ in $(seq 1 80); do
  if ! kill -0 "$godot_pid" 2>/dev/null; then
    cat "$LOG_FILE" >&2
    echo "Godot exited before a compositor window was found." >&2
    exit 1
  fi

  rect="$(find_godot_rect)"
  if [[ -n "$rect" ]]; then
    place_godot_window
    break
  fi
  sleep 0.1
done

if [[ -z "$rect" ]]; then
  cat "$LOG_FILE" >&2
  echo "Could not locate SMW Godot window in Sway tree." >&2
  exit 1
fi

for _ in $(seq 1 120); do
  if grep -q "smw-runtime: level=$LEVEL_ID" "$LOG_FILE"; then
    break
  fi
  if ! kill -0 "$godot_pid" 2>/dev/null; then
    cat "$LOG_FILE" >&2
    echo "Godot exited before runtime level $LEVEL_ID was ready." >&2
    exit 1
  fi
  sleep 0.1
done

if ! grep -q "smw-runtime: level=$LEVEL_ID" "$LOG_FILE"; then
  cat "$LOG_FILE" >&2
  echo "Timed out waiting for runtime level $LEVEL_ID before capture." >&2
  exit 1
fi

sleep "$CAPTURE_DELAY"
rect="$(find_godot_rect)"
if [[ -z "$rect" ]]; then
  cat "$LOG_FILE" >&2
  echo "Could not locate SMW Godot window after moving it to workspace $SWAY_WORKSPACE." >&2
  exit 1
fi
mkdir -p "$(dirname "$CAPTURE_PATH")"
grim -g "$rect" "$CAPTURE_PATH"
cat "$LOG_FILE"

python3 - "$CAPTURE_PATH" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = path.read_bytes()
assert data.startswith(b"\x89PNG\r\n\x1a\n"), path
assert data[12:16] == b"IHDR", path
width = int.from_bytes(data[16:20], "big")
height = int.from_bytes(data[20:24], "big")
assert width > 0 and height > 0, (path, width, height)
print(f"smw-compositor-capture check: {path} {width}x{height}")
PY
