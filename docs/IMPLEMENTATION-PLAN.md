# Complete SMW implementation and verification plan

## Objective

Implement the complete supplied vanilla USA Super Mario World as native Godot
game systems: every original sequence, menu, level, player state, enemy, block,
world event, save rule, visual effect, song, and sound effect. Verify exact
behavior and presentation against the original ROM, including TAS playback.

The immediate acceptance gate is [the complete first course](FIRST-COURSE.md):
`105` with its `1CB` pipe room, ordinary input, original behavior/presentation,
completion, progression and replay. Expand through new behavior families only
after that gate. Shared native data and Godot scenes should also support later
ROM-format profiles and explicit native implementations of custom behaviors;
arbitrary ROM-hack assembly is not data-only compatibility.

The baseline is the unheadered 512 KiB USA ROM, SHA-1
`6B47BB75D16514B6A476AA0C73A683A2A4C18765`. Regional variants and ROM hacks
are separate work. Their behavior must not change this vanilla baseline.

## Runtime and asset boundary

- Ship Godot and native C# gameplay, rendering, and audio systems. Read the ROM
  as data; do not execute its CPU instructions or embed an emulator or hardware
  core in the game. External emulators and reference executables are tools.
- Load graphics, palettes, tilemaps, animation tables, fonts, levels, sprites,
  entrances, overworld, music, samples, and other game data through a shared
  filesystem-free C# ROM reader. Desktop and browser hosts use the same data
  contracts, with an optional cache keyed by ROM hash and decoder version.
- Keep ROMs, decoded tables, asset packs, captures, audio, movies, SRAM,
  snapshots, and reference builds ignored. Never commit or publish them.
- Python can remain a development/comparison tool; normal game startup must
  ultimately load the supplied ROM without a pre-generated Python asset pack.
- Preserve deterministic input, trace, RCON, checkpoints, and frame inspection
  throughout the implementation. Keep development presentation explicitly
  available as tools while implementing the original game shell.

## Source hierarchy and research

Original-ROM observations decide behavior. Named C routines in
`snesrev/smw` explain it; the `galaxyhaxz/smw-src` and SMWDisX disassemblies
cross-check addresses, control flow, signed arithmetic, and data. The
SuperMarioWorldRecomp project provides additional translated control flow and
debugging techniques. Record exact source revisions and routine/label links.

Both reimplementation/recompilation references contain emulated hardware.
Use their game routines and external comparison facilities; do not transplant
their hardware backends into Godot. Distinguish vanilla code from Lunar Magic,
mods, workarounds, and unverified changes. Preserve applicable provenance and
attribution for substantial translated logic, and load ROM tables from the ROM.

See [REFERENCE-RESEARCH.md](REFERENCE-RESEARCH.md) for pinned references and
the current tooling audit. The existing [REFERENCE.md](REFERENCE.md) contains
historical notes about a different, modified native checkout.

## Baseline and assumption audit

Godot .NET 4.7.2 is installed on macOS. The existing 4.6.3 C# project builds,
boots, and passes focused physics and generated-asset verification. The full
gameplay suite was already red before the native data migration: its actor-contact
fixture produced identical failing state on 4.6.3 and 4.7.2. With current native
terrain, the suite still fails its actor-hit assertion: `y=288.25` versus the
fixture's `288.00`. This needs original contact/state evidence; the behavioral
assertion remains intact. The updated construction smoke checks include full
original buffer hashes rather than relying solely on obsolete preview counts.

| Finding | Required correction and evidence |
| --- | --- |
| Native adapters require custom runner/trace features absent in upstream snesrev. | Add capability checks and explicit upstream adapters; identify historical modified-fork workflows. |
| Notes name `.cpp` files while pinned upstream uses `.c`. | Link pinned files and named routines; preserve historical notes as historical. |
| `60` host ticks are treated as the native frame clock. | Define game frames, controller polling, update order, render handoff, and clock cadence from traces. |
| Trace comparison defaults to input delay, Y offset, and one-pixel tolerance. | Exact mode uses zero tolerance and declared coordinate/frame semantics. Diagnostic transforms cannot yield a parity pass. |
| TAS slicing assumes a fixed frame `1525`. | Derive and record actual anchors. Preserve console events, SRAM, region, controllers, lag, and source frame IDs. |
| Input scripts discard Start/Select/L/R and merge X/Y before edge detection. | Preserve both ports' raw bits and original held/pressed latches before mapping gameplay controls; reject unsupported capture inputs. |
| Actor/contact fixtures disagree with the current runtime. | Capture original clipping, slot/spawn state, and update order before changing behavior or expectations. |
| Collision contains approximate geometry and level-position-specific bridges. | Port native Map16/block contact and fixed-point rules; remove each bridge with original-ROM regression evidence. |
| Import success or zero unsupported objects is treated too broadly. | Verify ROM bytes, object expansion, Map16, entrances, layers, actors, and playability independently. |
| Partial sprite logic, OAM, and development visuals cover many IDs. | Inventory all dispatch entries and verify behavior, lifecycle, clipping, rendering, and interactions separately. |
| Selector, HUD, fonts, transitions, and audio are development approximations. | Port original mode routines and ROM-driven tile/font/music data. |
| Desktop success is conflated with browser readiness. | Verify the custom fork, templates, marshalling fixes, SDK, and workloads independently. |
| Emulator power-on RAM defaults are randomized. | Record the declared initial RAM/SRAM and configuration; prove repeated captures reproduce. |

## Observable iteration loop

Complete this setup before starting the perfection implementation goal:

1. Validate/fingerprint the ROM, sources, tool builds, input, and initial state.
2. Capture the original ROM through Mesen headless Lua callbacks, including
   exact integer state, controller latches, mode/RNG/timers, relevant memory,
   logical framebuffer, and later audio and sprite/OAM data.
3. Replay the same declared case in Godot. Maintain pause, single-frame step,
   controller injection, checkpoint save/load, terrain, actor, OAM, and captures.
4. Store source frame IDs, sampling phases, input hashes, start-state hashes,
   visible area, palette conversion, and tool configurations in a case manifest.
5. Report the first missing field/frame or exact state/pixel/audio difference.
   Do not reconstruct raw positions from rounded floating-point log text.
6. Reduce each failure to a short case, port one routine, rebuild, and rerun
   that case and its affected regressions. Keep the full local evidence.

An incompatible initial state is an explicit comparison incompatibility.
Diagnostic offsets, skipped intro frames, mutable emulator defaults, cropped
captures, and approximations must not silently establish a successful match.
Headless and graphical state stepping must agree. On macOS, bounded graphical
inspection follows the required focus workflow and only controls created windows.

## Milestones and gates

### P0 — Portable setup and trustworthy comparison

Implement portable import/run/debug/check commands; ignored local ROM/tool
configuration; source pins and capability checks; deterministic Mesen capture;
TAS inspection; exact state/pixel comparison; concise first-divergence reports;
and an inventory of failing, stale, unsupported, and source-only checks.

Gate: another session can reproduce an original capture, run and inspect a
Godot case interactively, replay inputs, and receive a truthful difference report.

### P1 — Complete shared ROM data loading

Started: shared validated ROM access, bounded LC_LZ2, all 52 graphics formats,
pixel-index decoding, and original contextual upload rules. All files pass byte
checks; boot GFX00 and four font uploads match actual original VRAM exactly.
Native palette assembly matches all 512 boot mirror bytes. Godot now builds
player/sprite/Map16/background textures and reads 12 player animation/OAM tables
directly from the ROM. Two metadata-only level boots and 28 complete RGBA images
pass exact data/CPU-transfer checks. Native streams/entrances audit all 512 slots,
23,996 objects, 3,290 sprite records and 1,024 entrance records. The original
title entrance and the metadata-only `105 -> 1CB -> 105` route also pass.
Levels `0C7`, `105`, `106`, and `1CB` now expand their terrain natively: all
114,688 Map16 bytes match the original loader. Their Godot development entries
boot with an entirely empty pack, including no manifest, and the main/secondary
pipe route passes in that same setup. The title terrain comparison uses the
unchanged original power-on path. Audio remains disabled for this empty-pack gate.
Desktop ROM selection and the browser byte handoff now connect the same ROM to
native gameplay and audio. Normal startup writes no asset pack and no longer
runs Python. The C# catalog distinguishes 512 pointer slots from verified
terrain layouts, bounds vanilla names to $5D words, and preserves primary-layout
aliases. Native play rejects unverified terrain instead of importing preview
geometry. Managed music/SFX and the integer DSP are implemented, with the
whole-song timing limitations in [NATIVE-AUDIO.md](NATIVE-AUDIO.md).
See [ROM-DATA.md](ROM-DATA.md). This milestone remains open: additional object
families, world/UI/persistence data, actual Web runtime verification and
reachability analysis still require work.

Port LoROM mapping, decompression, palettes/planar graphics, Map16, object and
sprite streams, entrances, OAM/animation data, UI/font/HUD data, overworld,
audio/sample banks, and persistence metadata into `src/SmwAssets`. Add native
ROM selection, bounds checks, provenance, versioned contracts, and cache
invalidation. Classify actual referenced levels and unused pointer slots.

Gate: C# extraction matches verified ROM data, and desktop startup needs only
the ROM; browser memory loading can consume the same decoded contracts.

### P2 — Native deterministic game state and timing

Split global, player, level, world/menu, RNG/timer, sprite-slot, and audio-command
state from Godot nodes. Preserve native signedness, overflow, fixed-point units,
freeze rules, counters, processing order, and input polling. Implement both
controllers, held/pressed latches, pause/reset, complete checkpoint schemas,
and one authoritative simulation step/render snapshot boundary.

Gate: identical state/input produces identical traces on repeated runs, hosts,
headless/graphical operation, and verified slow/fast test execution.

### P3 — Original boot, title, menus, introduction, and HUD

Port the full game-mode table and original entry/transition routines: Nintendo
Presents, fades/circle/mosaic, title/demo input, file select, erasure, player
selection, opening message, status bar, reserve item, messages, pause, continue,
and game over. Decode original tile/font/palette/cursor/demo data. Keep the
development selector and probes behind explicit tooling entry points.

Gate: original power-on/menu movies reach matching modes/states on the same
source frame and match logical-frame pixels.

### P4 — Native level construction and terrain

Started: the shared Map16 tile cursor and generic tiles, ground edges/ledges,
vertical/horizontal pipes, Yoshi coins, cave ceilings, steep right slopes,
diagonal pipes/ledges, large/small bushes, gradual right slopes, goal gates and
switches construct `0C7`, `105`, `106`, and `1CB` exactly against the original
loader. Other object families, the remaining levels,
persistent collection state and contact behavior remain open. Port the next
families against complete original buffers rather than copying preview rules.

Port every vanilla object dispatch, extended object, Map16 act-as/block contact,
layer, horizontal/vertical layout, entrance, and exit rule. Implement exact
player/sprite sensors and slopes, ledges, ceilings, corners, embedded tiles,
pipes, water/lava, vines/nets, ice, and moving-layer interaction. Remove temporary
geometry and level-specific bridges one verified case at a time.

Gate: object/tile/entrance inventories agree and terrain fixtures match exact
per-frame state, not just preview appearance or successful level boot.

### P5 — Complete player behavior and OAM

Port walking/turning/running/P-meter, normal/spin jumping, ducking/sliding,
swimming/climbing, carrying, damage/death/transformation, cape flight/dive,
Yoshi, pipes, reserve items, pose/OAM assembly, dynamic tile selection, palette,
priority/hide rules, cape parts, and mounted composition. Preserve TAS input
edge cases, simultaneous directions, timing windows, and arithmetic overflow.

Gate: every player routine has original fixtures; the first full route and
sublevel have zero unexplained state and OAM differences.

### P6 — Blocks, items, sprite lifecycle, and all actor families

Port slot allocation/order, memory/spawn/despawn/offscreen/reload rules,
generators/shooters, auxiliary sprites, block bounce/debris, every block family,
coins, vines, switches/P-switches, items, shells, keys, carried objects, projectiles,
score sprites, all enemies/platforms, line-guided actors, Yoshi, hazards, and
bosses. Track each dispatch entry and table in a behavior/visual coverage matrix.

Gate: all required vanilla entries are implemented; lifecycle, clipping,
contact, animation, and interaction fixtures cover each family and boss phase.

### P7 — Full rendering and special level systems

Implement layer 1/2/3 and HUD composition, tile animation, scroll modes,
autoscroll, tides, layer-2 crush/movement, vertical levels, ghost/castle effects,
boss transforms, and required Mode 7 presentation using native Godot draw/shader
systems. Match integer pixel placement, flips/priorities, palette arithmetic,
color math, windows, transparency, fades, mosaic, and required scanline effects.
Separate logical pixels from host integer scaling and remove normal-path art
or font placeholders.

Gate: exact image comparisons pass across every level/effect mode and animation
sequence, including interior sprite pixels and changing priorities.

### P8 — Original music and sound effects

Current vertical slice: managed native SMW driver, integer eight-voice DSP,
versioned ROM audio cache, checkpointed sample/event timing, Godot final-mix
playback, and standalone song/SFX player. The 190 synthetic DSP cases and
50 five-second translated-driver cases pass with zero PCM mismatches. These
checks do not establish original firmware write timing or full-song parity.
[NATIVE-AUDIO.md](NATIVE-AUDIO.md) records the evidence and outstanding work.

Decode song/instrument/sample/envelope/pitch/loop data and port the game music
sequencer/channel rules without executing SPC700 firmware. Implement BRR,
voice scheduling, interpolation/envelopes, echo, SFX arbitration, pause/fades,
and game/audio command timing. Record raw original audio and voice events with
explicit sample/frame alignment and initial state.

Gate: every song/SFX plays from the ROM, and declared deterministic voice/PCM
fixtures match; host/backend conversions remain explicit.

### P9 — Overworld, progression, saves, and multiplayer

Port maps/submaps, paths/events, normal/secret exits, switches, midway flags,
castle destruction, Star Road/Special World, return flow, Mario/Luigi turn order,
lives/coins/items/Yoshi, and original save layout/checksums. Isolate local saves.
Cover creation, erasure, load/save, continue, and game over for both players.

Gate: every original progression branch and exit is correct; persistence and
two-player scenarios match original fixtures.

### P10 — Every sequence and full-game TAS completion

Finish goal/secret-exit flow, bonus game, boss victories, rescue/castle scenes,
final Bowser, ending, credits, enemy roll call, and post-completion changes.
Run complete movies from declared power-on/SRAM states for ordinary play,
alternative routes, all exits, death/continue/save flows, and TAS techniques.
Audit every fallback, unsupported branch, and unexplained constant.

Gate: the coverage matrix has no required unresolved branches; complete movies
have zero unexplained input, state, image, progression, or audio differences.

### P11 — Browser, host parity, and source-only distribution

Pin and validate the experimental Godot fork/editor/templates, marshalling
patches, SDK/workloads, and browser ROM memory loading. Reuse the native C#
simulation/decoder. Verify controller, audio, rendering, saves, and TAS on
macOS/Linux/Windows and the browser. Add source-only CI, optional private-ROM
verification, packaged builds, and tracked-file/release asset-boundary audits.

Gate: supported hosts pass the same corpus and distribution contains no ROM,
derived game data, captures, emulator/reference binaries, or local configuration.

## TAS acceptance contract

Preserve all 12 controller bits for both ports, held/pressed edges, opposing
directions, power/reset events, controller types, region, initial SRAM/state,
lag semantics, source frame IDs, and movie metadata. Reject any unimplemented
format/state instead of discarding it. Power-on movies exercise the complete
original shell; aligned level slices are separately named diagnostics.

Record an exact first divergence in integer state, OAM/pixels, progression, or
audio. A movie finishing successfully is insufficient. Use external emulator
snapshots for the oracle; Godot checkpoints serialize native game state and
must not introduce an emulated CPU into the runtime.

## Completion and execution discipline

Complete only when every required vanilla subsystem and sequence is implemented
and the declared full-game comparison corpus has zero unexplained differences.
Build success, a booting course, a visual approximation, or a shortened movie is
an intermediate result. Keep failed cases visible, fix their first cause, and
rerun affected checks. Do not relax tolerances or assertions to produce passes.
Preserve unrelated work and the separate browser fork; pushing/publishing still
requires explicit permission.
