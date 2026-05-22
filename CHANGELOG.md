# Changelog

## Unreleased

- Added a C# internal APU probe that streams BRR samples from imported SPC banks through `AudioStreamGenerator`, including port-1 jump and two-note command probes.
- Rendered generated Yoshi Island 1 Map16 tile placements at runtime and derived temporary merged collision rectangles from imported tile placement sources.
- Replaced the runtime player placeholder rectangle with an eight-sprite Mario composite sourced from generated GFX32 data and ROM-derived `PlayerGFXRt` head/body tile pointer tables.
- Expanded importer support for the direct pipe target level `0CB` by porting horizontal pipe plus rope mushroom platform object placement into the partial Map16 tilemap generator.
- Moved runtime level asset selection onto manifest-provided per-level tilemap, preview, and tileset paths instead of hardcoded `105` resource names.
- Added runtime world rebuild scaffolding and a headless `0CB` load smoke test so imported transition target layouts can be loaded by Godot.
- Corrected flat-ground walk/run/P-meter horizontal caps and ground friction toward the native `HandlePlayerPhysics` tables, and wired the C# physics smoke test into `tools/check-dotnet.sh`.
- Loaded generated sprite spawn records in the Godot runtime and rendered debug markers for the imported Yoshi Island 1 and `0CB` sprite layers.
- Added per-level sprite GFX VRAM atlas extraction from the vanilla sprite upload table, including generated previews for level `105` sprite GFX `8` and level `0CB` sprite GFX `4`.
- Switched the Godot logical viewport to SNES `256x224`, kept visible Wayland runs at 3x scale, and moved pipe debug triggers onto imported screen-exit pipe tiles.
- Strengthened headless runtime smoke checks for audio, tilemap, collision, and player sprite loading.
