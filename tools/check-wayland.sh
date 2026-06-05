#!/usr/bin/env bash
set -euo pipefail

GODOT_BIN="${GODOT_BIN:-$(command -v godot4-mono || command -v godot-mono || command -v godot4 || command -v godot || true)}"
SWAY_WORKSPACE="${SMW_SWAY_WORKSPACE:-6}"

if [[ ! -x "$GODOT_BIN" ]]; then
  echo "Godot executable not found or not executable: $GODOT_BIN" >&2
  exit 2
fi

if [[ "${XDG_SESSION_TYPE:-}" != "wayland" && -z "${WAYLAND_DISPLAY:-}" ]]; then
  echo "No Wayland session detected. Refusing graphical smoke run." >&2
  exit 2
fi

dotnet build SmwGodotNative.csproj

if command -v swaymsg >/dev/null; then
  swaymsg "workspace $SWAY_WORKSPACE" >/dev/null || true
fi

export GDK_BACKEND=wayland
export SDL_VIDEODRIVER=wayland
export QT_QPA_PLATFORM=wayland

"$GODOT_BIN" \
  --display-driver wayland \
  --rendering-driver opengl3 \
  --audio-driver Dummy \
  --path . \
  --quit-after 2 \
  --smw-test-autostart
