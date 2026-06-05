# Web Runtime

The target browser flow is:

1. Open the web runtime.
2. Select a compatible local ROM file with the browser's native file picker.
3. Validate and import the ROM locally in browser memory.
4. Build a runtime manifest and asset pack without uploading the ROM.
5. Launch gameplay from the generated in-memory assets.

## Current State

`web/` now provides the first source-only browser entrypoint. It validates a selected ROM locally, detects headered dumps, checks the expected size and SHA-1, probes importer table ranges through LoROM addressing, and can download a small browser manifest.

It is not a playable web runtime yet. The current game is a Godot 4 .NET/C# project, and its runtime loads generated assets from `res://generated/smw/`. That architecture works for local Godot playtesting but does not yet give the browser a way to run the C# Godot scene from an uploaded ROM.

Godot's own 4.x documentation also marks C# projects as unavailable for Web export, so this repository should not promise a direct Godot export path until that upstream constraint changes.

## Local Smoke Test

```bash
python3 -m http.server 8765 -d web
```

Open `http://localhost:8765`, choose a local ROM dump, and confirm the page reports local validation status. The file stays in browser memory.

## Importer Migration

`src/SmwAssets` is the new C# library for browser-safe, filesystem-free ROM inspection code. It currently exposes:

- expected ROM size and SHA-1 constants
- copier-header detection
- ROM inspection status
- LoROM address-to-byte-index mapping

The C# CLI tool under `tools/SmwAssetTool` references this library now. Future extraction work should move reusable parser/importer code into `src/SmwAssets` first, then keep CLI and browser hosts thin.

## Roadmap

1. Move manifest generation from `tools/smw_import.py` into `src/SmwAssets`.
2. Add a browser asset-pack format that can be built from a `byte[]`/`ReadOnlyMemory<byte>` without filesystem writes.
3. Replace `res://generated/smw/` assumptions with a runtime asset provider interface.
4. Build either a web-native runtime host or a future Godot web host once Godot 4 .NET can support this project shape.
5. Add source-only CI that checks the web loader, C# asset library, and public documentation without requiring a ROM.
