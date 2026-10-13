# Current Runtime Features

This document captures the current implementation slice. It is intentionally specific: most systems are incomplete, and the strongest regression coverage is still concentrated around the first-level route and its direct pipe target.

## Import and Manifest

- ROM validation and deterministic extraction for level `105` by default.
- Native C# extractor slices directly extract and verify core ROM assets used by the runtime, including GFX32/GFX33, raw SPC upload banks, global Map16, palette assets, level headers, object streams, sprite streams, layer-2 metadata, vanilla entrance tables, BRR-decoded preview WAVs, and Mario OAM/palette metadata.
- Layer 1/layer 2 raw object streams, Layer 2 RLE background previews, decoded placement metadata, screen exits, sprite streams, Map16, palettes, GFX atlases, sprite VRAM atlases, Map16 preview atlases, partial level layout previews, and secondary-exit tables are generated locally.
- The importer can generate a requested level, a list of levels, or the full `000..1FF` level-id range.
- The manifest records imported level metadata and decoded overworld level titles when available.
- With a configured ROM, runtime graphics/palettes/player OAM tables, object/sprite streams and entrances load natively in C#. Terrain placement, previews, and decoded level titles still use the development manifest.

## Menu and Runtime Shell

- Godot 4 .NET/C# project boots a SNES-sized `256x224` logical viewport.
- The course selector lists imported levels and supports type-ahead search by id or ROM-derived title.
- The selector can launch a selected imported level.
- `Esc`, `Backspace`, gamepad Back/Guide, and the current course-clear flow return from a level to the selector.
- Runtime toggles exist for debug gizmos, actor simulation, and actor visuals.
- Audio is opt-in through the selector toggle or command line; a standalone listening player is available.

## Rendering

- Runtime Map16 terrain uses Godot `TileMapLayer` and `TileSetAtlasSource` resources.
- ROM-decoded Layer 2 RLE backgrounds render behind covered Layer 1 Map16 placements; exact scrolling remains incomplete.
- Map16 rendering converts raw quadrant word order into render quadrant order.
- Level Map16 previews resolve through the native per-tileset Map16 pointer table.
- Runtime enemy visuals build eight palette-specific sprite atlases from native ROM graphics and per-level palette assembly.
- Mario runtime rendering uses a partial native `PlayerGFXRt` OAM bridge with ROM-decoded player atlas data, signed displacement tables, dynamic head/body tile upload pointer mapping, facing flips, walking/spin-jump animation table data, power-up forms, fire palette selection, and collision height changes. The pose/animation/OAM tables now load directly from the ROM.
- Native visual verification checks 28 complete RGBA images and Godot CPU image transfer for levels `105` and `1CB`; metadata-only boots and their main/secondary pipe route require no generated art, palette JSON, player graphics JSON, level-stream JSON or entrance JSON. Full framebuffer/gameplay parity remains unproven.

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
- ROM-derived sequences and BRR samples play through a native SMW sequencer and integer eight-voice S-DSP pipeline.
- The music player exposes 50 original song IDs, gameplay SFX, transport controls, volume and live playback diagnostics.
- DSP-only fixtures match `snes_spc` exactly. Register traces and PCM for 50 five-second song cases match the translated C driver at 500 Hz control boundaries; original SPC700 instruction timing is a separate, unfinished gate.
- No SPC700 CPU executes during gameplay. Godot receives the final stereo PCM through `AudioStreamGenerator`.
- See [NATIVE-AUDIO.md](NATIVE-AUDIO.md) for verification and limitations.

## Validation

- Headless/import/build validation scripts avoid opening a graphical window and assert audio, Map16, collision, and player sprite loading.
- Standalone .NET asset tools validate the generated manifest graph and key generated assets.
- Headless gates include actor-off and actors-on autoplay traversals for the focused first-level route.
- Deterministic input scripts, RCON diagnostics, and trace comparison tools are available for frame-level debugging.
