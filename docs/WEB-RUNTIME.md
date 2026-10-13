# Web Runtime

The target browser flow is:

1. Open the web runtime.
2. Select a compatible local ROM file with the browser's native file picker.
3. Validate the ROM locally in browser memory.
4. Send the ROM bytes directly into the embedded Godot runtime.
5. Let the shared C# decoder index ROM slots and construct verified layouts in memory.

## Current State

`web/` provides the source-only browser entrypoint. It validates a selected ROM locally, detects headered dumps, checks the expected size and SHA-1, probes importer table ranges through LoROM addressing, can download a small browser manifest, and passes the ROM bytes to the embedded Godot runtime. The ROM is not uploaded.

For the experimental playable path, the page loads the custom Godot .NET Web export in an iframe and calls the runtime bridge with the selected ROM bytes. The runtime uses `SmwRom`, `SmwLevelCatalog` and the same graphics, terrain, entrance and audio code as desktop. It does not generate a disk asset pack. Four terrain layouts are verified; gameplay fidelity is still in progress.

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

`src/SmwAssets` is the shared C# library for ROM inspection and native asset generation. It currently exposes:

- expected ROM size and SHA-1 constants
- copier-header detection
- ROM inspection status
- LoROM address-to-byte-index mapping
- the runtime importer used by desktop, CLI, and the experimental web build

The C# CLI tool references the same library. `inspect-level-catalog` reports slots, counts, aliases, ROM-derived names and verified terrain support. Offline extraction remains available for diagnostics.

## Roadmap

1. Move the remaining parity-only Python importer behavior into `src/SmwAssets`, especially full Map16/object projection.
2. Continue replacing `res://generated/smw/` assumptions with a runtime asset provider interface.
3. Harden the custom Godot Web export path and upstream the required marshalling fixes if possible.
4. Extend existing source-only CI (managed tests, conditional Web compilation and browser-loader handoff tests) with actual custom Web runtime execution. A desktop smoke or mocked JavaScript handoff is not a browser playback pass.
