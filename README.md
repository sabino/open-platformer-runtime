# Open Platformer Runtime

This repository is a Godot 4 .NET desktop port scaffold for a native classic-platformer-compatible engine. It is not an emulator and does not ship third-party copyrighted assets. A verified local compatible USA ROM is used only as input to an offline importer that writes Godot-readable generated data under `generated/smw/`.

The long-term bar is a 100% physics and gameplay semantics match with the original game. The first slices should therefore be table-driven from SMW reference constants and covered by regression tests, even when a subsystem starts incomplete.

The current first slice covers:

- ROM validation and deterministic extraction for level `105` by default.
- Layer 1/layer 2 raw object streams, decoded placement metadata, screen exits, sprite stream, Map16, palettes, GFX32/GFX33, player PNG atlases, level tileset GFX atlases, Map16 preview atlases, partial level layout previews, and secondary-exit tables.
- Raw SPC upload banks plus a few decoded BRR preview WAVs used only as importer verification artifacts while the full SPC/DSP sequencer is still pending.
- A Godot .NET C# menu and minimal playable scene.
- Runtime audio playback through a C# internal APU probe that streams decoded BRR samples from imported SPC engine/sample banks; the Godot runtime no longer depends on WAV/MP3 playback for these probes.
- A menu audio panel for internal port-1 SFX command probes and BRR sample probes, plus imported music bank visibility while the full SPC/DSP command sequencer is still pending.
- A debug asset overlay showing the imported level GFX, palette-aware Map16 preview, partial level layout preview, and player atlas while the level renderer is still being ported.
- Runtime placement of the generated Yoshi Island 1 Map16 tilemap, with temporary merged collision rectangles derived from imported tile placement sources.
- A runtime Mario sprite composite built from generated GFX32 PNG data and the ROM-derived `PlayerGFXRt` head/body tile pointer tables. This replaces the placeholder hitbox rectangle, but final frame/state correctness still depends on porting the direct OAM assembly tables.
- A C# fixed-step Mario movement prototype using SMW velocity units and jump/gravity constants from the native reference.
- Headless/import/build validation scripts that avoid opening a Wayland window and assert audio, Map16, collision, and player sprite loading.

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

Run a short visible Wayland smoke test:

```bash
tools/check-wayland.sh
```

Build the C# project:

```bash
tools/check-dotnet.sh
```

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
