# Asset Pipeline

The repository is source-only. Generated data is local runtime state and must not be committed.

## Local Generated Output

The importer writes Godot-readable files under:

```text
generated/smw/
```

That directory is ignored by git. Treat everything under it as local output derived from the user's ROM dump.

## Python Importer

`tools/smw_import.py` is the broad offline development extractor for the current asset set. It validates the ROM, extracts the focused runtime data, writes the manifest, and produces inspection previews used by the Godot runtime and tests.

Typical wrapper usage:

```bash
tools/import-smw.sh "/path/to/compatible-rom.sfc"
SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/import-smw.sh --level 106 --clean
SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/import-smw.sh --all-levels --clean
```

Validate importer transition data:

```bash
tools/check-importer.sh
```

## C# Asset Tool

`tools/SmwAssetTool` is the first checked-in C# extraction and verification slice. The long-term target is to keep moving normal project workflows into C# tooling so Python is no longer required for day-to-day runtime work.

Shared, filesystem-free ROM inspection code now lives in `src/SmwAssets`. New importer logic that must run in both CLI and browser contexts should be added there first, then called from host-specific tools.

Useful commands:

```bash
tools/check-native-extractor.sh
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-core "$SMW_ROM_PATH" /tmp/smw-native-core
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-levels "$SMW_ROM_PATH" /tmp/smw-native-levels
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-audio-previews "$SMW_ROM_PATH" /tmp/smw-native-audio
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-player-metadata "$SMW_ROM_PATH" /tmp/smw-native-player
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-entrance-tables "$SMW_ROM_PATH" /tmp/smw-native-entrances
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-palettes "$SMW_ROM_PATH" /tmp/smw-native-palettes
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- inspect-rom "$SMW_ROM_PATH"
```

## Browser Loader

`web/` contains the GitHub Pages browser loader. It uses the browser's native file picker, validates the selected ROM locally, probes importer table ranges through LoROM addressing, and can download a small browser manifest.

The loader does not yet produce the full Godot asset pack or launch gameplay. That requires moving the manifest/importer path into `src/SmwAssets` and adding a runtime asset provider that is not tied to `res://generated/smw/`.

## Asset Boundary

Do not commit:

- ROM files
- `generated/`
- extracted PNG/WAV/BIN/JSON asset packs
- saves, SRAM, emulator states, TAS downloads, screenshots, or videos derived from proprietary assets
- absolute machine-local paths

See [RELEASE-HYGIENE.md](RELEASE-HYGIENE.md) before publishing branches or tags.
