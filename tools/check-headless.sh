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
INPUT_SCRIPT="$(mktemp)"
PIPE_SCRIPT="$(mktemp)"
trap 'rm -f "$LOG_FILE" "$INPUT_SCRIPT" "$PIPE_SCRIPT"' EXIT
cat >"$INPUT_SCRIPT" <<'EOF'
# frame-count plus held controls; jump/spin are edge-pressed on the first frame of a segment.
1 right run
1 right run jump
EOF
printf '1 down\n' >"$PIPE_SCRIPT"
"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-autostart 2>&1 | tee "$LOG_FILE"
grep -q "smw-audio: internal_apu=1 samples=3" "$LOG_FILE"
grep -q "smw-runtime: level=105 layer1_objects=92 layer2_objects=0 layer2_bg=1 map16_tiles=1474 collision_rects=25 slope_surfaces=42 coin_pickups=4" "$LOG_FILE"
grep -q "pipe_rects=2" "$LOG_FILE"
grep -q "sprite_spawns=34" "$LOG_FILE"
grep -q "sprite_actors=31" "$LOG_FILE"
grep -q "goal_tapes=1" "$LOG_FILE"
grep -q "player_sprites=8" "$LOG_FILE"
grep -q "smw-runtime: entrance level=105 source=105 secondary=0 settings=0 spawn=16,288" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-autostart --smw-test-screen-exit=7 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: entrance_motion action=4 frames=28 dx=0.00 dy=1.00" "$LOG_FILE"
grep -q "smw-runtime: entrance level=1CB source=1CB secondary=0 settings=4 spawn=16,240" "$LOG_FILE"
grep -q "smw-runtime: level=1CB layer1_objects=16 layer2_objects=0 layer2_bg=1 map16_tiles=585 collision_rects=11 slope_surfaces=0 coin_pickups=7" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-level=1CB 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: level=1CB layer1_objects=16 layer2_objects=0 layer2_bg=1 map16_tiles=585 collision_rects=11 slope_surfaces=0 coin_pickups=7" "$LOG_FILE"
grep -q "pipe_rects=1" "$LOG_FILE"
grep -q "sprite_spawns=0" "$LOG_FILE"
grep -q "sprite_actors=0" "$LOG_FILE"
grep -q "goal_tapes=0" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-level=1CB --smw-test-screen-exit=1 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: entrance_motion action=6 frames=32 dx=4.00 dy=-4.00" "$LOG_FILE"
grep -q "smw-runtime: entrance level=105 source=1CB secondary=1 settings=6 spawn=24,242" "$LOG_FILE"
grep -q "smw-runtime: level=105 layer1_objects=92 layer2_objects=0 layer2_bg=1 map16_tiles=1474 collision_rects=25 slope_surfaces=42 coin_pickups=4" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 1 --smw-test-autostart --smw-test-spawn=272,176 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: coin_pickup level=105 dragon=1 coins=1 dragon_coins=1" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 1 --smw-test-level=1CB --smw-test-spawn=112,240 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: coin_pickup level=1CB dragon=0 coins=1 dragon_coins=0" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 1 --smw-test-autostart --smw-test-spawn=4828,282 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: course_clear level=105" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 1 --smw-test-autostart --smw-test-powerup=small 2>&1 | tee "$LOG_FILE"
grep -q "smw-test-powerup: powerup=0 height=16 render_y=-16" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-input-script="$INPUT_SCRIPT" 2>&1 | tee "$LOG_FILE"
grep -q "smw-input-script: loaded path=$INPUT_SCRIPT segments=2 frames=2" "$LOG_FILE"
grep -q "smw-input-script: done name=$INPUT_SCRIPT frames=2" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-test-spawn=2050,292 --smw-input-script="$PIPE_SCRIPT" 2>&1 | tee "$LOG_FILE"
! grep -q "pipe-debug screen=07" "$LOG_FILE"
grep -q "smw-input-script: done name=$PIPE_SCRIPT frames=1" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-test-spawn=2072,240 --smw-input-script="$PIPE_SCRIPT" 2>&1 | tee "$LOG_FILE"
grep -q "pipe-debug screen=07 target=1CB secondary=0 source=1CB kind=diagonal" "$LOG_FILE"
grep -q "smw-input-script: done name=$PIPE_SCRIPT frames=1" "$LOG_FILE"

"$GODOT_BIN" --headless --audio-driver Dummy --path . --quit-after 1 --smw-audio-preview=Level 2>&1 | tee "$LOG_FILE"
grep -q "smw-audio: music_preview=Level events=12 loop_frames=96" "$LOG_FILE"
