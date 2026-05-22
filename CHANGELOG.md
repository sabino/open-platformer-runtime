# Changelog

## Unreleased

- Added a C# internal APU probe that streams BRR samples from imported SPC banks through `AudioStreamGenerator`, including port-1 jump and two-note command probes.
- Rendered generated Yoshi Island 1 Map16 tile placements at runtime and derived temporary merged collision rectangles from imported tile placement sources.
- Replaced the runtime player placeholder rectangle with an eight-sprite Mario composite sourced from generated GFX32 data and ROM-derived `PlayerGFXRt` head/body tile pointer tables.
- Expanded importer support for the direct pipe target level `0CB` by porting horizontal pipe plus rope mushroom platform object placement into the partial Map16 tilemap generator.
- Moved runtime level asset selection onto manifest-provided per-level tilemap, preview, and tileset paths instead of hardcoded `105` resource names.
- Added runtime world rebuild scaffolding and a headless `0CB` load smoke test so imported transition target layouts can be loaded by Godot.
- Corrected flat-ground walk/run/P-meter horizontal caps and ground friction toward the native `HandlePlayerPhysics` tables, and wired the C# physics smoke test into `tools/check-dotnet.sh`.
- Strengthened headless runtime smoke checks for audio, tilemap, collision, and player sprite loading.
