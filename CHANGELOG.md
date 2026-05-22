# Changelog

## Unreleased

- Added a C# internal APU probe that streams BRR samples from imported SPC banks through `AudioStreamGenerator`, including port-1 jump and two-note command probes.
- Rendered generated Yoshi Island 1 Map16 tile placements at runtime and derived temporary merged collision rectangles from imported tile placement sources.
- Replaced the runtime player placeholder rectangle with an eight-sprite Mario composite sourced from generated GFX32 data and ROM-derived `PlayerGFXRt` head/body tile pointer tables.
- Expanded importer support for the direct pipe target level `0CB` by porting horizontal pipe plus rope mushroom platform object placement into the partial Map16 tilemap generator.
- Strengthened headless runtime smoke checks for audio, tilemap, collision, and player sprite loading.
