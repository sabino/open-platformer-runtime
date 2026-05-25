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
COIN_LIFE_COMMAND_FILE="$(mktemp)"
DRAGON_LIFE_COMMAND_FILE="$(mktemp)"
ACTOR_COMMAND_FILE="$(mktemp)"
ACTORS_OFF_COMMAND_FILE="$(mktemp)"
HURT_BLINK_COMMAND_FILE="$(mktemp)"
SMALL_HURT_COMMAND_FILE="$(mktemp)"
REX_COMMAND_FILE="$(mktemp)"
BANZAI_UNDER_COMMAND_FILE="$(mktemp)"
BREAK_COMMAND_FILE="$(mktemp)"
WING_BLOCK_COMMAND_FILE="$(mktemp)"
WING_BLOCK_REWARD_COMMAND_FILE="$(mktemp)"
ITEM_COLLECT_COMMAND_FILE="$(mktemp)"
FIREBALL_COMMAND_FILE="$(mktemp)"
AUTOPLAY_COMMAND_FILE="$(mktemp)"
ACTORS_AUTOPLAY_COMMAND_FILE="$(mktemp)"
START_IDLE_HURT_COMMAND_FILE="$(mktemp)"
STAR_ITEM_COMMAND_FILE="$(mktemp)"
STAR_HIT_COMMAND_FILE="$(mktemp)"
STATIC_QUESTION_COMMAND_FILE="$(mktemp)"
PIRANHA_HIDDEN_COMMAND_FILE="$(mktemp)"
PIRANHA_VISIBLE_COMMAND_FILE="$(mktemp)"
C7_NORMAL_VISUAL_COMMAND_FILE="$(mktemp)"
C7_DEBUG_VISUAL_COMMAND_FILE="$(mktemp)"
INVISIBLE_MUSHROOM_COMMAND_FILE="$(mktemp)"
SLOPE_PROBE_COMMAND_FILE="$(mktemp)"
PIPE_UNDERSIDE_COMMAND_FILE="$(mktemp)"
PIPE_SLOPE_SUPPORT_COMMAND_FILE="$(mktemp)"
PIPE_UNDERSIDE_JUMP_COMMAND_FILE="$(mktemp)"
PIPE_HELD_DOWN_COMMAND_FILE="$(mktemp)"
DEATH_COMMAND_FILE="$(mktemp)"
TIME_UP_COMMAND_FILE="$(mktemp)"
GAME_OVER_COMMAND_FILE="$(mktemp)"
PAUSE_COMMAND_FILE="$(mktemp)"
TRACE_COMMAND_FILE="$(mktemp)"
TRACE_LIVE_COMMAND_FILE="$(mktemp)"
COURSE_CLEAR_COMMAND_FILE="$(mktemp)"
RCON_LOG="$(mktemp)"
RCON_PORT=4617
trap 'rm -f "$LOG_FILE" "$INPUT_SCRIPT" "$PIPE_SCRIPT" "$DEBUG_COMMAND_FILE" "$COIN_LIFE_COMMAND_FILE" "$DRAGON_LIFE_COMMAND_FILE" "$ACTOR_COMMAND_FILE" "$ACTORS_OFF_COMMAND_FILE" "$HURT_BLINK_COMMAND_FILE" "$SMALL_HURT_COMMAND_FILE" "$REX_COMMAND_FILE" "$BANZAI_UNDER_COMMAND_FILE" "$BREAK_COMMAND_FILE" "$WING_BLOCK_COMMAND_FILE" "$WING_BLOCK_REWARD_COMMAND_FILE" "$ITEM_COLLECT_COMMAND_FILE" "$FIREBALL_COMMAND_FILE" "$AUTOPLAY_COMMAND_FILE" "$ACTORS_AUTOPLAY_COMMAND_FILE" "$START_IDLE_HURT_COMMAND_FILE" "$STAR_ITEM_COMMAND_FILE" "$STAR_HIT_COMMAND_FILE" "$STATIC_QUESTION_COMMAND_FILE" "$PIRANHA_HIDDEN_COMMAND_FILE" "$PIRANHA_VISIBLE_COMMAND_FILE" "$C7_NORMAL_VISUAL_COMMAND_FILE" "$C7_DEBUG_VISUAL_COMMAND_FILE" "$INVISIBLE_MUSHROOM_COMMAND_FILE" "$SLOPE_PROBE_COMMAND_FILE" "$PIPE_UNDERSIDE_COMMAND_FILE" "$PIPE_SLOPE_SUPPORT_COMMAND_FILE" "$PIPE_UNDERSIDE_JUMP_COMMAND_FILE" "$PIPE_HELD_DOWN_COMMAND_FILE" "$DEATH_COMMAND_FILE" "$TIME_UP_COMMAND_FILE" "$GAME_OVER_COMMAND_FILE" "$PAUSE_COMMAND_FILE" "$TRACE_COMMAND_FILE" "$TRACE_LIVE_COMMAND_FILE" "$COURSE_CLEAR_COMMAND_FILE" "$RCON_LOG"' EXIT
cat >"$INPUT_SCRIPT" <<'EOF'
# frame-count plus held controls; jump/spin are edge-pressed on the first frame of a segment.
@allow-opposing-directions
1 Start
1 right run
1 Right+Y+B
1 L,R,Select
EOF
printf '1 down\n' >"$PIPE_SCRIPT"

"$GODOT_BIN" --headless --path . --quit-after 1 2>&1 | tee "$LOG_FILE"
grep -q "smw-input-map: keyboard=1 gamepad=1 buttons=11 axes=4" "$LOG_FILE"
grep -q "smw-audio: disabled=1" "$LOG_FILE"
grep -q "smw-menu-audio: samples=0 buttons=5" "$LOG_FILE"
grep -q "sfx_buttons=6 music_buttons=4" "$LOG_FILE"
grep -q "smw-menu: assets=1 audio=0 actors=1 actor_visuals=1 level_preview=1 player_preview=1" "$LOG_FILE"
! grep -q "smw-runtime: level=" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 3 --smw-title-start --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-menu: assets=1 audio=0 actors=1 actor_visuals=1 level_preview=1 player_preview=1" "$LOG_FILE"
grep -q "smw-menu: title_start=1" "$LOG_FILE"
grep -q "smw-runtime: level=105" "$LOG_FILE"

cat >"$ACTORS_OFF_COMMAND_FILE" <<'EOF'
pause
state actors_cli
EOF
"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-actors=off --smw-actor-visuals=off --smw-debug-command-file="$ACTORS_OFF_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-menu: assets=1 audio=0 actors=0 actor_visuals=0 level_preview=1 player_preview=1" "$LOG_FILE"
grep -q "tag=actors_cli" "$LOG_FILE"
grep -q "actors_on=0 actor_visuals=0" "$LOG_FILE"

cat >"$DEBUG_COMMAND_FILE" <<'EOF'
pause
spawn 880 304
powerup small
state before
step 1
EOF
cat >"$COIN_LIFE_COMMAND_FILE" <<'EOF'
pause
level 1CB
spawn 112 240
coins 99
step 4
EOF
cat >"$DRAGON_LIFE_COMMAND_FILE" <<'EOF'
pause
spawn 272 176
dragon 4
step 1
EOF
cat >"$ACTOR_COMMAND_FILE" <<'EOF'
pause
spawn 512 304
powerup big
step 3
state after_actor_hit
EOF
cat >"$HURT_BLINK_COMMAND_FILE" <<'EOF'
pause
actors on
spawn 528 304 big
trace_oam 8 none tag=hurt_blink
EOF
cat >"$SMALL_HURT_COMMAND_FILE" <<'EOF'
pause
spawn 512 264
powerup small
step 3
state after_small_hurt
EOF
cat >"$REX_COMMAND_FILE" <<'EOF'
pause
spawn 752 268
powerup big
step 3
EOF
cat >"$BANZAI_UNDER_COMMAND_FILE" <<'EOF'
pause
spawn 440 288 big
actors on
ground on
step 24
actors_near 256
EOF
cat >"$BREAK_COMMAND_FILE" <<'EOF'
pause
spawn 1928 224
powerup big
velocity 0 24
spinjump on
step 1
EOF
cat >"$WING_BLOCK_COMMAND_FILE" <<'EOF'
pause
spawn 592 206
powerup big
velocity 0 24
step 3
EOF
cat >"$WING_BLOCK_REWARD_COMMAND_FILE" <<'EOF'
pause
spawn 592 256
powerup big
velocity 0 -64
step 3
EOF
cat >"$ITEM_COLLECT_COMMAND_FILE" <<'EOF'
pause
spawn 592 224 big
item flower 592 224
step 1
EOF
cat >"$FIREBALL_COMMAND_FILE" <<'EOF'
pause
spawn 496 288 fire
god on
tap run
step 4
EOF
cat >"$AUTOPLAY_COMMAND_FILE" <<'EOF'
pause
spawn 32 288 small
ground on
actors off
autoplay explore
step 3000
EOF
cat >"$ACTORS_AUTOPLAY_COMMAND_FILE" <<'EOF'
pause
spawn 16 288 big
ground on
god on
actors on
autoplay explore
step 5000
EOF
cat >"$START_IDLE_HURT_COMMAND_FILE" <<'EOF'
pause
spawn 16 288 big
ground on
step 420
state after_start_idle
EOF
cat >"$STAR_ITEM_COMMAND_FILE" <<'EOF'
pause
spawn 592 224 big
item star 592 224
step 1
EOF
cat >"$STAR_HIT_COMMAND_FILE" <<'EOF'
pause
spawn 528 304 big
star FF
step 3
EOF
cat >"$STATIC_QUESTION_COMMAND_FILE" <<'EOF'
pause
spawn 3888 224 big
velocity 0 -64
step 5
EOF
cat >"$PIRANHA_HIDDEN_COMMAND_FILE" <<'EOF'
pause
spawn 1808 320
powerup big
actors_near 160
step 1
EOF
cat >"$PIRANHA_VISIBLE_COMMAND_FILE" <<'EOF'
pause
spawn 2224 240
powerup big
step 3
EOF
cat >"$C7_NORMAL_VISUAL_COMMAND_FILE" <<'EOF'
pause
spawn 1632 304
actors_near 128
EOF
cat >"$C7_DEBUG_VISUAL_COMMAND_FILE" <<'EOF'
pause
overlays on
spawn 1632 304
actors_near 128
EOF
cat >"$INVISIBLE_MUSHROOM_COMMAND_FILE" <<'EOF'
pause
spawn 1632 272 big
step 3
EOF
cat >"$SLOPE_PROBE_COMMAND_FILE" <<'EOF'
pause
spawn 921 200 small
velocity 2 0
step 1
EOF
cat >"$PIPE_UNDERSIDE_COMMAND_FILE" <<'EOF'
pause
spawn 2056 304
powerup small
velocity -3 0
step 1
EOF
cat >"$PIPE_SLOPE_SUPPORT_COMMAND_FILE" <<'EOF'
pause
actors off
spawn 2025 240 small
velocity -3 0
step 1
EOF
cat >"$PIPE_UNDERSIDE_JUMP_COMMAND_FILE" <<'EOF'
pause
actors off
spawn 2048 304 small
ground on
trace_full 3 jump tag=pipe_underside_jump
EOF
cat >"$PIPE_HELD_DOWN_COMMAND_FILE" <<'EOF'
pause
actors off
spawn 1928 224 small
hold down
step 80
EOF
cat >"$DEATH_COMMAND_FILE" <<'EOF'
pause
spawn 128 640
velocity 0 64
step 1
state after_death
EOF
cat >"$TIME_UP_COMMAND_FILE" <<'EOF'
pause
timer frames 1
step 1
state after_time_up
EOF
cat >"$GAME_OVER_COMMAND_FILE" <<'EOF'
pause
lives 1
timer frames 1
step 1
EOF
cat >"$PAUSE_COMMAND_FILE" <<'EOF'
pause
spawn 32 288
timer frames 120
game_pause on
step 3
EOF
cat >"$TRACE_COMMAND_FILE" <<'EOF'
pause
spawn 32 288
velocity 0 0
ground on
trace 3 right run jump tag=jump_probe
EOF
cat >"$TRACE_LIVE_COMMAND_FILE" <<'EOF'
pause
spawn 32 288
velocity 0 0
ground on
actors off
autoplay explore
trace_live 3 tag=autoplay_probe
EOF
cat >"$COURSE_CLEAR_COMMAND_FILE" <<'EOF'
pause
spawn 4828 282 big
trace 12 none tag=goal_walkout
EOF
"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-autostart --smw-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-audio: internal_apu=1 samples=5" "$LOG_FILE"
grep -q "smw-runtime: level=105 layer1_objects=92 layer2_objects=0 layer2_bg=1 map16_tiles=1480 collision_rects=28 slope_surfaces=42 pipe_cells=14/38/10 coin_pickups=4" "$LOG_FILE"
grep -q "smw-runtime: sprite_palettes=8 source=vram" "$LOG_FILE"
grep -q "pipe_rects=1" "$LOG_FILE"
grep -q "sprite_spawns=34" "$LOG_FILE"
grep -q "sprite_actors=32" "$LOG_FILE"
grep -q "goal_tapes=1" "$LOG_FILE"
grep -q "player_sprites=8" "$LOG_FILE"
grep -q "smw-runtime: entrance level=105 source=105 secondary=0 settings=0 spawn=16,288" "$LOG_FILE"
grep -q "smw-runtime: level_music level=105 music_index=0 bank=Level" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-autostart --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-audio: disabled=1" "$LOG_FILE"
grep -q "smw-menu-audio: samples=0 buttons=5" "$LOG_FILE"
grep -q "sfx_buttons=6 music_buttons=4" "$LOG_FILE"
! grep -q "smw-audio: internal_apu=1" "$LOG_FILE"
grep -q "smw-runtime: level=105 layer1_objects=92 layer2_objects=0 layer2_bg=1 map16_tiles=1480" "$LOG_FILE"
grep -q "smw-runtime: sprite_palettes=8 source=vram" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-autostart --smw-test-screen-exit=7 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: entrance_motion action=4 frames=1 dx=0.00 dy=1.00" "$LOG_FILE"
grep -q "smw-runtime: entrance level=1CB source=1CB secondary=0 settings=4 spawn=24,240" "$LOG_FILE"
grep -q "smw-runtime: level=1CB layer1_objects=16 layer2_objects=0 layer2_bg=1 map16_tiles=585 collision_rects=9 slope_surfaces=0 pipe_cells=0/0/0 coin_pickups=7" "$LOG_FILE"
grep -q "smw-runtime: level_music level=1CB music_index=1 bank=Level" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 2 --smw-test-level=1CB 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: level=1CB layer1_objects=16 layer2_objects=0 layer2_bg=1 map16_tiles=585 collision_rects=9 slope_surfaces=0 pipe_cells=0/0/0 coin_pickups=7" "$LOG_FILE"
grep -q "pipe_rects=1" "$LOG_FILE"
grep -q "sprite_spawns=0" "$LOG_FILE"
grep -q "sprite_actors=0" "$LOG_FILE"
grep -q "goal_tapes=0" "$LOG_FILE"
grep -q "smw-runtime: level_music level=1CB music_index=1 bank=Level" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-level=1CB --smw-test-screen-exit=1 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: entrance_motion action=6 frames=1 dx=4.00 dy=-4.00" "$LOG_FILE"
grep -q "smw-runtime: entrance level=105 source=1CB secondary=1 settings=6 spawn=2072,242" "$LOG_FILE"
grep -q "smw-runtime: level=105 layer1_objects=92 layer2_objects=0 layer2_bg=1 map16_tiles=1480" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 1 --smw-test-autostart --smw-test-spawn=272,176 --smw-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: coin_pickup level=105 dragon=1 coins=0 dragon_coins=1" "$LOG_FILE"
grep -q "score=1000" "$LOG_FILE"
grep -q "smw-audio: sfx=dragon_coin port=3 command=01" "$LOG_FILE"
grep -q "native=1" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 1 --smw-test-level=1CB --smw-test-spawn=112,240 --smw-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: coin_pickup level=1CB dragon=0 coins=0 dragon_coins=0" "$LOG_FILE"
grep -q "score=100" "$LOG_FILE"
grep -q "smw-audio: sfx=coin port=3 command=01" "$LOG_FILE"
grep -q "native=1" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$COIN_LIFE_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$COIN_LIFE_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-test-coins: coins=99 lives=5 oneups=0" "$LOG_FILE"
grep -q "smw-runtime: one_up level=1CB source=coin lives=6 oneups=1 coins=0 score=100" "$LOG_FILE"
grep -q "smw-runtime: coin_pickup level=1CB dragon=0 coins=99 dragon_coins=0 score=100" "$LOG_FILE"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "coins=0"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "lives=6"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "oneups=1"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$DRAGON_LIFE_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$DRAGON_LIFE_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-test-dragon-coins: dragon=4 lives=5 oneups=0" "$LOG_FILE"
grep -q "smw-runtime: one_up level=105 source=dragon_coin_5 lives=6 oneups=1 coins=0 score=1000" "$LOG_FILE"
grep -q "smw-runtime: coin_pickup level=105 dragon=1 coins=0 dragon_coins=5 score=1000" "$LOG_FILE"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "dragon=5"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "lives=6"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "oneups=1"

"$GODOT_BIN" --headless --path . --quit-after 1 --smw-test-autostart --smw-test-spawn=4828,282 2>&1 | tee "$LOG_FILE"
grep -q "smw-runtime: course_clear level=105" "$LOG_FILE"
grep -q "walkout=right" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 5 --smw-test-autostart --smw-debug-command-file="$COURSE_CLEAR_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$COURSE_CLEAR_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-runtime: course_clear level=105 walkout=right" "$LOG_FILE"
grep -q "smw-debug-trace: tag=goal_walkout i=12/12" "$LOG_FILE"
grep "smw-debug-state: tag=goal_walkout_done" "$LOG_FILE" | grep -q "clear=1"
grep "smw-debug-state: tag=goal_walkout_done" "$LOG_FILE" | grep -q "walkout=11"
grep "smw-debug-state: tag=goal_walkout_done" "$LOG_FILE" | grep -q "x=4833.00"

"$GODOT_BIN" --headless --path . --quit-after 1 --smw-test-autostart --smw-test-powerup=small 2>&1 | tee "$LOG_FILE"
grep -q "smw-test-powerup: powerup=0 height=16 render_y=-1 player_palette=0" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 1 --smw-test-autostart --smw-test-powerup=fire 2>&1 | tee "$LOG_FILE"
grep -q "smw-test-powerup: powerup=3 height=32 render_y=-1 player_palette=2" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 1 --smw-test-autostart --smw-test-spawn=880,304 --smw-test-powerup=small 2>&1 | tee "$LOG_FILE"
grep -q "smw-test-powerup: powerup=0 height=16 render_y=-1 player_palette=0" "$LOG_FILE"
grep -q "smw-test-spawn: x=880.00 y=304.00" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 1 --smw-audio-sample=09 2>&1 | tee "$LOG_FILE"
grep -q "smw-audio: sample_preview sample=09 available=1 samples=5" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-input-script="$INPUT_SCRIPT" 2>&1 | tee "$LOG_FILE"
grep -q "smw-input-script: loaded path=$INPUT_SCRIPT segments=4 frames=4" "$LOG_FILE"
grep -q "smw-input-script: done name=$INPUT_SCRIPT frames=4" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 3040 --smw-test-autostart --smw-debug-command-file="$AUTOPLAY_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$AUTOPLAY_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-autoplay: mode=explore frame=0" "$LOG_FILE"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "autoplay=explore"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "clear=0"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "gameover=0"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "actors_on=0"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "actor_event=none"

"$GODOT_BIN" --headless --path . --quit-after 5060 --smw-test-autostart --smw-debug-command-file="$ACTORS_AUTOPLAY_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$ACTORS_AUTOPLAY_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-autoplay: mode=explore frame=0" "$LOG_FILE"
grep -q "smw-runtime: actor_contact level=105 action=god contact=9F" "$LOG_FILE"
grep -q "smw-runtime: sprite_stomp level=105 sprite=AB" "$LOG_FILE"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "actors_on=1"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "god=1"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "gameover=0"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "clear=0"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "deaths=0"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "actor_event=stomp:AB:1"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-test-spawn=2050,292 --smw-input-script="$PIPE_SCRIPT" 2>&1 | tee "$LOG_FILE"
! grep -q "pipe-debug screen=07" "$LOG_FILE"
grep -q "smw-input-script: done name=$PIPE_SCRIPT frames=1" "$LOG_FILE"
grep -q "x=2064.00 y=292.00" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-test-spawn=2072,240 --smw-input-script="$PIPE_SCRIPT" 2>&1 | tee "$LOG_FILE"
! grep -q "pipe-debug screen=07" "$LOG_FILE"
grep -q "smw-input-script: done name=$PIPE_SCRIPT frames=1" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$DEBUG_COMMAND_FILE" 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$DEBUG_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=before" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "x=880.00 y=304.00" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$ACTOR_COMMAND_FILE" 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$ACTOR_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=after_actor_hit" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "pow=0" "$LOG_FILE"
grep -q "hurt=127" "$LOG_FILE"
grep -q "xs=0 ys=0" "$LOG_FILE"
grep -q "x=512.00 y=288.00" "$LOG_FILE"
grep -q "actor_event=hurt:9F:0" "$LOG_FILE"
grep -q "actor_contact=9F:0:" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 12 --smw-test-autostart --smw-debug-command-file="$HURT_BLINK_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$HURT_BLINK_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-player-oam: tag=hurt_blink_" "$LOG_FILE"
grep "smw-debug-player-oam: tag=hurt_blink_" "$LOG_FILE" | grep -q "blink_hidden=0"
grep "smw-debug-player-oam: tag=hurt_blink_" "$LOG_FILE" | grep -q "blink_hidden=1"
grep "smw-debug-player-oam: tag=hurt_blink_" "$LOG_FILE" | grep -q "alpha=0.00"

"$GODOT_BIN" --headless --path . --quit-after 430 --smw-test-autostart --smw-debug-command-file="$START_IDLE_HURT_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$START_IDLE_HURT_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=after_start_idle" "$LOG_FILE"
grep "smw-debug-state: tag=after_start_idle" "$LOG_FILE" | grep -q "actors_active=1"
! grep "smw-debug-state: tag=after_start_idle" "$LOG_FILE" | grep -q "actors_active=32"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "actor_event=none" "$LOG_FILE"
grep -q "x=16.00 y=288.00" "$LOG_FILE"
grep -q "pow=1" "$LOG_FILE"
grep -q "deaths=0" "$LOG_FILE"
! grep -q "x=0.00" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$SMALL_HURT_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$SMALL_HURT_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-runtime: player_death level=105 cause=hurt count=1" "$LOG_FILE"
grep -q "smw-debug-state: tag=after_small_hurt" "$LOG_FILE"
grep -q "actor_event=death:hurt:9F:0" "$LOG_FILE"
grep -q "x=16.00 y=288.00" "$LOG_FILE"
grep -q "lives=4" "$LOG_FILE"
grep -q "deaths=1" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$REX_COMMAND_FILE" --smw-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$REX_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "pow=1" "$LOG_FILE"
grep -q "ys=-48" "$LOG_FILE"
grep -q "actor_event=stomp:AB:1" "$LOG_FILE"
grep -q "actor_contact=AB:0:.*down=1:top=1:cross=1" "$LOG_FILE"
grep -q "smw-runtime: sprite_stomp level=105 sprite=AB state=1 source=stomp:AB:1 chain=1 reward_index=1 score=200" "$LOG_FILE"
grep -q "smw-audio: sfx=stomp port=1 command=14" "$LOG_FILE"
grep -q "score=200" "$LOG_FILE"
grep -q "stomp_chain=1" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 32 --smw-test-autostart --smw-debug-command-file="$BANZAI_UNDER_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$BANZAI_UNDER_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "smw-debug-actors-near: radius=256.00 .*9F:state=0:.*active=1" "$LOG_FILE"
grep -q "9F:state=0:pos=496.00,240.00:rect=504.00,248.00,52.00,46.00" "$LOG_FILE"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "pow=1"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "hurt=0"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "actor_event=none"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "deaths=0"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$BREAK_COMMAND_FILE" --smw-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$BREAK_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-runtime: block_break level=105 count=2 total=2" "$LOG_FILE"
grep -q "smw-audio: sfx=block_break port=1 command=08" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "blocks=2" "$LOG_FILE"
grep -q "tile=120,20:----" "$LOG_FILE"
grep -q "solids=25" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$WING_BLOCK_COMMAND_FILE" 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$WING_BLOCK_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "x=591.00 y=192.00" "$LOG_FILE"
grep -q "g=1" "$LOG_FILE"
grep -q "near=83:0:591.25,239.50" "$LOG_FILE"
grep -q "actor_event=block:83:top" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$WING_BLOCK_REWARD_COMMAND_FILE" --smw-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$WING_BLOCK_REWARD_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-runtime: item_spawn level=105 sprite=75 reward=flower" "$LOG_FILE"
grep -q "smw-runtime: block_reward level=105 sprite=83 reward=flower" "$LOG_FILE"
grep -q "smw-audio: sfx=powerup_reward port=3 command=0A" "$LOG_FILE"
grep -q "pow=1 score=0" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "actors=33" "$LOG_FILE"
grep -q "actor_event=block:83:reward:flower" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$ITEM_COLLECT_COMMAND_FILE" 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$ITEM_COLLECT_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug: item sprite=75 x=592.00 y=224.00" "$LOG_FILE"
grep -q "smw-runtime: item_collect level=105 sprite=75" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "pow=3" "$LOG_FILE"
grep -q "score=1000" "$LOG_FILE"
grep -q "actor_event=item:75:collect" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$FIREBALL_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$FIREBALL_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-runtime: fireball_spawn level=105" "$LOG_FILE"
grep -q "xs=3.00 ys=48" "$LOG_FILE"
grep -q "smw-runtime: fireball_hit level=105 sprite=AB" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "pow=3" "$LOG_FILE"
grep -q "pose=67" "$LOG_FILE"
grep -q "score=200" "$LOG_FILE"
grep -q "fireballs=0" "$LOG_FILE"
grep -q "god=1" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$STAR_ITEM_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$STAR_ITEM_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug: item sprite=76 x=592.00 y=224.00" "$LOG_FILE"
grep -q "smw-runtime: item_collect level=105 sprite=76" "$LOG_FILE"
grep -q "star=FF score=1000" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "star=FE" "$LOG_FILE"
grep -q "actor_event=item:76:collect" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$STAR_HIT_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$STAR_HIT_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug: star=FF" "$LOG_FILE"
grep -q "smw-runtime: sprite_star level=105 sprite=AB" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "pow=1" "$LOG_FILE"
grep -q "star=FE" "$LOG_FILE"
grep -q "score=100" "$LOG_FILE"
grep -q "actor_event=star:AB:dead" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$STATIC_QUESTION_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$STATIC_QUESTION_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-runtime: item_spawn level=105 sprite=75 reward=flower x=3888.00 y=208.00 target_y=192.00" "$LOG_FILE"
grep -q "smw-runtime: block_reward level=105 map16=124 reward=flower tile=243,17" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "ys=8" "$LOG_FILE"
grep -q "pow=1" "$LOG_FILE"
grep -q "score=0" "$LOG_FILE"
grep -q "actors=33" "$LOG_FILE"
grep -q "actor_event=block:124:reward:flower" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$PIRANHA_HIDDEN_COMMAND_FILE" 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$PIRANHA_HIDDEN_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-actors-near:" "$LOG_FILE"
grep -q "4F:state=0:pos=1808.00,256.00" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "pow=1" "$LOG_FILE"
grep -q "actor_event=none" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$PIRANHA_VISIBLE_COMMAND_FILE" 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$PIRANHA_VISIBLE_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "near=4F:2:2224.00,240.00" "$LOG_FILE"
grep -q "actor_event=hurt:4F:2" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$C7_NORMAL_VISUAL_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$C7_NORMAL_VISUAL_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-actors-near:" "$LOG_FILE"
grep -q "C7:state=0:pos=1632.00,288.00" "$LOG_FILE"
grep -q "visuals=0" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$C7_DEBUG_VISUAL_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$C7_DEBUG_VISUAL_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-actors-near:" "$LOG_FILE"
grep -q "C7:state=0:pos=1632.00,288.00" "$LOG_FILE"
grep -q "visuals=2" "$LOG_FILE"
grep -q "overlays=1" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$INVISIBLE_MUSHROOM_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$INVISIBLE_MUSHROOM_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-runtime: invisible_mushroom level=105 source=C7" "$LOG_FILE"
grep -q "item=74" "$LOG_FILE"
grep -q "cooldown=32" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "actor_event=item:C7:reveal" "$LOG_FILE"
grep -q "actors=33" "$LOG_FILE"
grep -q "score=0" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$SLOPE_PROBE_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$SLOPE_PROBE_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "x=921.13 y=200.00" "$LOG_FILE"
grep -q "g=0" "$LOG_FILE"
grep -q "slope=-1" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$PIPE_UNDERSIDE_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$PIPE_UNDERSIDE_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "level=105" "$LOG_FILE"
grep -q "x=2055.81 y=304.00" "$LOG_FILE"
! grep -q "level=1CB" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$PIPE_SLOPE_SUPPORT_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$PIPE_SLOPE_SUPPORT_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-state: tag=step_done" "$LOG_FILE"
grep -q "x=2024.81 y=226.00" "$LOG_FILE"
grep -q "slope=21" "$LOG_FILE"
grep -q "actors_on=0" "$LOG_FILE"
! grep -q "x=2048.00" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 6 --smw-test-autostart --smw-debug-command-file="$PIPE_UNDERSIDE_JUMP_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$PIPE_UNDERSIDE_JUMP_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-test-ground: grounded=1" "$LOG_FILE"
grep -q "smw-debug-trace: tag=pipe_underside_jump i=1/3" "$LOG_FILE"
grep "smw-debug-trace: tag=pipe_underside_jump i=1/3" "$LOG_FILE" | grep -q "ys=-77"
! grep "smw-debug-trace: tag=pipe_underside_jump i=1/3" "$LOG_FILE" | grep -q "y=313.00"
grep -q "smw-debug-state: tag=pipe_underside_jump_done" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 100 --smw-test-autostart --smw-debug-command-file="$PIPE_HELD_DOWN_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$PIPE_HELD_DOWN_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug: hold input=--D----" "$LOG_FILE"
! grep -q "pipe-debug screen=07" "$LOG_FILE"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "level=105"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "x=1928.00 y=224.00"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$DEATH_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$DEATH_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-runtime: player_death level=105 cause=fall count=1" "$LOG_FILE"
grep -q "smw-debug-state: tag=after_death" "$LOG_FILE"
grep -q "x=16.00 y=288.00" "$LOG_FILE"
grep -q "actor_event=death:fall" "$LOG_FILE"
grep -q "deaths=1" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$TIME_UP_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$TIME_UP_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-timer: tag=set frames=1 seconds=1" "$LOG_FILE"
grep -q "smw-runtime: player_death level=105 cause=time_up count=1" "$LOG_FILE"
grep -q "smw-debug-state: tag=after_time_up" "$LOG_FILE"
grep -q "x=16.00 y=288.00" "$LOG_FILE"
grep -q "actor_event=death:time_up" "$LOG_FILE"
grep -q "time=300" "$LOG_FILE"
grep -q "deaths=1" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 4 --smw-test-autostart --smw-debug-command-file="$GAME_OVER_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$GAME_OVER_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-test-lives: lives=1 gameover=0" "$LOG_FILE"
grep -q "smw-runtime: player_death level=105 cause=time_up count=1 lives=0" "$LOG_FILE"
grep -q "smw-runtime: game_over level=105 cause=time_up deaths=1" "$LOG_FILE"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "gameover=1"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "lives=0"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "actor_event=gameover:time_up"

"$GODOT_BIN" --headless --path . --quit-after 7 --smw-test-autostart --smw-debug-command-file="$PAUSE_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$PAUSE_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-runtime: pause level=105 state=1 source=debug" "$LOG_FILE"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "gamepause=1"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "x=32.00 y=288.00"
grep "smw-debug-state: tag=step_done" "$LOG_FILE" | grep -q "timer_frames=120"

"$GODOT_BIN" --headless --path . --quit-after 5 --smw-test-autostart --smw-debug-command-file="$TRACE_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$TRACE_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-test-ground: grounded=1" "$LOG_FILE"
grep -q "smw-debug: trace queued=3 tag=jump_probe input=-R-Jj-Y" "$LOG_FILE"
grep -q "smw-debug-trace: tag=jump_probe i=1/3" "$LOG_FILE"
grep -q "ys=-77" "$LOG_FILE"
grep -q "smw-debug-trace: tag=jump_probe i=3/3" "$LOG_FILE"
grep -q "smw-debug-state: tag=jump_probe_done" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 5 --smw-test-autostart --smw-debug-command-file="$TRACE_LIVE_COMMAND_FILE" --smw-no-audio 2>&1 | tee "$LOG_FILE"
grep -q "smw-debug: command_file=$TRACE_LIVE_COMMAND_FILE" "$LOG_FILE"
grep -q "smw-debug-autoplay: mode=explore frame=0" "$LOG_FILE"
grep -q "smw-debug: trace queued=3 tag=autoplay_probe input=live" "$LOG_FILE"
grep -q "smw-debug-trace: tag=autoplay_probe i=1/3" "$LOG_FILE"
grep -q "input=-R-Jj-Y" "$LOG_FILE"
grep -q "smw-debug-state: tag=autoplay_probe_done" "$LOG_FILE"

"$GODOT_BIN" --headless --path . --quit-after 1800 --smw-test-autostart --smw-debug-rcon="$RCON_PORT" >"$LOG_FILE" 2>&1 &
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
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh pmeter 0x70 | tee "$RCON_LOG"
grep -q "p=70" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh spawn 880 304 small | tee "$RCON_LOG"
grep -q "x=880.00 y=304.00" "$RCON_LOG"
grep -q "pow=0 star=00 h=16" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh save pipe_start | tee "$RCON_LOG"
grep -q "smw-debug-checkpoint: action=save slot=pipe_start" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh spawn 921 208 small | tee "$RCON_LOG"
grep -q "x=921.00 y=208.00" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh load pipe_start | tee "$RCON_LOG"
grep -q "smw-debug-checkpoint: action=load slot=pipe_start" "$RCON_LOG"
grep -q "x=880.00 y=304.00" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh checkpoints | tee "$RCON_LOG"
grep -q "smw-debug-checkpoints: count=1" "$RCON_LOG"
grep -q "pipe_start:105:880.00,304.00" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh restart | tee "$RCON_LOG"
grep -q "smw-debug-state: tag=restart" "$RCON_LOG"
grep -q "x=16.00 y=288.00" "$RCON_LOG"
grep -q "actor_event=debug:restart" "$RCON_LOG"
grep -q "smw-debug: restart level=105" "$LOG_FILE"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh spawn 880 304 | tee "$RCON_LOG"
grep -q "x=880.00" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh powerup small | tee "$RCON_LOG"
grep -q "pow=0" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh pmeter 0x70 | tee "$RCON_LOG"
grep -q "p=70" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh physics rcon_physics | tee "$RCON_LOG"
grep -q "smw-debug-physics: tag=rcon_physics" "$RCON_LOG"
grep -q "jump_idx=" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh oam rcon_small | tee "$RCON_LOG"
grep -q "smw-debug-player-oam: tag=rcon_small metadata=1" "$RCON_LOG"
grep -q "palette=0" "$RCON_LOG"
grep -q "slot0:" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh pickups_near 1024 | tee "$RCON_LOG"
grep -q "smw-debug-pickups-near: radius=1024.00" "$RCON_LOG"
grep -q "dragon=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh overlays on | tee "$RCON_LOG"
grep -q "overlays=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh actors off | tee "$RCON_LOG"
grep -q "actors_on=0" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh actor_visuals off | tee "$RCON_LOG"
grep -q "actor_visuals=0" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh actor_visuals on | tee "$RCON_LOG"
grep -q "actor_visuals=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh god on | tee "$RCON_LOG"
grep -q "god=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh audio status | tee "$RCON_LOG"
grep -q "smw-debug-audio: tag=status enabled=1" "$RCON_LOG"
grep -q "bank=Level" "$RCON_LOG"
grep -q "mix_chunk=512" "$RCON_LOG"
grep -q "mix_frames=" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh audio sample 09 | tee "$RCON_LOG"
grep -q "smw-debug-audio: tag=sample" "$RCON_LOG"
grep -q "sample=09 available=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh audio jump | tee "$RCON_LOG"
grep -q "command=port1_jump" "$RCON_LOG"
grep -q "last_sfx=jump" "$RCON_LOG"
grep -q "last_sfx_native=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh audio coin | tee "$RCON_LOG"
grep -q "smw-debug-audio: tag=sfx" "$RCON_LOG"
grep -q "last_sfx=coin" "$RCON_LOG"
grep -q "last_sfx_cmd=01" "$RCON_LOG"
grep -q "last_sfx_native=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh audio stomp | tee "$RCON_LOG"
grep -q "last_sfx=stomp" "$RCON_LOG"
grep -q "last_sfx_cmd=13" "$RCON_LOG"
grep -q "last_sfx_native=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh audio 1up | tee "$RCON_LOG"
grep -q "last_sfx=one_up" "$RCON_LOG"
grep -q "last_sfx_cmd=02" "$RCON_LOG"
grep -q "last_sfx_native=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh perf status | tee "$RCON_LOG"
grep -q "smw-debug-perf: tag=status" "$RCON_LOG"
grep -q "process_ms=" "$RCON_LOG"
grep -q "draw_calls=" "$RCON_LOG"
grep -q "audio_loaded=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh audio off | tee "$RCON_LOG"
grep -q "smw-debug-audio: tag=toggle enabled=0" "$RCON_LOG"
grep -q "music=0" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh audio on | tee "$RCON_LOG"
grep -q "smw-debug-audio: tag=toggle enabled=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh hud | tee "$RCON_LOG"
grep -q "smw-debug-status:" "$RCON_LOG"
grep -q "score=0" "$RCON_LOG"
grep -q "lives=5" "$RCON_LOG"
grep -q "COIN 00" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh timer frames 120 | tee "$RCON_LOG"
grep -q "smw-debug-timer: tag=set frames=120 seconds=2" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh timer | tee "$RCON_LOG"
grep -q "smw-debug-timer: tag=status" "$RCON_LOG"
grep -q "seconds=2" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh game_pause on | tee "$RCON_LOG"
grep -q "gamepause=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh step 2 | tee "$RCON_LOG"
grep -q "ok step_queued=2" "$RCON_LOG"
sleep 0.2
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh state rcon_paused | tee "$RCON_LOG"
grep -q "gamepause=1" "$RCON_LOG"
grep -q "timer_frames=120" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh game_pause off | tee "$RCON_LOG"
grep -q "gamepause=0" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh lives 1 | tee "$RCON_LOG"
grep -q "lives=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh timer frames 1 | tee "$RCON_LOG"
grep -q "frames=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh step 1 | tee "$RCON_LOG"
grep -q "ok step_queued=1" "$RCON_LOG"
sleep 0.2
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh state rcon_gameover | tee "$RCON_LOG"
grep -q "gameover=1" "$RCON_LOG"
grep -q "lives=0" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh continue | tee "$RCON_LOG"
grep -q "gameover=0" "$RCON_LOG"
grep -q "lives=5" "$RCON_LOG"
grep -q "actor_event=gameover:continue" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh spawn 880 304 small | tee "$RCON_LOG"
grep -q "x=880.00 y=304.00" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh tile | tee "$RCON_LOG"
grep -q "smw-debug-tile:" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh collision 2064 304 48 | tee "$RCON_LOG"
grep -q "smw-debug-collision: point=2064.00,304.00 radius=48.00" "$RCON_LOG"
grep -q "solids=" "$RCON_LOG"
grep -q "slopes=" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh slope_probe 2064 304 16 -3 tag=rcon_pipe_slope | tee "$RCON_LOG"
grep -q "smw-debug-slope-probe: tag=rcon_pipe_slope" "$RCON_LOG"
grep -q "probe=center" "$RCON_LOG"
grep -q "kind=" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh pipe 2064 304 48 | tee "$RCON_LOG"
grep -q "smw-debug-pipe: point=2064.00,304.00 radius=48.00" "$RCON_LOG"
grep -q "floor=" "$RCON_LOG"
grep -q "body=" "$RCON_LOG"
grep -q "ceiling=" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh pipe_entrances | tee "$RCON_LOG"
grep -q "smw-debug-pipe-entrances: count=1" "$RCON_LOG"
grep -q "screen=07:kind=vertical:dir=down" "$RCON_LOG"
grep -q "source=120,22:vertical_pipe_top_left" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh goal | tee "$RCON_LOG"
grep -q "smw-debug-goal-tapes: count=1" "$RCON_LOG"
grep -q "speed=1.00" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh sensors rcon_sensors | tee "$RCON_LOG"
grep -q "smw-debug-sensors: tag=rcon_sensors" "$RCON_LOG"
grep -q "head=" "$RCON_LOG"
grep -q "foot_c=" "$RCON_LOG"
grep -q "floor_slope=" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh near 128 | tee "$RCON_LOG"
grep -q "smw-debug-actors-near: radius=128.00" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh actor_oam 128 | tee "$RCON_LOG"
grep -q "smw-debug-actor-oam: radius=128.00" "$RCON_LOG"
grep -q "tiles=" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh camera status | tee "$RCON_LOG"
grep -q "smw-debug-camera: tag=status" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh camera lock 1928 144 | tee "$RCON_LOG"
grep -q "smw-debug-camera: tag=lock" "$RCON_LOG"
grep -q "locked=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh camera unlock | tee "$RCON_LOG"
grep -q "smw-debug-camera: tag=unlock" "$RCON_LOG"
grep -q "locked=0" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh step 1 | tee "$RCON_LOG"
grep -q "ok step_queued=1" "$RCON_LOG"
sleep 0.2
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh state rcon_after_step | tee "$RCON_LOG"
grep -q "smw-debug-state: tag=rcon_after_step" "$RCON_LOG"
grep -q "x=880.00 y=304.00" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh ground on | tee "$RCON_LOG"
grep -q "g=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh hold right run | tee "$RCON_LOG"
grep -q "ok hold input=-R----Y" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh release | tee "$RCON_LOG"
grep -q "ok hold input=-------" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh trace 2 right run tag=rcon_probe | tee "$RCON_LOG"
grep -q "ok trace_queued=2" "$RCON_LOG"
for _ in $(seq 1 80); do
  if grep -q "smw-debug-state: tag=rcon_probe_done" "$LOG_FILE"; then
    break
  fi
  sleep 0.05
done
grep -q "smw-debug-trace: tag=rcon_probe i=2/2" "$LOG_FILE"
grep -q "smw-debug-state: tag=rcon_probe_done" "$LOG_FILE"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh trace_oam 2 right run tag=rcon_oam_probe | tee "$RCON_LOG"
grep -q "ok trace_queued=2" "$RCON_LOG"
for _ in $(seq 1 80); do
  if grep -q "smw-debug-state: tag=rcon_oam_probe_done" "$LOG_FILE"; then
    break
  fi
  sleep 0.05
done
grep -q "smw-debug-trace: tag=rcon_oam_probe i=2/2" "$LOG_FILE"
grep -q "smw-debug-player-oam: tag=rcon_oam_probe_01 metadata=1" "$LOG_FILE"
grep -q "smw-debug-state: tag=rcon_oam_probe_done" "$LOG_FILE"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh powerup small | tee "$RCON_LOG"
grep -q "pow=0" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh spawn 921 200 | tee "$RCON_LOG"
grep -q "x=921.00" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh velocity 2 0 | tee "$RCON_LOG"
grep -q "xs=2" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh step 1 | tee "$RCON_LOG"
grep -q "ok step_queued=1" "$RCON_LOG"
for _ in $(seq 1 80); do
  if grep -q "x=921.13 y=200.00" "$LOG_FILE"; then
    break
  fi
  sleep 0.05
done
grep -q "x=921.13 y=200.00" "$LOG_FILE"
grep -q "slope=-1" "$LOG_FILE"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh spawn 921 187 | tee "$RCON_LOG"
grep -q "x=921.00 y=187.00" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh ground on | tee "$RCON_LOG"
grep -q "g=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh trace 4 jump tag=rcon_slope_jump | tee "$RCON_LOG"
grep -q "ok trace_queued=4" "$RCON_LOG"
for _ in $(seq 1 80); do
  if grep -q "smw-debug-state: tag=rcon_slope_jump_done" "$LOG_FILE"; then
    break
  fi
  sleep 0.05
done
grep -q "smw-debug-trace: tag=rcon_slope_jump i=1/4" "$LOG_FILE"
grep -q "ys=-77" "$LOG_FILE"
grep -q "g=0" "$LOG_FILE"
grep -q "smw-debug-state: tag=rcon_slope_jump_done" "$LOG_FILE"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh trace_sensors 2 right tag=rcon_sensor_trace | tee "$RCON_LOG"
grep -q "ok trace_queued=2" "$RCON_LOG"
for _ in $(seq 1 80); do
  if grep -q "smw-debug-slope-probe: tag=rcon_sensor_trace_01" "$LOG_FILE"; then
    break
  fi
  sleep 0.05
done
grep -q "smw-debug-sensors: tag=rcon_sensor_trace_01" "$LOG_FILE"
grep -q "smw-debug-slope-probe: tag=rcon_sensor_trace_01" "$LOG_FILE"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh powerup small | tee "$RCON_LOG"
grep -q "pow=0" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh spawn 32 288 | tee "$RCON_LOG"
grep -q "x=32.00" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh ground on | tee "$RCON_LOG"
grep -q "g=1" "$RCON_LOG"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh trace 8 spin tag=rcon_spin_pose | tee "$RCON_LOG"
grep -q "ok trace_queued=8" "$RCON_LOG"
for _ in $(seq 1 80); do
  if grep -q "smw-debug-state: tag=rcon_spin_pose_done" "$LOG_FILE"; then
    break
  fi
  sleep 0.05
done
grep -q "smw-debug-trace: tag=rcon_spin_pose i=8/8" "$LOG_FILE"
grep "smw-debug-trace: tag=rcon_spin_pose" "$LOG_FILE" | grep -q "sj=1"
grep "smw-debug-trace: tag=rcon_spin_pose" "$LOG_FILE" | grep -q "pose_face=0"
grep "smw-debug-trace: tag=rcon_spin_pose" "$LOG_FILE" | grep -q "pose_face=1"
grep "smw-debug-trace: tag=rcon_spin_pose" "$LOG_FILE" | grep -Eq "pose=(15|37)"
SMW_DEBUG_RCON_PORT="$RCON_PORT" tools/smw-rcon.sh quit >/dev/null || true
wait "$RCON_PID" || true

"$GODOT_BIN" --headless --audio-driver Dummy --path . --quit-after 1 --smw-audio-preview=Level 2>&1 | tee "$LOG_FILE"
grep -q "smw-audio: music_preview=Level events=12 loop_frames=96" "$LOG_FILE"

"$GODOT_BIN" --headless --audio-driver Dummy --path . --quit-after 1 --smw-audio-preview=Star 2>&1 | tee "$LOG_FILE"
grep -q "smw-audio: music_preview=Star events=12 loop_frames=64" "$LOG_FILE"
