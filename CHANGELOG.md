# Changelog

## Unreleased

- Added a C# internal APU probe that streams BRR samples from imported SPC banks through `AudioStreamGenerator`, including port-1 jump and two-note command probes.
- Rendered generated Yoshi Island 1 Map16 tile placements at runtime and derived temporary merged collision rectangles from imported tile placement sources.
- Replaced the runtime player placeholder rectangle with an eight-sprite Mario composite sourced from generated GFX32 data and ROM-derived `PlayerGFXRt` head/body tile pointer tables.
- Corrected vanilla screen-exit high-bit routing so Yoshi Island 1 imports direct pipe target `1CB` instead of the unrelated `0CB` room, and expanded the underground target's ceiling ledge/edge objects.
- Moved runtime level asset selection onto manifest-provided per-level tilemap, preview, and tileset paths instead of hardcoded `105` resource names.
- Added runtime world rebuild scaffolding and a headless `1CB` load smoke test so imported transition target layouts can be loaded by Godot.
- Corrected flat-ground walk/run/P-meter horizontal caps and ground friction toward the native `HandlePlayerPhysics` tables, and wired the C# physics smoke test into `tools/check-dotnet.sh`.
- Loaded generated sprite spawn records in the Godot runtime and rendered debug markers for the imported Yoshi Island 1 and transition-target sprite layers.
- Added per-level sprite GFX VRAM atlas extraction from the vanilla sprite upload table, including generated previews for level `105` and level `1CB` sprite GFX `8`.
- Switched the Godot logical viewport to SNES `256x224`, kept visible Wayland runs at 3x scale, and moved pipe debug triggers onto imported screen-exit pipe tiles.
- Added per-level full CGRAM palette extraction for generated level previews, with Lunar Magic custom palette and Super GFX bypass table detection guarded by the documented ROM hooks.
- Corrected vanilla GFX preview extraction to expand SMW 3bpp graphics into 4bpp and populate the full 512-tile BG/sprite VRAM windows used by Map16 tile words.
- Ported the native per-tileset Map16 pointer initialization for level previews, fixing Yoshi Island 1 grassland ledge art that was previously pulled from the wrong linear Map16 offset.
- Added a Sway/Wayland compositor capture wrapper that places Godot on workspace 6, waits for the runtime level log, and captures the exact Godot PID instead of matching unrelated `smw` terminal windows.
- Ported the first big-Mario runtime render path to imported `PlayerGFXRt` OAM tables, including dynamic head/body tile pointer mapping, native displacement/facing data, corrected OBJ row 8 palette layout, and the normal `2,1,0` walking pose cycle.
- Added vertical camera follow/clamping against generated level bounds so lower Yoshi Island 1 terrain remains visible during drops instead of looking like the map ends.
- Added a `--smw-test-spawn=x,y` debug launch argument for reproducible viewport captures at specific world coordinates.
- Documented that the Python importer is an offline extraction tool for now and should eventually move into C# Godot tooling or a C# asset-pipeline project.
- Decoded vanilla Layer 2 RLE background tilemaps into generated preview layers and rendered them behind the runtime Layer 1 Map16 placements.
- Corrected sprite spawn metadata and runtime markers to use the native `yyyyEESY / XXXXssss / id` screen, X, and Y bit layout.
- Corrected imported Level 1/2 object placement to match Lunar Magic Universal's `x=b1 low nibble` and `y=b0 low 5 bits` decode, and aligned more pipe/slope/diagonal ledge Map16 projection rules with its `lmcore` renderer.
- Added runtime slope-surface collision for imported diagonal pipe, diagonal ledge, and steep-slope tile clusters so those objects no longer become rectangular walls.
- Reordered rendered Map16 quadrants from the raw SMW `TL/BL/TR/BR` word layout into `TL/TR/BL/BR`, fixing twisted pipe, ledge, and cave tile graphics.
- Bound screen-exit pipe triggers to the rightmost vertical pipe top in the exit screen, so Yoshi Island 1 enters the intended next pipe instead of the earlier pipe in the same screen.
- Strengthened headless runtime smoke checks for audio, tilemap, collision, and player sprite loading.
- Made collision/sprite/screen-line markers, the debug HUD, and the asset preview panel opt-in through `--smw-debug-overlays`, so normal runs show the game viewport without debug clutter.
- Wired the spin button into the physics step so pressing `X` starts a spin jump instead of only playing the sound probe.
- Replaced the temporary center-follow camera with SMW-style horizontal and vertical threshold scrolling using the native `0x80 +/- 12`, `0x64`, and `0x7C` screen anchors plus native-like vertical scroll caps.
- Kept terrain Map16 cells visually in front of overlapping pipe shaft/body cells so pipes remain masked by grass/ledge tiles while pipe tops still render as entrances.
- Preserved full 8-bit native sprite IDs in imported sprite streams, so level 105 keeps enemies such as `0xAB` Rex and `0xBD` sliding naked blue Koopa instead of aliasing them to low six-bit IDs.
- Tightened temporary runtime slope classification so slope/ledge fill cells stay solid and only diagonal pipe, diagonal ledge edge, and steep-slope surface cells become slope surfaces.
- Expanded the physics reference notes with Hamaluik's measured SMW motion regressions and the native SMW `HandlePlayerPhysics`/slope table targets to port next.
