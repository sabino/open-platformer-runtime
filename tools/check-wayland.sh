#!/usr/bin/env bash
set -euo pipefail

GODOT_BIN="${GODOT_BIN:-godot4-mono}"

if [[ ! -x "$GODOT_BIN" ]]; then
  echo "Godot executable not found or not executable: $GODOT_BIN" >&2
  exit 2
fi

if [[ "${XDG_SESSION_TYPE:-}" != "wayland" && -z "${WAYLAND_DISPLAY:-}" ]]; then
  echo "No Wayland session detected. Refusing graphical smoke run." >&2
  exit 2
fi

dotnet build SmwGodotNative.csproj

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

