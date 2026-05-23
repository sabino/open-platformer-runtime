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
grep -q "smw-runtime: level=105 layer1_objects=92 layer2_objects=0 layer2_bg=1 map16_tiles=1488 collision_rects=61 slope_surfaces=30" "$LOG_FILE"
grep -q "pipe_rects=1" "$LOG_FILE"
grep -q "sprite_spawns=34" "$LOG_FILE"
grep -q "sprite_actors=31" "$LOG_FILE"
grep -q "goal_tapes=1" "$LOG_FILE"
grep -q "player_sprites=8" "$LOG_FILE"
grep -q "smw-runtime: entrance level=105 source=105 secondary=0 settings=0 spawn=16,288" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-autostart --smw-test-screen-exit=7 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: entrance level=1CB source=1CB secondary=0 settings=4 spawn=16,240" "$LOG_FILE"
grep -q "smw-runtime: level=1CB layer1_objects=16 layer2_objects=0 layer2_bg=1 map16_tiles=585 collision_rects=13 slope_surfaces=0" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-level=1CB 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: level=1CB layer1_objects=16 layer2_objects=0 layer2_bg=1 map16_tiles=585 collision_rects=13 slope_surfaces=0" "$LOG_FILE"
grep -q "pipe_rects=1" "$LOG_FILE"
grep -q "sprite_spawns=0" "$LOG_FILE"
grep -q "sprite_actors=0" "$LOG_FILE"
grep -q "goal_tapes=0" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-level=1CB --smw-test-screen-exit=1 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: entrance level=105 source=1CB secondary=1 settings=6 spawn=16,240" "$LOG_FILE"
grep -q "smw-runtime: level=105 layer1_objects=92 layer2_objects=0 layer2_bg=1 map16_tiles=1488 collision_rects=61 slope_surfaces=30" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 1 --smw-test-autostart --smw-test-spawn=4828,282 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: course_clear level=105" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 1 --smw-test-autostart --smw-test-powerup=small 2>&1 | tee "$LOG_FILE"
grep -q "smw-test-powerup: powerup=0 height=16 render_y=-16" "$LOG_FILE"

"$GODOT_BIN" --headless --audio-driver Dummy --path . --quit-after 1 --smw-audio-preview=Level 2>&1 | tee "$LOG_FILE"
grep -q "smw-audio: music_preview=Level events=12 loop_frames=96" "$LOG_FILE"
