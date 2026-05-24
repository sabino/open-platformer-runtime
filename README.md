# Open Platformer Runtime

This repository is a Godot 4 .NET desktop port scaffold for a native classic-platformer-compatible engine. It is not an emulator and does not ship third-party copyrighted assets. A verified local compatible USA ROM is used only as input to an offline importer that writes Godot-readable generated data under `generated/smw/`.

The long-term bar is a 100% physics and gameplay semantics match with the original game. The first slices should therefore be table-driven from SMW reference constants and covered by regression tests, even when a subsystem starts incomplete.

The current first slice covers:

- ROM validation and deterministic extraction for level `105` by default. Native C# extractor slices now directly extract and verify core ROM assets used by the runtime, including GFX32/GFX33, raw SPC upload banks, global Map16, global/per-level palette assets, level `105`/`1CB` headers, object stream lengths/raw bytes, sprite stream positions, layer-2 background source metadata, the level `105` screen-07 pipe route into `1CB`, vanilla main/secondary entrance tables, BRR-decoded audio preview WAVs, and Mario OAM/palette metadata.
- Layer 1/layer 2 raw object streams, Layer 2 RLE background previews, decoded placement metadata, screen exits, sprite stream, Map16, global palettes, per-level full CGRAM palettes, GFX32/GFX33, player PNG atlases, level tileset GFX atlases, sprite GFX VRAM atlases, Map16 preview atlases, partial level layout previews, and secondary-exit tables.
- Raw SPC upload banks plus a few decoded BRR preview WAVs used only as importer verification artifacts while the full SPC/DSP sequencer is still pending.
- A Godot .NET C# menu and minimal playable scene; the initial menu uses generated Yoshi Island 1 preview art, exposes audio/debug-gizmo toggles, includes internal sound/music test buttons, and starts the playable first-level slice.
- A SNES-sized `256x224` logical viewport that opens as a 3x Wayland window for normal graphical runs.
- Runtime audio playback through a C# internal APU probe that streams decoded BRR samples from imported SPC engine/sample banks; named SFX probes now decode native SPC RAM command streams before falling back to hand-authored notes, and the Godot runtime no longer depends on WAV/MP3 playback for these probes.
- A menu audio panel for internal port-1 SFX command probes, named gameplay SFX probes, decoded BRR sample probes, and imported music bank previews while the full SPC/DSP command sequencer is still pending.
- An opt-in debug overlay showing imported asset previews plus collision rectangles/outlines, slope lines, player hitbox/feet, active camera bounds, pipe/goal triggers, sprite hitboxes, sprite spawn markers, object markers, coin/block semantic markers, and the Map16 tile currently under Mario's feet.
- Runtime placement of the generated Yoshi Island 1 Map16 tilemap through a batched custom draw layer, with temporary merged collision rectangles derived from imported tile placement sources.
- Runtime rendering of generated Layer 2 RLE background previews behind the imported Layer 1 Map16 placements for level `105` and pipe target `1CB`.
- Layer 1/2 object placement and several Map16 projection rules are cross-checked against Lunar Magic Universal's current `lmcore` parser/renderer, including native object tile coordinates, pipes, standard `0x12` slopes, left diagonal ledges, right diagonal pipes, and vanilla extended objects such as screen jumps, moons, invisible 1-ups, question/switch blocks, midway bars, Yoshi coins, goal markers, boss doors, and generic one-tile extended objects. Generated levels `105` and `1CB` currently have no unsupported extended-object records in the partial layout projection.
- Map16 rendering converts the raw SMW quadrant word order into render quadrant order before generating Godot atlases and level previews.
- Runtime collision separates imported diagonal pipe/slope/ledge clusters into per-Map16-tile floor and ceiling slope surfaces instead of long averaged slope lines; terrain slope-family support/fill rectangles are suppressed, while diagonal-pipe body/mouth Map16 cells now emit solid blockers so the pipe volume cannot be walked through.
- Mario floor-slope resolution probes center plus both feet, biased toward the current horizontal movement direction, so diagonal pipe lips and slope edges are less likely to miss contact when the player is partly over a tile boundary.
- Runtime coin pickup state is derived from imported Map16 coin markers, including normal coins and dragon coins, with named internal APU SFX probes on collection and extra-life events.
- Runtime block interaction includes a focused Yoshi Island 1 slice for imported turn blocks, flying question blocks, and static extended question blocks; the static question-block importer now preserves flower/feather/star/coin-style content labels so underside hits can swap to the used block face and spawn native item actors.
- A normal-play status bar shows score, lives, normal coins, dragon coins, level timer, current level, gameplay pause, course-clear state, and the first game-over flow; the timer now triggers a first time-up death/restart path, coins roll into the first 100-coin extra-life rule, the fifth dragon coin awards a life, and the same fields are exposed through RCON for headless verification.
- Runtime camera scrolling uses SMW-style horizontal and vertical screen-space thresholds instead of center-follow, clamped to generated tile bounds so Yoshi Island 1's lower routes can be inspected during drops.
- A runtime Mario sprite composite built from generated GFX32 PNG data and the ROM-derived `PlayerGFXRt` head/body tile pointer tables. This replaces the placeholder hitbox rectangle, but final frame/state correctness still depends on porting the direct OAM assembly tables.
- Partial object expansion for the Yoshi Island 1 direct pipe target `1CB`, including horizontal pipes and underground ceiling ledges/edges in the generated Map16 tilemap.
- Manifest-driven runtime asset selection for the current level's tilemap, preview, and tileset atlases, rather than hardcoded generated filenames.
- Runtime world rebuild scaffolding for loading imported level targets; headless checks currently verify both startup level `105` and direct pipe target `1CB`.
- Runtime pipe debug triggers derived from imported screen exits and placed pipe tiles instead of a hardcoded screen position, including vertical, horizontal, and right-diagonal pipe clusters. Downward pipe triggers now latch only after a grounded player overlaps the imported entrance rectangle, avoiding accidental warps from held-down setup frames or nearby pipe/block-top probes.
- Runtime loading and debug rendering of imported sprite spawn records using the native `yyyyEESY / XXXXssss / id` coordinate layout, including 34 Yoshi Island 1 sprite spawns; the current direct target `1CB` has no sprite spawns.
- A first-pass runtime actor layer that turns common imported Yoshi Island 1 enemy records into simple moving/stompable/hurting bodies with per-sprite hitboxes for atlas-backed Rex, Banzai Bill, sliding Koopa, and jumping Piranha. Runtime actors now wake only inside an expanded camera window, so offscreen sprite records do not move, hurt Mario, or receive fireball hits before the camera reaches them; spawned power-up actors stay always-active. Jumping Piranha now has a first timed hidden/rise/extended/fall cycle with no hurtbox while hidden, block-style sprite actors such as the flying question block are solid and can spawn native item actors for mushroom/flower/feather/1-up rewards, the imported invisible mushroom sprite `0xC7` now reveals a real mushroom actor on contact, stomped actors feed the first SMW score reward table, the level-control warp-hole sprite `0x8E` is kept out of normal gameplay visuals, and the level has a temporary course-clear trigger sourced from the imported native goal-tape sprite `0x7B`.
- Actors-on collision has a first frame-crossing stomp resolver for Rex/Koopa style bodies, a narrower Banzai Bill gameplay hitbox, and a focused Clappin' Chuck overlap guard for deterministic debug traversal. The headless gate now proves the actors-on Yoshi Island 1 autoplay probe can stomp the late goal-section Chuck and enter course clear without deaths, while exact native enemy state machines remain pending.
- Fire Mario now has a first player-fireball slice sourced from the native `SpawnPlayerFireball` offsets/speeds: tapping run/Y spawns a capped extended-sprite fireball, plays the fireball SFX probe, shows the shoot pose timer, bounces against temporary terrain collision, and can defeat runtime actors.
- Runtime enemy visuals now build eight palette-specific sprite atlases directly from the imported SNES 4bpp `$6000-$7FFF` VRAM and the per-level CGRAM palette, then select the row from each OAM property byte instead of drawing every actor through one preview palette.
- Per-level sprite GFX extraction through vanilla and Lunar Magic-aware GFX slot resolution, producing raw `$6000-$7FFF` sprite VRAM atlases for Yoshi Island 1 and the direct pipe target.
- Vanilla GFX preview extraction expands SMW 3bpp slots into 4bpp and fills the full 512-tile BG/sprite VRAM windows used by Map16 and OAM tile numbers.
- Level Map16 previews are resolved through the native per-tileset Map16 pointer table, so level `105` uses its grassland/YI1 tile definitions instead of treating `$0D8000` as a flat linear Map16 page.
- Mario runtime rendering now uses imported `PlayerGFXRt` OAM placement tables, signed X/Y displacement tables, dynamic head/body tile upload pointer mapping, native facing flips, native walking/spin-jump animation table data, and generated OBJ row 8 player palette atlases for the first pose set. The runtime also tracks explicit small/big/cape/fire power-up state, switches fire form to the native Fire Mario palette variant, and adjusts the collision height when forms change.
- A C# fixed-step Mario movement prototype using SMW velocity units, native flat-ground walk/run/P-meter caps, table-driven acceleration/drag around the cap, native ground friction, native jump/gravity tables, first native `$72` in-air jump/fall states, and the first cape hold-jump fall cap from the native reference. See `docs/PHYSICS-REFERENCE.md` for the current native-unit notes and the Hamaluik regression sanity checks.
- Headless/import/build validation scripts that avoid opening a Wayland window and assert audio, Map16, collision, and player sprite loading.
- Standalone .NET asset tools validate the current `manifest.json` graph, core level `105`/`1CB` metadata, vanilla entrance-table anchors, player OAM table exports, palette dimensions and key palette anchors, tileset/sprite GFX uploads, audio bank hashes, PNG/WAV headers, and key Map16 projection anchors without invoking Python. The native extractor tool also verifies that generated GFX32/GFX33, raw SPC banks, global Map16, first-level level/sprite/exit metadata, entrance/secondary-exit tables, global/per-level palette data, BRR sample preview WAVs, and generated `PlayerGFXRt` OAM/palette tables still match direct C# extraction/decoding from the original ROM.
- The headless gate now includes both actor-off and actors-on `--smw-autoplay=explore` traversals that reach the imported level `105` goal and enter the course-clear walkout, providing end-to-end regressions for first-level layout, camera, timer, deterministic control, and the current runtime enemy interaction slice.
- Wayland/Sway graphical wrappers default to workspace `6` through `SMW_SWAY_WORKSPACE`, and the capture wrapper can grab a compositor screenshot of the Godot PID for visual inspection.

## Local Commands

Import assets from the local ROM:

```bash
tools/import-smw.sh "/path/to/compatible-rom.sfc"
```

Validate importer transition data:

```bash
tools/check-importer.sh
```

Run headless Godot smoke tests:

```bash
tools/check-headless.sh
```

The headless check exercises the static start menu, the real menu-to-level start path with `--smw-title-start`, the default playable level, and a direct target load using `--smw-test-level=1CB`.

Run a short visible Wayland smoke test:

```bash
tools/check-wayland.sh
```

Capture the current level through Sway/Wayland:

```bash
tools/capture-wayland.sh generated/smw/captures/level_105_compositor.png 105
tools/capture-wayland.sh generated/smw/captures/level_105_pipe_probe.png 105 --smw-test-spawn=2224,240 --smw-debug-overlays --smw-no-audio
```

The visible wrappers default to Sway workspace `6`; override with `SMW_SWAY_WORKSPACE=<number>` if needed.

For reproducible lower-route captures, the runtime also accepts `--smw-test-spawn=x,y` alongside `--smw-capture=...`. Use `--smw-test-powerup=small|big|cape|fire` to force the debug player form for hitbox checks.

During a debug RCON run, `tools/smw-rcon.sh snapshot res://generated/smw/captures/name.png` saves the current Godot viewport immediately in visible runs, while `capture <path> <frames>` schedules a capture after one or more process frames. The same RCON channel can toggle inspection helpers live: `overlays on`, `actors off`, `actor_visuals off`, `god on`, and `audio off|on|status` are useful before positioning Mario for a screenshot or frame-step probe. Audio probes also accept `audio sample 09`, `audio jump`, `audio spin`, `audio coin`, `audio stomp`, `audio 1up`, `audio fireball`, `audio music Level`, `audio music Star`, and `audio stop`. Use `perf`/`fps` to print FPS, node counts, collision counts, and audio generator state when a visible run feels slow. Use `timer`, `timer 123`, or `timer frames 1` to inspect or force the gameplay clock, `game_pause on|off` to freeze/unfreeze normal gameplay while preserving the timer, `coins 99`, `dragon 4`, `lives 1`, and `star on|off|FF` for coin-life, dragon-life, game-over, and starman probes, and `continue` to restart after game over. Use `autoplay explore|off` to enable or stop deterministic right/run/jump exploration while paused frame stepping or during a visible run. Use `tile` or `tile <world-x> <world-y>` to query the Map16/collision role under a point, `sensors [tag]` to dump Mario's old-school head/side/foot probe tiles, `collision [x y radius]` to list nearby solid rectangles and slope segments, `pipe [x y radius]` to list nearby diagonal pipe floor/body/ceiling cells, `near <radius>` to list nearby runtime sprite actors, and `oam [tag]` to dump Mario's current native pose/OAM descriptor mapping. Use `hold right run`, `tap jump`, `release`, `ground on|off`, `step <frames>`, and `trace <frames> [inputs...] [tag=name]` to control or step a paused run while logging per-frame position, speed, camera, rendered pose/facing, foot tile, nearest actor data, and the cape-float timer; the headless gate now includes live RCON slope-jump and spin-jump pose traces.

For deterministic input checks, pass `--smw-input-script=/path/to/script` or `--smw-autoplay=explore`. Each script non-comment line starts with a frame count followed by held inputs separated by spaces, commas, colons, semicolons, or `+`, for example `8 right run`, `4 Right,Y`, or `6 Right+Y+B`. Supported gameplay tokens are `left`, `right`, `down`, `jump`/`B`, `spin`/`A`, and `run`/`X`/`Y`; native `.input` directives and menu/shoulder tokens such as `Start`, `Select`, `Up`, `L`, and `R` are accepted as no-ops for compatibility with the current `smw/` TAS/native-snapshot converter output. Normal play maps keyboard arrows/A/D/S plus Z/Space, X, Shift/C, and Enter, and also maps a standard gamepad: D-pad/left stick for movement, A for jump, B for spin jump, X or right shoulder for run/fire, and Start for pause/menu start.

To record your own native-source playthrough as a Godot input baseline, use:

```bash
tools/run-native-recording-wayland.sh
```

That legacy wrapper launches `/path/to/native-reference` visibly through its Wayland wrapper on workspace `6`, then converts a native `StateRecorder` snapshot into `generated/smw/recordings/latest-native-recording.input`. It is useful for extracting an input script from an already-created snapshot, but the sync harness no longer replays native save slots because that path can mutate the live native save-state directory. Prefer a clean frame-counted `.input` script recorded from game start plus a noted level-start frame.

To record and compare without touching save states, use `tools/run-native-input-recording-wayland.sh`. It records native input from boot into `generated/smw/recordings/latest-native-full.input`, writes markers to `latest-native-full.input.markers`, and slices `generated/smw/recordings/latest-level-start.input` from the `K` marker or automatic level-start marker. Use `tools/run-native-input-wayland.sh path/to/full-route.input` for native cold-boot playback. Use `tools/run-recording-compare-wayland.sh --input path/to/full-route.input --level-start-frame N` to open native cold-boot playback next to Godot running the level-start slice, or `tools/run-recording-trace-compare.sh --input path/to/full-route.input --level-start-frame N --frames 600` to write native/Godot logs plus JSONL-normalized traces under `generated/smw/traces/` and report the first player position or power-up divergence. `tools/slice-input-script.py` is the helper that turns the second signal, the level-start frame, into the Godot slice.

To drive the current Godot level with an external TAS diagnostic slice, use:

```bash
tools/prepare-tas-diagnostic.sh
tools/run-tas-diagnostic.sh --visible --overlays --smw-no-audio
```

`prepare-tas-diagnostic.sh` downloads and caches the TASVideos `3849S` movie zip under ignored `generated/smw/tas/`, converts the embedded Snes9x movie into the local input-script format, and writes a sliced diagnostic segment. That movie enters Yoshi's Island 2 first, so this is a hard desync/fidelity harness for future physics and collision work, not a Yoshi Island 1 sync claim and not a replacement for the native recording workflow above.

To compare the native source-port reference and Godot visually, use:

```bash
tools/run-tas-compare-wayland.sh
```

That launches `/path/to/native-reference` with the full converted movie from reset, waits for the traced active-gameplay frame, then launches Godot with the sliced input on Sway workspace `6`. Godot TAS playback is forced to small Mario because the reference movie enters its first played stage with `powerup=0`.

Pass `--smw-no-audio` or set `SMW_AUDIO=0` to disable the internal BRR/APU probe for quick performance A/B tests. Normal playtest runs keep audio enabled.

Pass `--smw-debug-overlays` to a Godot run when you want collision rectangles/outlines, slope lines, player hitbox/feet, camera bounds, pipe/goal triggers, sprite hitboxes, screen lines, object/sprite/coin/block markers, the debug HUD, the foot-tile Map16 probe, and the imported asset preview panel. Normal playable runs hide those overlays.

The start menu also exposes `Actors` and `Sprites` toggles. For deterministic launches, pass `--smw-actors=off` or `--smw-actor-visuals=off` to disable runtime sprite logic or only hide their visuals before the level scene builds.
Use `--smw-title-start` when you need a headless/automated probe that opens the menu first and then starts gameplay through the normal title-start path instead of bypassing the menu with `--smw-test-autostart`.

Build the C# project:

```bash
tools/check-dotnet.sh
```

This also runs the standalone C# physics smoke executable.
The physics smoke includes native-unit golden trajectories for sustained walk/run, held/released small jumps, and ledge falls so movement changes are caught at the pixel/subpixel state level, not only by single-frame table checks.
When `generated/smw/manifest.json` exists, this command also runs the standalone C# generated-asset contract checker.

Run only the native generated-asset contract checker:

```bash
dotnet run --project tests/SmwAssetCheck/SmwAssetCheck.csproj -- generated/smw
```

Run only the native C# ROM extraction/verification slice:

```bash
tools/check-native-extractor.sh
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-core "$SMW_ROM_PATH" /tmp/smw-native-core
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-levels "$SMW_ROM_PATH" /tmp/smw-native-levels
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-audio-previews "$SMW_ROM_PATH" /tmp/smw-native-audio
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-player-metadata "$SMW_ROM_PATH" /tmp/smw-native-player
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-entrance-tables "$SMW_ROM_PATH" /tmp/smw-native-entrances
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-palettes "$SMW_ROM_PATH" /tmp/smw-native-palettes
```

Importer note: `tools/smw_import.py` is still the broader offline development extractor for the full current first-level asset set, not a runtime dependency. The shipped Godot scene consumes generated JSON/PNG/BIN assets and runs in C#. The long-term target is to keep moving this extraction pipeline into C# Godot tooling or a C# asset-pipeline project so Python is no longer required for normal project workflows; `tools/SmwAssetTool` is the first checked-in C# extraction slice.

The Godot scripts default to the local 4.6.3 .NET build:

```bash
godot4-mono
```

An older standard Godot source tree is also present at `<local Godot source checkout>`, but this project should use the Mono/.NET executable above.

## Graphical Run

For Wayland safety, this repo does not automatically start a graphical Godot window. When you intentionally want to open it, use:

```bash
tools/run-wayland.sh
```

## Legal Asset Boundary

`generated/` is ignored by git. Do not commit ROMs, generated the original rights holder asset data, saves, or captures derived from proprietary content.
