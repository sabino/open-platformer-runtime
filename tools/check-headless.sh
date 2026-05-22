#!/usr/bin/env bash
set -euo pipefail

GODOT_BIN="${GODOT_BIN:-godot4-mono}"

if [[ ! -x "$GODOT_BIN" ]]; then
  echo "Godot executable not found or not executable: $GODOT_BIN" >&2
  exit 2
fi

export GDK_BACKEND="${GDK_BACKEND:-wayland}"
export SDL_VIDEODRIVER="${SDL_VIDEODRIVER:-wayland}"
export QT_QPA_PLATFORM="${QT_QPA_PLATFORM:-wayland}"

"$GODOT_BIN" --version

dotnet build SmwGodotNative.csproj
LOG_FILE="$(mktemp)"
trap 'rm -f "$LOG_FILE"' EXIT
"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-autostart 2>&1 | tee "$LOG_FILE"
grep -q "smw-audio: internal_apu=1 samples=3" "$LOG_FILE"
grep -q "smw-runtime: level=105 layer1_objects=92 layer2_objects=0 map16_tiles=1408 collision_rects=" "$LOG_FILE"
grep -q "pipe_rects=1" "$LOG_FILE"
grep -q "sprite_spawns=34" "$LOG_FILE"
grep -q "player_sprites=8" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-level=1CB 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: level=1CB layer1_objects=16 layer2_objects=0 map16_tiles=567 collision_rects=" "$LOG_FILE"
grep -q "pipe_rects=0" "$LOG_FILE"
grep -q "sprite_spawns=0" "$LOG_FILE"
