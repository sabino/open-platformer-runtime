# Current Runtime Features

This document captures the current implementation slice. It is intentionally specific: most systems are incomplete, and the strongest regression coverage is still concentrated around the first-level route and its direct pipe target.

## Import and Manifest

- ROM validation and deterministic extraction for level `105` by default.
- Native C# extractor slices directly extract and verify core ROM assets used by the runtime, including GFX32/GFX33, raw SPC upload banks, global Map16, palette assets, level headers, object streams, sprite streams, layer-2 metadata, vanilla entrance tables, BRR-decoded preview WAVs, and Mario OAM/palette metadata.
- Layer 1/layer 2 raw object streams, Layer 2 RLE background previews, decoded placement metadata, screen exits, sprite streams, Map16, palettes, GFX atlases, sprite VRAM atlases, Map16 preview atlases, partial level layout previews, and secondary-exit tables are generated locally.
- The importer can generate a requested level, a list of levels, or the full `000..1FF` level-id range.
- The manifest records imported level metadata and decoded overworld level titles when available.
- Runtime asset selection is manifest-driven for tilemaps, previews, tileset atlases, and decoded level titles.

## Menu and Runtime Shell

- Godot 4 .NET/C# project boots a SNES-sized `256x224` logical viewport.
- The course selector lists imported levels and supports type-ahead search by id or ROM-derived title.
- The selector can launch a selected imported level.
- `Esc`, `Backspace`, gamepad Back/Guide, and the current course-clear flow return from a level to the selector.
- Runtime toggles exist for debug gizmos, actor simulation, and actor visuals.
- Audio is intentionally opt-in and greyed out in the selector.

## Rendering

- Runtime placement of generated Map16 tilemaps uses a batched custom draw layer.
- Generated Layer 2 RLE background previews render behind covered Layer 1 Map16 placements.
- Map16 rendering converts raw quadrant word order into render quadrant order.
- Level Map16 previews resolve through the native per-tileset Map16 pointer table.
- Runtime enemy visuals build palette-specific sprite atlases from imported sprite VRAM and per-level CGRAM where possible.
- Mario runtime rendering uses a partial native `PlayerGFXRt` OAM bridge with generated player atlas data, signed displacement tables, dynamic head/body tile upload pointer mapping, facing flips, walking/spin-jump animation table data, power-up forms, fire palette selection, and collision height changes.

## Gameplay Slice

- A compact normal-play HUD shows score, lives, normal coins, dragon-coin pips, level timer, pause, course-clear state, and a first game-over flow.
- The timer can trigger a first time-up death/restart path.
- Normal coins and dragon coins can be picked up from imported Map16 coin markers.
- The fifth dragon coin and the 100-coin rule can award extra lives.
- A first world-space score popup layer exists for coin, item, stomp, star, and 1UP rewards.
- Runtime camera scrolling uses SMW-style horizontal and vertical thresholds instead of center-follow.
- Runtime pipe debug triggers are derived from imported screen exits and placed pipe tiles.
- Partial pipe and screen-exit transitions exist for the focused route.
- A temporary course-clear trigger is sourced from the imported native goal-tape sprite record.

## Collision and Physics

- The C# fixed-step movement prototype uses SMW-style native units for flat-ground walk/run/P-meter caps, acceleration, drag, friction, jump/gravity tables, first in-air jump/fall states, and the first cape hold-jump fall cap.
- Collision separates imported diagonal pipe/slope/ledge clusters into per-Map16-tile floor and ceiling slope surfaces instead of long averaged slope lines.
- Mario floor-slope resolution probes center plus both feet and biases toward current horizontal movement direction.
- The runtime still uses temporary geometric bridges until full native Map16 act-as and block-code dispatch are ported.

## Blocks, Items, and Actors

- Focused Yoshi Island 1 turn-block, flying-question-block, and static-question-block interactions exist.
- Static question-block content labels are preserved for several flower/feather/star/coin-style cases.
- First item actors exist for mushroom/flower/feather/1-up style rewards.
- Runtime sprite spawn records use the native coordinate layout.
- A first-pass actor layer covers selected early-route actors such as Rex, Banzai Bill, sliding Koopa/shell, Jumping Piranha, flying question blocks, and invisible mushroom behavior.
- Runtime actors wake inside an expanded camera window; spawned power-up actors stay always active.
- Fire Mario has a first player-fireball slice sourced from native spawn offsets/speeds.

## Audio

- Audio is disabled by default.
- The current audio path is a diagnostic/internal BRR/APU probe, not a complete SPC/DSP sequencer.
- Named SFX probes and music-preview controls exist for development verification.

## Validation

- Headless/import/build validation scripts avoid opening a graphical window and assert audio, Map16, collision, and player sprite loading.
- Standalone .NET asset tools validate the generated manifest graph and key generated assets.
- Headless gates include actor-off and actors-on autoplay traversals for the focused first-level route.
- Deterministic input scripts, RCON diagnostics, and trace comparison tools are available for frame-level debugging.
