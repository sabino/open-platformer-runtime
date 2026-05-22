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
- Screen-exit property semantics from `src/smw_0d.cpp` and level-load destination construction from `src/smw_05.cpp`
- Player graphics source data from `GFX32`/`GFX33`, player palettes, and `PlayerGFXRt` tile pointer tables. Current PNG atlases are usable, but state/frame categorization remains pending until the OAM assembly tables are ported directly.

Palette note: raw 8x8 GFX tiles are not enough to show final colors. The level layout preview renders through Map16 tile words because those words carry BG palette bits 10-12, priority, and flip flags. The current importer maps BG palette rows 2-7 to the vanilla foreground palette rows extracted from `0x00B190`.

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
