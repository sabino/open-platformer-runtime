# Open Platformer Runtime

This repository is a Godot 4 .NET desktop port scaffold for a native classic-platformer-compatible engine. It is not an emulator and does not ship third-party copyrighted assets. A verified local compatible USA ROM is used only as input to an offline importer that writes Godot-readable generated data under `generated/smw/`.

The long-term bar is a 100% physics and gameplay semantics match with the original game. The first slices should therefore be table-driven from SMW reference constants and covered by regression tests, even when a subsystem starts incomplete.

The current first slice covers:

- ROM validation and deterministic extraction for level `105` by default.
- Layer 1/layer 2 raw object streams, decoded placement metadata, screen exits, sprite stream, Map16, palettes, GFX32/GFX33, player PNG atlases, level tileset GFX atlases, Map16 preview atlases, and secondary-exit tables.
- A Godot .NET C# menu and minimal playable scene.
- A debug asset overlay showing the imported level GFX, Map16 preview, and player atlas while the level renderer is still being ported.
- A C# fixed-step Mario movement prototype using SMW velocity units and jump/gravity constants from the native reference.
- Headless/import/build validation scripts that avoid opening a Wayland window.

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
