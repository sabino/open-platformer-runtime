# Reference Notes

Reference inputs used for this port:

- Native repo: `/path/to/native-reference`
- Godot .NET executable: `godot4-mono`
- Older Godot source tree available locally: `<local Godot source checkout>`
- Local ROM used for validation: `/path/to/compatible-rom.sfc`
- Expected unheadered SMW USA SHA1: `6B47BB75D16514B6A476AA0C73A683A2A4C18765`

Importer references translated into `tools/smw_import.py`:

- LoROM address conversion and decompression from `assets/util.py`
- Level object length parsing, Map16, palette, graphics, level pointer, and sprite pointer addresses from `assets/compile_resources.py`
- Foreground/background GFX upload order from `kUploadGraphicsFiles_FGAndBGGFXList` in `src/smw_00.cpp`; level `105` uses tileset `7`, which uploads GFX `15`, `1B`, `17`, and `14` into level VRAM order.
- Sprite GFX upload order from `kUploadGraphicsFiles_SpriteGFXList` and `UploadGraphicsFiles` in `src/smw_00.cpp`; level `105` uses sprite GFX setting `8`, which uploads GFX `20`, `13`, `01`, and `00` into the `$6000-$7FFF` sprite VRAM window, while direct pipe target `0CB` uses setting `4`, which uploads `06`, `13`, `01`, and `00`.
- Screen-exit property semantics from `src/smw_0d.cpp` and level-load destination construction from `src/smw_05.cpp`
- Player graphics source data from `GFX32`/`GFX33`, player palettes, and `PlayerGFXRt` tile pointer tables. Current PNG atlases are usable, but state/frame categorization remains pending until the OAM assembly tables are ported directly.
- SPC upload bank addresses from `assets/compile_resources.py`: engine `0x0E8000`, samples `0x0F8000`, level music `0x0EAED6`, overworld music `0x0E98B1`, and credits music `0x03E400`.
- BRR preview decoding follows the native `assets/util.py` BRR decoder and the SPC upload block format used by `src/smw_spc_player.cpp`. The vanilla DSP sample directory starts at SPC RAM `$8000`.

Palette note: raw 8x8 GFX tiles are not enough to show final colors. The level layout preview renders through Map16 tile words because those words carry BG palette bits 10-12, priority, and flip flags. The current importer maps BG palette rows 2-7 to the vanilla foreground palette rows extracted from `0x00B190`.

Audio note: the importer preserves the original SPC engine/sample/music banks. Godot runtime playback now uses a C# internal APU probe that parses the SPC upload streams, reads the vanilla instrument table from the engine RAM image, decodes selected BRR entries from the sample directory, and streams them through `AudioStreamGenerator`; the WAV files are only importer verification artifacts. The current command probes cover the native SPC port-1 jump and two-note command shapes, but exact SMW music and SFX still require porting the complete SPC command sequencing, ADSR, pitch sweep, and DSP state.

Runtime collision note: `GameScene` now renders the generated Yoshi Island 1 Map16 placements as individual tiles from the palette-aware Map16 preview atlas and derives temporary merged AABB collision rectangles from the imported placement source labels. This is useful for playable traversal, but it is not a substitute for the final Map16 act-as table, slope semantics, and block interaction routines.

Runtime player graphics note: `GameScene` now draws Mario from the generated GFX32 player atlas by composing the imported `PlayerGFXRt` head/body tile pointer entries into eight 8x8 sprites. The pose picker currently maps idle, walk, jump, and spin-jump to early pointer-table entries so the runtime uses original graphics instead of a placeholder rectangle. Exact animation states, cape/fire/small variants, tile flips, tile size selection, and OAM priority still need the direct SMW OAM assembly port.

Pipe target layout note: level `0CB` uses rope tileset `8`. The partial object expander now covers standard horizontal pipes and the rope mushroom top/column objects used there, so the generated `level_0CB_partial_tilemap.json` contains a usable platform layout instead of only the goal tape and Yoshi coin markers.

Runtime level asset note: `GameScene` loads the current level through `generated/smw/manifest.json` and uses the manifest's per-level `tileset_assets` and `layout_preview` paths. Level `105` is still the startup level, but the renderer no longer assumes `level_105_tileset7_*` filenames internally.

Runtime transition scaffolding note: `GameScene` can rebuild world geometry, collision rectangles, HUD previews, and player spawn from another imported level. The CLI argument `--smw-test-level=0CB` is used by `tools/check-headless.sh` to verify that the imported direct pipe target loads with 131 Map16 placements and generated collision rectangles.

Runtime viewport note: the game scene now uses a `256x224` logical viewport, matching the SNES visible level area, while `Main` requests a `768x672` Wayland window for normal visible runs. Debug asset previews no longer determine the playable viewport scale.

Runtime pipe note: pipe debug rectangles are rebuilt from imported screen-exit records and the corresponding generated `vertical_pipe_top_left` Map16 placements. For level `105`, this puts the active pipe trigger on the screen `07` pipe instead of the old hardcoded screen `01` marker.

Runtime sprite note: `GameScene` loads generated `sprite_layer.sprites` records and renders their spawn points as debug markers with sprite IDs. The current coordinate decode uses the vanilla screen nibble plus 8-bit x byte and low y nibble, which is enough to inspect Yoshi Island 1's 34 imported sprite records while full sprite simulation and exact OAM rendering remain pending.

Sprite GFX note: the importer now writes `spritesets/level_*_spritegfx*_8x8.png` and matching metadata from the same sprite upload table used by the native code. These files are raw sprite VRAM previews, not final enemy frames. Correct Koopa, Yoshi, power-up, and effect rendering still requires porting each sprite's OAM assembly, tile index, tile size, flip, priority, and palette selection rules.

The key transition invariant is:

```text
destination = ((screen_exit_properties & 1) << 8) | screen_exit_low_byte
secondary = (screen_exit_properties >> 1) & 1
```

The importer also preserves the raw `R11` byte so Lunar Magic and vanilla differences are not lost.

Known current native-reference caveat: the C++ repo is being actively debugged for a pipe/bonus misroute. The Godot importer should treat ROM data as the source of truth for transition routing until that native bug is fixed. For Yoshi Island 1, the direct pipe route imported from the ROM is:

```text
level 105, screen 07 -> level 0CB, secondary=0, raw_r11=00
```

`tools/import-smw.sh` imports direct screen-exit targets by default so level `0CB` is available to Godot even though it is reached through a transition.
