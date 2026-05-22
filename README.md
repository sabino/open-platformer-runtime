# Open Platformer Runtime

This repository is a Godot 4 .NET desktop port scaffold for a native classic-platformer-compatible engine. It is not an emulator and does not ship third-party copyrighted assets. A verified local compatible USA ROM is used only as input to an offline importer that writes Godot-readable generated data under `generated/smw/`.

The long-term bar is a 100% physics and gameplay semantics match with the original game. The first slices should therefore be table-driven from SMW reference constants and covered by regression tests, even when a subsystem starts incomplete.

The current first slice covers:

- ROM validation and deterministic extraction for level `105` by default.
- Layer 1/layer 2 raw object streams, decoded placement metadata, screen exits, sprite stream, Map16, global palettes, per-level full CGRAM palettes, GFX32/GFX33, player PNG atlases, level tileset GFX atlases, sprite GFX VRAM atlases, Map16 preview atlases, partial level layout previews, and secondary-exit tables.
- Raw SPC upload banks plus a few decoded BRR preview WAVs used only as importer verification artifacts while the full SPC/DSP sequencer is still pending.
- A Godot .NET C# menu and minimal playable scene.
- A SNES-sized `256x224` logical viewport that opens as a 3x Wayland window for normal graphical runs.
- Runtime audio playback through a C# internal APU probe that streams decoded BRR samples from imported SPC engine/sample banks; the Godot runtime no longer depends on WAV/MP3 playback for these probes.
- A menu audio panel for internal port-1 SFX command probes and BRR sample probes, plus imported music bank visibility while the full SPC/DSP command sequencer is still pending.
- A debug asset overlay showing the imported level GFX, sprite GFX, full-CGRAM palette-aware Map16 preview, and partial level layout preview while the level renderer is still being ported.
- Runtime placement of the generated Yoshi Island 1 Map16 tilemap, with temporary merged collision rectangles derived from imported tile placement sources.
- A runtime Mario sprite composite built from generated GFX32 PNG data and the ROM-derived `PlayerGFXRt` head/body tile pointer tables. This replaces the placeholder hitbox rectangle, but final frame/state correctness still depends on porting the direct OAM assembly tables.
- Partial object expansion for the Yoshi Island 1 direct pipe target `0CB`, including horizontal pipes and rope mushroom platforms in the generated Map16 tilemap.
- Manifest-driven runtime asset selection for the current level's tilemap, preview, and tileset atlases, rather than hardcoded generated filenames.
- Runtime world rebuild scaffolding for loading imported level targets; headless checks currently verify both startup level `105` and direct pipe target `0CB`.
- Runtime pipe debug triggers derived from imported screen exits and placed pipe tiles instead of a hardcoded screen position.
- Runtime loading and debug rendering of imported sprite spawn records, including 34 Yoshi Island 1 sprite spawns and the direct target's single spawn.
- Per-level sprite GFX extraction through vanilla and Lunar Magic-aware GFX slot resolution, producing raw `$6000-$7FFF` sprite VRAM atlases for Yoshi Island 1 and the direct pipe target.
- Vanilla GFX preview extraction expands SMW 3bpp slots into 4bpp and fills the full 512-tile BG/sprite VRAM windows used by Map16 and OAM tile numbers.
- Level Map16 previews are resolved through the native per-tileset Map16 pointer table, so level `105` uses its grassland/YI1 tile definitions instead of treating `$0D8000` as a flat linear Map16 page.
- Mario runtime rendering now uses imported `PlayerGFXRt` OAM placement tables, signed X/Y displacement tables, dynamic head/body tile upload pointer mapping, native facing flips, and the proper OBJ row 8 player palette layout for the first big-Mario pose set.
- A C# fixed-step Mario movement prototype using SMW velocity units, native flat-ground walk/run/P-meter caps, native ground friction, and jump/gravity constants from the native reference.
- Headless/import/build validation scripts that avoid opening a Wayland window and assert audio, Map16, collision, and player sprite loading.
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

The headless check runs the default playable level and a direct target load using `--smw-test-level=0CB`.

Run a short visible Wayland smoke test:

```bash
tools/check-wayland.sh
```

Capture the current level through Sway/Wayland:

```bash
tools/capture-wayland.sh generated/smw/captures/level_105_compositor.png 105
```

The visible wrappers default to Sway workspace `6`; override with `SMW_SWAY_WORKSPACE=<number>` if needed.

Build the C# project:

```bash
tools/check-dotnet.sh
```

This also runs the standalone C# physics smoke executable.

Importer note: `tools/smw_import.py` is an offline development extractor, not a runtime dependency. The shipped Godot scene consumes generated JSON/PNG/BIN assets and runs in C#. The long-term target is to port this extraction pipeline into C# Godot tooling or a C# asset-pipeline project so Python is no longer required for normal project workflows.

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
