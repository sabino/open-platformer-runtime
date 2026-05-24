using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class Program
{
    private const string SmwUsSha1 = "6B47BB75D16514B6A476AA0C73A683A2A4C18765";
    private const int SmwUsRomSize = 0x80000;
    private static readonly int[] LevelVerticalTable =
    [
        0x00, 0x00, 0x80, 0x01, 0x81, 0x02, 0x82, 0x03,
        0x83, 0x00, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80,
    ];

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return 2;
            }

            return args[0] switch
            {
                "extract-core" when args.Length == 3 => ExtractCore(args[1], args[2]),
                "verify-core" when args.Length == 3 => VerifyCore(args[1], args[2]),
                "extract-levels" when args.Length == 3 => ExtractLevels(args[1], args[2]),
                "verify-levels" when args.Length == 3 => VerifyLevels(args[1], args[2]),
                _ => UsageError(args),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"smw-asset-tool: failed: {ex.Message}");
            return 1;
        }
    }

    private static int ExtractCore(string romPath, string outDir)
    {
        var rom = Rom.Load(romPath);
        var outputRoot = Path.GetFullPath(outDir);
        Directory.CreateDirectory(outputRoot);

        var manifest = BuildCoreManifest(rom);
        foreach (var asset in manifest.Assets)
        {
            WriteBinary(Path.Combine(outputRoot, asset.File), asset.Data);
        }

        var manifestPath = Path.Combine(outputRoot, "native_core_manifest.json");
        WriteJson(manifestPath, manifest.ToSerializable());
        Console.WriteLine($"smw-asset-tool: extracted core assets to {outputRoot}");
        return 0;
    }

    private static int VerifyCore(string romPath, string generatedDir)
    {
        var rom = Rom.Load(romPath);
        var generatedRoot = Path.GetFullPath(generatedDir);
        var manifest = BuildCoreManifest(rom);
        foreach (var asset in manifest.Assets)
        {
            var path = Path.Combine(generatedRoot, asset.File);
            Check(File.Exists(path), $"generated asset missing: {asset.File}");
            var actual = File.ReadAllBytes(path);
            Check(actual.SequenceEqual(asset.Data), $"generated asset differs from C# ROM extraction: {asset.File}");
        }

        Console.WriteLine($"smw-asset-tool: verified {manifest.Assets.Count} core generated assets from {Path.GetFileName(rom.Path)}");
        return 0;
    }

    private static int ExtractLevels(string romPath, string outDir)
    {
        var rom = Rom.Load(romPath);
        var outputRoot = Path.GetFullPath(outDir);
        Directory.CreateDirectory(outputRoot);
        var summaries = BuildLevelSummaries(rom, [0x105, 0x1CB]);
        WriteJson(Path.Combine(outputRoot, "native_level_summary.json"), new
        {
            schema_version = 1,
            source_rom_sha1 = rom.Sha1,
            levels = summaries.ToDictionary(pair => pair.Key, pair => pair.Value.ToSerializable()),
        });
        Console.WriteLine($"smw-asset-tool: extracted native level summaries to {outputRoot}");
        return 0;
    }

    private static int VerifyLevels(string romPath, string generatedDir)
    {
        var rom = Rom.Load(romPath);
        var generatedRoot = Path.GetFullPath(generatedDir);
        var summaries = BuildLevelSummaries(rom, [0x105, 0x1CB]);
        foreach (var (levelKey, summary) in summaries)
        {
            VerifyGeneratedLevel(generatedRoot, levelKey, summary);
        }

        Console.WriteLine($"smw-asset-tool: verified native level metadata for {string.Join(",", summaries.Keys)}");
        return 0;
    }

    private static CoreManifest BuildCoreManifest(Rom rom)
    {
        var assets = new List<CoreAsset>();
        AddPlayerGfx(rom, assets, "gfx32", 0x00B8D8);
        AddPlayerGfx(rom, assets, "gfx33", 0x00B88B);
        AddAudioBanks(rom, assets);
        AddGlobalMap16(rom, assets);

        return new CoreManifest(
            SchemaVersion: 1,
            SourceRomSha1: rom.Sha1,
            SourceRomSize: rom.Data.Length,
            Assets: assets);
    }

    private static void AddPlayerGfx(Rom rom, List<CoreAsset> assets, string name, int pointerAddress)
    {
        var sourceAddress = 0x080000 | rom.GetWord(pointerAddress);
        var (data, compressedLength) = SmwDecompress(rom, sourceAddress);
        Check(data.Length % 32 == 0, $"{name} decompressed to non-4bpp length {data.Length}");
        assets.Add(new CoreAsset(
            File: $"gfx/{name}.bin",
            Kind: "player_gfx",
            SourceAddress: sourceAddress,
            Format: "snes_4bpp_planar",
            Data: data,
            Extra: new Dictionary<string, object>
            {
                ["compressed_length"] = compressedLength,
                ["decompressed_length"] = data.Length,
                ["tile_count"] = data.Length / 32,
            }));
    }

    private static void AddAudioBanks(Rom rom, List<CoreAsset> assets)
    {
        var banks = new[]
        {
            new AudioBank("spc_engine", 0x0E8000, 6321, new byte[] { 0x00, 0x00 }),
            new AudioBank("spc_samples", 0x0F8000, 28538, Array.Empty<byte>()),
            new AudioBank("spc_level_music_bank", 0x0EAED6, 16899, Array.Empty<byte>()),
            new AudioBank("spc_overworld_music_bank", 0x0E98B1, 5667, Array.Empty<byte>()),
            new AudioBank("spc_credits_music_bank", 0x03E400, 6624, Array.Empty<byte>()),
        };

        foreach (var bank in banks)
        {
            var data = rom.GetBytes(bank.SourceAddress, bank.Length).Concat(bank.Suffix).ToArray();
            assets.Add(new CoreAsset(
                File: $"audio/{bank.Name}.bin",
                Kind: "spc_upload_bank",
                SourceAddress: bank.SourceAddress,
                Format: "spc_upload_stream",
                Data: data,
                Extra: new Dictionary<string, object>
                {
                    ["length"] = data.Length,
                    ["suffix_length"] = bank.Suffix.Length,
                }));
        }
    }

    private static void AddGlobalMap16(Rom rom, List<CoreAsset> assets)
    {
        var wordCount = (0xA100 - 0x8000) / 2;
        var payload = new byte[wordCount * 2];
        for (var i = 0; i < wordCount; i++)
        {
            var word = rom.GetWord(0x0D8000 + i * 2);
            payload[i * 2] = (byte)(word & 0xFF);
            payload[i * 2 + 1] = (byte)(word >> 8);
        }

        assets.Add(new CoreAsset(
            File: "map16/global_map16.bin",
            Kind: "global_map16",
            SourceAddress: 0x0D8000,
            Format: "little_endian_uint16",
            Data: payload,
            Extra: new Dictionary<string, object>
            {
                ["word_count"] = wordCount,
            }));
    }

    private static Dictionary<string, LevelSummary> BuildLevelSummaries(Rom rom, int[] levelIds)
    {
        var summaries = new Dictionary<string, LevelSummary>(StringComparer.Ordinal);
        var spriteBanks = rom.GetByte(0x05D8F5) == 0x22 ? rom.GetBytes(0x0EF100, 512) : Enumerable.Repeat((byte)7, 512).ToArray();

        foreach (var levelId in levelIds)
        {
            var layer1Address = rom.Get24(0x05E000 + levelId * 3);
            var layer1Length = CalculateLevelLength(rom, layer1Address);
            var layer1Raw = rom.GetBytes(layer1Address, layer1Length);
            var header = DecodeLevelHeader(layer1Raw);
            var parsedLayer1 = ParseLevelObjects(layer1Raw, header, layerIndex: 0);
            AnnotateVanillaScreenExits(parsedLayer1.ScreenExits, levelId);

            var layer2Address = rom.Get24(0x05E600 + levelId * 3);
            var layer2Kind = "object_stream";
            byte[] layer2Raw;
            int layer2Length;
            if ((layer2Address & 0xFF0000) == 0xFF0000)
            {
                layer2Address = (layer2Address & 0xFFFF) | 0x0C0000;
                (layer2Raw, layer2Length) = UnpackRle(rom, layer2Address);
                layer2Kind = "rle_background";
            }
            else
            {
                layer2Length = CalculateLevelLength(rom, layer2Address);
                layer2Raw = rom.GetBytes(layer2Address, layer2Length);
            }

            var spriteAddress = rom.GetWord(0x05EC00 + levelId * 2) | (spriteBanks[levelId] << 16);
            var spriteLength = CalculateSpriteDataLength(rom, spriteAddress);
            var spriteRaw = rom.GetBytes(spriteAddress, spriteLength);
            var parsedSprites = ParseSpriteData(spriteRaw);

            var key = $"{levelId:X3}";
            summaries[key] = new LevelSummary(
                LevelId: key,
                Header: header,
                Layer1Address: layer1Address,
                Layer1Length: layer1Length,
                Layer1Raw: layer1Raw.Select(value => (int)value).ToArray(),
                Layer1Sha1: Convert.ToHexString(SHA1.HashData(layer1Raw)),
                Layer1ObjectCount: parsedLayer1.Objects.Count,
                FirstLayer1Object: parsedLayer1.Objects.FirstOrDefault(),
                LastLayer1Object: parsedLayer1.Objects.LastOrDefault(),
                ScreenExits: parsedLayer1.ScreenExits,
                Layer2Address: layer2Address,
                Layer2Length: layer2Length,
                Layer2Kind: layer2Kind,
                Layer2Raw: layer2Raw.Select(value => (int)value).ToArray(),
                Layer2Sha1: Convert.ToHexString(SHA1.HashData(layer2Raw)),
                SpriteAddress: spriteAddress,
                SpriteLength: spriteLength,
                SpriteHeader: parsedSprites.Header,
                SpriteRaw: spriteRaw.Select(value => (int)value).ToArray(),
                SpriteSha1: Convert.ToHexString(SHA1.HashData(spriteRaw)),
                SpriteCount: parsedSprites.Sprites.Count,
                FirstSprite: parsedSprites.Sprites.FirstOrDefault());
        }

        return summaries;
    }

    private static int CalculateLevelLength(Rom rom, int address)
    {
        var start = address;
        address += 5;
        while (true)
        {
            var b0 = rom.GetByte(address);
            address++;
            if (b0 == 0xFF)
            {
                break;
            }

            var b1 = rom.GetByte(address);
            var b2 = rom.GetByte(address + 1);
            address += 2;
            var objectId = (b1 >> 4) | ((b0 & 0x60) >> 1);
            if (objectId == 0 && b2 == 0)
            {
                address++;
            }
            else if (objectId is 0x22 or 0x23)
            {
                address++;
            }
            else if (objectId is 0x27 or 0x29)
            {
                address += 2;
            }
        }

        return address - start;
    }

    private static int CalculateSpriteDataLength(Rom rom, int address)
    {
        var start = address;
        address++;
        while (rom.GetByte(address) != 0xFF)
        {
            address += 3;
        }

        return address + 1 - start;
    }

    private static LevelHeader DecodeLevelHeader(byte[] raw)
    {
        Check(raw.Length >= 5, "level stream is too short for header");
        var screens = (raw[0] & 0x1F) + 1;
        var mode = raw[1] & 0x1F;
        var layoutFlags = LevelVerticalTable[mode];
        var vertical = (layoutFlags & 1) != 0;
        return new LevelHeader(
            Raw: raw.Take(5).Select(value => (int)value).ToArray(),
            Screens: screens,
            BgPalette: raw[0] >> 5,
            LevelMode: mode,
            BackgroundColor: raw[1] >> 5,
            SpriteGraphics: raw[2] & 0x0F,
            MusicIndex: (raw[2] >> 4) & 0x07,
            Layer3Priority: (raw[2] >> 7) & 0x01,
            FgPalette: raw[3] & 0x07,
            SpritePalette: (raw[3] >> 3) & 0x07,
            TimerIndex: raw[3] >> 6,
            Tileset: raw[4] & 0x0F,
            Layer1Scroll: (raw[4] >> 4) & 0x03,
            ItemMemory: raw[4] >> 6,
            LayoutFlags: layoutFlags,
            Vertical: vertical,
            WidthTiles: vertical ? 16 : screens * 16,
            HeightTiles: vertical ? screens * 16 : 27);
    }

    private static ParsedLayerObjects ParseLevelObjects(byte[] raw, LevelHeader header, int layerIndex)
    {
        var objects = new List<LevelObject>();
        var exits = new List<ScreenExit>();
        var index = 5;
        var sequence = 0;
        var screenCursor = 0;
        while (index < raw.Length)
        {
            var offset = index;
            var b0 = raw[index++];
            if (b0 == 0xFF)
            {
                break;
            }

            Check(index + 2 <= raw.Length, "truncated level object stream");
            var b1 = raw[index];
            var b2 = raw[index + 1];
            index += 2;
            var objectId = (b1 >> 4) | ((b0 & 0x60) >> 1);
            if ((b0 & 0x80) != 0)
            {
                screenCursor++;
            }

            var extra = new List<int>();
            if (objectId == 0 && b2 == 0)
            {
                extra.Add(raw[index++]);
                var vanillaProperties = b1 & 0x03;
                exits.Add(new ScreenExit(
                    Screen: b0 & 0x1F,
                    ScreenCursor: screenCursor,
                    ExitLow: extra[0],
                    RawR11: b1,
                    VanillaProperties: vanillaProperties,
                    VanillaDestinationPropertyBits: ((vanillaProperties & 1) << 8) | extra[0],
                    VanillaSecondaryPropertyBits: (vanillaProperties >> 1) & 1,
                    LunarMagicProperties: b1 & 0x0F,
                    LunarMagicSecondary: (b1 >> 1) & 1,
                    VanillaDestinationLow: null,
                    VanillaSourceMapHigh: null,
                    VanillaDestination: null,
                    VanillaSecondary: null));
            }
            else if (objectId is 0x22 or 0x23)
            {
                extra.Add(raw[index++]);
            }
            else if (objectId is 0x27 or 0x29)
            {
                extra.Add(raw[index++]);
                extra.Add(raw[index++]);
            }

            objects.Add(new LevelObject(
                Sequence: sequence,
                Offset: offset,
                Raw: [(int)b0, b1, b2],
                ObjectId: objectId,
                SizeOrType: b2,
                Extra: extra.ToArray(),
                Placement: DecodeObjectPlacement(b0, b1, b2, objectId, screenCursor, header.LayoutFlags, layerIndex)));

            if (objectId == 0 && b2 == 0x01)
            {
                screenCursor = b0 & 0x1F;
            }

            sequence++;
        }

        return new ParsedLayerObjects(objects, exits);
    }

    private static ObjectPlacement DecodeObjectPlacement(int b0, int b1, int b2, int objectId, int screenCursor, int layoutFlags, int layerIndex)
    {
        var adjustedB0 = b0;
        var adjustedB1 = b1;
        var layerFlags = layerIndex == 0 ? layoutFlags : layoutFlags >> 1;
        if ((layerFlags & 1) != 0 && ((objectId << 8) | b2) >= 2)
        {
            var lowNibble = b0 & 0x0F;
            adjustedB0 = (b1 & 0x0F) | (b0 & 0xF0);
            adjustedB1 = lowNibble | (b1 & 0xF0);
        }

        var subX = adjustedB1 & 0x0F;
        var subY = adjustedB0 & 0x0F;
        var yTile = adjustedB0 & 0x1F;
        return new ObjectPlacement(
            Layer: layerIndex + 1,
            ScreenCursor: screenCursor,
            ScreenIncrement: (b0 & 0x80) != 0,
            SubX: subX,
            SubY: subY,
            HighSubscreen: (adjustedB0 & 0x10) != 0,
            Map16Offset: yTile * 16 + subX,
            XTile: screenCursor * 16 + subX,
            YTile: yTile,
            XPx: (screenCursor * 16 + subX) * 16,
            YPx: yTile * 16,
            AdjustedRaw: [adjustedB0, adjustedB1, b2]);
    }

    private static void AnnotateVanillaScreenExits(List<ScreenExit> exits, int sourceLevelId)
    {
        var sourceMapHigh = sourceLevelId & 0x100;
        for (var i = 0; i < exits.Count; i++)
        {
            var exit = exits[i];
            exits[i] = exit with
            {
                VanillaDestinationLow = exit.ExitLow,
                VanillaSourceMapHigh = sourceMapHigh >> 8,
                VanillaDestination = sourceMapHigh | exit.ExitLow,
                VanillaSecondary = exit.RawR11 >> 1,
            };
        }
    }

    private static ParsedSprites ParseSpriteData(byte[] raw)
    {
        Check(raw.Length > 0, "sprite stream is too short for header");
        var sprites = new List<SpriteRecord>();
        var index = 1;
        while (index < raw.Length && raw[index] != 0xFF)
        {
            Check(index + 3 <= raw.Length, "truncated sprite stream");
            var first = raw[index];
            var second = raw[index + 1];
            var screen = ((first << 3) & 0x10) | (second & 0x0F);
            sprites.Add(new SpriteRecord(
                Offset: index,
                ScreenY: first,
                XId: second,
                Screen: screen,
                XPx: screen * 0x100 + (second & 0xF0),
                YPx: (first & 0xF0) | (((first & 0x01) != 0) ? 0x100 : 0),
                ExtraBits: (first >> 2) & 0x03,
                SpriteId: raw[index + 2],
                Raw: [raw[index], raw[index + 1], raw[index + 2]]));
            index += 3;
        }

        return new ParsedSprites(raw[0], sprites);
    }

    private static (byte[] Data, int Length) UnpackRle(Rom rom, int address)
    {
        var start = address;
        var output = new List<byte>();
        while (rom.GetWord(address) != 0xFFFF)
        {
            var control = rom.GetByte(address++);
            var count = (control & 0x7F) + 1;
            if ((control & 0x80) != 0)
            {
                var value = (byte)rom.GetByte(address++);
                for (var i = 0; i < count; i++)
                {
                    output.Add(value);
                }
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    output.Add((byte)rom.GetByte(address + i));
                }
                address += count;
            }
        }

        return (output.ToArray(), address + 2 - start);
    }

    private static void VerifyGeneratedLevel(string generatedRoot, string levelKey, LevelSummary summary)
    {
        var path = Path.Combine(generatedRoot, "levels", $"level_{levelKey}.json");
        Check(File.Exists(path), $"generated level JSON missing: {path}");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var header = Required(root, "header");
        CheckJsonArrayEquals(Required(header, "raw"), summary.Header.Raw, $"level {levelKey} header raw");
        Check(Required(header, "screens").GetInt32() == summary.Header.Screens, $"level {levelKey} screens mismatch");
        Check(Required(header, "tileset").GetInt32() == summary.Header.Tileset, $"level {levelKey} tileset mismatch");
        Check(Required(header, "sprite_graphics").GetInt32() == summary.Header.SpriteGraphics, $"level {levelKey} sprite graphics mismatch");
        Check(Required(header, "width_tiles").GetInt32() == summary.Header.WidthTiles, $"level {levelKey} width mismatch");
        Check(Required(header, "height_tiles").GetInt32() == summary.Header.HeightTiles, $"level {levelKey} height mismatch");

        var layer1 = Required(root, "layer1");
        Check((Required(layer1, "source_addr").GetString() ?? "") == $"0x{summary.Layer1Address:X6}", $"level {levelKey} layer1 address mismatch");
        Check(Required(layer1, "length").GetInt32() == summary.Layer1Length, $"level {levelKey} layer1 length mismatch");
        Check(RequiredArray(layer1, "objects").Count() == summary.Layer1ObjectCount, $"level {levelKey} layer1 object count mismatch");
        CheckJsonArrayEquals(Required(layer1, "raw"), summary.Layer1Raw, $"level {levelKey} layer1 raw");

        var layer2 = Required(root, "layer2");
        Check((Required(layer2, "source_addr").GetString() ?? "") == $"0x{summary.Layer2Address:X6}", $"level {levelKey} layer2 address mismatch");
        Check((Required(layer2, "kind").GetString() ?? "") == summary.Layer2Kind, $"level {levelKey} layer2 kind mismatch");
        Check(Required(layer2, "length").GetInt32() == summary.Layer2Length, $"level {levelKey} layer2 length mismatch");
        CheckJsonArrayEquals(Required(layer2, "raw"), summary.Layer2Raw, $"level {levelKey} layer2 raw");

        var spriteLayer = Required(root, "sprite_layer");
        Check((Required(spriteLayer, "source_addr").GetString() ?? "") == $"0x{summary.SpriteAddress:X6}", $"level {levelKey} sprite address mismatch");
        Check(Required(spriteLayer, "length").GetInt32() == summary.SpriteLength, $"level {levelKey} sprite length mismatch");
        Check(Required(spriteLayer, "header").GetInt32() == summary.SpriteHeader, $"level {levelKey} sprite header mismatch");
        Check(RequiredArray(spriteLayer, "sprites").Count() == summary.SpriteCount, $"level {levelKey} sprite count mismatch");
        CheckJsonArrayEquals(Required(spriteLayer, "raw"), summary.SpriteRaw, $"level {levelKey} sprite raw");

        var generatedExits = RequiredArray(root, "screen_exits").ToArray();
        Check(generatedExits.Length == summary.ScreenExits.Count, $"level {levelKey} screen-exit count mismatch");
        for (var i = 0; i < summary.ScreenExits.Count; i++)
        {
            var expected = summary.ScreenExits[i];
            var actual = generatedExits[i];
            Check(Required(actual, "screen").GetInt32() == expected.Screen, $"level {levelKey} exit {i} screen mismatch");
            Check(Required(actual, "exit_low").GetInt32() == expected.ExitLow, $"level {levelKey} exit {i} low-byte mismatch");
            Check(Required(actual, "raw_r11").GetInt32() == expected.RawR11, $"level {levelKey} exit {i} raw_r11 mismatch");
            Check(Required(actual, "vanilla_destination").GetInt32() == expected.VanillaDestination, $"level {levelKey} exit {i} destination mismatch");
            Check(Required(actual, "vanilla_secondary").GetInt32() == expected.VanillaSecondary, $"level {levelKey} exit {i} secondary mismatch");
        }
    }

    private static (byte[] Data, int CompressedLength) SmwDecompress(Rom rom, int address)
    {
        var result = new List<byte>();
        var reader = new RomReader(rom, address);
        while (true)
        {
            var command = reader.Next();
            if (command == 0xFF)
            {
                return (result.ToArray(), (reader.Address - address) & 0x7FFF);
            }

            int op;
            int length;
            if ((command & 0xE0) != 0xE0)
            {
                length = command & 0x1F;
                op = command & 0xE0;
            }
            else
            {
                op = (command << 3) & 0xE0;
                length = ((command & 0x03) << 8) | reader.Next();
            }
            length++;

            if (op == 0x00)
            {
                for (var i = 0; i < length; i++)
                {
                    result.Add((byte)reader.Next());
                }
            }
            else if ((op & 0x80) != 0)
            {
                var offset = (reader.Next() << 8) | reader.Next();
                for (var i = 0; i < length; i++)
                {
                    Check(offset >= 0 && offset < result.Count, $"LC_LZ2 repeat offset out of range: {offset}");
                    result.Add(result[offset]);
                    offset++;
                }
            }
            else if ((op & 0x40) == 0)
            {
                var value = (byte)reader.Next();
                for (var i = 0; i < length; i++)
                {
                    result.Add(value);
                }
            }
            else if ((op & 0x20) == 0)
            {
                var first = (byte)reader.Next();
                var second = (byte)reader.Next();
                while (length > 0)
                {
                    result.Add(first);
                    length--;
                    if (length <= 0)
                    {
                        break;
                    }

                    result.Add(second);
                    length--;
                }
            }
            else
            {
                var value = reader.Next();
                for (var i = 0; i < length; i++)
                {
                    result.Add((byte)value);
                    value = (value + 1) & 0xFF;
                }
            }
        }
    }

    private static void WriteBinary(string path, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllBytes(path, data);
    }

    private static void WriteJson(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions) + "\n");
    }

    private static int UsageError(string[] args)
    {
        Console.Error.WriteLine($"invalid arguments: {string.Join(" ", args)}");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("usage:");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-core <rom.sfc> <out-dir>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-core <rom.sfc> <generated/smw>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-levels <rom.sfc> <out-dir>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-levels <rom.sfc> <generated/smw>");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static JsonElement Required(JsonElement element, string name)
    {
        Check(element.ValueKind == JsonValueKind.Object, $"expected object while reading property {name}");
        Check(element.TryGetProperty(name, out var value), $"missing JSON property {name}");
        return value;
    }

    private static IEnumerable<JsonElement> RequiredArray(JsonElement element, string name)
    {
        var value = Required(element, name);
        Check(value.ValueKind == JsonValueKind.Array, $"JSON property {name} should be an array");
        return value.EnumerateArray();
    }

    private static void CheckJsonArrayEquals(JsonElement element, IReadOnlyList<int> expected, string description)
    {
        Check(element.ValueKind == JsonValueKind.Array, $"{description} should be an array");
        var actual = element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
        Check(actual.SequenceEqual(expected), $"{description} mismatch");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record AudioBank(string Name, int SourceAddress, int Length, byte[] Suffix);

    private sealed record LevelSummary(
        string LevelId,
        LevelHeader Header,
        int Layer1Address,
        int Layer1Length,
        int[] Layer1Raw,
        string Layer1Sha1,
        int Layer1ObjectCount,
        LevelObject? FirstLayer1Object,
        LevelObject? LastLayer1Object,
        List<ScreenExit> ScreenExits,
        int Layer2Address,
        int Layer2Length,
        string Layer2Kind,
        int[] Layer2Raw,
        string Layer2Sha1,
        int SpriteAddress,
        int SpriteLength,
        int SpriteHeader,
        int[] SpriteRaw,
        string SpriteSha1,
        int SpriteCount,
        SpriteRecord? FirstSprite)
    {
        public object ToSerializable()
        {
            return new
            {
                level_id = LevelId,
                header = Header.ToSerializable(),
                layer1 = new
                {
                    source_addr = $"0x{Layer1Address:X6}",
                    length = Layer1Length,
                    sha1 = Layer1Sha1,
                    object_count = Layer1ObjectCount,
                    first_object = FirstLayer1Object?.ToSerializable(),
                    last_object = LastLayer1Object?.ToSerializable(),
                },
                layer2 = new
                {
                    source_addr = $"0x{Layer2Address:X6}",
                    length = Layer2Length,
                    kind = Layer2Kind,
                    sha1 = Layer2Sha1,
                },
                sprite_layer = new
                {
                    source_addr = $"0x{SpriteAddress:X6}",
                    length = SpriteLength,
                    header = SpriteHeader,
                    sha1 = SpriteSha1,
                    sprite_count = SpriteCount,
                    first_sprite = FirstSprite?.ToSerializable(),
                },
                screen_exits = ScreenExits.Select(exit => exit.ToSerializable()).ToArray(),
            };
        }
    }

    private sealed record LevelHeader(
        int[] Raw,
        int Screens,
        int BgPalette,
        int LevelMode,
        int BackgroundColor,
        int SpriteGraphics,
        int MusicIndex,
        int Layer3Priority,
        int FgPalette,
        int SpritePalette,
        int TimerIndex,
        int Tileset,
        int Layer1Scroll,
        int ItemMemory,
        int LayoutFlags,
        bool Vertical,
        int WidthTiles,
        int HeightTiles)
    {
        public object ToSerializable()
        {
            return new
            {
                raw = Raw,
                screens = Screens,
                bg_palette = BgPalette,
                level_mode = LevelMode,
                background_color = BackgroundColor,
                sprite_graphics = SpriteGraphics,
                music_index = MusicIndex,
                layer3_priority = Layer3Priority,
                fg_palette = FgPalette,
                sprite_palette = SpritePalette,
                timer_index = TimerIndex,
                tileset = Tileset,
                layer1_scroll = Layer1Scroll,
                item_memory = ItemMemory,
                layout_flags = LayoutFlags,
                vertical = Vertical,
                width_tiles = WidthTiles,
                height_tiles = HeightTiles,
            };
        }
    }

    private sealed record ParsedLayerObjects(List<LevelObject> Objects, List<ScreenExit> ScreenExits);

    private sealed record LevelObject(
        int Sequence,
        int Offset,
        int[] Raw,
        int ObjectId,
        int SizeOrType,
        int[] Extra,
        ObjectPlacement Placement)
    {
        public object ToSerializable()
        {
            return new
            {
                sequence = Sequence,
                offset = Offset,
                raw = Raw,
                object_id = ObjectId,
                size_or_type = SizeOrType,
                extra = Extra,
                placement = Placement.ToSerializable(),
            };
        }
    }

    private sealed record ObjectPlacement(
        int Layer,
        int ScreenCursor,
        bool ScreenIncrement,
        int SubX,
        int SubY,
        bool HighSubscreen,
        int Map16Offset,
        int XTile,
        int YTile,
        int XPx,
        int YPx,
        int[] AdjustedRaw)
    {
        public object ToSerializable()
        {
            return new
            {
                layer = Layer,
                screen_cursor = ScreenCursor,
                screen_increment = ScreenIncrement,
                sub_x = SubX,
                sub_y = SubY,
                high_subscreen = HighSubscreen,
                map16_offset = Map16Offset,
                x_tile = XTile,
                y_tile = YTile,
                x_px = XPx,
                y_px = YPx,
                adjusted_raw = AdjustedRaw,
            };
        }
    }

    private sealed record ScreenExit(
        int Screen,
        int ScreenCursor,
        int ExitLow,
        int RawR11,
        int VanillaProperties,
        int VanillaDestinationPropertyBits,
        int VanillaSecondaryPropertyBits,
        int LunarMagicProperties,
        int LunarMagicSecondary,
        int? VanillaDestinationLow,
        int? VanillaSourceMapHigh,
        int? VanillaDestination,
        int? VanillaSecondary)
    {
        public object ToSerializable()
        {
            return new
            {
                screen = Screen,
                screen_cursor = ScreenCursor,
                exit_low = ExitLow,
                raw_r11 = RawR11,
                vanilla_properties = VanillaProperties,
                vanilla_destination_property_bits = VanillaDestinationPropertyBits,
                vanilla_secondary_property_bits = VanillaSecondaryPropertyBits,
                lunar_magic_properties = LunarMagicProperties,
                lunar_magic_secondary = LunarMagicSecondary,
                vanilla_destination_low = VanillaDestinationLow,
                vanilla_source_map_high = VanillaSourceMapHigh,
                vanilla_destination = VanillaDestination,
                vanilla_secondary = VanillaSecondary,
            };
        }
    }

    private sealed record ParsedSprites(int Header, List<SpriteRecord> Sprites);

    private sealed record SpriteRecord(
        int Offset,
        int ScreenY,
        int XId,
        int Screen,
        int XPx,
        int YPx,
        int ExtraBits,
        int SpriteId,
        int[] Raw)
    {
        public object ToSerializable()
        {
            return new
            {
                offset = Offset,
                screen_y = ScreenY,
                x_id = XId,
                screen = Screen,
                x_px = XPx,
                y_px = YPx,
                extra_bits = ExtraBits,
                sprite_id = SpriteId,
                raw = Raw,
                format = "yyyyEESY_XXXXssss_NNNNNNNN",
            };
        }
    }

    private sealed record CoreManifest(
        int SchemaVersion,
        string SourceRomSha1,
        int SourceRomSize,
        List<CoreAsset> Assets)
    {
        public object ToSerializable()
        {
            return new
            {
                schema_version = SchemaVersion,
                source_rom_sha1 = SourceRomSha1,
                source_rom_size = SourceRomSize,
                assets = Assets.Select(asset => asset.ToSerializable()).ToArray(),
            };
        }
    }

    private sealed record CoreAsset(
        string File,
        string Kind,
        int SourceAddress,
        string Format,
        byte[] Data,
        Dictionary<string, object> Extra)
    {
        public object ToSerializable()
        {
            var properties = new Dictionary<string, object?>
            {
                ["file"] = File,
                ["kind"] = Kind,
                ["source_addr"] = $"0x{SourceAddress:X6}",
                ["format"] = Format,
                ["length"] = Data.Length,
                ["sha1"] = Convert.ToHexString(SHA1.HashData(Data)),
            };

            foreach (var (key, value) in Extra)
            {
                properties[key] = value;
            }

            return properties;
        }
    }

    private sealed class Rom
    {
        private Rom(string path, byte[] data, string sha1)
        {
            Path = path;
            Data = data;
            Sha1 = sha1;
        }

        public string Path { get; }
        public byte[] Data { get; }
        public string Sha1 { get; }

        public static Rom Load(string path)
        {
            var fullPath = System.IO.Path.GetFullPath(path);
            Check(File.Exists(fullPath), $"ROM does not exist: {fullPath}");
            var data = File.ReadAllBytes(fullPath);
            var sha1 = Convert.ToHexString(SHA1.HashData(data));

            if ((data.Length & 0xFFFFF) == 0x200)
            {
                throw new InvalidOperationException($"headered ROMs are not supported: size={data.Length} sha1={sha1}");
            }

            Check(data.Length == SmwUsRomSize, $"unsupported ROM size={data.Length}; expected {SmwUsRomSize}");
            Check(string.Equals(sha1, SmwUsSha1, StringComparison.OrdinalIgnoreCase),
                $"unsupported ROM sha1={sha1}; expected unheadered SMW USA {SmwUsSha1}");
            return new Rom(fullPath, data, sha1);
        }

        public int LoRomIndex(int address)
        {
            Check((address & 0x8000) != 0, $"LoROM address must have bit 0x8000 set: 0x{address:X6}");
            var index = ((address >> 16) & 0x7F) * 0x8000 + (address & 0x7FFF);
            Check(index >= 0 && index < Data.Length, $"LoROM address out of range: 0x{address:X6}");
            return index;
        }

        public int GetByte(int address)
        {
            return Data[LoRomIndex(address)];
        }

        public int GetWord(int address)
        {
            return GetByte(address) | (GetByte(address + 1) << 8);
        }

        public int Get24(int address)
        {
            return GetWord(address) | (GetByte(address + 2) << 16);
        }

        public byte[] GetBytes(int address, int count)
        {
            var output = new byte[count];
            for (var i = 0; i < count; i++)
            {
                output[i] = (byte)GetByte(address);
                address++;
                if ((address & 0x8000) == 0)
                {
                    address += 0x8000;
                }
            }

            return output;
        }
    }

    private sealed class RomReader
    {
        private readonly Rom _rom;

        public RomReader(Rom rom, int address)
        {
            _rom = rom;
            Address = address;
        }

        public int Address { get; private set; }

        public int Next()
        {
            var value = _rom.GetByte(Address);
            Address++;
            if ((Address & 0xFFFF) == 0)
            {
                Address += 0x8000;
            }

            return value;
        }
    }
}
