# Web Runtime

The target browser flow is:

1. Open the web runtime.
2. Select a compatible local ROM file with the browser's native file picker.
3. Validate and import the ROM locally in browser memory.
4. Build a runtime manifest and asset pack without uploading the ROM.
5. Launch gameplay from the generated in-memory assets.

## Current State

`web/` now provides the first source-only browser entrypoint. It validates a selected ROM locally, detects headered dumps, checks the expected size and SHA-1, probes importer table ranges through LoROM addressing, and can download a small browser manifest.

For the experimental playable path, the page runs the Python importer in Pyodide, writes a focused level asset pack in browser memory, loads the custom Godot .NET Web export in an iframe, and streams the generated files to the runtime with `postMessage`. The ROM is not uploaded.

Stock Godot 4.x still does not provide this C# Web export flow. The playable browser path depends on a custom Godot build from `godotengine/godot#106125` and local engine fixes for Web .NET marshalling.

An experimental direct-Godot path now exists in this repository for custom builds based on `godotengine/godot#106125`. See [WEB-DOTNET-PROTOTYPE.md](WEB-DOTNET-PROTOTYPE.md). That path is intentionally separate from the stock Godot 4.6.3 workflow.

## Local Smoke Test

```bash
python3 -m http.server 8765 -d web
```

Open `http://localhost:8765`, choose a local ROM dump, and confirm the page reports local validation status. The file stays in browser memory.

For the playable Godot Web export, build the custom template described in [WEB-DOTNET-PROTOTYPE.md](WEB-DOTNET-PROTOTYPE.md), export to `web-export/out/`, then serve the combined preview:

```bash
tools/serve-web-dotnet-prototype.sh /path/to/prepared/public-root
```

## Importer Migration

`src/SmwAssets` is the new C# library for browser-safe, filesystem-free ROM inspection code. It currently exposes:

- expected ROM size and SHA-1 constants
- copier-header detection
- ROM inspection status
- LoROM address-to-byte-index mapping

The C# CLI tool under `tools/SmwAssetTool` references this library now. Future extraction work should move reusable parser/importer code into `src/SmwAssets` first, then keep CLI and browser hosts thin.

## Roadmap

1. Move more importer logic from `tools/smw_import.py` into `src/SmwAssets`.
2. Replace the Pyodide importer bridge with a native C# browser asset-pack builder when Godot Web .NET support can host it cleanly.
3. Continue replacing `res://generated/smw/` assumptions with a runtime asset provider interface.
4. Harden the custom Godot Web export path and upstream the required marshalling fixes if possible.
5. Add source-only CI that checks the web loader, C# asset library, and public documentation without requiring a ROM.
