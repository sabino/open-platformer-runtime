# Debugging and Verification

This project keeps deterministic debug paths available because the runtime target is exact gameplay and physics behavior, not just approximate playability.

## Headless Checks

Run the main source check:

```bash
tools/check-dotnet.sh
```

Run headless Godot smoke tests:

```bash
tools/check-headless.sh
```

Run a short visible Wayland smoke test:

```bash
tools/check-wayland.sh
```

## Captures

Capture the current level through Sway/Wayland:

```bash
tools/capture-wayland.sh generated/smw/captures/level_105_compositor.png 105
tools/capture-wayland.sh generated/smw/captures/level_105_pipe_probe.png 105 --smw-test-spawn=2224,240 --smw-debug-overlays --smw-no-audio
```

The visible wrappers default to Sway workspace `6`; override with `SMW_SWAY_WORKSPACE=<number>` if needed.

For reproducible lower-route captures, the runtime accepts `--smw-test-spawn=x,y` and `--smw-test-powerup=small|big|cape|fire`.

## RCON

During a debug RCON run, `tools/smw-rcon.sh snapshot res://generated/smw/captures/name.png` saves the current Godot viewport in visible runs. `capture <path> <frames>` schedules a capture after one or more process frames.

Useful live RCON controls:

- `overlays on`
- `actors off`
- `actor_visuals off`
- `god on`
- `audio off|on|status`
- `perf` or `fps`
- `timer`, `timer 123`, or `timer frames 1`
- `game_pause on|off`
- `coins 99`, `dragon 4`, `lives 1`, `star on|off|FF`
- `continue`
- `autoplay explore|off`
- `tile` or `tile <world-x> <world-y>`
- `sensors [tag]`
- `collision [x y radius]`
- `pipe [x y radius]`
- `near <radius>`
- `oam [tag]`
- `hold right run`, `tap jump`, `release`, `ground on|off`, `step <frames>`
- `trace <frames> [inputs...] [tag=name]`

Longer RCON behavior notes live in [REFERENCE.md](REFERENCE.md).

## Deterministic Input Scripts

Pass `--smw-input-script=/path/to/script` or `--smw-autoplay=explore`.

Each script line starts with a frame count followed by held inputs separated by spaces, commas, colons, semicolons, or `+`:

```text
8 right run
4 Right,Y
6 Right+Y+B
```

Supported gameplay tokens are `left`, `right`, `down`, `jump`/`B`, `spin`/`A`, and `run`/`X`/`Y`. Native `.input` directives and menu/shoulder tokens such as `Start`, `Select`, `Up`, `L`, and `R` are accepted as no-ops for compatibility with converted recordings.

## Native Recording and Trace Comparison

Record and compare without touching save states:

```bash
tools/run-native-input-recording-wayland.sh
tools/run-native-input-wayland.sh path/to/full-route.input
tools/run-recording-compare-wayland.sh --input path/to/full-route.input --level-start-frame N
tools/run-recording-trace-compare.sh --input path/to/full-route.input --level-start-frame N --frames 600
```

The trace wrapper writes native/Godot logs plus JSONL-normalized traces under `generated/smw/traces/` and reports the first player position or power-up divergence.

## TAS Diagnostic Harness

Prepare and run the current external TAS diagnostic slice:

```bash
tools/prepare-tas-diagnostic.sh
tools/run-tas-diagnostic.sh --visible --overlays --smw-no-audio
```

The diagnostic movie enters another level first, so this is a hard desync/fidelity harness for future physics and collision work, not a broad sync claim.

Compare the native source-port reference and Godot visually:

```bash
tools/run-tas-compare-wayland.sh
```
