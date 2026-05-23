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
DEBUG_COMMAND_FILE="$(mktemp)"
ACTOR_COMMAND_FILE="$(mktemp)"
REX_COMMAND_FILE="$(mktemp)"
BREAK_COMMAND_FILE="$(mktemp)"
PIRANHA_HIDDEN_COMMAND_FILE="$(mktemp)"
PIRANHA_VISIBLE_COMMAND_FILE="$(mktemp)"
RCON_LOG="$(mktemp)"
RCON_PORT=4617
trap 'rm -f "$LOG_FILE" "$INPUT_SCRIPT" "$PIPE_SCRIPT" "$DEBUG_COMMAND_FILE" "$ACTOR_COMMAND_FILE" "$REX_COMMAND_FILE" "$BREAK_COMMAND_FILE" "$PIRANHA_HIDDEN_COMMAND_FILE" "$PIRANHA_VISIBLE_COMMAND_FILE" "$RCON_LOG"' EXIT
cat >"$INPUT_SCRIPT" <<'EOF'
# frame-count plus held controls; jump/spin are edge-pressed on the first frame of a segment.
1 right run
1 right run jump
EOF
printf '1 down\n' >"$PIPE_SCRIPT"
cat >"$DEBUG_COMMAND_FILE" <<'EOF'
pause
spawn 880 304
powerup small
state before
step 1
EOF
cat >"$ACTOR_COMMAND_FILE" <<'EOF'
pause
spawn 528 304
powerup big
step 1
state after_actor_hit
EOF
cat >"$REX_COMMAND_FILE" <<'EOF'
pause
spawn 528 248
powerup big
step 1
EOF
cat >"$BREAK_COMMAND_FILE" <<'EOF'
pause
spawn 1928 224
powerup big
velocity 0 24
spinjump on
step 1
EOF
cat >"$PIRANHA_HIDDEN_COMMAND_FILE" <<'EOF'
pause
spawn 1808 320
powerup big
step 1
EOF
cat >"$PIRANHA_VISIBLE_COMMAND_FILE" <<'EOF'
pause
spawn 2224 240
powerup big
step 1
EOF
"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-autostart 2>&1 | tee "$LOG_FILE"
grep -q "smw-audio: internal_apu=1 samples=3" "$LOG_FILE"
grep -q "smw-runtime: level=105 layer1_objects=92 layer2_objects=0 layer2_bg=1 map16_tiles=1474 collision_rects=25 slope_surfaces=42 pipe_cells=38/10 coin_pickups=4" "$LOG_FILE"
grep -q "pipe_rects=1" "$LOG_FILE"
grep -q "sprite_spawns=34" "$LOG_FILE"
grep -q "sprite_actors=33" "$LOG_FILE"
grep -q "goal_tapes=1" "$LOG_FILE"
grep -q "player_sprites=8" "$LOG_FILE"
grep -q "smw-runtime: entrance level=105 source=105 secondary=0 settings=0 spawn=16,288" "$LOG_FILE"
grep -q "smw-runtime: level_music level=105 music_index=0 bank=Level" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-autostart --smw-test-screen-exit=7 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: entrance_motion action=4 frames=28 dx=0.00 dy=1.00" "$LOG_FILE"
grep -q "smw-runtime: entrance level=1CB source=1CB secondary=0 settings=4 spawn=16,240" "$LOG_FILE"
grep -q "smw-runtime: level=1CB layer1_objects=16 layer2_objects=0 layer2_bg=1 map16_tiles=585 collision_rects=11 slope_surfaces=0 pipe_cells=0/0 coin_pickups=7" "$LOG_FILE"
grep -q "smw-runtime: level_music level=1CB music_index=1 bank=Level" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-level=1CB 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: level=1CB layer1_objects=16 layer2_objects=0 layer2_bg=1 map16_tiles=585 collision_rects=11 slope_surfaces=0 pipe_cells=0/0 coin_pickups=7" "$LOG_FILE"
grep -q "pipe_rects=1" "$LOG_FILE"
grep -q "sprite_spawns=0" "$LOG_FILE"
grep -q "sprite_actors=0" "$LOG_FILE"
grep -q "goal_tapes=0" "$LOG_FILE"
grep -q "smw-runtime: level_music level=1CB music_index=1 bank=Level" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-level=1CB --smw-test-screen-exit=1 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: entrance_motion action=6 frames=32 dx=4.00 dy=-4.00" "$LOG_FILE"
grep -q "smw-runtime: entrance level=105 source=1CB secondary=1 settings=6 spawn=24,242" "$LOG_FILE"
grep -q "smw-runtime: level=105 layer1_objects=92 layer2_objects=0 layer2_bg=1 map16_tiles=1474 collision_rects=25 slope_surfaces=42 pipe_cells=38/10 coin_pickups=4" "$LOG_FILE"

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
grep -q "x=2064.00 y=288.00" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-test-spawn=2072,240 --smw-input-script="$PIPE_SCRIPT" 2>&1 | tee "$LOG_FILE"
! grep -q "pipe-debug screen=07" "$LOG_FILE"
grep -q "smw-input-script: done name=$PIPE_SCRIPT frames=1" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$DEBUG_COMMAND_FILE" 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$DEBUG_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=before" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "x=896.00 y=304.00" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$ACTOR_COMMAND_FILE" 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$ACTOR_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=after_actor_hit" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "pow=0" "$LOG_FILE"
grep -q "ys=-32" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$REX_COMMAND_FILE" 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$REX_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "pow=1" "$LOG_FILE"
grep -q "ys=-48" "$LOG_FILE"
grep -q "actor_event=stomp:AB:1" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$BREAK_COMMAND_FILE" 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$BREAK_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-runtime: block_break level=105 count=2 total=2" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "blocks=2" "$LOG_FILE"
grep -q "tile=120,20:----" "$LOG_FILE"
grep -q "solids=24" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$PIRANHA_HIDDEN_COMMAND_FILE" 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$PIRANHA_HIDDEN_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "near=4F:0:1808.00,288.00" "$LOG_FILE"
grep -q "pow=1" "$LOG_FILE"
grep -q "actor_event=none" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$PIRANHA_VISIBLE_COMMAND_FILE" 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$PIRANHA_VISIBLE_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "near=4F:2:2224.00,240.00" "$LOG_FILE"
grep -q "actor_event=hurt:4F:2" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 600 --smw-test-autostart --smw-debug-rcon="$RCON_PORT" >"$LOG_FILE" 2>&1 &
RCON_PID="$!"
for _ in $(seq 1 80); do
  if grep -q "smw-rcon: listening=127.0.0.1:$RCON_PORT" "$LOG_FILE"; then
    break
  fi
  if ! kill -0 "$RCON_PID" 2>/dev/null; then
    wait "$RCON_PID"
  fi
  sleep 0.05
done
grep -q "smw-rcon: listening=127.0.0.1:$RCON_PORT" "$LOG_FILE"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh pause | tee "$RCON_LOG"
grep -q "ok paused=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh spawn 880 304 | tee "$RCON_LOG"
grep -q "x=880.00" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh powerup small | tee "$RCON_LOG"
grep -q "pow=0" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh step 1 | tee "$RCON_LOG"
grep -q "ok step_queued=1" "$RCON_LOG"
for _ in $(seq 1 80); do
  if grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"; then
    break
  fi
  sleep 0.05
done
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "x=896.00 y=304.00" "$LOG_FILE"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh quit >/dev/null || true
wait "$RCON_PID" || true

"$GODOT_BIN" --headless --audio-driver Dummy --path . --quit-after 1 --smw-audio-preview=Level 2>&1 | tee "$LOG_FILE"
grep -q "smw-audio: music_preview=Level events=12 loop_frames=96" "$LOG_FILE"
