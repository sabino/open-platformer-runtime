# Current Status and Roadmap

This project is an early Godot 4 .NET runtime clone/source-port style engine. It is source code and tooling only. It does not include ROMs, extracted art, audio, level data, saves, videos, screenshots, or generated asset packs.

The runtime is useful today for technical playtesting, importer validation, and frame-by-frame behavior work. It is not a complete game, not a finished engine, and not ready to be presented as a faithful replacement for the original runtime.

## Public Release Position

- Keep the public repository source-only.
- Users must provide their own compatible local ROM dump to generate local assets.
- `generated/`, `.godot/`, ROM files, saves, captures, TAS downloads, and extracted binary/audio/image data must stay ignored and untracked.
- The project should be described generically as a classic-platformer-compatible Godot runtime clone.
- Do not use third-party logos, box art, screenshots, extracted sprites, or generated level captures on the public project page.
- Source code is licensed under MIT; see `LICENSE`.

## What Works Now

- Godot 4 .NET/C# project boots to a `256x224` logical viewport.
- The local asset importer validates a compatible ROM and writes Godot-readable generated assets under `generated/smw/`.
- The importer can generate one requested level, a list of levels, or the full `000..1FF` level-id range.
- The manifest includes imported level metadata and ROM-derived overworld level titles when available.
- Desktop and browser ROM selection load directly into native gameplay and audio. The selector lists all 512 pointer slots and gates native play to four verified terrain layouts. The others remain inspectable entries. Names and aliases are not reachability evidence.
- A level can return to the selector with `Esc`, `Backspace`, gamepad Back/Guide, or after the current course-clear walkout.
- Runtime toggles exist for debug gizmos, actor simulation, actor visuals, and audio. Gizmos default on; audio is opt-in. The Music / SFX Player offers standalone listening.
- A GitHub Pages browser loader exists under `web/`. It validates a local file in browser memory and passes its bytes directly to native Godot decoding. It does not upload the ROM or run the Python importer.
- An experimental Godot .NET Web export track exists for custom builds based on `godotengine/godot#106125`; it is not part of the stock Godot 4.6.3 path and currently depends on local Godot marshalling fixes.
- Level rendering covers the current partial Map16/object projection, generated palettes, level previews, layer backgrounds for covered cases, and generated player/sprite atlases.
- The main verified playability slice is still the first-level route plus its direct pipe target. Other imported levels may boot and render, but playability varies widely.
- The runtime includes a first-pass fixed-step player physics core, basic HUD, timer/death/game-over paths, coin and dragon-coin pickups, first block interactions, first pipe transitions, a temporary goal/course-clear path, and a small actor layer.
- Headless tools cover build checks, asset-contract checks, importer checks, deterministic input scripts, RCON diagnostics, and autoplay smoke routes.

## Known Missing or Incomplete Areas

### Legal and Release Readiness

- CI checks managed builds, source-only physics/ROM/audio tests, conditional Web compilation, browser-loader handoff and exact synthetic DSP fixtures.
- Public package metadata, repository description, contribution policy, and issue templates are not finalized.
- Current documentation still contains many internal development details and compatibility terms. Keep public-facing wording generic where possible.

### Asset Pipeline

- Generated assets are local artifacts and are not part of the repository.
- The broad importer is still Python-based; the C# asset tool only covers focused extraction/verification slices.
- The browser loader passes ROM bytes into the same native decoder used by desktop. Actual execution and audio in the custom Web build remain separate compatibility gates.
- The direct Godot .NET Web path depends on an unmerged upstream PR and custom export templates. It should be treated as experimental and may need COOP/COEP headers from the deployment host.
- The level object expander is partial and does not implement every native object routine.
- Lunar Magic/custom-ROM behavior is only partially understood; vanilla-compatible ROM data is the current target.
- Some generated preview files are inspection aids, not authoritative runtime data.

### Levels and World Flow

- There is no overworld map, save system, event progression, path unlocking, switch-state persistence, or normal course-entry flow.
- The selector is a development/playtest course picker, not the final game shell.
- Only a narrow first-level route has strong regression coverage.
- Many levels will have missing objects, wrong geometry, wrong entrances, wrong layer behavior, or unimplemented hazards.
- Vertical levels, layer-2 interaction levels, autoscrollers, water/tide levels, ghost houses, castles, boss rooms, switch palaces, bonus rooms, and special mode levels should be treated as unsupported until proven.

### Collision and Physics

- Collision is still a hybrid of native-table work and temporary geometric bridges.
- The full Map16 act-as/block-code dispatch is not ported.
- Slope, ledge, diagonal-pipe, ceiling, corner, one-pixel edge, and embedded-tile behavior can still diverge from native behavior.
- Water, climbing, nets, ropes, vines, conveyor-like behavior, ice/friction variants, lava/hazard tiles, layer-2 crush/scroll collision, and many special terrain rules are missing or incomplete.
- Player state coverage is incomplete for cape flight, cape dive, swimming, climbing, Yoshi riding, carrying, sliding, damage/knockback details, power-up transitions, and many animation/control edge cases.

### Blocks, Items, and Map16 Semantics

- Only focused turn-block/question-block/flying-block cases are implemented.
- Full block bounce sprites, debris, note blocks, multi-coin blocks, vines, hidden blocks, P-switch behavior, ON/OFF behavior, directional coins, item-box behavior, reserve item rules, switch blocks, and most special blocks are missing or partial.
- Power-up actors exist only as a first gameplay slice. Exact item state machines, spawn timing, collection rules, and interactions need native-backed ports.

### Sprites, Enemies, and Spawn/Despawn

- The actor layer is intentionally small and focused on early routes.
- Some sprites have runtime behavior, some are visual/debug markers only, and most are not implemented.
- Exact native sprite slots, processing order, offscreen processing, despawn/reload windows, generator behavior, sprite memory settings, sprite-to-sprite interaction, carried-item semantics, and score-sprite multiplexing are incomplete.
- Common actors such as Rex, Banzai Bill, sliding Koopa/shell, Jumping Piranha, Clappin' Chuck contact guards, flying question blocks, and invisible mushroom have partial coverage only.
- Bosses, platforms, moving layer objects, line-guided objects, Lakitu/cloud behavior, Yoshi, keys, P-switches, shells across all states, throw blocks, most projectiles, generators, and many enemy families are missing or not faithful.

### Rendering and Presentation

- Mario rendering uses a partial native OAM bridge; many poses, cape/Yoshi variants, static descriptor mappings, priority, hide masks, and special animation states remain pending.
- Enemy rendering uses generated sprite VRAM/palette data where possible, but most exact OAM assembly, priorities, and animation phases are missing.
- The selector currently uses compact Godot UI fonts sized for the SNES viewport; the original ROM bitmap font is not wired into the UI.
- Layer 3, message boxes, status-bar details, windowing effects, mode-specific backgrounds, fade effects, and many visual effects are not complete.

### Audio

- Audio is disabled by default.
- Native SMW sequencing and integer S-DSP processing now play ROM-derived music and SFX without running an SPC700 CPU.
- The listening player offers all three song banks, transport controls and playback diagnostics.
- DSP fixtures pass exact comparisons; translated-driver cases pass at 500 Hz control boundaries. Full-song firmware timing, exhaustive SFX/transition coverage, and browser playback remain unverified. See [NATIVE-AUDIO.md](NATIVE-AUDIO.md).

### Tooling and Testing

- The strongest automated checks are focused on the current first-level slice and importer contracts.
- GitHub Pages deployment and source-only CI exist. Local `tools/check-rom-startup.sh` verifies ROM selection, header stripping, verified level entry, the first-course pipe route and the audio player with an empty asset pack.
- Full regression coverage across all imported levels does not exist.
- Frame-level comparison tooling exists, but many systems still need native trace anchors and golden tests.
- Proprietary ROM-dependent captures remain local and are never CI artifacts.

## Roadmap

1. Public-source cleanup
   - Keep MIT license metadata present and accurate.
   - Keep project naming and README framing generic.
   - Run history scans for generated assets, ROMs, screenshots, local paths, and accidental binary data.
   - Maintain source-only CI and extend behavioral evidence for the first course.

2. Runtime/importer contract hardening
   - Keep moving extraction and validation into C# tooling.
   - Move browser-safe manifest generation into `src/SmwAssets`.
   - Make manifest schemas explicit and versioned.
   - Keep generated asset contracts machine-checkable.
   - Separate inspection previews from authoritative runtime data.

3. Browser runtime
   - Keep the GitHub Pages loader source-only and ROM-free.
   - Grow the focused in-memory browser asset pack toward broader level coverage.
   - Test the custom `godotengine/godot#106125` export path separately from stock Godot.
   - Add a runtime asset provider so gameplay code can consume browser-generated assets instead of `res://generated/smw/`.
   - Keep the local Godot Web .NET marshalling fixes isolated and upstreamable.

4. Exact Map16 and collision core
   - Port native Map16 act-as lookup and block-code dispatch.
   - Replace temporary geometric slope/pipe bridges with native-backed terrain semantics.
   - Add golden tests for edge, corner, ceiling, slope, pipe, and block-contact cases.

5. First-level fidelity milestone
   - Make the first-level route and direct pipe target stable with actors on.
   - Preserve deterministic input and trace comparison for every physics or sprite change.
   - Drive mismatches down with frame-level native trace evidence.

6. Player state completeness
   - Finish player OAM mapping.
   - Port cape, swimming, climbing, carrying, Yoshi, damage, power-up transitions, and special movement states.
   - Replace temporary state bridges with table-driven/native-backed code.

7. Sprite and item framework
   - Implement native sprite slot lifecycle, loading, despawn, sprite memory, generators, and processing order.
   - Port common enemies and carryable items before expanding to bosses and rare sprites.
   - Add actor-specific tests for contact, stomp, hurt, carry, fireball, offscreen, and score behavior.

8. Level systems
   - Expand object projection and runtime semantics beyond the first-level slice.
   - Add layer 2/3 modes, autoscroll, water/tides, vertical levels, castles, ghost houses, switch palaces, bonus rooms, and boss rooms.
   - Add a compatibility matrix per level rather than implying that all imported levels are playable.

9. Audio and presentation
   - Implement the full music/SFX sequencing path.
   - Port status-bar, message, layer-3, fade, score-sprite, and transition presentation.
   - Replace development UI pieces only after the runtime behavior is stable.

10. Overworld and persistence
   - Add overworld map/runtime progression, course completion events, save data, lives/coins persistence, switch flags, midway points, and return-to-map behavior.

## Suggested Public Issue Labels

- `asset-boundary`
- `importer`
- `manifest`
- `collision`
- `map16`
- `physics`
- `player`
- `sprites`
- `blocks`
- `audio`
- `ui`
- `tests`
- `release-hygiene`
