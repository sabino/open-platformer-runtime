#!/usr/bin/env bash
set -euo pipefail

GODOT_BIN="${GODOT_BIN:-godot4-mono}"
SWAY_WORKSPACE="${SMW_SWAY_WORKSPACE:-6}"

if [[ ! -x "$GODOT_BIN" ]]; then
  echo "Godot executable not found or not executable: $GODOT_BIN" >&2
  exit 2
fi

if [[ "${XDG_SESSION_TYPE:-}" == "wayland" || -n "${WAYLAND_DISPLAY:-}" ]]; then
  if command -v swaymsg >/dev/null; then
    swaymsg "workspace $SWAY_WORKSPACE" >/dev/null || true
  fi
fi

export GDK_BACKEND=wayland
export SDL_VIDEODRIVER=wayland
export QT_QPA_PLATFORM=wayland

if command -v swaymsg >/dev/null && command -v jq >/dev/null; then
  "$GODOT_BIN" --display-driver wayland --rendering-driver opengl3 --path . "$@" &
  godot_pid="$!"
  for _ in $(seq 1 80); do
    if ! kill -0 "$godot_pid" 2>/dev/null; then
      wait "$godot_pid"
      exit "$?"
    fi

    rect="$(swaymsg -t get_tree | jq -r --argjson pid "$godot_pid" '
      [
        .. | objects
        | select(.pid? == $pid)
        | select(.rect? and .rect.width > 0 and .rect.height > 0)
        | "\(.rect.x),\(.rect.y) \(.rect.width)x\(.rect.height)"
      ][0] // ""
    ')"
    if [[ -n "$rect" ]]; then
      swaymsg "[pid=\"$godot_pid\"] move container to workspace $SWAY_WORKSPACE" >/dev/null || true
      swaymsg "[pid=\"$godot_pid\"] floating enable" >/dev/null || true
      swaymsg "[pid=\"$godot_pid\"] resize set width 768 px height 672 px" >/dev/null || true
      swaymsg "[pid=\"$godot_pid\"] move position center" >/dev/null || true
      swaymsg "workspace $SWAY_WORKSPACE" >/dev/null || true
      break
    fi
    sleep 0.1
  done
  wait "$godot_pid"
else
  exec "$GODOT_BIN" --display-driver wayland --rendering-driver opengl3 --path . "$@"
fi
