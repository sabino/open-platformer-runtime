# Reference Notes

Reference inputs used for this port:

- Native repo: `/path/to/native-reference`
- Secondary disassembly reference: `SMWDisX` at `https://github.com/IsoFrieze/SMWDisX`
- Godot .NET executable: `godot4-mono`
- Older Godot source tree available locally: `<local Godot source checkout>`
- Local ROM used for validation: `/path/to/compatible-rom.sfc`
- Expected unheadered SMW USA SHA1: `6B47BB75D16514B6A476AA0C73A683A2A4C18765`

Importer references translated into `tools/smw_import.py`:

- LoROM address conversion and decompression from `assets/util.py`
- Level object length parsing, Map16, palette, graphics, level pointer, and sprite pointer addresses from `assets/compile_resources.py`
- Foreground/background GFX upload order from `kUploadGraphicsFiles_FGAndBGGFXList` in `src/smw_00.cpp`; level `105` uses tileset `7`, which uploads GFX `15`, `1B`, `17`, and `14` into level VRAM order.
- Sprite GFX upload order from `kUploadGraphicsFiles_SpriteGFXList` and `UploadGraphicsFiles` in `src/smw_00.cpp`; level `105` and its direct pipe target `1CB` use sprite GFX setting `8`, which uploads GFX `20`, `13`, `01`, and `00` into the `$6000-$7FFF` sprite VRAM window.
- Screen-exit property semantics from `src/smw_0d.cpp` and level-load destination construction from `src/smw_05.cpp`
- Player graphics source data from `GFX32`/`GFX33`, player palettes, and `PlayerGFXRt` tile pointer/OAM placement tables.
- SPC upload bank addresses from `assets/compile_resources.py`: engine `0x0E8000`, samples `0x0F8000`, level music `0x0EAED6`, overworld music `0x0E98B1`, and credits music `0x03E400`.
- Vanilla secondary entrance tables from `0x05F800`, `0x05FA00`, `0x05FC00`, and `0x05FE00`, including the Yoshi Island 1 return-pipe entry at `0x1CB`.
- BRR preview decoding follows the native `assets/util.py` BRR decoder and the SPC upload block format used by `src/smw_spc_player.cpp`. The vanilla DSP sample directory starts at SPC RAM `$8000`.

Palette note: raw 8x8 GFX tiles are not enough to show final colors. The level layout preview renders through Map16 tile words because those words carry CGRAM row bits 10-12, priority, and flip flags. The importer now writes `palettes/level_*_palette.json` as a full 256-color level CGRAM image. For the verified vanilla ROM, this is assembled from the level header's back-area color, BG palette, FG palette, sprite palette, layer 3, object, berry, player, and animated-color tables. The importer also recognizes the Lunar Magic custom palette hijack at `$00A5C0 -> $0EF570` and the `$0EF600` per-level palette pointer table structurally, but the validation gate still uses the clean SMW USA ROM unless ROM support is explicitly widened later.

GFX note: foreground/background and sprite VRAM atlases are resolved per level. Clean vanilla levels use the native upload tables, while Lunar Magic Super GFX bypass data is recognized through the table pointer at `$0FF7FF` and slot words for `FG3/BG1/FG2/FG1` and `SP4/SP3/SP2/SP1`. Vanilla SMW GFX slots are stored as 3bpp for most normal files, so generated previews expand them into 4bpp and populate 128 tiles per uploaded GFX file, yielding the full 512-tile BG and sprite VRAM windows needed by Map16/tilemap words. The importer can also resolve ExGFX pointer tables structurally, but generated validation assets are still produced from the verified vanilla ROM.

Map16 note: foreground Map16 IDs are not a flat `map16_id * 4` index into `$0D8000`. The importer now ports the native `InitializeMap16Pointers()` table from `src/smw_05.cpp`, including the global-vs-tileset bitmask, tileset base pointers, and the grassland/YI1 override slots for tilesets `0` and `7`. Level `105` therefore resolves tile `0x100` through the YI1 grassland Map16 pointer path before rendering the Godot atlas and runtime tile sprites.

Audio note: the importer preserves the original SPC engine/sample/music banks. Godot runtime playback now uses a C# internal APU probe that parses the SPC upload streams, reads the vanilla instrument table from the engine RAM image, decodes selected BRR entries from the sample directory, and streams them through `AudioStreamGenerator`; the WAV files are only importer verification artifacts. The command probes cover the native SPC port-1 jump and two-note command shapes, and the menu music buttons now drive a small C# pattern sequencer through imported BRR instruments for level/overworld/credits previews. Exact SMW music and SFX still require porting the complete SPC command sequencing, ADSR, pitch sweep, echo, and DSP state.

Runtime collision note: `GameScene` now renders the generated Yoshi Island 1 Map16 placements as individual tiles from the palette-aware Map16 preview atlas and derives temporary merged AABB collision rectangles from the imported placement source labels. Imported diagonal ledge, diagonal pipe, and slope cells emit per-Map16-tile slope segments rather than long connected-component averages, so adjacent slope objects do not create large false walkable lines. Standard slope collision now reads the Map16 ID family for the actual edge tile and emits gradual `4 px`, normal `8 px`, or steep `16 px` surface segments instead of treating every slope cell as a full 45-degree ramp. The importer follows Lunar Magic Universal's current `0x12` standard slope projection, including native slope overwrite tile adjustment, and the newer left diagonal ledge lower-row/first-fill-row dirt placement plus slope/line overwrite flags. This is useful for playable traversal, but it is not a substitute for the final Map16 act-as table, slope semantics, and block interaction routines.

Runtime player graphics note: `GameScene` now draws Mario from the generated GFX32 player atlas through the native `PlayerGFXRt` data path for the first big-Mario pose set. The importer preserves `player_xy_disp_index_index` from `$00DCEC`, `player_xy_disp_index` from `$00DD32`, signed X/Y displacement words from `$00DD4E/$00DE32`, `powerup_tileset_index` from `$00DF16`, `tiles_index` from `$00DF4C`, OAM tile descriptors from `$00DFDA`, head/body tile pointer indices from `$00E00C/$00E0CC`, and tile X-flip flags from `$00E18C`. The runtime maps OAM descriptors `0/1/2/3` through the dynamic upload pointer math used by `PlayerGFXRt_00F636`, so head/body source tiles come from GFX32 offsets instead of treating descriptor bytes as raw source tile IDs. The generated PNG atlas uses a full OBJ palette row 8 layout: fixed colors `0-1`, vanilla object colors `2-5`, and Mario's dynamic palette colors `6-F` from `$00B2C8`. The current runtime player state tracks SMW power-up forms `0..3` and uses a temporary 16px small / 32px big-family collision height while preserving foot position across form changes. Small Mario's OAM is shifted up by 16px at render time because the native small-form OAM data still occupies the lower half of a 32px player anchor. The current temporary animation picker cycles normal big-Mario walking poses `2,1,0`; the exact animation timer, high-speed pose branch, small/fire/cape/Yoshi variants, cape OAM, priority, hide-mask behavior, and native hitbox tables still need the direct SMW pose/OAM port.

Asset-pipeline note: Python is currently used only for the offline development extractor because it is fast to iterate and easy to compare against ROM bytes. The Godot runtime consumes generated assets and does not call Python. The intended end state is a C# importer/editor tool or C# asset-pipeline project so asset extraction can live inside the Godot/.NET codebase.

Pipe target layout note: vanilla Yoshi Island 1 resolves the screen `07` pipe to level `1CB`, not `0CB`. Level `1CB` uses underground tileset `3`; the partial object expander now covers its standard horizontal pipes plus underground ceiling ledges and ceiling edges, so `level_1CB_partial_tilemap.json` contains the target room's main shell geometry. Pipe objects now use Lunar Magic Universal's foreground-preservation rule, so overlapping terrain stays in front of shaft/body cells in both generated previews and runtime tile selection.

Runtime level asset note: `GameScene` loads the current level through `generated/smw/manifest.json` and uses the manifest's per-level `tileset_assets` and `layout_preview` paths. Level `105` is still the startup level, but the renderer no longer assumes `level_105_tileset7_*` filenames internally.

Runtime transition scaffolding note: `GameScene` can rebuild world geometry, collision rectangles, HUD previews, and player spawn from another imported level. The CLI argument `--smw-test-level=1CB` is used by `tools/check-headless.sh` to verify that the imported direct pipe target loads with generated collision rectangles.

Runtime viewport note: the game scene now uses a `256x224` logical viewport, matching the SNES visible level area, while `Main` requests a `768x672` Wayland window for normal visible runs. The camera follows horizontally and vertically, clamped to generated tile bounds, so lower terrain in Yoshi Island 1 remains inspectable during drops. Debug asset previews no longer determine the playable viewport scale.

Runtime debug launch note: `--smw-test-spawn=x,y` places Mario at an explicit world coordinate after the level is loaded, and `--smw-test-powerup=small|big|cape|fire` forces the runtime form for hitbox checks. These are intended for reproducible viewport captures and lower-route inspection while autoplay/TAS input playback is still being ported.

Runtime debug overlay note: `--smw-debug-overlays` adds world-space diagnostics for the current partial port: player hitbox/feet/form label, active `256x224` camera rectangle, merged collision rectangles, slope lines, pipe trigger rectangles, goal tape trigger rectangles, runtime sprite actor hitboxes, imported sprite/object markers, screen boundaries, and coin/block markers inferred from generated Map16 tile sources. These gizmos are debugging aids for comparing importer placement and collision behavior; they are not game objects and should stay disabled for normal playtests.

Runtime pipe note: pipe debug rectangles are rebuilt from imported screen-exit records and the corresponding generated `vertical_pipe_top_left` Map16 placements. For level `105`, this puts the active pipe trigger on the screen `07` pipe instead of the old hardcoded screen `01` marker.

Runtime sprite note: `GameScene` loads generated `sprite_layer.sprites` records and renders their spawn points as debug markers with sprite IDs. The current coordinate decode uses the vanilla screen nibble plus 8-bit x byte and low y nibble, which is enough to inspect Yoshi Island 1's 34 imported sprite records. The first runtime actor layer turns 31 common YI1 enemy records into simple moving/stompable/hurting bodies tied to those imported positions. Exact sprite simulation and OAM rendering still require porting each sprite's native state machine, hitbox table, tile assembly, animation, palette, and despawn/loading logic.

Runtime goal note: sprite `0x7B` is the native goal tape (`Spr07B_GoalTape` in the C++ reference). Yoshi Island 1 imports one such sprite at screen `12`, x `4832`, and `GameScene` now creates a visible tape trigger from that record. Touching it enters a temporary course-clear state and plays the internal credits-preview sequencer; native bonus stars, walkout, score tally, overworld progression, and post-goal power-up logic remain pending.

Runtime capture note: `tools/capture-wayland.sh` launches the Godot .NET binary with `--display-driver wayland`, waits for the exact launched PID to appear in Sway, moves it to workspace `6` by default, floats/resizes it to the intended 3x SNES viewport, waits for the `smw-runtime` level log, and captures that compositor rectangle with `grim`. This avoids the previous false-positive capture of existing terminal windows whose titles contained `smw`.

Sprite GFX note: the importer now writes `spritesets/level_*_spritegfx*_8x8.png` and matching metadata from the same sprite upload table used by the native code. These files are raw sprite VRAM previews, not final enemy frames. Correct Koopa, Yoshi, power-up, and effect rendering still requires porting each sprite's OAM assembly, tile index, tile size, flip, priority, and palette selection rules.

The key vanilla transition invariant is:

```text
destination = current_overworld_map_high_bit | screen_exit_low_byte
secondary = raw_r11 >> 1
```

The importer also preserves the raw `R11` byte and the old property-bit destination projection so Lunar Magic and vanilla differences are not lost. Lunar Magic exits can still use property bits differently, but the verified vanilla path follows the native `LoadLevel` behavior fixed in the C++ reference.

For Yoshi Island 1, the direct pipe route imported from the ROM is:

```text
level 105, screen 07 -> level 1CB, secondary=0, raw_r11=00, exit_low=CB
```

`tools/import-smw.sh` imports direct screen-exit targets by default so level `1CB` is available to Godot even though it is reached through a transition.
