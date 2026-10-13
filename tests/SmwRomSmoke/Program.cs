using System;
using System.IO;
using System.Linq;
using OpenPlatformerRuntime.SmwAssets;

var bytes = new byte[0x10000];
bytes[0x7FFF] = 0x78;
bytes[0x8000] = 0x56;
bytes[0x8001] = 0x34;
var data = new LoRomData(bytes);
bytes[0x7FFF] = 0;
Check(data.ReadByte(0x00FFFF) == 0x78, "ROM image must own its bytes");
Check(data.ReadUInt16(0x00FFFF) == 0x5678, "16-bit data read must cross a ROM bank");
Check(data.ReadUInt24(0x80FFFF) == 0x345678, "24-bit read must cross a mirrored ROM bank");
Check(LoRomData.NextAddress(0x00FFFF) == 0x018000, "sequential cursor must skip the lower bank half");
Check(LoRomData.NextAddress(0x80FFFF) == 0x818000, "mirrored cursor must preserve the bank mirror");
Throws<ArgumentOutOfRangeException>(() => data.ReadByte(0x010000), "unmapped data address accepted");
Throws<ArgumentOutOfRangeException>(() => data.ReadByte(0x1008000), "address above 24 bits accepted");
Throws<ArgumentOutOfRangeException>(() => data.ReadByte(-1), "negative address accepted");
Throws<ArgumentOutOfRangeException>(() => data.ReadUInt16(0x01FFFF), "read beyond final ROM bank accepted");
Throws<ArgumentOutOfRangeException>(() => data.ReadBytes(0x008000, -1), "negative range accepted");
Throws<ArgumentOutOfRangeException>(() => LoRomData.NextAddress(0xFFFFFF), "cursor beyond 24 bits accepted");
Throws<ArgumentException>(() => SmwRom.FromBytes(new byte[0x80000]), "wrong ROM hash accepted");
Throws<ArgumentException>(() => SmwRom.FromSelectedBytes(new byte[0x80200]), "wrong headered ROM hash accepted");
Throws<ArgumentException>(() => SmwRom.FromSelectedBytes(new byte[512]), "header without cartridge accepted");
Throws<ArgumentException>(() => new LoRomData(new byte[1]), "incomplete ROM bank accepted");

var overlap = Lz2Decoder.Decode(new byte[] { 0x01, 0x41, 0x42, 0x85, 0, 0, 0xFF });
Check(overlap.Bytes.SequenceEqual("ABABABAB"u8.ToArray()), "overlapping LC_LZ2 copy");
Check(overlap.CompressedLength == 7, "exact compressed byte count");
var commands = Lz2Decoder.Decode(new byte[] { 0x22, 0xAA, 0x43, 0x12, 0x34, 0x63, 0xFE, 0xA1, 0, 0, 0xFF });
Check(commands.Bytes.SequenceEqual(new byte[] { 0xAA, 0xAA, 0xAA, 0x12, 0x34, 0x12, 0x34, 0xFE, 0xFF, 0, 1, 0xAA, 0xAA }), "LC_LZ2 fill/word/increment/copy aliases");
var extended = new byte[] { 0xE0, 32 }.Concat(Enumerable.Range(0, 33).Select(i => (byte)i)).Append((byte)0xFF).ToArray();
Check(Lz2Decoder.Decode(extended).Bytes.SequenceEqual(Enumerable.Range(0, 33).Select(i => (byte)i)), "extended LC_LZ2 length");
Throws<InvalidDataException>(() => Lz2Decoder.Decode(new byte[] { 0x80, 0, 0, 0xFF }), "invalid repeat offset accepted");
Throws<InvalidDataException>(() => Lz2Decoder.Decode(new byte[] { 0, 1 }), "missing compressed terminator accepted");
Throws<InvalidDataException>(() => Lz2Decoder.Decode(new byte[] { 0x22, 0, 0xFF }, 2), "decompressed output limit ignored");

var tile2 = new byte[16];
tile2[0] = 0x55;
tile2[1] = 0x33;
Check(SnesGraphics.DecodePlanar(tile2, 2).Take(8).SequenceEqual(new byte[] { 0, 1, 2, 3, 0, 1, 2, 3 }), "2bpp left-to-right colors");
var tile3 = new byte[24];
tile2.CopyTo(tile3, 0);
tile3[16] = 0x0F;
Check(SnesGraphics.DecodePlanar(tile3, 3).Take(8).SequenceEqual(Enumerable.Range(0, 8).Select(i => (byte)i)), "3bpp third plane layout");
var tile4 = SnesGraphics.ExpandTo4Bpp(tile3, 3);
Check(SnesGraphics.DecodePlanar(tile4, 4).SequenceEqual(SnesGraphics.DecodePlanar(tile3, 3)), "3bpp expansion must retain every pixel");
tile4[17] = 0xF0;
Check(SnesGraphics.DecodePlanar(tile4, 4).Take(8).SequenceEqual(new byte[] { 8, 9, 10, 11, 4, 5, 6, 7 }), "4bpp fourth plane layout");
Check(SnesGraphics.DecodePlanar(SnesGraphics.ExpandTo4Bpp(tile2, 2), 4).SequenceEqual(SnesGraphics.DecodePlanar(tile2, 2)), "2bpp expansion must retain every pixel");
var packed = new byte[24];
packed[0] = 0x05;
packed[1] = 0x39;
packed[2] = 0x77;
Check(SnesGraphics.DecodePacked3Bpp(packed).Take(8).SequenceEqual(Enumerable.Range(0, 8).Select(i => (byte)i)), "Mode 7 packed pixel ordering");
Throws<ArgumentException>(() => SnesGraphics.DecodePlanar(new byte[17], 2), "partial tile accepted");
Throws<ArgumentOutOfRangeException>(() => SnesGraphics.DecodePlanar(tile2, 8), "unsupported planar depth accepted");

var rle = Rle1Decoder.Decode(new byte[] { 1, 65, 66, 0x82, 67, 0xFF, 0xFF });
Check(rle.Bytes.SequenceEqual("ABCCC"u8.ToArray()) && rle.CompressedLength == 7, "LC_RLE1 literal/repeat and exact terminator");
Check(Rle1Decoder.Decode(new byte[] { 0xFF, 0, 0xFF, 0xFF }).Bytes.Length == 128, "FF is a run command except at an FF FF terminator");
Throws<InvalidDataException>(() => Rle1Decoder.Decode(new byte[] { 1, 65 }), "truncated LC_RLE1 literal accepted");
Throws<InvalidDataException>(() => Rle1Decoder.Decode(new byte[] { 0x82, 0, 0xFF, 0xFF }, 2), "LC_RLE1 output limit ignored");

var atlas = SmwGraphicsImages.CreateAtlas(SnesGraphics.DecodePlanar(tile2, 2), new ushort[] { 0, 0x001F, 0x03E0, 0x7C00, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 1);
Check(atlas.Width == 8 && atlas.Height == 8, "single-tile atlas dimensions");
Check(atlas.Bytes.AsSpan(0, 16).SequenceEqual(new byte[] { 0, 0, 0, 0, 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255 }), "atlas colors and transparent index zero");
Check(SmwPaletteDecoder.ToRgb(0x7FFF) == ((byte)255, (byte)255, (byte)255), "full white conversion");

var objectBytes = new byte[] { 0, 0, 0, 0, 0, 0x82, 0x13, 0, 6, 0, 1, 0x12, 0x14, 0x55, 7, 2, 0, 0xCB, 0xFF };
var horizontal = SmwLevelStreams.ParseObjects(objectBytes, 0);
Check(horizontal.Objects[0].Placement.XTile == 19 && horizontal.Objects[0].Placement.YTile == 2, "horizontal object screen increment");
Check(horizontal.Objects[2].Placement.Screen == 6, "screen jump controls the following object");
Check(horizontal.Exits[0].ResolveId(false) == 0xCB && horizontal.Exits[0].ResolveId(true) == 0x1CB && horizontal.Exits[0].Secondary, "exit bank comes from declared overworld state");
var vertical = SmwLevelStreams.ParseObjects(objectBytes, 1);
Check(vertical.Objects[2].Placement.XTile == 20 && vertical.Objects[2].Placement.YTile == 98, "vertical object swaps axes and preserves the second half-screen");
var vanilla22 = SmwLevelStreams.ParseObjects(new byte[] { 0, 0, 0, 0, 0, 0x40, 0x20, 0, 0, 0x10, 0, 0xFF }, 0);
Check(vanilla22.Objects.Count == 2 && vanilla22.Objects[0].Id == 0x22 && vanilla22.Objects[1].Id == 1, "vanilla stream must not consume Lunar Magic extra bytes");
Throws<InvalidDataException>(() => SmwLevelStreams.ParseObjects(new byte[] { 0, 0, 0, 0, 0, 0, 0 }, 0), "truncated object accepted");
Throws<InvalidDataException>(() => SmwLevelStreams.ParseObjects(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, 0), "truncated exit accepted");
Throws<InvalidDataException>(() => SmwLevelStreams.ParseObjects(new byte[] { 0, 0, 0, 0, 0, 0, 0x10, 0 }, 0), "unterminated object stream accepted");
var spriteBytes = new byte[] { 0xC0, 0x15, 0xA3, 0x9F, 0xFF };
var spriteHorizontal = SmwLevelStreams.ParseSprites(spriteBytes, false);
var spriteVertical = SmwLevelStreams.ParseSprites(spriteBytes, true);
Check(spriteHorizontal.Sprites[0].X == 0x3A0 && spriteHorizontal.Sprites[0].Y == 0x510, "vanilla sprite preserves high coordinate bits");
Check(spriteVertical.Sprites[0].X == 0x510 && spriteVertical.Sprites[0].Y == 0x3A0, "vertical sprite exchanges world axes");
Check(spriteHorizontal.Buoyancy == 0xC0 && spriteHorizontal.MemorySetting == 0, "sprite header fields");
Throws<InvalidDataException>(() => SmwLevelStreams.ParseSprites(new byte[] { 0, 1, 2 }, false), "truncated sprite accepted");

var romPath = Environment.GetEnvironmentVariable("SMW_ROM_PATH");
if (!string.IsNullOrWhiteSpace(romPath))
{
    var rom = SmwRom.FromBytes(File.ReadAllBytes(romPath));
    Check(rom.Data.Length == 0x80000, "validated ROM length");
    Check(rom.Inspection.Sha1 == SmwRomInspector.ExpectedUnheaderedSha1, "validated ROM hash");
    var original = File.ReadAllBytes(romPath);
    Check(rom.Data.ReadByte(0x008000) == original[0], "first ROM bank data");
    Check(rom.Data.ReadByte(0x0FFFFF) == original[^1], "final ROM bank data");
    Check(rom.Data.ReadByte(0x808000) == original[0], "ROM bank mirror");
    var headered = new byte[original.Length + 512];
    original.CopyTo(headered, 512);
    Throws<ArgumentException>(() => SmwRom.FromBytes(headered), "copier header accepted as native ROM");
    var selected = SmwRom.FromSelectedBytes(headered);
    Check(selected.Inspection.Sha1 == rom.Inspection.Sha1 && selected.Data.ReadRange(0x008000, original.Length).SequenceEqual(original), "copier header must strip without changing canonical ROM data");
    headered[512] ^= 1;
    Throws<ArgumentException>(() => SmwRom.FromSelectedBytes(headered), "mutated headered cartridge accepted");
    var catalog = new SmwLevelCatalog(rom);
    Check(catalog.Entries.Count == 512 && catalog.VerifiedTerrainCount == 4, "native catalog must distinguish all slots from verified layouts");
    Check(catalog[0x105].Name == "YOSHI'S ISLAND 1" && catalog[0x106].Name == "YOSHI'S ISLAND 2", "native catalog must read original name tables");
    Check(catalog[0x139].Name == "" && catalog[0x1CB].Name == "", "catalog must not read code beyond the vanilla name table as level titles");
    Check(catalog[0x14].Name == "YELLOW SWITCH PALACE", "native catalog must trim to 19 tiles before expanding compound glyphs");
    Check(catalog[0x105].ObjectCount == 92 && catalog[0x105].SpriteCount == 34 && catalog[0x105].ExitCount == 1, "native catalog must preserve the first course's records");
    Check(catalog[0x1CB].TerrainVerified && !catalog[0x107].TerrainVerified, "unverified layouts cannot be offered as native supported courses");
    foreach (var entry in catalog.Entries)
    {
        Check(entry.LayoutAliases.Contains(entry.Id), "a layout alias group must contain its slot");
        Check(entry.LayoutAliases.All(id => catalog[id].Layer1Address == entry.Layer1Address), "layout alias group must preserve the actual pointer");
    }
    Throws<ArgumentOutOfRangeException>(() => _ = catalog[512], "catalog accepted out-of-range slot");
    var graphics = new SmwGraphicsDecoder(rom);
    Check(graphics.DecodeAll().Count == 52, "all vanilla graphics files");
    Check(graphics.Decode(0x28).Format == SmwGraphicsFormat.Planar2Bpp && graphics.Decode(0x28).TileCount == 128, "character set format");
    Check(graphics.Decode(0x2F).Format == SmwGraphicsFormat.Planar2Bpp && graphics.Decode(0x2F).TileCount == 64, "large characters format");
    Check(graphics.Decode(0x27).Format == SmwGraphicsFormat.Packed3Bpp && graphics.Decode(0x27).DecodePixels().Length == 8192, "Mode 7 graphics format");
    Check(graphics.Decode(0x33).Format == SmwGraphicsFormat.Planar3Bpp && graphics.Decode(0x33).ExpandTo4Bpp().Length == 0x3000, "animation graphics are 3bpp, not 4bpp");
    Check(graphics.DecodeLevelUpload(1, 0, specialWorld: true).Length == 0x1000, "Special World GFX01 retains a complete upload");
    Throws<ArgumentOutOfRangeException>(() => graphics.Decode(-1), "negative graphics ID accepted");
    Throws<ArgumentOutOfRangeException>(() => graphics.Decode(52), "non-vanilla graphics ID accepted");
    Throws<ArgumentException>(() => graphics.DecodeLevelUpload(0x28, 0), "2bpp file accepted as normal level upload");
    Throws<InvalidOperationException>(() => graphics.Decode(0x27).ExpandTo4Bpp(), "Mode 7 data accepted as planar data");
    var palettes = new SmwPaletteDecoder(rom);
    var previousPalette = Enumerable.Repeat((ushort)0x1234, 256).ToArray();
    palettes.ApplyLevelBase(previousPalette, default);
    Check(previousPalette[0] == 0x1234 && previousPalette[16] == 0x1234, "palette assembly must preserve untouched entries");
    Check(previousPalette[1] == 0x7FDD && previousPalette[129] == 0x7FFF, "BG and OBJ use different white words");
    Check(palettes.CreatePlayerRow(0)[1] == 0x7FFF, "player uses original OBJ white");
    var level = new SmwLevelGraphics(rom, 0x105);
    Check(level.Header.Tileset == 7 && level.Header.SpriteGraphics == 8 && level.Header.Screens == 20, "ROM level 105 header");
    Check(level.ReadForegroundMap16().Length == 2048 && level.ReadBackgroundWindow().Length == 0x4000, "native Map16 and graphics windows");
    Check(level.ReadBackgroundTiles()?.Bytes.Length == 864, "native Layer 2 background");
    Throws<ArgumentOutOfRangeException>(() => SmwLevelHeader.Read(rom, 512), "out-of-range level header accepted");
    Throws<ArgumentOutOfRangeException>(() => palettes.CreatePlayerRow(4), "out-of-range player palette accepted");
    var streams = new SmwLevelData(rom, 0x105);
    Check(streams.Layer1.Objects.Count == 92 && streams.Sprites.Sprites.Count == 34, "ROM level 105 stream inventory");
    Check(streams.Layer1.Exits.Single().ResolveId(true) == 0x1CB, "ROM level 105 exit low byte and bank selection");
    var entrances = new SmwEntranceDecoder(rom);
    var mainPipeRoom = entrances.Main(0x1CB);
    Check(mainPipeRoom.X == 16 && mainPipeRoom.Y == 304 && mainPipeRoom.Action == 4, "ROM main pipe-room entrance");
    var returnPipe = entrances.Secondary(0x1CB);
    Check(returnPipe.Level == 0x105 && returnPipe.X == 0x810 && returnPipe.Y == 0x130 && returnPipe.Action == 6, "ROM secondary return pipe entrance");
    Throws<ArgumentOutOfRangeException>(() => entrances.Main(512), "out-of-range main entrance accepted");
    var terrain = new SmwMap16Terrain(rom, new SmwLevelData(rom, 0x1CB));
    // Complete original buffers captured before the $058416 loader return,
    // with zero item memory and collection flags, not preview geometry.
    Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(terrain.LowBytes)) == "F4F07E96D0627BF5140C89855E3760BAA860F228D31CEC55D27D6A2838A32C50", "1CB original low Map16 buffer");
    Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(terrain.HighBytes)) == "4FC9BA6E15E73D2963C3B3D0674E223227920B3C9C57C60ABA071CB44898901E", "1CB original high Map16 buffer");
    var ground = new SmwMap16Terrain(rom, streams);
    Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ground.LowBytes)) == "7ABA94DEAB748E9676B51F309344B3237F5B7EF311D653A56A0661A7BE39F4CD", "105 original low Map16 buffer");
    Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ground.HighBytes)) == "3DFE79DF5AE5EEF9A3CC093E6D727706F2388E14DA2CC7E8F41430B893BE68BF", "105 original high Map16 buffer");
    foreach (var (id, low, high) in new[]
    {
        (0x0C7, "AD1A113E84B365CEB38EEB78DC757F39295CDE21E1A3DD2F9B3822D2BABA41CC", "3DA683BE743F332804B8158C6455DF6E198A12625E2BB5CF6C9D1CD3D9224C70"),
        (0x106, "118966934650E52F1C612FF443B10C64ABD4D9B1A5CD5457924513B759F4033B", "958DF50CB39793E5C92CD0F258A11E44E8C9AC63E08FE2C1A2ABBFF66959E5C6"),
    })
    {
        var expanded = new SmwMap16Terrain(rom, new SmwLevelData(rom, id));
        Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(expanded.LowBytes)) == low, $"{id:X3} original low Map16 buffer");
        Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(expanded.HighBytes)) == high, $"{id:X3} original high Map16 buffer");
    }
}
Console.WriteLine("smw ROM data smoke: ok");

static void Check(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

static void Throws<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException(message);
}
