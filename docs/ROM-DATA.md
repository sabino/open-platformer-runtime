# Shared native ROM data

`src/SmwAssets` is filesystem-free C# used by the CLI and compiled into Godot.
`SmwRom.FromBytes` accepts only the verified vanilla USA image. `LoRomData`
owns a copy of its bytes and reads sequential data across 32 KiB ROM banks,
including mirrored addresses. It does not execute instructions or implement
a CPU memory bus. `Lz2Decoder` bounds input, output, and overlapping references
and returns the exact compressed byte count.

`SmwGraphicsDecoder` resolves all 52 original files from the ROM's pointer
tables, retains decompressed bytes and provenance, and provides tile pixels as
palette indices. Storage formats are explicit:

| GFX IDs | Original storage |
| --- | --- |
| `00..26`, `2C..2E`, `30..31`, `33` | Planar 3bpp, 24 bytes per tile |
| `28..2B`, `2F` | Planar 2bpp, 16 bytes per tile |
| `32` | Planar 4bpp, 32 bytes per tile |
| `27` | Eight packed 3-bit Mode 7 pixels per three bytes |

`SnesGraphics` decodes those formats without palettes or a renderer.
`ExpandTo4Bpp` preserves the pixel indices and zeroes additional planes.
`DecodeLevelUpload` reproduces the original normal 128-tile upload's additional
color plane: selected GFX01/GFX17 tiles, all GFX1E tiles, and all GFX08 tiles
when the active tileset is at least `11`. Special World GFX31 replaces only
GFX01's first 64 tiles; the remaining 64 retain GFX01 data.

These rules follow
[UploadGraphicsFiles_UploadGFXFile and ConvertGFX27IntoNormallFormat](https://github.com/snesrev/smw/blob/eae20c65c58930c8b62c76188d259579ad4130f1/src/smw_00.c#L2694)
and are cross-checked with
[the original upload disassembly](https://github.com/galaxyhaxz/smw-src/blob/30e66e1ae3e8e0e518885dbcd5f85f2909a4969e/project/mw_e10/game.s#L4845)
and [animation conversion](https://github.com/galaxyhaxz/smw-src/blob/30e66e1ae3e8e0e518885dbcd5f85f2909a4969e/project/mw_e10/graphics.s#L5).
Attribution is in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).

Verification is `tools/check-native-graphics.sh`. It compares every raw file,
format, pointer, compressed size, pixel index, planar expansion, and normal
upload against independent ROM decoding. `--oracle-boot` additionally captures
the original upload return and compares all 12,288 bytes of GFX00 and the four
font uploads. All outputs remain under ignored `generated/` storage.

The Python development importer now labels and previews GFX33 as 3bpp and uses
the tileset-dependent GFX08 conversion. It still supplies the current game's
partial level pack.

`SmwPaletteDecoder` ports original level, title, overworld, player, and boss
palette assembly. It preserves entries the original routines do not write.
Background color 1 is `7FDD`; sprite color 1 is `7FFF`. The original routine
return (`$00ACEC`) matches all 512 bytes of the zero-based boot palette mirror.
The importer and existing player palette checks were corrected using that
original execution evidence.

`SmwLevelHeader` reads the five header bytes and layout flags from the ROM.
`SmwLevelData` and `SmwLevelStreams` read bounded vanilla object/sprite streams.
They preserve record order, screen increments/jumps, exit bytes, original
vertical axes and high sprite-coordinate bits. Lunar Magic's additional object
bytes are excluded from this validated vanilla format. The CLI uses the same
decoder rather than maintaining a second parser.

`SmwEntranceDecoder` reads all eight entrance tables and original coordinate
tables. Main/secondary entrances retain original pixel coordinates, actions,
camera offsets, layout and introduction flags. Exit bank selection takes the
current overworld map explicitly. The development selector supplies an initial
bank proxy while the full world state is unfinished; entering another sublevel
does not infer a new world bank from that level's ID. The secondary selector is
a global last-exit-record value in vanilla SMW, and repeated screen records
overwrite earlier exits.

```bash
tools/check-native-level-data.sh --oracle-entry
```

This audits all 512 pointer slots: 23,996 object records across both layers,
3,290 sprite records, 11 vertical slots, 1,024 main/secondary entrance records,
and every byte in the eight entrance tables. The inventory has 26 shared pointer
groups; shared pointers do not establish that a slot is unused. The optional
oracle capture matches the original title-level `0C7` entrance position, camera
offsets and action at the `LoadLevel` return (`$05DAEE`).

`SmwLevelGraphics` reads graphics slots, Map16 masks/pointers, and LC_RLE1 Layer 2
backgrounds. `SmwPlayerGraphicsData` reads 12 player animation/OAM tables.
`SmwLevelVisuals` and `SmwGraphicsImages` provide RGBA texture inputs without
Godot or filesystem dependencies. The current entry explicitly initializes a
development level; original mode transitions and animated palette/tile updates
remain separate work.

With a configured ROM, Godot builds player, sprite, Map16, and background
textures from this decoder and verifies their RGBA8 transfer. These gameplay
paths do not read generated PNGs, sprite graphics binaries, palette JSON,
player graphics JSON, level-stream JSON, or entrance JSON. The experimental browser's older pack bridge remains
available until its native ROM-memory handoff is completed.

```bash
tools/check-native-visuals.sh --oracle-palette
```

This compares every byte of 28 images for levels `105` and `1CB`, including all
four player palettes and eight sprite palettes, then checks Godot's CPU image
transfer and boots each level from an isolated metadata-only pack. It checks the
main/secondary route `105 -> 1CB -> 105` without level-stream, entrance, or
terrain-placement JSON. It also verifies original boot palette memory through
Mesen when requested. All test
artifacts stay ignored. Pillow is used only by this development verifier.

Framebuffer composition, camera/scroll timing, original game modes, dynamic
rendering, full audio, complete level construction, and TAS execution remain
outstanding milestones. Exact texture inputs alone do not establish game parity.
Native object expansion now covers all objects in `0C7`, `105`, `106`, and `1CB`.
Godot constructs their levels without a generated pack or manifest. This is a fresh development
entry: item memory, moon/checkpoint/five-Yoshi-coin collection flags, midpoints,
and palace switches start clear.
Other levels still use generated placement metadata while their routines are
ported and checked; original persistent collection state remains to be wired.

```bash
tools/check-native-terrain.sh --godot
```

This captures the original Map16 buffers immediately before `$058416` returns
from loading objects. The `0C7` title terrain uses unchanged original power-on;
the other cases use declared diagnostic entrance overrides. It compares every
byte of both 14 KiB pages per level: 114,688 bytes total, with zero differences.
The final nonempty counts are 623 tiles for `0C7`, 1,468 for `105`, 1,850 for `106`,
and 585 for `1CB`. With `--godot`, it also verifies each level's buffer hashes
through Godot using an entirely empty pack, then replays the main/secondary
`105 -> 1CB -> 105` route. It rebuilds the current Godot project first.
The native loader preserves object order and the original low-byte overlap
rules; unsupported object types fail explicitly.
This verifies construction, not player collision or full movie playback.
The diagonal ledge routines preserve the original page-byte omission; the
diagonal pipe's final extra tile and the slope's final interior edge are also
part of the verified buffers. Large bushes clear page bytes even for skipped
transparent cells and merge their edges with previous low bytes. Remaining
slope kinds and object families still fail explicitly when called by the
shared decoder.
Title menus, the title demo, original player/contact behavior and framebuffer
composition remain separate gates; exact title terrain does not establish them.
