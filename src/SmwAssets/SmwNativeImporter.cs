using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace OpenPlatformerRuntime.SmwAssets;

public sealed record SmwImportProgress(string Stage, int Completed, int Total, string? LevelId = null);

public sealed record SmwImportResult(
    string RomSha1,
    int IndexedLevelCount,
    int GeneratedLevelCount,
    string ManifestPath);

public static class SmwNativeImporter
{
    public const int EditorLevelTitleCount = 0x200;

    private const int SchemaVersion = 1;
    private const int GfxFileCount = 50;
    private const int PaletteBlack = 0x0000;
    private const int PaletteWhite = 0x7FDD;
    private const int BgPaletteAddress = 0x00B0B0;
    private const int FgPaletteAddress = 0x00B190;
    private const int ObjectPaletteAddress = 0x00B250;
    private const int PlayerPaletteAddress = 0x00B2C8;
    private const int SpritePaletteAddress = 0x00B318;
    private const int Layer3PaletteAddress = 0x00B170;
    private const int BerryPaletteAddress = 0x00B674;
    private const int BackAreaColorAddress = 0x00B0A0;
    private const int AnimatedColorAddress = 0x00B60C;
    private const int LevelNamesAddress = 0x04A0FC;
    private const int LevelNameStringsAddress = 0x049AC5;
    private const int LevelNamePrefixOffsetsAddress = 0x049C91;
    private const int LevelNameMiddleOffsetsAddress = 0x049CCF;
    private const int LevelNameSuffixOffsetsAddress = 0x049CED;
    private const int LmLevelNamesHookAddress = 0x048E81;
    private const int LmLevelNamesPointerAddress = 0x03BB57;
    private const int LmLevelNamesPatchBytes = 0x100 * 19;
    private const int LmLevelNameCharacterCount = 19;
    private const int LmCustomPalettePointerTable = 0x0EF600;
    private const int LmCustomPaletteHijackAddress = 0x00A5C0;
    private const int LmCustomPaletteRoutineAddress = 0x0EF570;
    private const int LmSuperGfxPointerAddress = 0x0FF7FF;

    private static readonly JsonWriterOptions JsonOptions = new()
    {
        Indented = true,
    };

    private static readonly int[] LevelVerticalTable =
    [
        0x00, 0x00, 0x80, 0x01, 0x81, 0x02, 0x82, 0x03,
        0x83, 0x00, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80,
    ];

    private static readonly bool[] NormalGfx3BppExpand =
    [
        true, true, true, true, true, true, true, true, true, true, true, true, true,
        true, true, true, true, true, true, true, true, true, true, true, true, true,
        true, true, true, true, true, true, true, true, true, true, true, true, true,
        false, false, false, false, false, true, true, true, false, true, true, false,
        true,
    ];

    private static readonly int[] FgAndBgGfxList =
    [
        0x14, 0x17, 0x19, 0x15, 0x14, 0x17, 0x1B, 0x18,
        0x14, 0x17, 0x1B, 0x16, 0x14, 0x17, 0x0C, 0x1A,
        0x14, 0x17, 0x1B, 0x08, 0x14, 0x17, 0x0C, 0x07,
        0x14, 0x17, 0x0C, 0x16, 0x14, 0x17, 0x1B, 0x15,
        0x14, 0x17, 0x19, 0x16, 0x14, 0x17, 0x0D, 0x1A,
        0x14, 0x17, 0x1B, 0x08, 0x14, 0x17, 0x1B, 0x18,
        0x14, 0x17, 0x19, 0x1F, 0x14, 0x17, 0x0D, 0x07,
        0x14, 0x17, 0x19, 0x1A, 0x14, 0x17, 0x14, 0x14,
        0x0E, 0x0F, 0x17, 0x17, 0x1C, 0x1D, 0x08, 0x1E,
        0x1C, 0x1D, 0x08, 0x1E, 0x1C, 0x1D, 0x08, 0x1E,
        0x1C, 0x1D, 0x08, 0x1E, 0x1C, 0x1D, 0x08, 0x1E,
        0x1C, 0x1D, 0x08, 0x1E, 0x1C, 0x1D, 0x08, 0x1E,
        0x14, 0x17, 0x19, 0x2C, 0x19, 0x17, 0x1B, 0x18,
    ];

    private static readonly int[] SpriteGfxList =
    [
        0x00, 0x01, 0x13, 0x02, 0x00, 0x01, 0x12, 0x03,
        0x00, 0x01, 0x13, 0x05, 0x00, 0x01, 0x13, 0x04,
        0x00, 0x01, 0x13, 0x06, 0x00, 0x01, 0x13, 0x09,
        0x00, 0x01, 0x13, 0x04, 0x00, 0x01, 0x06, 0x11,
        0x00, 0x01, 0x13, 0x20, 0x00, 0x01, 0x13, 0x0F,
        0x00, 0x01, 0x13, 0x23, 0x00, 0x01, 0x0D, 0x14,
        0x00, 0x01, 0x24, 0x0E, 0x00, 0x01, 0x0A, 0x22,
        0x00, 0x01, 0x13, 0x0E, 0x00, 0x01, 0x13, 0x14,
        0x00, 0x00, 0x00, 0x08, 0x10, 0x0F, 0x1C, 0x1D,
        0x00, 0x01, 0x24, 0x22, 0x00, 0x01, 0x25, 0x22,
        0x00, 0x22, 0x13, 0x2D, 0x00, 0x01, 0x0F, 0x22,
        0x00, 0x26, 0x2E, 0x22, 0x21, 0x0B, 0x25, 0x0A,
        0x00, 0x0D, 0x24, 0x22, 0x2C, 0x30, 0x2D, 0x0E,
    ];

    private static readonly int[] GenericRepeatedTiles =
        [0x02, 0x21, 0x23, 0x2A, 0x2B, 0x3F, 0x03, 0x13, 0x1E, 0x24, 0x2E, 0x2F, 0x30, 0x32, 0x65];

    private static readonly int[] GenericExtendedObjectTiles =
    [
        0x1F, 0x22, 0x24, 0x42, 0x43, 0x27, 0x29, 0x25, 0x6E, 0x6F, 0x70,
        0x71, 0x72, 0x45, 0x46, 0x47, 0x48, 0x36, 0x37, 0x11, 0x12, 0x14,
        0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x29, 0x1D, 0x1F,
        0x20, 0x21, 0x22, 0x23, 0x25, 0x26, 0x27, 0x28, 0x2A, 0xDE, 0xE0,
        0xE2, 0xE4, 0xEC, 0xED, 0x2C, 0x25, 0x2D,
    ];

    private static readonly int[] VerticalPipeTopLeft = [0x33, 0x37, 0x39, 0x00, 0x00];
    private static readonly int[] VerticalPipeTopRight = [0x34, 0x38, 0x3A, 0x00, 0x00];
    private static readonly int[] VerticalPipeBottomLeft = [0x00, 0x00, 0x39, 0x33, 0x37];
    private static readonly int[] VerticalPipeBottomRight = [0x00, 0x00, 0x3A, 0x34, 0x38];
    private static readonly int[] HorizontalPipeEnd = [0x3B, 0x3C, 0x3B, 0x3F, 0x3B, 0x3C, 0x3B, 0x3F];
    private static readonly int[] HorizontalPipeShaft = [0x3D, 0x3E, 0x3D, 0x3E, 0x3D, 0x3E, 0x3D, 0x3E];
    private static readonly int[] GroundEdgeTop =
    [
        0x0040, 0x0041, 0x0006, 0x0145, 0x014B, 0x0148, 0x014C, 0x0101,
        0x0103, 0x01B6, 0x01B7, 0x0145, 0x014B, 0x0148, 0x014C,
    ];
    private static readonly int[] GroundEdgeMiddle1 =
    [
        0x0040, 0x0041, 0x0006, 0x014B, 0x014B, 0x014C, 0x014C, 0x0040,
        0x0041, 0x014B, 0x014C, 0x014B, 0x014B, 0x014C, 0x014C,
    ];
    private static readonly int[] GroundEdgeMiddle2 =
    [
        0x0040, 0x0041, 0x0006, 0x014B, 0x014B, 0x014C, 0x014C, 0x0040,
        0x0041, 0x014B, 0x014C, 0x014B, 0x014B, 0x014C, 0x014C,
    ];
    private static readonly int[] GroundEdgeBottom =
    [
        0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF,
        0xFFFF, 0xFFFF, 0xFFFF, 0x01E2, 0x01E2, 0x01E4, 0x01E4,
    ];
    private static readonly int[] MidwayTop = [0x2F, 0x25, 0x32];
    private static readonly int[] MidwayMiddle = [0x30, 0x25, 0x33];
    private static readonly int[] MidwayBottom = [0x31, 0x25, 0x34];
    private static readonly int[] GoalTop = [0x39, 0x25, 0x3C];
    private static readonly int[] GoalMiddle = [0x3A, 0x25, 0x3D];
    private static readonly int[] GoalBottom = [0x3B, 0x25, 0x3E];
    private static readonly int[] RopeCloudLine = [0x05, 0x06];
    private static readonly int[] SmallBushLeft = [0x73, 0x7A, 0x85, 0x88, 0xC3];
    private static readonly int[] SmallBushMiddle = [0x74, 0x7B, 0x86, 0x89, 0xC3];
    private static readonly int[] SmallBushRight = [0x79, 0x80, 0x87, 0x8E, 0xC3];
    private static readonly int[] DiagonalPipeRow0 = [0x01C4, 0x01C5];
    private static readonly int[] DiagonalPipeRow1 = [0x01C7, 0x01EC, 0x01ED, 0x01C6];
    private static readonly int[] DiagonalPipeRow2 = [0x01C7, 0x01EE, 0x0159, 0x015A, 0x01EF];
    private static readonly int[] DiagonalPipeRowN = [0x01C7, 0x01EE, 0x0159, 0x015B, 0x015C];

    private static readonly int[] Map16PointerMasks =
    [
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xE0, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xFE, 0x00, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xE0, 0x00, 0x00, 0x03, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
    ];

    private static readonly int[] TilesetMap16Pointers =
    [
        0x8B70, 0xBC00, 0xC800, 0xD400, 0xE300,
        0xE300, 0xC800, 0x8B70, 0xC800, 0xD400,
        0xD400, 0xD400, 0x8B70, 0xE300, 0xD400,
    ];

    private static readonly PlayerTableSpec[] PlayerOamTableSpecs =
    [
        new("player_xy_disp_index_index", 0x00DCEC, 70, "u8"),
        new("player_xy_disp_index", 0x00DD32, 28, "u8"),
        new("x_disp", 0x00DD4E, 114, "s16"),
        new("y_disp", 0x00DE32, 114, "s16"),
        new("powerup_tileset_index", 0x00DF16, 4, "u8"),
        new("tiles_index", 0x00DF4C, 192, "u8"),
        new("tiles", 0x00DFDA, 50, "u8"),
        new("head_tile_pointer_index", 0x00E00C, 192, "u8"),
        new("body_tile_pointer_index", 0x00E0CC, 192, "u8"),
        new("tile_x_flip", 0x00E18C, 2, "u8"),
    ];

    private static readonly PaletteTableSpec[] GlobalPaletteSpecs =
    [
        new("sky", 0x00B0A0, 16),
        new("background", 0x00B0B0, 96),
        new("layer3", 0x00B170, 16),
        new("foreground", 0x00B190, 96),
        new("objects", 0x00B250, 60),
        new("player", 0x00B2C8, 40),
        new("sprites", 0x00B318, 84),
        new("flashing", 0x00B60C, 16),
        new("yoshi_berry", 0x00B674, 21),
    ];

    public static SmwImportResult InitializeAssetPack(
        byte[] selectedRomBytes,
        string? fileName,
        string outputRoot,
        IProgress<SmwImportProgress>? progress = null)
    {
        var rom = SmwRom.FromBytes(selectedRomBytes, fileName);
        var root = Path.GetFullPath(outputRoot);
        Directory.CreateDirectory(root);

        progress?.Report(new SmwImportProgress("Reading level names", 1, 6));
        var levelIndex = BuildLevelIndex(rom);
        progress?.Report(new SmwImportProgress("Writing core assets", 2, 6));
        var assets = ExtractGlobalAssets(rom, root);

        progress?.Report(new SmwImportProgress("Writing manifest", 5, 6));
        var manifest = BuildManifest(rom, assets, new Dictionary<string, object?>(StringComparer.Ordinal), levelIndex);
        var manifestPath = Path.Combine(root, "manifest.json");
        WriteJson(manifestPath, manifest);
        progress?.Report(new SmwImportProgress("Ready", 6, 6));

        return new SmwImportResult(rom.Sha1, levelIndex.Levels.Count, 0, manifestPath);
    }

    public static SmwImportResult ImportLevel(
        byte[] selectedRomBytes,
        string? fileName,
        string outputRoot,
        string levelId,
        IProgress<SmwImportProgress>? progress = null,
        bool includeExitTargets = true,
        int exitDepth = 1)
    {
        var rom = SmwRom.FromBytes(selectedRomBytes, fileName);
        var root = Path.GetFullPath(outputRoot);
        Directory.CreateDirectory(root);

        var manifestPath = Path.Combine(root, "manifest.json");
        var existingLevels = ReadExistingLevels(manifestPath);
        var assets = ExtractGlobalAssets(rom, root);
        var levelIndex = BuildLevelIndex(rom);
        var titles = levelIndex.Titles;

        var queue = new List<int> { ParseLevelId(levelId) };
        var depthByLevel = new Dictionary<int, int> { [queue[0]] = 0 };
        var generated = 0;
        for (var index = 0; index < queue.Count; index++)
        {
            var currentLevelId = queue[index];
            var key = FormatLevelId(currentLevelId);
            progress?.Report(new SmwImportProgress("Extracting level", index + 1, queue.Count, key));
            var levelInfo = ExtractLevel(rom, root, currentLevelId, titles, levelIndex.TitleSource);
            existingLevels[key] = levelInfo;
            generated++;

            if (!includeExitTargets || depthByLevel[currentLevelId] >= exitDepth)
            {
                continue;
            }

            if (levelInfo.TryGetValue("screen_exits", out var exitsObj) &&
                exitsObj is IEnumerable<object?> exits)
            {
                foreach (var exitObj in exits)
                {
                    if (exitObj is not Dictionary<string, object?> exit ||
                        !exit.TryGetValue("vanilla_destination", out var destinationObj))
                    {
                        continue;
                    }

                    var destination = Convert.ToInt32(destinationObj, CultureInfo.InvariantCulture);
                    if (destination < 0 ||
                        destination >= EditorLevelTitleCount ||
                        depthByLevel.ContainsKey(destination))
                    {
                        continue;
                    }

                    depthByLevel[destination] = depthByLevel[currentLevelId] + 1;
                    queue.Add(destination);
                }
            }
        }

        var manifest = BuildManifest(rom, assets, existingLevels, levelIndex);
        WriteJson(manifestPath, manifest);
        progress?.Report(new SmwImportProgress("Ready", generated, generated));
        return new SmwImportResult(rom.Sha1, levelIndex.Levels.Count, existingLevels.Count, manifestPath);
    }

    public static SmwLevelIndex BuildLevelIndex(byte[] selectedRomBytes, string? fileName = null)
    {
        return BuildLevelIndex(SmwRom.FromBytes(selectedRomBytes, fileName));
    }

    private static SmwLevelIndex BuildLevelIndex(SmwRom rom)
    {
        Dictionary<int, string> titles;
        string source;
        string? error = null;
        try
        {
            (titles, source) = LoadOverworldLevelTitles(rom);
        }
        catch (Exception exc)
        {
            titles = new Dictionary<int, string>();
            source = "unavailable";
            error = exc.Message;
        }

        var levels = new List<Dictionary<string, object?>>();
        var invalid = 0;
        for (var levelId = 0; levelId < EditorLevelTitleCount; levelId++)
        {
            try
            {
                var layer1Address = rom.Get24(0x05E000 + levelId * 3);
                var layer1Length = CalculateLevelLength(rom, layer1Address);
                var header = DecodeLevelHeader(rom.GetBytes(layer1Address, 5));
                var key = FormatLevelId(levelId);
                var title = titles.GetValueOrDefault(levelId, "");
                levels.Add(Dict(
                    ("id", key),
                    ("name", title),
                    ("display_name", string.IsNullOrWhiteSpace(title) ? $"Level {key}" : title),
                    ("title_source", string.IsNullOrWhiteSpace(title) ? "none" : source),
                    ("layer1_addr", Hex24(layer1Address)),
                    ("layer1_length", layer1Length),
                    ("screens", header.Screens),
                    ("vertical", header.Vertical)));
            }
            catch
            {
                invalid++;
            }
        }

        return new SmwLevelIndex(source, error == null ? "ok" : "partial", error, titles, levels, invalid);
    }

    private static Dictionary<string, object?> ExtractGlobalAssets(SmwRom rom, string root)
    {
        var assets = new Dictionary<string, object?>(StringComparer.Ordinal);
        assets["map16_global"] = ExtractGlobalMap16(rom, root);
        assets["palettes_global"] = ExtractGlobalPalettes(rom, root);
        assets["secondary_tables"] = ExtractEntranceTables(rom, root);
        assets["player_graphics"] = ExtractPlayerGraphics(rom, root);
        assets["audio"] = ExtractAudioBanks(rom, root);
        return assets;
    }

    private static Dictionary<string, object?> ExtractGlobalMap16(SmwRom rom, string root)
    {
        var wordCount = (0xA100 - 0x8000) / 2;
        var payload = new byte[wordCount * 2];
        for (var i = 0; i < wordCount; i++)
        {
            var word = rom.GetWord(0x0D8000 + i * 2);
            payload[i * 2] = (byte)(word & 0xFF);
            payload[i * 2 + 1] = (byte)(word >> 8);
        }

        var path = Path.Combine(root, "map16", "global_map16.bin");
        return Dict(
            ("file", Rel(path, root)),
            ("format", "little_endian_uint16"),
            ("word_count", wordCount),
            ("sha1", WriteBinary(path, payload)));
    }

    private static Dictionary<string, object?> ExtractGlobalPalettes(SmwRom rom, string root)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var spec in GlobalPaletteSpecs)
        {
            var words = rom.GetWords(spec.Address, spec.WordCount);
            payload[spec.Name] = Dict(
                ("snes_bgr555", words),
                ("rgb888", SnesWordsToRgb(words)));
        }

        var path = Path.Combine(root, "palettes", "global_palettes.json");
        return Dict(
            ("file", Rel(path, root)),
            ("sha1", WriteJson(path, payload)));
    }

    private static Dictionary<string, object?> ExtractEntranceTables(SmwRom rom, string root)
    {
        var payload = Dict(
            ("level_info_05f000", rom.GetBytes(0x05F000, 0x200).Select(value => (int)value).ToArray()),
            ("level_info_05f200", rom.GetBytes(0x05F200, 0x200).Select(value => (int)value).ToArray()),
            ("level_info_05f400", rom.GetBytes(0x05F400, 0x200).Select(value => (int)value).ToArray()),
            ("level_info_05f600", rom.GetBytes(0x05F600, 0x200).Select(value => (int)value).ToArray()),
            ("secondary_level_low_05f800", rom.GetBytes(0x05F800, 0x200).Select(value => (int)value).ToArray()),
            ("secondary_y_05fa00", rom.GetBytes(0x05FA00, 0x200).Select(value => (int)value).ToArray()),
            ("secondary_x_05fc00", rom.GetBytes(0x05FC00, 0x200).Select(value => (int)value).ToArray()),
            ("secondary_entrance_type_05fe00", rom.GetBytes(0x05FE00, 0x200).Select(value => (int)value).ToArray()));
        var path = Path.Combine(root, "levels", "secondary_tables.json");
        return Dict(
            ("file", Rel(path, root)),
            ("sha1", WriteJson(path, payload)));
    }

    private static Dictionary<string, object?> ExtractPlayerGraphics(SmwRom rom, string root)
    {
        var paletteVariants = new List<Dictionary<string, object?>>();
        var paletteRgbs = new List<int[][]>();
        var names = new[] { "mario", "luigi", "fire_mario", "fire_luigi" };
        for (var i = 0; i < names.Length; i++)
        {
            var words = BuildPlayerSpritePaletteWords(rom, i);
            var rgb = SnesWordsToRgb(words);
            paletteRgbs.Add(rgb);
            paletteVariants.Add(Dict(
                ("index", i),
                ("name", names[i]),
                ("snes_bgr555", words),
                ("colors", rgb)));
        }

        var sourceGfx = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, pointerAddress) in new[] { ("gfx32", 0x00B8D8), ("gfx33", 0x00B88B) })
        {
            var gfxAddress = 0x080000 | rom.GetWord(pointerAddress);
            var (gfxData, compressedLength) = SmwDecompress(rom, gfxAddress);
            var gfxPath = Path.Combine(root, "gfx", $"{name}.bin");
            var variants = new List<Dictionary<string, object?>>();
            for (var paletteIndex = 0; paletteIndex < paletteRgbs.Count; paletteIndex++)
            {
                var atlasPath = Path.Combine(root, "player", $"{name}_player_palette{paletteIndex}.png");
                var atlas = Write4BppAtlasPng(atlasPath, gfxData, paletteRgbs[paletteIndex]);
                atlas["file"] = Rel(atlasPath, root);
                atlas["palette_variant"] = paletteIndex;
                atlas["palette_name"] = names[paletteIndex];
                variants.Add(atlas);
            }

            sourceGfx[name] = variants[0]["file"];
            _ = WriteBinary(gfxPath, gfxData);
        }

        var oamTables = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var spec in PlayerOamTableSpecs)
        {
            var values = spec.Format == "s16"
                ? rom.GetWords(spec.Address, spec.Count).Select(Signed16).ToArray()
                : rom.GetBytes(spec.Address, spec.Count).Select(value => (int)value).ToArray();
            oamTables[spec.Name] = Dict(
                ("source_addr", Hex24(spec.Address)),
                ("count", spec.Count),
                ("format", spec.Format),
                ("values", values));
        }

        var payload = Dict(
            ("status", "partial"),
            ("source_gfx", sourceGfx),
            ("palette", Dict(
                ("source", "palettes/global_palettes.json"),
                ("set", "player"),
                ("variant", 0),
                ("snes_bgr555", BuildPlayerSpritePaletteWords(rom, 0)),
                ("colors", paletteRgbs[0]),
                ("variants", paletteVariants),
                ("layout", "full OBJ palette row 8: colors 0-1 fixed, 2-5 object row, 6-15 dynamic player palette from $00B2C8"))),
            ("tile_pointer_tables", Dict(
                ("head", rom.GetBytes(0x00E00C, 192).Select(value => (int)value).ToArray()),
                ("body", rom.GetBytes(0x00E0CC, 192).Select(value => (int)value).ToArray()),
                ("walking_pose_count", rom.GetBytes(0x00DC78, 4).Select(value => (int)value).ToArray()),
                ("animation_speed_table", rom.GetBytes(0x00DC7C, 112).Select(value => (int)value).ToArray()))),
            ("oam_tables", oamTables),
            ("categories", Dict(
                ("player", Dict(("small", Array.Empty<object>()), ("big", Array.Empty<object>()), ("cape", Array.Empty<object>()), ("fire", Array.Empty<object>()), ("yoshi", Array.Empty<object>()))),
                ("states_pending_direct_oam_port", new[]
                {
                    "idle", "walk", "run", "jump", "spin_jump", "fall", "duck", "climb", "swim",
                    "cape_flight", "powerup_transition", "damage_transition",
                }))),
            ("notes", new[]
            {
                "PNG atlases are usable in Godot now.",
                "The runtime uses native OAM placement tables for the first big-player pose set.",
                "Full frame/state categorization, cape, Yoshi, powerup transition, and damage transition rendering are still pending.",
            }));
        var metadataPath = Path.Combine(root, "player", "player_graphics.json");
        return Dict(
            ("file", Rel(metadataPath, root)),
            ("sha1", WriteJson(metadataPath, payload)));
    }

    private static Dictionary<string, object?> ExtractAudioBanks(SmwRom rom, string root)
    {
        var banks = new (string Name, int Address, int Length, byte[] Suffix)[]
        {
            ("spc_engine", 0x0E8000, 6321, [0x00, 0x00]),
            ("spc_samples", 0x0F8000, 28538, []),
            ("spc_level_music_bank", 0x0EAED6, 16899, []),
            ("spc_overworld_music_bank", 0x0E98B1, 5667, []),
            ("spc_credits_music_bank", 0x03E400, 6624, []),
        };
        var bankPayload = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var bank in banks)
        {
            var data = rom.GetBytes(bank.Address, bank.Length).Concat(bank.Suffix).ToArray();
            var path = Path.Combine(root, "audio", $"{bank.Name}.bin");
            bankPayload[bank.Name] = Dict(
                ("file", Rel(path, root)),
                ("source_addr", Hex24(bank.Address)),
                ("length", data.Length),
                ("sha1", WriteBinary(path, data)),
                ("format", "spc_upload_stream"));
        }

        var manifest = Dict(
            ("status", "partial"),
            ("sample_rate", 32000),
            ("notes", new[]
            {
                "Raw SPC upload banks are preserved from the original ROM.",
                "Preview WAV extraction is still owned by the Python parity tool.",
                "Full SPC/DSP music and SFX sequencing is not ported yet.",
            }),
            ("banks", bankPayload),
            ("decoded_samples", Array.Empty<object>()));
        var manifestPath = Path.Combine(root, "audio", "audio_manifest.json");
        return Dict(
            ("file", Rel(manifestPath, root)),
            ("status", "partial"),
            ("sample_rate", 32000),
            ("banks", bankPayload),
            ("decoded_samples", Array.Empty<object>()),
            ("sha1", WriteJson(manifestPath, manifest)));
    }

    private static Dictionary<string, object?> ExtractLevel(
        SmwRom rom,
        string root,
        int levelId,
        IReadOnlyDictionary<int, string> titles,
        string titleSource)
    {
        if (levelId < 0 || levelId >= EditorLevelTitleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(levelId), $"Level id out of range: 0x{levelId:X}");
        }

        var levelKey = FormatLevelId(levelId);
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
        ParsedObjects parsedLayer2 = new([], []);
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
            parsedLayer2 = ParseLevelObjects(layer2Raw, header, layerIndex: 1);
            AnnotateVanillaScreenExits(parsedLayer2.ScreenExits, levelId);
        }

        var banks = rom.GetByte(0x05D8F5) == 0x22
            ? rom.GetBytes(0x0EF100, 512)
            : Enumerable.Repeat((byte)7, 512).ToArray();
        var spriteAddress = rom.GetWord(0x05EC00 + levelId * 2) | (banks[levelId] << 16);
        var spriteLength = CalculateSpriteDataLength(rom, spriteAddress);
        var spriteRaw = rom.GetBytes(spriteAddress, spriteLength);
        var parsedSprites = ParseSpriteData(spriteRaw);

        var paletteAssets = ExtractLevelPaletteAssets(rom, root, levelId, header);
        var tilesetAssets = ExtractLevelTilesetAssets(rom, root, levelId, header, paletteAssets);
        var spriteTilesetAssets = ExtractLevelSpriteTilesetAssets(rom, root, levelId, header, paletteAssets);
        var layer2Background = ExtractLayer2BackgroundPreview(rom, root, levelId, levelKey, header, layer2Raw, layer2Kind, layer2Address, paletteAssets);
        var layoutPreview = ExtractLevelLayoutPreview(rom, root, levelId, levelKey, header, parsedLayer1.Objects, paletteAssets);

        var levelPath = Path.Combine(root, "levels", $"level_{levelKey}.json");
        var payload = Dict(
            ("level_id", levelKey),
            ("header", header.ToSerializable()),
            ("layout", Dict(
                ("vertical", header.Vertical),
                ("screens", header.Screens),
                ("width_tiles", header.WidthTiles),
                ("height_tiles", header.HeightTiles),
                ("tile_size_px", 16),
                ("width_px", header.WidthTiles * 16),
                ("height_px", header.HeightTiles * 16))),
            ("layer1", Dict(
                ("source_addr", Hex24(layer1Address)),
                ("length", layer1Length),
                ("raw", layer1Raw.Select(value => (int)value).ToArray()),
                ("objects", parsedLayer1.Objects.Select(obj => obj.ToSerializable()).ToArray()))),
            ("layer2", Dict(
                ("source_addr", Hex24(layer2Address)),
                ("length", layer2Length),
                ("kind", layer2Kind),
                ("raw", layer2Raw.Select(value => (int)value).ToArray()),
                ("objects", parsedLayer2.Objects.Select(obj => obj.ToSerializable()).ToArray()))),
            ("sprite_layer", Dict(
                ("source_addr", Hex24(spriteAddress)),
                ("length", spriteLength),
                ("raw", spriteRaw.Select(value => (int)value).ToArray()),
                ("header", parsedSprites.Header),
                ("sprites", parsedSprites.Sprites.Select(sprite => sprite.ToSerializable()).ToArray()))),
            ("screen_exits", parsedLayer1.ScreenExits.Select(exit => exit.ToSerializable()).ToArray()),
            ("palette_assets", Dict(("file", paletteAssets["file"]), ("source", paletteAssets["source"]), ("back_area_color", paletteAssets["back_area_color"]))),
            ("tileset_assets", tilesetAssets),
            ("sprite_tileset_assets", spriteTilesetAssets),
            ("layout_preview", layoutPreview),
            ("layer2_background", layer2Background));

        var levelSha = WriteJson(levelPath, payload);
        var title = titles.GetValueOrDefault(levelId, "");
        return Dict(
            ("file", Rel(levelPath, root)),
            ("sha1", levelSha),
            ("layer1_addr", Hex24(layer1Address)),
            ("layer2_addr", Hex24(layer2Address)),
            ("sprite_addr", Hex24(spriteAddress)),
            ("object_count", parsedLayer1.Objects.Count),
            ("sprite_count", parsedSprites.Sprites.Count),
            ("screen_exits", parsedLayer1.ScreenExits.Select(exit => exit.ToSerializable()).ToArray()),
            ("tileset_assets", Dict(
                ("file", tilesetAssets["file"]),
                ("atlas_png", ((Dictionary<string, object?>)tilesetAssets["atlas_png"]!)["file"]),
                ("map16_preview_png", ((Dictionary<string, object?>)tilesetAssets["map16_preview_png"]!)["file"]))),
            ("sprite_tileset_assets", Dict(
                ("file", spriteTilesetAssets["file"]),
                ("atlas_png", ((Dictionary<string, object?>)spriteTilesetAssets["atlas_png"]!)["file"]),
                ("vram", ((Dictionary<string, object?>)spriteTilesetAssets["vram"]!)["file"]),
                ("status", spriteTilesetAssets["status"]),
                ("sprite_graphics", spriteTilesetAssets["sprite_graphics"]),
                ("uploads", spriteTilesetAssets["uploads"]))),
            ("palette_assets", Dict(("file", paletteAssets["file"]), ("source", paletteAssets["source"]))),
            ("layout_preview", Dict(
                ("file", layoutPreview["file"]),
                ("preview_png", ((Dictionary<string, object?>)layoutPreview["preview_png"]!)["file"]),
                ("status", layoutPreview["status"]),
                ("placed_tile_count", layoutPreview["placed_tile_count"]))),
            ("layer2_background", layer2Background == null
                ? null
                : Dict(
                    ("file", layer2Background["file"]),
                    ("preview_png", ((Dictionary<string, object?>)layer2Background["preview_png"]!)["file"]),
                    ("placed_tile_count", layer2Background["placed_tile_count"]),
                    ("map16_page", layer2Background["map16_page"]))),
            ("name", title),
            ("display_name", string.IsNullOrWhiteSpace(title) ? $"Level {levelKey}" : title),
            ("title_source", string.IsNullOrWhiteSpace(title) ? "none" : titleSource));
    }

    private static Dictionary<string, object?> ExtractLevelPaletteAssets(SmwRom rom, string root, int levelId, LevelHeader header)
    {
        var levelKey = FormatLevelId(levelId);
        var path = Path.Combine(root, "palettes", $"level_{levelKey}_palette.json");
        var (source, backAreaColor, words) = BuildLevelPaletteWords(rom, levelId, header);
        var payload = Dict(
            ("status", "preview"),
            ("source", source),
            ("level_id", levelKey),
            ("back_area_color", Dict(("snes_bgr555", backAreaColor), ("rgb888", SnesWordsToRgb([backAreaColor])[0]))),
            ("snes_bgr555", words),
            ("rgb888", SnesWordsToRgb(words)),
            ("layout", Dict(
                ("rows", 16),
                ("colors_per_row", 16),
                ("tilemap_palette_bits", "bits 10-12 select CGRAM rows 0-7 for BG/FG Map16 rendering"),
                ("sprite_rows", "rows 8-15 are used by OAM sprite palettes"))),
            ("header_palette_indexes", Dict(
                ("background_color", header.BackgroundColor),
                ("bg_palette", header.BgPalette),
                ("fg_palette", header.FgPalette),
                ("sprite_palette", header.SpritePalette))),
            ("notes", new[]
            {
                "Vanilla palette assembly follows the header-selected tables.",
                "Lunar Magic custom palette pointers at $0EF600 are recognized when a supported ROM exposes them.",
            }));
        payload["file"] = Rel(path, root);
        payload["sha1"] = WriteJson(path, payload);
        return payload;
    }

    private static Dictionary<string, object?> ExtractLevelTilesetAssets(
        SmwRom rom,
        string root,
        int levelId,
        LevelHeader header,
        Dictionary<string, object?> paletteAssets)
    {
        var levelKey = FormatLevelId(levelId);
        var tileset = header.Tileset;
        var (vram, uploads, gfxSource) = LevelFgBgVram(rom, levelId, header);
        var map16Words = LevelMap16Words(rom, tileset);
        var key = $"level_{levelKey}_tileset{tileset}";
        var vramPath = Path.Combine(root, "tilesets", $"{key}_vram.bin");
        var atlasPath = Path.Combine(root, "tilesets", $"{key}_8x8.png");
        var map16Path = Path.Combine(root, "tilesets", $"{key}_map16_preview.png");
        var metadataPath = Path.Combine(root, "tilesets", $"{key}.json");
        var rgb = (int[][])paletteAssets["rgb888"]!;
        var previewPalette = rgb.Skip(2 * 16).Take(16).ToArray();
        if (previewPalette.Length < 16)
        {
            previewPalette = rgb.Take(16).ToArray();
        }

        var atlas = Write4BppAtlasPng(atlasPath, vram, previewPalette);
        atlas["file"] = Rel(atlasPath, root);
        var map16 = WriteMap16PreviewPng(map16Path, map16Words, vram, rgb);
        map16["file"] = Rel(map16Path, root);
        var metadata = Dict(
            ("status", "preview"),
            ("level_id", levelKey),
            ("tileset", tileset),
            ("fg_palette", header.FgPalette),
            ("palette_assets", Dict(("file", paletteAssets["file"]), ("source", paletteAssets["source"]))),
            ("palette_mapping", Dict(
                ("tile_word_palette_bits", "bits 10-12"),
                ("source", "per-level full CGRAM palette"),
                ("cgram_row_indexing", true),
                ("preview_row", 2))),
            ("gfx_source", gfxSource),
            ("map16_pointer_source", Dict(("source", "native_initialize_map16_pointers"), ("tileset", tileset))),
            ("uploads", uploads),
            ("vram", Dict(
                ("file", Rel(vramPath, root)),
                ("sha1", WriteBinary(vramPath, vram)),
                ("format", "snes_4bpp_tiles_in_level_vram_order"),
                ("tile_count", vram.Length / 32))),
            ("atlas_png", atlas),
            ("map16_preview_png", map16),
            ("notes", new[]
            {
                "Uses the foreground/background GFX upload list resolved for this level.",
                "Map16 preview renders raw Map16 tile words from the ROM and level VRAM.",
                "SNES 4bpp BG graphics do not carry a final palette by themselves; Map16/tilemap words select CGRAM rows through palette bits.",
            }));
        metadata["file"] = Rel(metadataPath, root);
        metadata["sha1"] = WriteJson(metadataPath, metadata);
        return metadata;
    }

    private static Dictionary<string, object?> ExtractLevelSpriteTilesetAssets(
        SmwRom rom,
        string root,
        int levelId,
        LevelHeader header,
        Dictionary<string, object?> paletteAssets)
    {
        var levelKey = FormatLevelId(levelId);
        var spriteGraphics = header.SpriteGraphics;
        var (vram, uploads, gfxSource) = LevelSpriteVram(rom, levelId, header);
        var key = $"level_{levelKey}_spritegfx{spriteGraphics}";
        var vramPath = Path.Combine(root, "spritesets", $"{key}_vram.bin");
        var atlasPath = Path.Combine(root, "spritesets", $"{key}_8x8.png");
        var metadataPath = Path.Combine(root, "spritesets", $"{key}.json");
        var rgb = (int[][])paletteAssets["rgb888"]!;
        var previewPalette = rgb.Skip(14 * 16).Take(16).ToArray();
        if (previewPalette.Length < 16)
        {
            previewPalette = rgb.Skip(8 * 16).Take(16).ToArray();
        }

        var atlas = Write4BppAtlasPng(atlasPath, vram, previewPalette);
        atlas["file"] = Rel(atlasPath, root);
        var metadata = Dict(
            ("status", "preview"),
            ("level_id", levelKey),
            ("sprite_graphics", spriteGraphics),
            ("sprite_palette", header.SpritePalette),
            ("palette_assets", Dict(("file", paletteAssets["file"]), ("source", paletteAssets["source"]))),
            ("palette_mapping", Dict(("source", "per-level full CGRAM palette rows 8-15"), ("preview_row", 14), ("final_oam_palette_selection_pending", true))),
            ("gfx_source", gfxSource),
            ("uploads", uploads),
            ("vram", Dict(
                ("file", Rel(vramPath, root)),
                ("sha1", WriteBinary(vramPath, vram)),
                ("format", "snes_4bpp_tiles_in_sprite_vram_order_0x6000_to_0x7fff"),
                ("tile_count", vram.Length / 32))),
            ("atlas_png", atlas),
            ("notes", new[]
            {
                "Uses the sprite GFX upload list resolved for this level.",
                "Tiles are placed in the same $6000-$7FFF sprite VRAM window used by UploadGraphicsFiles.",
            }));
        metadata["file"] = Rel(metadataPath, root);
        metadata["sha1"] = WriteJson(metadataPath, metadata);
        return metadata;
    }

    private static Dictionary<string, object?> BuildLayer2BackgroundTilemap(byte[] layer2Raw, int sourceAddress)
    {
        const int widthTiles = 32;
        const int heightTiles = 27;
        const int halfWidth = widthTiles / 2;
        const int halfSize = halfWidth * heightTiles;
        var map16Page = (sourceAddress & 0xFFFF) >= 0xE8FE ? 1 : 0;
        var placed = new List<Dictionary<string, object?>>();
        var count = Math.Min(layer2Raw.Length, widthTiles * heightTiles);

        for (var index = 0; index < count; index++)
        {
            var half = index / halfSize;
            var local = index % halfSize;
            var x = half * halfWidth + (local % halfWidth);
            var y = local / halfWidth;
            if (y >= heightTiles)
            {
                continue;
            }

            placed.Add(Dict(
                ("x", x),
                ("y", y),
                ("map16", map16Page * 0x100 + layer2Raw[index]),
                ("source", "layer2_rle_background")));
        }

        return Dict(
            ("status", "partial"),
            ("kind", "rle_background"),
            ("width_tiles", widthTiles),
            ("height_tiles", heightTiles),
            ("map16_page", map16Page),
            ("placed_tiles", placed),
            ("placed_tile_count", placed.Count),
            ("notes", new[]
            {
                "Decoded from the vanilla Layer 2 LC_RLE1 background stream.",
                "Tile bytes are ordered as the left 16x27 half followed by the right 16x27 half.",
                "This is a visual background layer; exact scrolling and BG interaction remain runtime work.",
            }));
    }

    private static Dictionary<string, object?>? ExtractLayer2BackgroundPreview(
        SmwRom rom,
        string root,
        int levelId,
        string levelKey,
        LevelHeader header,
        byte[] layer2Raw,
        string layer2Kind,
        int layer2Address,
        Dictionary<string, object?> paletteAssets)
    {
        if (layer2Kind != "rle_background")
        {
            return null;
        }

        var tilemap = BuildLayer2BackgroundTilemap(layer2Raw, layer2Address);
        var rgb = (int[][])paletteAssets["rgb888"]!;
        var (vram, _uploads, gfxSource) = LevelFgBgVram(rom, levelId, header);
        var map16Words = BackgroundMap16Words(rom);
        var previewPath = Path.Combine(root, "levels", $"level_{levelKey}_layer2_background.png");
        var tilemapPath = Path.Combine(root, "levels", $"level_{levelKey}_layer2_background.json");
        var preview = WriteLevelLayoutPreviewPng(
            previewPath,
            Convert.ToInt32(tilemap["width_tiles"], CultureInfo.InvariantCulture),
            Convert.ToInt32(tilemap["height_tiles"], CultureInfo.InvariantCulture),
            (List<Dictionary<string, object?>>)tilemap["placed_tiles"]!,
            map16Words,
            vram,
            rgb);
        preview["file"] = Rel(previewPath, root);
        tilemap["preview_png"] = preview;
        tilemap["palette_assets"] = Dict(("file", paletteAssets["file"]), ("source", paletteAssets["source"]));
        tilemap["map16_pointer_source"] = Dict(
            ("source", "native_buffer_bg_tilemap"),
            ("base_addr", "0x0D9100"),
            ("page_threshold", "0xE8FE"));
        tilemap["gfx_source"] = gfxSource;
        tilemap["file"] = Rel(tilemapPath, root);
        tilemap["sha1"] = WriteJson(tilemapPath, tilemap);
        return tilemap;
    }

    private static Dictionary<string, object?> ExtractLevelLayoutPreview(
        SmwRom rom,
        string root,
        int levelId,
        string levelKey,
        LevelHeader header,
        IReadOnlyList<LevelObject> objects,
        Dictionary<string, object?> paletteAssets)
    {
        var tilemap = BuildPartialLevelTilemap(header, objects);
        var rgb = (int[][])paletteAssets["rgb888"]!;
        var (vram, _uploads, gfxSource) = LevelFgBgVram(rom, levelId, header);
        var map16Words = LevelMap16Words(rom, header.Tileset);
        var tilemapPath = Path.Combine(root, "levels", $"level_{levelKey}_partial_tilemap.json");
        var previewPath = Path.Combine(root, "levels", $"level_{levelKey}_partial_layout.png");
        var preview = WriteLevelLayoutPreviewPng(
            previewPath,
            Convert.ToInt32(tilemap["width_tiles"], CultureInfo.InvariantCulture),
            Convert.ToInt32(tilemap["height_tiles"], CultureInfo.InvariantCulture),
            (List<Dictionary<string, object?>>)tilemap["placed_tiles"]!,
            map16Words,
            vram,
            rgb);
        preview["file"] = Rel(previewPath, root);
        tilemap["preview_png"] = preview;
        tilemap["palette_assets"] = Dict(("file", paletteAssets["file"]), ("source", paletteAssets["source"]));
        tilemap["map16_pointer_source"] = Dict(("source", "native_initialize_map16_pointers"), ("tileset", header.Tileset));
        tilemap["gfx_source"] = gfxSource;
        tilemap["file"] = Rel(tilemapPath, root);
        tilemap["sha1"] = WriteJson(tilemapPath, tilemap);
        return Dict(
            ("status", "partial"),
            ("file", Rel(tilemapPath, root)),
            ("sha1", tilemap["sha1"]),
            ("preview_png", preview),
            ("placed_tile_count", tilemap["placed_tile_count"]),
            ("notes", tilemap["notes"]));
    }

    private static Dictionary<string, object?> BuildManifest(
        SmwRom rom,
        Dictionary<string, object?> assets,
        Dictionary<string, object?> levels,
        SmwLevelIndex index)
    {
        return Dict(
            ("schema_version", SchemaVersion),
            ("importer", Dict(
                ("name", "src/SmwAssets/SmwNativeImporter.cs"),
                ("runtime", "pure_csharp"),
                ("asset_boundary", "generated data is local-only and must not be committed"),
                ("parity_oracle", "tools/smw_import.py"))),
            ("rom", Dict(
                ("file_name", rom.FileName),
                ("sha1", rom.Sha1),
                ("size", rom.Data.Length),
                ("title", SmwRomInspector.ExpectedRomLabel),
                ("headered", false))),
            ("assets", assets),
            ("level_titles", Dict(
                ("source", index.TitleSource),
                ("status", index.Status),
                ("error", index.Error),
                ("count", index.Titles.Count),
                ("titles", index.Titles.OrderBy(pair => pair.Key).ToDictionary(pair => FormatLevelId(pair.Key), pair => pair.Value, StringComparer.Ordinal)))),
            ("level_index", index.ToSerializable()),
            ("levels", levels));
    }

    private static Dictionary<string, object?> ReadExistingLevels(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!document.RootElement.TryGetProperty("levels", out var levels) ||
            levels.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        return JsonElementToObject(levels) as Dictionary<string, object?> ??
            new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static int CalculateLevelLength(SmwRom rom, int address)
    {
        var start = address;
        address = SmwRom.IncrementLoRomAddress(address, 5);
        while (true)
        {
            var b0 = rom.GetByte(address);
            address = SmwRom.IncrementLoRomAddress(address, 1);
            if (b0 == 0xFF)
            {
                break;
            }

            var b1 = rom.GetByte(address);
            var b2 = rom.GetByte(SmwRom.IncrementLoRomAddress(address, 1));
            address = SmwRom.IncrementLoRomAddress(address, 2);
            var objectId = (b1 >> 4) | ((b0 & 0x60) >> 1);
            if (objectId == 0 && b2 == 0)
            {
                address = SmwRom.IncrementLoRomAddress(address, 1);
            }
            else if (objectId is 0x22 or 0x23)
            {
                address = SmwRom.IncrementLoRomAddress(address, 1);
            }
            else if (objectId is 0x27 or 0x29)
            {
                address = SmwRom.IncrementLoRomAddress(address, 2);
            }
        }

        return LoRomDistance(start, address);
    }

    private static int CalculateSpriteDataLength(SmwRom rom, int address)
    {
        var start = address;
        address = SmwRom.IncrementLoRomAddress(address, 1);
        while (rom.GetByte(address) != 0xFF)
        {
            address = SmwRom.IncrementLoRomAddress(address, 3);
        }

        return LoRomDistance(start, SmwRom.IncrementLoRomAddress(address, 1));
    }

    private static LevelHeader DecodeLevelHeader(byte[] raw)
    {
        if (raw.Length < 5)
        {
            throw new InvalidOperationException("level stream is too short for header");
        }

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

    private static ParsedObjects ParseLevelObjects(byte[] raw, LevelHeader header, int layerIndex)
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
            if (index + 2 > raw.Length)
            {
                throw new InvalidOperationException("truncated level object stream");
            }

            var b1 = raw[index++];
            var b2 = raw[index++];
            var objectId = (b1 >> 4) | ((b0 & 0x60) >> 1);
            if ((b0 & 0x80) != 0)
            {
                screenCursor++;
            }

            var placement = DecodeObjectPlacement(b0, b1, b2, objectId, screenCursor, header.LayoutFlags, layerIndex);
            var extra = new List<int>();
            if (objectId == 0 && b2 == 0)
            {
                extra.Add(raw[index++]);
                var vanillaProperties = b1 & 0x03;
                var exitLow = extra[0];
                exits.Add(new ScreenExit(
                    Screen: b0 & 0x1F,
                    ScreenCursor: screenCursor,
                    ExitLow: exitLow,
                    RawR11: b1,
                    VanillaProperties: vanillaProperties,
                    VanillaDestinationPropertyBits: ((vanillaProperties & 1) << 8) | exitLow,
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
                Raw: [b0, b1, b2],
                ObjectId: objectId,
                SizeOrType: b2,
                Extra: extra.ToArray(),
                Placement: placement));
            if (objectId == 0 && b2 == 0x01)
            {
                screenCursor = b0 & 0x1F;
            }
            sequence++;
        }

        return new ParsedObjects(objects, exits);
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
        var highSubscreen = (adjustedB0 & 0x10) != 0;
        var yTile = adjustedB0 & 0x1F;
        return new ObjectPlacement(
            Layer: layerIndex + 1,
            ScreenCursor: screenCursor,
            ScreenIncrement: (b0 & 0x80) != 0,
            SubX: subX,
            SubY: subY,
            HighSubscreen: highSubscreen,
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
        if (raw.Length == 0)
        {
            throw new InvalidOperationException("sprite stream is too short for header");
        }

        var sprites = new List<SpriteRecord>();
        var index = 1;
        while (index < raw.Length && raw[index] != 0xFF)
        {
            if (index + 3 > raw.Length)
            {
                throw new InvalidOperationException("truncated sprite stream");
            }

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

    private static (byte[] Data, int Length) UnpackRle(SmwRom rom, int address)
    {
        var start = address;
        var output = new List<byte>();
        while (rom.GetWord(address) != 0xFFFF)
        {
            var control = rom.GetByte(address);
            address = SmwRom.IncrementLoRomAddress(address, 1);
            var count = (control & 0x7F) + 1;
            if ((control & 0x80) != 0)
            {
                var value = (byte)rom.GetByte(address);
                address = SmwRom.IncrementLoRomAddress(address, 1);
                for (var i = 0; i < count; i++)
                {
                    output.Add(value);
                }
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    output.Add((byte)rom.GetByte(SmwRom.IncrementLoRomAddress(address, i)));
                }
                address = SmwRom.IncrementLoRomAddress(address, count);
            }
        }

        return (output.ToArray(), LoRomDistance(start, SmwRom.IncrementLoRomAddress(address, 2)));
    }

    private static Dictionary<string, object?> BuildPartialLevelTilemap(LevelHeader header, IReadOnlyList<LevelObject> objects)
    {
        var widthTiles = header.WidthTiles;
        var heightTiles = Math.Max(header.HeightTiles, 32);
        var placed = new List<Dictionary<string, object?>>();
        var occupied = new Dictionary<(int X, int Y), int>();
        var unsupported = new Dictionary<string, int>(StringComparer.Ordinal);

        static bool PipeObjectShouldPreserveExistingForeground(int objectId)
        {
            return objectId is 0x0F or 0x10 or 0x1F or 0x20 or 0x39;
        }

        static int AdjustedSlopeTile(int baseTile, int? existingTile)
        {
            if (existingTile == null)
            {
                return baseTile;
            }
            return (existingTile.Value & 0x00FF) switch
            {
                0x3F => baseTile + 0x0001,
                0x01 => baseTile + 0x0003,
                0x03 => baseTile + 0x0004,
                _ => baseTile,
            };
        }

        static int AdjustedDiagonalLineTile(int baseTile, int? existingTile)
        {
            if (existingTile == null)
            {
                return baseTile;
            }
            return (existingTile.Value & 0x00FF) switch
            {
                0x25 => baseTile,
                0x3F => baseTile + 0x0001,
                _ => baseTile + 0x0002,
            };
        }

        void PlaceMap16(
            int x,
            int y,
            int map16Id,
            string source,
            bool preserveExisting = false,
            bool slopeActual = false,
            bool diagonalLine = false)
        {
            if (map16Id == 0xFFFF || x < 0 || y < 0 || x >= widthTiles || y >= heightTiles)
            {
                return;
            }

            var key = (x, y);
            if (preserveExisting && occupied.ContainsKey(key))
            {
                return;
            }

            if (slopeActual)
            {
                int? existingTile = occupied.TryGetValue(key, out var existing) ? existing : null;
                map16Id = AdjustedSlopeTile(map16Id, existingTile);
            }
            else if (diagonalLine)
            {
                int? existingTile = occupied.TryGetValue(key, out var existing) ? existing : null;
                map16Id = AdjustedDiagonalLineTile(map16Id, existingTile);
            }

            occupied[key] = map16Id;
            placed.Add(Dict(("x", x), ("y", y), ("map16", map16Id), ("source", source)));
        }

        void Place(int x, int y, int page, int low, string source, bool preserveExisting = false)
        {
            if (low == 0xFF)
            {
                return;
            }
            PlaceMap16(x, y, page * 0x100 + low, source, preserveExisting);
        }

        void PlaceRelative(
            int originX,
            int originY,
            int relX,
            int relY,
            int map16Id,
            string source,
            bool preserveExisting = false,
            bool slopeActual = false,
            bool diagonalLine = false)
        {
            PlaceMap16(originX + relX, originY + relY, map16Id, source, preserveExisting, slopeActual, diagonalLine);
        }

        void FillRect(int x, int y, int width, int height, int page, int low, string source)
        {
            for (var yy = 0; yy < height; yy++)
            {
                for (var xx = 0; xx < width; xx++)
                {
                    Place(x + xx, y + yy, page, low, source);
                }
            }
        }

        void RenderGroundEdge(int x, int y, int size)
        {
            var kind = size & 0x0F;
            var rows = (size >> 4) + 1;
            if (kind >= GroundEdgeTop.Length)
            {
                return;
            }

            PlaceMap16(x, y, GroundEdgeTop[kind], "ground_edge_top");
            if (rows <= 1)
            {
                return;
            }

            PlaceMap16(x, y + 1, GroundEdgeMiddle1[kind], "ground_edge_middle");
            for (var yy = 2; yy < rows; yy++)
            {
                PlaceMap16(x, y + yy, GroundEdgeMiddle2[kind], "ground_edge_middle");
            }
            if (GroundEdgeBottom[kind] != 0xFFFF)
            {
                PlaceMap16(x, y + rows, GroundEdgeBottom[kind], "ground_edge_bottom");
            }
        }

        void RenderSmallBush(int x, int y, int size)
        {
            var width = (size & 0x0F) + 1;
            var kind = Math.Min(size >> 4, SmallBushLeft.Length - 1);
            Place(x, y, 0, SmallBushLeft[kind], "small_bush_left");
            for (var xx = 1; xx < Math.Max(1, width - 1); xx++)
            {
                Place(x + xx, y, 0, SmallBushMiddle[kind], "small_bush_middle");
            }
            if (width > 1)
            {
                Place(x + width - 1, y, 0, SmallBushRight[kind], "small_bush_right");
            }
        }

        void RenderDiagonalPipe(LevelObject obj, int x, int y)
        {
            var rows = Math.Max(1, ObjectHeightTiles(obj.ObjectId, obj.SizeOrType));
            var preserveExisting = PipeObjectShouldPreserveExistingForeground(obj.ObjectId);
            for (var yy = 0; yy < rows; yy++)
            {
                var sourceRow = yy switch
                {
                    0 => DiagonalPipeRow0,
                    1 => DiagonalPipeRow1,
                    2 => DiagonalPipeRow2,
                    _ => DiagonalPipeRowN,
                };
                for (var xx = 0; xx < sourceRow.Length; xx++)
                {
                    PlaceRelative(x, y, xx - yy, yy, sourceRow[xx], "right_diagonal_pipe", preserveExisting);
                }
            }
            PlaceRelative(x, y, -rows, rows, 0x01EB, "right_diagonal_pipe", preserveExisting);
        }

        bool RenderSlope(int x, int y, int size)
        {
            var slopeType = (size & 0x0F) % 10;
            var rows = (size >> 4) + 1;
            var upsideDownUnits = Math.Max(size >> 4, 1);

            void PlaceActual(int relX, int relY, int map16Id, string source)
            {
                PlaceRelative(x, y, relX, relY, map16Id, source, slopeActual: true);
            }

            void PlacePlain(int relX, int relY, int map16Id, string source)
            {
                PlaceRelative(x, y, relX, relY, map16Id, source);
            }

            void Fill(int firstX, int endX, int relY, int map16Id, string source)
            {
                for (var relX = firstX; relX < endX; relX++)
                {
                    PlacePlain(relX, relY, map16Id, source);
                }
            }

            if (rows <= 0)
            {
                return false;
            }

            if (slopeType == 0)
            {
                var width = rows * 2;
                for (var yy = 0; yy < rows; yy++)
                {
                    var slopeX = width - (yy + 1) * 2;
                    PlaceActual(slopeX, yy, 0x0196, "normal_left_slope_edge");
                    PlaceActual(slopeX + 1, yy, 0x019B, "normal_left_slope_edge");
                    if (yy != 0)
                    {
                        PlacePlain(slopeX + 2, yy, 0x01DE, "normal_left_slope_surface");
                        PlacePlain(slopeX + 3, yy, 0x01E6, "normal_left_slope_surface");
                        Fill(slopeX + 4, width, yy, 0x003F, "normal_left_slope_fill");
                    }
                }
                PlacePlain(0, rows, 0x01DE, "normal_left_slope_surface");
                PlacePlain(1, rows, 0x01E6, "normal_left_slope_surface");
                Fill(2, width, rows, 0x003F, "normal_left_slope_fill");
                return true;
            }

            if (slopeType == 1)
            {
                for (var yy = 0; yy < rows; yy++)
                {
                    var slopeX = rows - 1 - yy;
                    PlaceActual(slopeX, yy, 0x01AA, "steep_left_slope_edge");
                    if (slopeX + 1 < rows)
                    {
                        PlacePlain(slopeX + 1, yy, 0x01E2, "steep_left_slope_surface");
                        Fill(slopeX + 2, rows, yy, 0x003F, "steep_left_slope_fill");
                    }
                }
                PlacePlain(0, rows, 0x01E2, "steep_left_slope_surface");
                Fill(1, rows, rows, 0x003F, "steep_left_slope_fill");
                return true;
            }

            if (slopeType == 2)
            {
                var actual = new[] { 0x016E, 0x0173, 0x0178, 0x017D };
                var assist = new[] { 0x01D8, 0x01DA, 0x01E6, 0x01E6 };
                var width = rows * actual.Length;
                for (var yy = 0; yy < rows; yy++)
                {
                    var slopeX = width - (yy + 1) * actual.Length;
                    for (var xx = 0; xx < actual.Length; xx++)
                    {
                        PlaceActual(slopeX + xx, yy, actual[xx], "gradual_left_slope_edge");
                    }
                    if (yy != 0)
                    {
                        for (var xx = 0; xx < assist.Length; xx++)
                        {
                            PlacePlain(slopeX + actual.Length + xx, yy, assist[xx], "gradual_left_slope_surface");
                        }
                        Fill(slopeX + actual.Length + assist.Length, width, yy, 0x003F, "gradual_left_slope_fill");
                    }
                }
                for (var xx = 0; xx < assist.Length; xx++)
                {
                    PlacePlain(xx, rows, assist[xx], "gradual_left_slope_surface");
                }
                Fill(assist.Length, width, rows, 0x003F, "gradual_left_slope_fill");
                return true;
            }

            if (slopeType == 3)
            {
                var actual = new[] { 0x01A0, 0x01A5 };
                var assist = new[] { 0x01E6, 0x01E0 };
                var width = rows * 2;
                for (var yy = 0; yy < rows; yy++)
                {
                    Fill(0, yy * 2, yy, 0x003F, "normal_right_slope_fill");
                    if (yy != 0)
                    {
                        PlacePlain(yy * 2 - 2, yy, assist[0], "normal_right_slope_surface");
                        PlacePlain(yy * 2 - 1, yy, assist[1], "normal_right_slope_surface");
                    }
                    PlaceActual(yy * 2, yy, actual[0], "normal_right_slope_edge");
                    PlaceActual(yy * 2 + 1, yy, actual[1], "normal_right_slope_edge");
                }
                Fill(0, width - 2, rows, 0x003F, "normal_right_slope_fill");
                PlacePlain(width - 2, rows, assist[0], "normal_right_slope_surface");
                PlacePlain(width - 1, rows, assist[1], "normal_right_slope_surface");
                return true;
            }

            if (slopeType == 4)
            {
                PlaceActual(0, 0, 0x01AF, "steep_right_slope_edge");
                for (var yy = 1; yy < rows; yy++)
                {
                    Fill(0, yy - 1, yy, 0x003F, "steep_right_slope_fill");
                    PlacePlain(yy - 1, yy, 0x01E4, "steep_right_slope_surface");
                    PlaceActual(yy, yy, 0x01AF, "steep_right_slope_edge");
                }
                Fill(0, rows - 1, rows, 0x003F, "steep_right_slope_fill");
                PlacePlain(rows - 1, rows, 0x01E4, "steep_right_slope_surface");
                return true;
            }

            if (slopeType == 5)
            {
                var actual = new[] { 0x0182, 0x0187, 0x018C, 0x0191 };
                var assist = new[] { 0x01E6, 0x01E6, 0x01DB, 0x01DC };
                var width = rows * actual.Length;
                for (var yy = 0; yy < rows; yy++)
                {
                    Fill(0, yy * actual.Length, yy, 0x003F, "gradual_right_slope_fill");
                    if (yy != 0)
                    {
                        var assistX = (yy - 1) * actual.Length;
                        for (var xx = 0; xx < assist.Length; xx++)
                        {
                            PlacePlain(assistX + xx, yy, assist[xx], "gradual_right_slope_surface");
                        }
                    }
                    var slopeX = yy * actual.Length;
                    for (var xx = 0; xx < actual.Length; xx++)
                    {
                        PlaceActual(slopeX + xx, yy, actual[xx], "gradual_right_slope_edge");
                    }
                }
                Fill(0, width - assist.Length, rows, 0x003F, "gradual_right_slope_fill");
                for (var xx = 0; xx < assist.Length; xx++)
                {
                    PlacePlain(width - assist.Length + xx, rows, assist[xx], "gradual_right_slope_surface");
                }
                return true;
            }

            if (slopeType == 6)
            {
                var width = upsideDownUnits * 2;
                for (var yy = 0; yy < upsideDownUnits + 1; yy++)
                {
                    var rowStart = yy == 0 ? 0 : (yy - 1) * 2;
                    if (rowStart >= width)
                    {
                        break;
                    }
                    if (yy != 0)
                    {
                        PlacePlain(rowStart, yy, 0x01C6, "upside_down_normal_left_slope_surface");
                        PlacePlain(rowStart + 1, yy, 0x01C7, "upside_down_normal_left_slope_surface");
                    }
                    var assistX = rowStart + (yy == 0 ? 0 : 2);
                    if (assistX + 1 < width)
                    {
                        PlacePlain(assistX, yy, 0x01EE, "upside_down_normal_left_slope_edge");
                        PlacePlain(assistX + 1, yy, 0x01F0, "upside_down_normal_left_slope_edge");
                        Fill(assistX + 2, width, yy, 0x0165, "upside_down_normal_left_slope_fill");
                    }
                }
                return true;
            }

            if (slopeType == 7)
            {
                var width = upsideDownUnits * 2;
                for (var yy = 0; yy < upsideDownUnits + 1; yy++)
                {
                    var rowWidth = yy == 0 ? width : width - (yy - 1) * 2;
                    if (rowWidth == 0)
                    {
                        continue;
                    }
                    if (yy == 0)
                    {
                        Fill(0, rowWidth > 2 ? rowWidth - 2 : 0, yy, 0x0165, "upside_down_normal_right_slope_fill");
                        if (rowWidth >= 2)
                        {
                            PlacePlain(rowWidth - 2, yy, 0x01F0, "upside_down_normal_right_slope_edge");
                            PlacePlain(rowWidth - 1, yy, 0x01EF, "upside_down_normal_right_slope_edge");
                        }
                    }
                    else
                    {
                        Fill(0, rowWidth > 4 ? rowWidth - 4 : 0, yy, 0x0165, "upside_down_normal_right_slope_fill");
                        if (rowWidth >= 4)
                        {
                            PlacePlain(rowWidth - 4, yy, 0x01F0, "upside_down_normal_right_slope_edge");
                            PlacePlain(rowWidth - 3, yy, 0x01EF, "upside_down_normal_right_slope_edge");
                        }
                        if (rowWidth >= 2)
                        {
                            PlacePlain(rowWidth - 2, yy, 0x01C8, "upside_down_normal_right_slope_surface");
                            PlacePlain(rowWidth - 1, yy, 0x01C9, "upside_down_normal_right_slope_surface");
                        }
                    }
                }
                return true;
            }

            if (slopeType == 8)
            {
                var width = upsideDownUnits;
                for (var yy = 0; yy < upsideDownUnits + 1; yy++)
                {
                    var rowStart = yy == 0 ? 0 : yy - 1;
                    if (rowStart >= width)
                    {
                        break;
                    }
                    if (yy != 0)
                    {
                        PlacePlain(rowStart, yy, 0x01C4, "upside_down_steep_left_slope_surface");
                    }
                    var assistX = rowStart + (yy == 0 ? 0 : 1);
                    if (assistX < width)
                    {
                        PlacePlain(assistX, yy, 0x01EC, "upside_down_steep_left_slope_edge");
                        Fill(assistX + 1, width, yy, 0x0165, "upside_down_steep_left_slope_fill");
                    }
                }
                return true;
            }

            if (slopeType == 9)
            {
                var width = upsideDownUnits;
                for (var yy = 0; yy < upsideDownUnits + 1; yy++)
                {
                    var rowWidth = yy == 0 ? width : width - (yy - 1);
                    if (rowWidth == 0)
                    {
                        continue;
                    }
                    if (yy == 0)
                    {
                        Fill(0, rowWidth > 1 ? rowWidth - 1 : 0, yy, 0x0165, "upside_down_steep_right_slope_fill");
                        PlacePlain(rowWidth - 1, yy, 0x01ED, "upside_down_steep_right_slope_edge");
                    }
                    else
                    {
                        Fill(0, rowWidth > 2 ? rowWidth - 2 : 0, yy, 0x0165, "upside_down_steep_right_slope_fill");
                        if (rowWidth >= 2)
                        {
                            PlacePlain(rowWidth - 2, yy, 0x01ED, "upside_down_steep_right_slope_edge");
                        }
                        PlacePlain(rowWidth - 1, yy, 0x01C5, "upside_down_steep_right_slope_surface");
                    }
                }
                return true;
            }

            return false;
        }

        void RenderLeftDiagonalLedge(int x, int y, int size)
        {
            var diagonalRows = (size & 0x0F) + 1;
            var fillRows = (size >> 4) + 1;
            PlaceRelative(x, y, 0, 0, 0x01AA, "left_diagonal_ledge_edge", slopeActual: true);
            PlaceRelative(x, y, 1, 0, 0x00A1, "left_diagonal_ledge_top", diagonalLine: true);

            var rowWidth = 3;
            for (var yy = 1; yy < diagonalRows; yy++)
            {
                var rowLeft = -yy;
                PlaceRelative(x, y, rowLeft, yy, 0x01AA, "left_diagonal_ledge_edge", slopeActual: true);
                PlaceRelative(x, y, rowLeft + 1, yy, 0x01E2, "left_diagonal_ledge_assist");
                for (var xx = 2; xx < rowWidth; xx++)
                {
                    PlaceRelative(x, y, rowLeft + xx, yy, 0x003F, "left_diagonal_ledge_fill");
                }
                PlaceRelative(x, y, rowLeft + rowWidth, yy, 0x00A6, "left_diagonal_ledge_edge", diagonalLine: true);
                rowWidth += 2;
            }

            var bottomY = diagonalRows;
            var bottomLeft = -(diagonalRows - 1);
            var lowerRowWidth = rowWidth > 0 ? rowWidth - 1 : rowWidth;
            PlaceRelative(x, y, bottomLeft, bottomY, 0x01F7, "left_diagonal_ledge_bottom", slopeActual: true);
            for (var xx = 1; xx < lowerRowWidth; xx++)
            {
                PlaceRelative(x, y, bottomLeft + xx, bottomY, 0x003F, "left_diagonal_ledge_fill");
            }
            PlaceRelative(x, y, bottomLeft + lowerRowWidth, bottomY, 0x00A6, "left_diagonal_ledge_edge", diagonalLine: true);

            for (var yy = 1; yy < fillRows; yy++)
            {
                var fillY = bottomY + yy;
                var fillLeft = bottomLeft + yy;
                PlaceRelative(x, y, fillLeft, fillY, 0x00A3, "left_diagonal_ledge_fill", diagonalLine: true);
                for (var xx = 1; xx < lowerRowWidth; xx++)
                {
                    PlaceRelative(x, y, fillLeft + xx, fillY, 0x003F, "left_diagonal_ledge_fill");
                }
                PlaceRelative(x, y, fillLeft + lowerRowWidth, fillY, 0x00A6, "left_diagonal_ledge_edge", diagonalLine: true);
            }
        }

        void RenderRightDiagonalLedge(int x, int y, int size)
        {
            var diagonalRows = (size & 0x0F) + 1;
            var fillRows = (size >> 4) + 1;
            PlaceRelative(x, y, 0, 0, 0x00AF, "right_diagonal_ledge_edge", diagonalLine: true);
            PlaceRelative(x, y, 1, 0, 0x01AF, "right_diagonal_ledge_edge", slopeActual: true);

            for (var yy = 1; yy < diagonalRows; yy++)
            {
                var rowLeft = -yy;
                PlaceRelative(x, y, rowLeft, yy, 0x00A9, "right_diagonal_ledge_edge", diagonalLine: true);
                for (var relX = rowLeft + 1; relX < yy; relX++)
                {
                    PlaceRelative(x, y, relX, yy, 0x003F, "right_diagonal_ledge_fill");
                }
                PlaceRelative(x, y, yy, yy, 0x01E4, "right_diagonal_ledge_assist");
                PlaceRelative(x, y, yy + 1, yy, 0x01AF, "right_diagonal_ledge_edge", slopeActual: true);
            }

            var bottomY = diagonalRows;
            var bottomLeft = -diagonalRows;
            var bottomRight = diagonalRows;
            PlaceRelative(x, y, bottomLeft, bottomY, 0x00A9, "right_diagonal_ledge_edge", diagonalLine: true);
            for (var relX = bottomLeft + 1; relX < bottomRight; relX++)
            {
                PlaceRelative(x, y, relX, bottomY, 0x003F, "right_diagonal_ledge_fill");
            }
            PlaceRelative(x, y, bottomRight, bottomY, 0x01F9, "right_diagonal_ledge_bottom", diagonalLine: true);

            for (var yy = 1; yy < fillRows; yy++)
            {
                var fillY = bottomY + yy;
                var fillLeft = bottomLeft - yy;
                var fillRight = bottomRight - yy;
                PlaceRelative(x, y, fillLeft, fillY, 0x00A9, "right_diagonal_ledge_edge", diagonalLine: true);
                for (var relX = fillLeft + 1; relX < fillRight; relX++)
                {
                    PlaceRelative(x, y, relX, fillY, 0x003F, "right_diagonal_ledge_fill");
                }
                PlaceRelative(x, y, fillRight, fillY, 0x00AC, "right_diagonal_ledge_edge", diagonalLine: true);
            }
        }

        void RenderRopeMushroomTop(int x, int y, int size)
        {
            var width = (size & 0x0F) + 1;
            Place(x, y, 1, 0x07, "rope_mushroom_top_left");
            for (var xx = 1; xx < width - 1; xx++)
            {
                Place(x + xx, y, 1, 0x08, "rope_mushroom_top_middle");
            }
            if (width > 1)
            {
                Place(x + width - 1, y, 1, 0x09, "rope_mushroom_top_right");
            }
        }

        void RenderRopeMushroomColumn(int x, int y, int size)
        {
            var width = (size & 0x0F) + 1;
            var rows = (size >> 4) + 1;
            for (var yy = 0; yy < rows; yy++)
            {
                Place(x, y + yy, 0, 0x73, "rope_mushroom_column_left");
                for (var xx = 1; xx < width - 1; xx++)
                {
                    Place(x + xx, y + yy, 0, 0x74, "rope_mushroom_column_middle");
                }
                if (width > 1)
                {
                    Place(x + width - 1, y + yy, 0, 0x75, "rope_mushroom_column_right");
                }
            }
        }

        void RenderUndergroundCeilingLedge(int x, int y, int size)
        {
            var width = (size & 0x0F) + 1;
            var fillerRows = size >> 4;
            for (var yy = 0; yy < fillerRows; yy++)
            {
                FillRect(x, y + yy, width, 1, 1, 0x65, "underground_ceiling_ledge_fill");
            }
            FillRect(x, y + fillerRows, width, 1, 1, 0x4E, "underground_ceiling_ledge_bottom");
        }

        void RenderUndergroundCeilingEdge(int x, int y, int size)
        {
            var edgeKind = size & 0x0F;
            var rows = size >> 4;
            int[] topTiles = [0x50, 0x50, 0x51, 0x51];
            int[] bottomTiles = [0x4D, 0x50, 0x4F, 0x51];
            if (edgeKind >= topTiles.Length)
            {
                return;
            }
            for (var yy = 0; yy < rows; yy++)
            {
                Place(x, y + yy, 1, topTiles[edgeKind], "underground_ceiling_edge");
            }
            Place(x, y + rows, 1, bottomTiles[edgeKind], "underground_ceiling_edge_bottom");
        }

        foreach (var obj in objects)
        {
            var x = obj.Placement.XTile;
            var y = obj.Placement.YTile;
            var size = obj.SizeOrType;
            var tileset = header.Tileset;
            var width = ObjectWidthTiles(obj.ObjectId, size);
            var height = ObjectHeightTiles(obj.ObjectId, size);
            var preservePipe = PipeObjectShouldPreserveExistingForeground(obj.ObjectId);

            if (obj.ObjectId is >= 1 and <= 0x0E)
            {
                var index = obj.ObjectId - 1;
                var page = index >= 7 ? 1 : 0;
                FillRect(x, y, width, height, page, GenericRepeatedTiles[index], $"std_generic_{index:02X}");
            }
            else if (obj.ObjectId == 0x0F)
            {
                var pipeType = size & 0x0F;
                if (pipeType >= VerticalPipeTopLeft.Length)
                {
                    pipeType = 0;
                }
                if (pipeType == 5)
                {
                    for (var yy = 0; yy < height; yy++)
                    {
                        Place(x, y + yy, 1, 0x68, "skinny_vertical_pipe_left", preservePipe);
                        Place(x + 1, y + yy, 1, 0x69, "skinny_vertical_pipe_right", preservePipe);
                    }
                    continue;
                }
                if (pipeType < 3)
                {
                    Place(x, y, 1, VerticalPipeTopLeft[pipeType], "vertical_pipe_top_left", preservePipe);
                    Place(x + 1, y, 1, VerticalPipeTopRight[pipeType], "vertical_pipe_top_right", preservePipe);
                }
                for (var yy = pipeType < 3 ? 1 : 0; yy < height; yy++)
                {
                    Place(x, y + yy, 1, 0x35, "vertical_pipe_shaft_left", preservePipe);
                    Place(x + 1, y + yy, 1, 0x36, "vertical_pipe_shaft_right", preservePipe);
                }
                if (pipeType is >= 2 and < 5)
                {
                    Place(x, y + height - 1, 1, VerticalPipeBottomLeft[pipeType], "vertical_pipe_bottom_left", preservePipe);
                    Place(x + 1, y + height - 1, 1, VerticalPipeBottomRight[pipeType], "vertical_pipe_bottom_right", preservePipe);
                }
            }
            else if (obj.ObjectId == 0x10)
            {
                var pipeType = (size >> 4) & 0x0F;
                var endOnRight = pipeType >= 4;
                for (var row = 0; row < 2; row++)
                {
                    var tileKind = Math.Min(pipeType + row, HorizontalPipeEnd.Length - 1);
                    for (var xx = 0; xx < width; xx++)
                    {
                        var isEnd = (!endOnRight && xx == 0) || (endOnRight && xx + 1 == width);
                        Place(x + xx, y + row, 1, isEnd ? HorizontalPipeEnd[tileKind] : HorizontalPipeShaft[tileKind],
                            isEnd ? "horizontal_pipe_end" : "horizontal_pipe_shaft", preservePipe);
                    }
                }
            }
            else if (obj.ObjectId == 0x12)
            {
                if (!RenderSlope(x, y, size))
                {
                    var key = $"{obj.ObjectId:X2}";
                    unsupported[key] = unsupported.GetValueOrDefault(key) + 1;
                }
            }
            else if (obj.ObjectId == 0x13)
            {
                RenderGroundEdge(x, y, size);
            }
            else if (obj.ObjectId == 0x14)
            {
                var ledgeWidth = (size & 0x0F) + 1;
                var lowerRows = size >> 4;
                for (var xx = 0; xx < ledgeWidth; xx++)
                {
                    Place(x + xx, y, 1, 0x00, "standard_ledge_top");
                }
                for (var yy = 1; yy <= lowerRows; yy++)
                {
                    for (var xx = 0; xx < ledgeWidth; xx++)
                    {
                        Place(x + xx, y + yy, 0, 0x3F, "standard_ledge_fill");
                    }
                }
            }
            else if (obj.ObjectId == 0x15)
            {
                var rows = Math.Max(1, size >> 4);
                var goal = (size & 0x0F) != 0;
                var top = goal ? GoalTop : MidwayTop;
                var middle = goal ? GoalMiddle : MidwayMiddle;
                var bottom = goal ? GoalBottom : MidwayBottom;
                for (var xx = 0; xx < 3; xx++)
                {
                    Place(x + xx, y, 0, top[xx], goal ? "goal_top" : "midway_top");
                    for (var yy = 1; yy < rows; yy++)
                    {
                        Place(x + xx, y + yy, 0, middle[xx], goal ? "goal_middle" : "midway_middle");
                    }
                    Place(x + xx, y + rows, 0, bottom[xx], goal ? "goal_bottom" : "midway_bottom");
                }
            }
            else if (obj.ObjectId == 0x17)
            {
                var tileKind = Math.Min(size >> 4, RopeCloudLine.Length - 1);
                FillRect(x, y, width, 1, 1, RopeCloudLine[tileKind], "rope_cloud_line");
            }
            else if (obj.ObjectId == 0x1F)
            {
                var rows = Math.Max(1, size >> 4);
                Place(x, y, 1, 0x53, "skinny_vertical_top", preservePipe);
                for (var yy = 1; yy < rows; yy++)
                {
                    Place(x, y + yy, 1, 0x54, "skinny_vertical_middle", preservePipe);
                }
                Place(x, y + rows, 1, 0x55, "skinny_vertical_bottom", preservePipe);
            }
            else if (obj.ObjectId == 0x21)
            {
                for (var xx = 0; xx < size + 1; xx++)
                {
                    Place(x + xx, y, 1, 0x00, "wide_scale_ledge_top");
                    Place(x + xx, y + 1, 0, 0x3F, "wide_scale_ledge_fill");
                    Place(x + xx, y + 2, 0, 0x3F, "wide_scale_ledge_fill");
                }
            }
            else if (obj.ObjectId == 0x39)
            {
                RenderDiagonalPipe(obj, x, y);
            }
            else if (obj.ObjectId == 0x3A)
            {
                RenderLeftDiagonalLedge(x, y, size);
            }
            else if (obj.ObjectId == 0x3B)
            {
                RenderRightDiagonalLedge(x, y, size);
            }
            else if (obj.ObjectId == 0x3C && tileset is 2 or 6 or 8)
            {
                RenderRopeMushroomTop(x, y, size);
            }
            else if (obj.ObjectId == 0x3D && tileset is 2 or 6 or 8)
            {
                RenderRopeMushroomColumn(x, y, size);
            }
            else if (obj.ObjectId == 0x3D && tileset == 3)
            {
                RenderUndergroundCeilingLedge(x, y, size);
            }
            else if (obj.ObjectId == 0x3E && tileset == 3)
            {
                RenderUndergroundCeilingEdge(x, y, size);
            }
            else if (obj.ObjectId == 0x3F)
            {
                RenderSmallBush(x, y, size);
            }
            else if (obj.ObjectId == 0x00 && size == 0x00)
            {
            }
            else if (obj.ObjectId == 0x00 && RenderExtendedObject(x, y, size, (px, py, map16, source, preserveExisting) => PlaceMap16(px, py, map16, source, preserveExisting)))
            {
            }
            else if (obj.ObjectId is 0x27 or 0x29 && obj.Extra.Length >= 2)
            {
                var baseTile = obj.Extra[0] | ((obj.Extra[1] & 0x3F) << 8);
                var pageOffset = obj.ObjectId == 0x29 ? 0x4000 : 0;
                if (baseTile < 0x200 || obj.ObjectId == 0x29)
                {
                    for (var yy = 0; yy < height; yy++)
                    {
                        for (var xx = 0; xx < width; xx++)
                        {
                            PlaceMap16(x + xx, y + yy, pageOffset + baseTile + yy * width + xx, "direct_map16");
                        }
                    }
                }
            }
            else
            {
                var key = $"{obj.ObjectId:X2}";
                unsupported[key] = unsupported.GetValueOrDefault(key) + 1;
            }
        }

        return Dict(
            ("status", "partial"),
            ("width_tiles", widthTiles),
            ("height_tiles", heightTiles),
            ("placed_tiles", placed),
            ("placed_tile_count", placed.Count),
            ("unsupported_object_counts", unsupported),
            ("notes", new[]
            {
                "This tilemap is generated by the pure C# runtime importer.",
                "It covers common vanilla object families needed for runtime playback, but it is not yet a complete 1:1 port of ProcessStandardAndTilesetSpecificObjects.",
            }));
    }

    private static bool RenderExtendedObject(int x, int y, int size, Action<int, int, int, string, bool> place)
    {
        switch (size)
        {
            case 0x01:
                return true;
            case 0x17:
                place(x, y, 0x012D, "extended_green_star_block", false);
                return true;
            case 0x18:
                place(x, y, 0x006E, "extended_3up_moon", false);
                return true;
            case 0x2B:
                place(x, y, 0x011A, "extended_invisible_1up", false);
                return true;
            case >= 0x30 and <= 0x38:
                var questionSource = size switch
                {
                    0x30 => "extended_question_block_flower",
                    0x31 => "extended_question_block_feather",
                    0x32 => "extended_question_block_star",
                    0x33 => "extended_question_block_coin",
                    0x34 => "extended_question_block_multi_coin",
                    0x35 => "extended_question_block_special",
                    0x36 => "extended_question_block_yoshi_1up",
                    0x37 => "extended_question_block_37",
                    _ => "extended_question_block_38",
                };
                place(x, y, 0x0124, questionSource, false);
                return true;
            case 0x41:
                place(x, y, 0x002D, "yoshi_coin_top", false);
                place(x, y + 1, 0x002E, "yoshi_coin_bottom", false);
                return true;
            case 0x46:
                if (x > 0)
                {
                    place(x - 1, y, 0x0035, "extended_midway_bar", false);
                }
                place(x, y, 0x0038, "extended_midway_bar", false);
                return true;
            case 0x47:
                place(x, y, 0x001F, "extended_yellow_question_top", false);
                place(x, y + 1, 0x0020, "extended_yellow_question_bottom", false);
                return true;
            case 0x48:
                place(x, y, 0x0027, "extended_green_question_top", false);
                place(x, y + 1, 0x0028, "extended_green_question_bottom", false);
                return true;
            case 0x86:
                place(x, y, 0x0066, "extended_goal_marker", false);
                place(x + 1, y, 0x0067, "extended_goal_marker", false);
                place(x, y + 1, 0x0068, "extended_goal_marker", false);
                place(x + 1, y + 1, 0x0069, "extended_goal_marker", false);
                return true;
            case 0x87:
                place(x, y, 0x006A, "extended_green_switch_block_entry", false);
                return true;
            case >= 0x8A and <= 0x8D:
                var switchTiles = size switch
                {
                    0x8A => new[] { 0x00EC, 0x00ED, 0x00EE, 0x00EF },
                    0x8B => new[] { 0x00F0, 0x00F1, 0x00F2, 0x00F3 },
                    0x8C => new[] { 0x00F4, 0x00F5, 0x00F6, 0x00F7 },
                    _ => new[] { 0x00F8, 0x00F9, 0x00FA, 0x00FB },
                };
                place(x, y, switchTiles[0], "extended_switch", false);
                place(x + 1, y, switchTiles[1], "extended_switch", false);
                place(x, y + 1, switchTiles[2], "extended_switch", false);
                place(x + 1, y + 1, switchTiles[3], "extended_switch", false);
                return true;
            case 0x8E:
                place(x, y, 0x006B, "extended_yellow_switch_block", false);
                return true;
            case 0x90:
                int[] bossDoor = [0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009C];
                for (var index = 0; index < bossDoor.Length; index++)
                {
                    place(x + index % 2, y + index / 2, bossDoor[index], "extended_large_boss_door", false);
                }
                return true;
        }

        var generic = GenericExtendedObjectTile(size);
        if (generic != null)
        {
            place(x, y, generic.Value, "extended_generic", false);
            return true;
        }

        return false;
    }

    private static int? GenericExtendedObjectTile(int extendedType)
    {
        if (extendedType < 0x10)
        {
            return null;
        }

        var index = extendedType - 0x10;
        if (index < 0 || index >= GenericExtendedObjectTiles.Length)
        {
            return null;
        }

        return (index >= 19 ? 0x0100 : 0) | GenericExtendedObjectTiles[index];
    }

    private static int ObjectWidthTiles(int objectId, int sizeOrType)
    {
        var slopeUnits = sizeOrType >> 4;
        var slopeSize = slopeUnits + 1;
        var slopeType = (sizeOrType & 0x0F) % 10;
        if (objectId == 0)
        {
            return sizeOrType == 0x46 ? 2 : 1;
        }
        if (objectId == 0x0F)
        {
            return 2;
        }
        if (objectId == 0x10)
        {
            return (sizeOrType & 0x0F) + 2;
        }
        if (objectId is 0x11 or 0x13)
        {
            return 1;
        }
        if (objectId == 0x12)
        {
            if (slopeType is 1 or 4)
            {
                return slopeSize;
            }
            if (slopeType is 0 or 3)
            {
                return slopeSize * 2;
            }
            if (slopeType is 2 or 5)
            {
                return slopeSize * 4;
            }
            if (slopeType is 6 or 7)
            {
                return Math.Max(slopeUnits, 1) * 2;
            }
            if (slopeType is 8 or 9)
            {
                return Math.Max(slopeUnits, 1);
            }
        }
        if (objectId == 0x1E)
        {
            return 1;
        }
        if (objectId == 0x21)
        {
            return Math.Min(sizeOrType + 1, 0xFF);
        }
        if (objectId is 0x3A or 0x3B)
        {
            var diagonal = (sizeOrType & 0x0F) + 1;
            var fill = (sizeOrType >> 4) + 1;
            return Math.Min(diagonal * 2 + fill, 0xFF);
        }
        if (objectId == 0x39)
        {
            return Math.Min(2 + 2 * (sizeOrType >> 4), 5);
        }
        return (sizeOrType & 0x0F) + 1;
    }

    private static int ObjectHeightTiles(int objectId, int sizeOrType)
    {
        if (objectId == 0)
        {
            return sizeOrType == 0x41 ? 2 : 1;
        }
        if (objectId == 0x0F)
        {
            var pipeType = sizeOrType & 0x0F;
            var size = sizeOrType >> 4;
            if (pipeType == 5)
            {
                return size + 1;
            }
            if (pipeType >= 2)
            {
                return Math.Max(2, size + 1);
            }
            return size + 2;
        }
        if (objectId == 0x10)
        {
            return 2;
        }
        if (objectId == 0x13 && (sizeOrType & 0x0F) >= 0x0B)
        {
            return 2;
        }
        if (objectId is 0x17 or 0x20)
        {
            return 1;
        }
        if (objectId == 0x1C)
        {
            return 2;
        }
        if (objectId == 0x12)
        {
            var slopeType = (sizeOrType & 0x0F) % 10;
            var slopeUnits = sizeOrType >> 4;
            if (slopeType >= 6)
            {
                return Math.Max(slopeUnits, 1) + 1;
            }
            return slopeUnits + 2;
        }
        if (objectId == 0x21)
        {
            return 3;
        }
        if (objectId == 0x3F)
        {
            return 1;
        }
        if (objectId is 0x3A or 0x3B)
        {
            return (sizeOrType & 0x0F) + (sizeOrType >> 4) + 2;
        }
        return (sizeOrType >> 4) + 1;
    }

    private static (string Source, int BackAreaColor, int[] Words) BuildLevelPaletteWords(SmwRom rom, int levelId, LevelHeader header)
    {
        var custom = ReadLevelCustomPalette(rom, levelId);
        if (custom != null)
        {
            return ("lunar_magic_custom_palette", custom.Value.BackAreaColor, custom.Value.Words);
        }

        var palette = new int[256];
        for (var row = 0; row < 16; row++)
        {
            palette[row * 16] = PaletteBlack;
            palette[row * 16 + 1] = PaletteWhite;
        }

        var backAreaColor = rom.GetWord(BackAreaColorAddress + header.BackgroundColor * 2);
        var bgWords = rom.GetWords(BgPaletteAddress + header.BgPalette * 0x18, 12);
        CopyPaletteWords(palette, 0, 2, bgWords.Take(6));
        CopyPaletteWords(palette, 1, 2, bgWords.Skip(6));
        var fgWords = rom.GetWords(FgPaletteAddress + header.FgPalette * 0x18, 12);
        CopyPaletteWords(palette, 2, 2, fgWords.Take(6));
        CopyPaletteWords(palette, 3, 2, fgWords.Skip(6));
        for (var row = 4; row < 14; row++)
        {
            CopyPaletteWords(palette, row, 2, rom.GetWords(ObjectPaletteAddress + (row - 4) * 0x0C, 6));
        }
        CopyPaletteWords(palette, 8, 6, rom.GetWords(PlayerPaletteAddress, 10));
        var spriteWords = rom.GetWords(SpritePaletteAddress + header.SpritePalette * 0x18, 12);
        CopyPaletteWords(palette, 14, 2, spriteWords.Take(6));
        CopyPaletteWords(palette, 15, 2, spriteWords.Skip(6));
        for (var row = 0; row < 2; row++)
        {
            CopyPaletteWords(palette, row, 8, rom.GetWords(Layer3PaletteAddress + row * 0x10, 8));
        }
        for (var offset = 0; offset < 3; offset++)
        {
            var berryWords = rom.GetWords(BerryPaletteAddress + offset * 0x0E, 7);
            CopyPaletteWords(palette, offset + 2, 9, berryWords);
            CopyPaletteWords(palette, offset + 9, 9, berryWords);
        }
        palette[6 * 16 + 4] = rom.GetWord(AnimatedColorAddress);
        return ("vanilla_header_tables", backAreaColor, palette);
    }

    private static (int BackAreaColor, int[] Words)? ReadLevelCustomPalette(SmwRom rom, int levelId)
    {
        if (!rom.HasLoRomRange(LmCustomPaletteHijackAddress, 4) ||
            rom.GetByte(LmCustomPaletteHijackAddress) != 0x22 ||
            rom.Get24(LmCustomPaletteHijackAddress + 1) != LmCustomPaletteRoutineAddress)
        {
            return null;
        }

        var pointerAddress = LmCustomPalettePointerTable + levelId * 3;
        if (!rom.HasLoRomRange(pointerAddress, 3))
        {
            return null;
        }

        var pointer = rom.Get24(pointerAddress);
        if (pointer is 0x000000 or 0xFFFFFF || !rom.HasLoRomRange(pointer, 0x202))
        {
            return null;
        }

        var words = rom.GetWords(pointer, 0x101);
        return (words[0], words.Skip(1).ToArray());
    }

    private static int[] BuildPlayerSpritePaletteWords(SmwRom rom, int player)
    {
        var palette = Enumerable.Repeat(PaletteBlack, 16).ToArray();
        palette[0] = PaletteBlack;
        palette[1] = PaletteWhite;
        CopyPaletteWords(palette, 0, 2, rom.GetWords(ObjectPaletteAddress + 4 * 0x0C, 6));
        CopyPaletteWords(palette, 0, 6, rom.GetWords(PlayerPaletteAddress + Math.Clamp(player, 0, 3) * 0x14, 10));
        return palette;
    }

    private static void CopyPaletteWords(int[] target, int row, int color, IEnumerable<int> words)
    {
        var index = row * 16 + color;
        foreach (var word in words)
        {
            if (index >= 0 && index < target.Length)
            {
                target[index] = word;
            }
            index++;
        }
    }

    private static int[][] SnesWordsToRgb(IReadOnlyList<int> words)
    {
        var colors = new int[words.Count][];
        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];
            var r5 = word & 0x1F;
            var g5 = (word >> 5) & 0x1F;
            var b5 = (word >> 10) & 0x1F;
            colors[i] = [(r5 << 3) | (r5 >> 2), (g5 << 3) | (g5 >> 2), (b5 << 3) | (b5 >> 2)];
        }

        return colors;
    }

    private static int[] LevelMap16Words(SmwRom rom, int tileset)
    {
        if (tileset < 0 || tileset >= TilesetMap16Pointers.Length)
        {
            throw new InvalidOperationException($"Unsupported Map16 tileset index: {tileset}");
        }

        var pointers = new List<int>(0x200);
        var globalPointer = 0x8000;
        var tilesetPointer = TilesetMap16Pointers[tileset];
        foreach (var mask in Map16PointerMasks)
        {
            var bits = mask;
            for (var i = 0; i < 8; i++)
            {
                if ((bits & 0x80) != 0)
                {
                    pointers.Add(globalPointer);
                    globalPointer += 8;
                }
                else
                {
                    pointers.Add(tilesetPointer);
                    tilesetPointer += 8;
                }

                bits = (bits << 1) & 0xFF;
            }
        }

        if (tileset is 0 or 7)
        {
            var overridePointer = 0x8A70;
            for (var index = 452; index < 456; index++)
            {
                pointers[index] = overridePointer;
                overridePointer += 8;
            }
            for (var index = 492; index < 496; index++)
            {
                pointers[index] = overridePointer;
                overridePointer += 8;
            }
        }

        var words = new int[pointers.Count * 4];
        for (var i = 0; i < pointers.Count; i++)
        {
            var tileWords = rom.GetWords(0x0D0000 | pointers[i], 4);
            Array.Copy(tileWords, 0, words, i * 4, 4);
        }

        return words;
    }

    private static int[] BackgroundMap16Words(SmwRom rom)
    {
        var words = new int[0x200 * 4];
        for (var map16Id = 0; map16Id < 0x200; map16Id++)
        {
            var tileWords = rom.GetWords(0x0D9100 + map16Id * 8, 4);
            Array.Copy(tileWords, 0, words, map16Id * 4, 4);
        }

        return words;
    }

    private static (byte[] Vram, List<Dictionary<string, object?>> Uploads, Dictionary<string, object?> Source) LevelFgBgVram(SmwRom rom, int levelId, LevelHeader header)
    {
        var vram = new byte[0x4000];
        var uploads = new List<Dictionary<string, object?>>();
        var (gfxIds, source) = ResolveFgBgGfxIds(rom, levelId, header);
        var destinations = new[] { 0x3000, 0x2000, 0x1000, 0x0000 };
        for (var slot = 0; slot < gfxIds.Length; slot++)
        {
            var (data, address, compressedLength) = DecompressGraphicsFile(rom, gfxIds[slot]);
            var destination = destinations[slot];
            var copyLength = Math.Min(0x1000, data.Length);
            Array.Copy(data, 0, vram, destination, copyLength);
            uploads.Add(Dict(
                ("slot", slot),
                ("gfx_id", $"{gfxIds[slot]:X2}"),
                ("source_addr", Hex24(address)),
                ("compressed_length", compressedLength),
                ("decompressed_length", data.Length),
                ("vram_offset", Hex16(destination)),
                ("tile_start", destination / 32),
                ("tile_count", copyLength / 32)));
        }

        return (vram, uploads, source);
    }

    private static (byte[] Vram, List<Dictionary<string, object?>> Uploads, Dictionary<string, object?> Source) LevelSpriteVram(SmwRom rom, int levelId, LevelHeader header)
    {
        var vram = new byte[0x4000];
        var uploads = new List<Dictionary<string, object?>>();
        var (gfxIds, source) = ResolveSpriteGfxIds(rom, levelId, header);
        var destinations = new[] { 0x3000, 0x2000, 0x1000, 0x0000 };
        for (var slot = 0; slot < gfxIds.Length; slot++)
        {
            var (data, address, compressedLength) = DecompressGraphicsFile(rom, gfxIds[slot]);
            var destination = destinations[slot];
            var copyLength = Math.Min(0x1000, data.Length);
            Array.Copy(data, 0, vram, destination, copyLength);
            uploads.Add(Dict(
                ("slot", slot),
                ("gfx_id", $"{gfxIds[slot]:X2}"),
                ("source_addr", Hex24(address)),
                ("compressed_length", compressedLength),
                ("decompressed_length", data.Length),
                ("vram_base", "0x6000"),
                ("vram_addr", Hex16(0x6000 + destination / 2)),
                ("vram_offset", Hex16(destination)),
                ("tile_start", destination / 32),
                ("tile_count", copyLength / 32)));
        }

        return (vram, uploads, source);
    }

    private static (int[] GfxIds, Dictionary<string, object?> Source) ResolveFgBgGfxIds(SmwRom rom, int levelId, LevelHeader header)
    {
        var entryWords = LmSuperGfxEntryWords(rom, levelId);
        if (entryWords != null && (entryWords[0] & 0x8000) != 0)
        {
            return ([GfxSlotNumber(entryWords[4]), GfxSlotNumber(entryWords[5]), GfxSlotNumber(entryWords[6]), GfxSlotNumber(entryWords[7])],
                Dict(("source", "lunar_magic_super_gfx_bypass"), ("entry_words", entryWords.Select(word => Hex16(word)).ToArray()), ("slot_order", new[] { "FG3", "BG1", "FG2", "FG1" })));
        }

        return (LevelGfxIds(FgAndBgGfxList, header.Tileset),
            Dict(("source", "vanilla_fg_bg_gfx_list"), ("tileset", header.Tileset), ("slot_order", new[] { "FG3", "BG1", "FG2", "FG1" })));
    }

    private static (int[] GfxIds, Dictionary<string, object?> Source) ResolveSpriteGfxIds(SmwRom rom, int levelId, LevelHeader header)
    {
        var entryWords = LmSuperGfxEntryWords(rom, levelId);
        if (entryWords != null && (entryWords[0] & 0x8000) != 0)
        {
            return ([GfxSlotNumber(entryWords[8]), GfxSlotNumber(entryWords[9]), GfxSlotNumber(entryWords[10]), GfxSlotNumber(entryWords[11])],
                Dict(("source", "lunar_magic_super_gfx_bypass"), ("entry_words", entryWords.Select(word => Hex16(word)).ToArray()), ("slot_order", new[] { "SP4", "SP3", "SP2", "SP1" })));
        }

        return (LevelGfxIds(SpriteGfxList, header.SpriteGraphics),
            Dict(("source", "vanilla_sprite_gfx_list"), ("sprite_graphics", header.SpriteGraphics), ("slot_order", new[] { "SP4", "SP3", "SP2", "SP1" })));
    }

    private static int[] LevelGfxIds(int[] table, int index)
    {
        if (index < 0 || (index + 1) * 4 > table.Length)
        {
            throw new InvalidOperationException($"unsupported graphics index: {index}");
        }

        var source = table.Skip(index * 4).Take(4).ToArray();
        var result = new int[4];
        for (var i = 0; i < source.Length; i++)
        {
            result[3 - i] = source[i];
        }
        return result;
    }

    private static int[]? LmSuperGfxEntryWords(SmwRom rom, int levelId)
    {
        if (!rom.HasLoRomRange(LmSuperGfxPointerAddress, 3))
        {
            return null;
        }
        var tableAddress = rom.Get24(LmSuperGfxPointerAddress);
        if (tableAddress is 0x000000 or 0xFFFFFF || !rom.HasLoRomRange(tableAddress + levelId * 0x20, 0x20))
        {
            return null;
        }
        return rom.GetWords(tableAddress + levelId * 0x20, 16);
    }

    private static int GfxSlotNumber(int word)
    {
        return word & 0x0FFF;
    }

    private static (byte[] Data, int Address, int CompressedLength) DecompressGraphicsFile(SmwRom rom, int gfxId)
    {
        var address = GraphicsFileAddress(rom, gfxId);
        var (data, compressedLength) = SmwDecompress(rom, address);
        if (gfxId >= 0 && gfxId < NormalGfx3BppExpand.Length && NormalGfx3BppExpand[gfxId])
        {
            data = Expand3BppTo4Bpp(data);
            data = ApplyGfxMaskFixes(data, gfxId);
        }
        if (data.Length % 32 != 0)
        {
            throw new InvalidOperationException($"GFX{gfxId:X2} decompressed to non-4bpp length {data.Length}");
        }

        return (data, address, compressedLength);
    }

    private static int GraphicsFileAddress(SmwRom rom, int gfxId)
    {
        if (gfxId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gfxId));
        }
        if (gfxId >= 0x100)
        {
            var tablePointer = rom.Get24(0x0FF937);
            if (tablePointer is 0x000000 or 0xFFFFFF)
            {
                throw new InvalidOperationException($"ExGFX pointer table is not installed for GFX{gfxId:000}");
            }
            var address = rom.Get24(tablePointer + (gfxId - 0x100) * 3);
            if (address == 0)
            {
                throw new InvalidOperationException($"ExGFX{gfxId:000} is not inserted");
            }
            return address;
        }
        if (gfxId >= 0x80)
        {
            var address = rom.Get24(0x0FF600 + (gfxId - 0x80) * 3);
            if (address == 0)
            {
                throw new InvalidOperationException($"ExGFX{gfxId:000} is not inserted");
            }
            return address;
        }
        if (gfxId >= GfxFileCount)
        {
            throw new InvalidOperationException($"GFX id out of range for vanilla table: {gfxId}");
        }

        var lo = rom.GetByte(0x00B992 + gfxId);
        var hi = rom.GetByte(0x00B9C4 + gfxId);
        var bank = rom.GetByte(0x00B9F6 + gfxId);
        return (bank << 16) | (hi << 8) | lo;
    }

    private static (byte[] Data, int CompressedLength) SmwDecompress(SmwRom rom, int address)
    {
        var result = new List<byte>();
        var reader = new SmwRomReader(rom, address);
        while (true)
        {
            var command = reader.Next();
            if (command == 0xFF)
            {
                return (result.ToArray(), LoRomDistance(address, reader.Address));
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
                    if (offset < 0 || offset >= result.Count)
                    {
                        throw new InvalidOperationException($"LC_LZ2 repeat offset out of range: {offset}");
                    }
                    result.Add(result[offset++]);
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
                    result.Add((byte)(value & 0xFF));
                    value = (value + 1) & 0xFF;
                }
            }
        }
    }

    private static byte[] Expand3BppTo4Bpp(byte[] data)
    {
        if (data.Length % 24 != 0)
        {
            throw new InvalidOperationException($"3bpp graphics length must be divisible by 24: {data.Length}");
        }

        var expanded = new List<byte>(data.Length / 24 * 32);
        for (var offset = 0; offset < data.Length; offset += 24)
        {
            expanded.AddRange(data.Skip(offset).Take(16));
            for (var row = 0; row < 8; row++)
            {
                expanded.Add(data[offset + 16 + row]);
                expanded.Add(0);
            }
        }
        return expanded.ToArray();
    }

    private static byte[] ApplyGfxMaskFixes(byte[] data, int gfxId)
    {
        var patched = data.ToArray();
        if (gfxId is 0x01 or 0x17 or 0x31)
        {
            PatchSparseMaskRows(patched, [(0x10, 0x40), (0x210, 0x240)]);
        }
        else if (gfxId == 0x1E)
        {
            PatchMaskRows(patched, 0x10, 0x1000);
        }
        else if (gfxId == 0x08)
        {
            foreach (var tile in new[] { 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x5B, 0x7A, 0x7B, 0x60, 0x70, 0x6E, 0x6F, 0x7E, 0x7F })
            {
                PatchMaskRows(patched, tile * 32 + 0x10, tile * 32 + 0x20);
            }
        }
        return patched;
    }

    private static void PatchSparseMaskRows(byte[] data, (int Begin, int End)[] ranges)
    {
        var existing = 0;
        foreach (var (begin, end) in ranges)
        {
            for (var offset = begin; offset < end;)
            {
                if ((offset & 0x10) == 0)
                {
                    offset += 0x10;
                    continue;
                }
                if (offset + 1 < data.Length)
                {
                    existing |= data[offset + 1];
                }
                offset += 2;
            }
        }
        if (existing != 0)
        {
            return;
        }
        foreach (var (begin, end) in ranges)
        {
            PatchMaskRows(data, begin, end);
        }
    }

    private static void PatchMaskRows(byte[] data, int begin, int end)
    {
        for (var offset = begin; offset < end;)
        {
            if ((offset & 0x10) == 0)
            {
                offset += 0x10;
                continue;
            }
            if (offset + 1 < data.Length && offset >= 16)
            {
                data[offset + 1] = (byte)(data[offset - 16] | data[offset - 15] | data[offset]);
            }
            offset += 2;
        }
    }

    private static Dictionary<int, string> LoadTitleDictionaryFallback() => new();

    private static (Dictionary<int, string> Titles, string Source) LoadOverworldLevelTitles(SmwRom rom)
    {
        var lmPayload = LmLevelNamesPayloadPc(rom);
        var titles = new Dictionary<int, string>();
        if (lmPayload != null)
        {
            for (var overworldLevel = 1; overworldLevel < 0x100; overworldLevel++)
            {
                var editorLevelId = EditorLevelIdForOverworldLevel(overworldLevel);
                if (editorLevelId >= EditorLevelTitleCount)
                {
                    continue;
                }
                var offset = lmPayload.Value + overworldLevel * LmLevelNameCharacterCount;
                var title = NormalizeLevelTitle(string.Concat(rom.Data.Skip(offset).Take(LmLevelNameCharacterCount).Select(value => LevelNameGlyph(value & 0x7F))));
                if (!string.IsNullOrWhiteSpace(title))
                {
                    titles[editorLevelId] = title;
                }
            }
            return (titles, "lunar_magic_expanded_level_name_patch");
        }

        var strings = rom.GetBytes(LevelNameStringsAddress, 460);
        var levelWords = rom.GetWords(LevelNamesAddress, 0x100);
        var prefixOffsets = rom.GetWords(LevelNamePrefixOffsetsAddress, 31);
        var middleOffsets = rom.GetWords(LevelNameMiddleOffsetsAddress, 15);
        var suffixOffsets = rom.GetWords(LevelNameSuffixOffsetsAddress, 13);
        for (var overworldLevel = 1; overworldLevel < levelWords.Length; overworldLevel++)
        {
            var editorLevelId = EditorLevelIdForOverworldLevel(overworldLevel);
            if (editorLevelId >= EditorLevelTitleCount)
            {
                continue;
            }
            var title = DecodeTitleWord(levelWords[overworldLevel], strings, prefixOffsets, middleOffsets, suffixOffsets);
            if (!string.IsNullOrWhiteSpace(title))
            {
                titles[editorLevelId] = title;
            }
        }

        return (titles, "vanilla_overworld_level_name_tables");
    }

    private static int? LmLevelNamesPayloadPc(SmwRom rom)
    {
        if (rom.GetByte(LmLevelNamesHookAddress) != 0x22)
        {
            return null;
        }
        var payloadAddress = rom.Get24(LmLevelNamesPointerAddress);
        if (payloadAddress is 0 or 0xFFFFFF)
        {
            throw new InvalidOperationException("Lunar Magic level-name patch hook is present but its payload pointer is empty");
        }
        var payloadPc = rom.LoRomIndex(payloadAddress);
        if (payloadPc > rom.Data.Length || LmLevelNamesPatchBytes > rom.Data.Length - payloadPc)
        {
            throw new InvalidOperationException("Lunar Magic level-name patch payload points past the ROM size");
        }
        var ratsSize = RatsTagTotalSize(rom.Data, payloadPc - 8);
        if (ratsSize == null || ratsSize < LmLevelNamesPatchBytes + 8)
        {
            throw new InvalidOperationException("Lunar Magic level-name patch payload is not protected by a valid RATS tag");
        }
        return payloadPc;
    }

    private static int? RatsTagTotalSize(byte[] data, int offset)
    {
        if (offset < 0 || offset + 8 > data.Length ||
            data[offset] != (byte)'S' ||
            data[offset + 1] != (byte)'T' ||
            data[offset + 2] != (byte)'A' ||
            data[offset + 3] != (byte)'R')
        {
            return null;
        }
        var size = data[offset + 4] | (data[offset + 5] << 8);
        var complement = data[offset + 6] | (data[offset + 7] << 8);
        return (size ^ complement) == 0xFFFF ? size + 1 + 8 : null;
    }

    private static string DecodeTitleWord(int word, byte[] strings, int[] prefixOffsets, int[] middleOffsets, int[] suffixOffsets)
    {
        var output = "";
        var prefixIndex = (word >> 8) & 0x7F;
        if (prefixIndex < prefixOffsets.Length)
        {
            var offset = prefixOffsets[prefixIndex];
            if (offset < strings.Length && (strings[offset] & 0x80) == 0)
            {
                output += AppendLevelNameSegment(strings, offset);
            }
        }

        var middleIndex = (word & 0x00F0) >> 4;
        if (middleIndex < middleOffsets.Length)
        {
            var offset = middleOffsets[middleIndex];
            if (offset < strings.Length && strings[offset] != 0x9F)
            {
                output += AppendLevelNameSegment(strings, offset);
            }
        }

        var suffixIndex = word & 0x000F;
        if (suffixIndex < suffixOffsets.Length)
        {
            output += AppendLevelNameSegment(strings, suffixOffsets[suffixIndex]);
        }

        output = NormalizeLevelTitle(output);
        return output == "YELLOW SWITCH PALACE 3" ? "YELLOW SWITCH PALACE" : output;
    }

    private static string AppendLevelNameSegment(byte[] strings, int offset)
    {
        if (offset >= strings.Length)
        {
            return "";
        }
        var output = "";
        for (var i = offset; i < strings.Length; i++)
        {
            output += LevelNameGlyph(strings[i] & 0x7F);
            if ((strings[i] & 0x80) != 0)
            {
                break;
            }
        }
        return output;
    }

    private static string NormalizeLevelTitle(string title)
    {
        return string.Join(" ", title.ToUpperInvariant().Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string LevelNameGlyph(int code)
    {
        if (code is >= 0 and <= 25)
        {
            return ((char)('A' + code)).ToString();
        }
        return code switch
        {
            0x1C => "-",
            0x1F => " ",
            0x5A => "#",
            0x5D => "'",
            0x62 => ".",
            >= 0x64 and <= 0x69 => ((char)('1' + (code - 0x64))).ToString(),
            0x32 => "I",
            0x33 => "L",
            0x34 => "L",
            0x35 => "U",
            0x36 => "S",
            0x37 => "I",
            0x38 => "Y",
            0x39 => "E",
            0x3A => "L",
            0x3B => "L",
            0x3C => "OW",
            _ => "",
        };
    }

    private static int EditorLevelIdForOverworldLevel(int overworldLevel)
    {
        return overworldLevel >= 0x25 ? 0x100 + (overworldLevel - 0x24) : overworldLevel;
    }

    private static Dictionary<string, object?> Write4BppAtlasPng(string path, byte[] gfxData, int[][] paletteRgb, int columns = 16)
    {
        if (gfxData.Length % 32 != 0)
        {
            throw new InvalidOperationException($"4bpp graphics length must be divisible by 32: {gfxData.Length}");
        }
        var tileCount = gfxData.Length / 32;
        var rows = (tileCount + columns - 1) / columns;
        var width = columns * 8;
        var height = rows * 8;
        var rgba = new byte[width * height * 4];
        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            var tileX = (tileIndex % columns) * 8;
            var tileY = (tileIndex / columns) * 8;
            var tileOffset = tileIndex * 32;
            for (var y = 0; y < 8; y++)
            {
                var p0 = gfxData[tileOffset + y * 2];
                var p1 = gfxData[tileOffset + y * 2 + 1];
                var p2 = gfxData[tileOffset + 16 + y * 2];
                var p3 = gfxData[tileOffset + 16 + y * 2 + 1];
                for (var x = 0; x < 8; x++)
                {
                    var bit = 7 - x;
                    var colorIndex = ((p0 >> bit) & 1) | (((p1 >> bit) & 1) << 1) | (((p2 >> bit) & 1) << 2) | (((p3 >> bit) & 1) << 3);
                    var output = ((tileY + y) * width + tileX + x) * 4;
                    var rgb = paletteRgb[Math.Min(colorIndex, paletteRgb.Length - 1)];
                    rgba[output] = (byte)rgb[0];
                    rgba[output + 1] = (byte)rgb[1];
                    rgba[output + 2] = (byte)rgb[2];
                    rgba[output + 3] = colorIndex == 0 ? (byte)0 : (byte)255;
                }
            }
        }

        return Dict(
            ("sha1", WriteRgbaPng(path, width, height, rgba)),
            ("width", width),
            ("height", height),
            ("tile_count", tileCount),
            ("columns", columns),
            ("tile_width", 8),
            ("tile_height", 8),
            ("format", "rgba_png_from_snes_4bpp"));
    }

    private static Dictionary<string, object?> WriteMap16PreviewPng(
        string path,
        IReadOnlyList<int> map16Words,
        byte[] vram4Bpp,
        int[][] levelPaletteRgb,
        int firstTile = 0,
        int tileCount = 0x200,
        int columns = 16)
    {
        var rows = (tileCount + columns - 1) / columns;
        var width = columns * 16;
        var height = rows * 16;
        var rgba = new byte[width * height * 4];

        for (var localIndex = 0; localIndex < tileCount; localIndex++)
        {
            var map16Id = firstTile + localIndex;
            var tileX = (localIndex % columns) * 16;
            var tileY = (localIndex / columns) * 16;
            if (!BlitMap16Tile(rgba, width, tileX, tileY, map16Id, map16Words, vram4Bpp, levelPaletteRgb))
            {
                break;
            }
        }

        return Dict(
            ("sha1", WriteRgbaPng(path, width, height, rgba)),
            ("width", width),
            ("height", height),
            ("first_map16_tile", firstTile),
            ("map16_tile_count", tileCount),
            ("columns", columns),
            ("format", "png_preview_from_map16_tile_words_and_level_vram"));
    }

    private static Dictionary<string, object?> WriteLevelLayoutPreviewPng(
        string path,
        int widthTiles,
        int heightTiles,
        IEnumerable<Dictionary<string, object?>> placedTiles,
        IReadOnlyList<int> map16Words,
        byte[] vram4Bpp,
        int[][] levelPaletteRgb)
    {
        var width = Math.Max(1, widthTiles * 16);
        var height = Math.Max(1, heightTiles * 16);
        var rgba = new byte[width * height * 4];
        var placedTileCount = 0;
        var renderedTileCount = 0;

        foreach (var placed in placedTiles)
        {
            placedTileCount++;
            var x = Convert.ToInt32(placed["x"], CultureInfo.InvariantCulture);
            var y = Convert.ToInt32(placed["y"], CultureInfo.InvariantCulture);
            if (x < 0 || y < 0 || x >= widthTiles || y >= heightTiles)
            {
                continue;
            }

            var map16 = Convert.ToInt32(placed["map16"], CultureInfo.InvariantCulture);
            if (BlitMap16Tile(rgba, width, x * 16, y * 16, map16, map16Words, vram4Bpp, levelPaletteRgb))
            {
                renderedTileCount++;
            }
        }

        return Dict(
            ("sha1", WriteRgbaPng(path, width, height, rgba)),
            ("width", width),
            ("height", height),
            ("width_tiles", widthTiles),
            ("height_tiles", heightTiles),
            ("placed_tile_count", placedTileCount),
            ("rendered_tile_count", renderedTileCount),
            ("format", "partial_level_preview_from_object_map16_ids"));
    }

    private static bool BlitMap16Tile(
        byte[] rgba,
        int canvasWidth,
        int x0,
        int y0,
        int map16Id,
        IReadOnlyList<int> map16Words,
        byte[] vram4Bpp,
        int[][] levelPaletteRgb)
    {
        var words = Map16TileWords(map16Words, map16Id);
        if (words == null)
        {
            return false;
        }

        var vramTileCount = vram4Bpp.Length / 32;
        for (var sub = 0; sub < words.Length; sub++)
        {
            var word = words[sub];
            var tileId = word & 0x03FF;
            if (tileId >= vramTileCount)
            {
                continue;
            }

            var subX = x0 + ((sub & 1) != 0 ? 8 : 0);
            var subY = y0 + ((sub & 2) != 0 ? 8 : 0);
            Blit8x8Tile(
                rgba,
                canvasWidth,
                subX,
                subY,
                vram4Bpp,
                tileId,
                word,
                levelPaletteRgb,
                xFlip: (word & 0x4000) != 0,
                yFlip: (word & 0x8000) != 0);
        }

        return true;
    }

    private static void Blit8x8Tile(
        byte[] rgba,
        int canvasWidth,
        int x0,
        int y0,
        byte[] vram4Bpp,
        int tileId,
        int tileWord,
        int[][] levelPaletteRgb,
        bool xFlip,
        bool yFlip)
    {
        var tileOffset = tileId * 32;
        for (var y = 0; y < 8; y++)
        {
            var srcY = yFlip ? 7 - y : y;
            var p0 = vram4Bpp[tileOffset + srcY * 2];
            var p1 = vram4Bpp[tileOffset + srcY * 2 + 1];
            var p2 = vram4Bpp[tileOffset + 16 + srcY * 2];
            var p3 = vram4Bpp[tileOffset + 16 + srcY * 2 + 1];

            for (var x = 0; x < 8; x++)
            {
                var srcX = xFlip ? 7 - x : x;
                var bit = 7 - srcX;
                var colorIndex = ((p0 >> bit) & 1) |
                    (((p1 >> bit) & 1) << 1) |
                    (((p2 >> bit) & 1) << 2) |
                    (((p3 >> bit) & 1) << 3);
                if (colorIndex == 0)
                {
                    continue;
                }

                var rgb = PaletteColorFromTileWord(tileWord, colorIndex, levelPaletteRgb);
                var output = ((y0 + y) * canvasWidth + x0 + x) * 4;
                rgba[output] = (byte)rgb[0];
                rgba[output + 1] = (byte)rgb[1];
                rgba[output + 2] = (byte)rgb[2];
                rgba[output + 3] = 255;
            }
        }
    }

    private static int[] PaletteColorFromTileWord(int word, int colorIndex, int[][] levelPaletteRgb)
    {
        var paletteId = (word >> 10) & 0x07;
        var index = paletteId * 16 + colorIndex;
        if (index < 0 || index >= levelPaletteRgb.Length)
        {
            index = colorIndex;
        }

        return levelPaletteRgb[Math.Clamp(index, 0, levelPaletteRgb.Length - 1)];
    }

    private static int[]? Map16TileWords(IReadOnlyList<int> map16Words, int map16Id)
    {
        var wordOffset = map16Id * 4;
        if (wordOffset < 0 || wordOffset + 4 > map16Words.Count)
        {
            return null;
        }

        return
        [
            map16Words[wordOffset],
            map16Words[wordOffset + 2],
            map16Words[wordOffset + 1],
            map16Words[wordOffset + 3],
        ];
    }

    private static string WriteRgbaPng(string path, int width, int height, byte[] rgba)
    {
        if (rgba.Length != width * height * 4)
        {
            throw new ArgumentException("RGBA buffer size does not match PNG dimensions", nameof(rgba));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var output = new MemoryStream();
        output.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header[0..4], width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..8], height);
        header[8] = 8;
        header[9] = 6;
        WritePngChunk(output, "IHDR"u8, header);
        var filtered = new byte[(width * 4 + 1) * height];
        for (var y = 0; y < height; y++)
        {
            filtered[y * (width * 4 + 1)] = 0;
            Array.Copy(rgba, y * width * 4, filtered, y * (width * 4 + 1) + 1, width * 4);
        }
        WritePngChunk(output, "IDAT"u8, WriteZlibStoredBlocks(filtered));
        WritePngChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
        var payload = output.ToArray();
        File.WriteAllBytes(path, payload);
        return SmwRom.Sha1Hex(payload);
    }

    private static void WritePngChunk(Stream output, ReadOnlySpan<byte> kind, ReadOnlySpan<byte> payload)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, payload.Length);
        output.Write(length);
        output.Write(kind);
        output.Write(payload);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, Crc32(kind, payload));
        output.Write(crcBytes);
    }

    private static byte[] WriteZlibStoredBlocks(ReadOnlySpan<byte> payload)
    {
        using var output = new MemoryStream();
        output.WriteByte(0x78);
        output.WriteByte(0x01);

        var offset = 0;
        do
        {
            var blockLength = Math.Min(0xFFFF, payload.Length - offset);
            var isFinal = offset + blockLength >= payload.Length;
            output.WriteByte(isFinal ? (byte)0x01 : (byte)0x00);
            output.WriteByte((byte)(blockLength & 0xFF));
            output.WriteByte((byte)(blockLength >> 8));
            var inverse = (ushort)~blockLength;
            output.WriteByte((byte)(inverse & 0xFF));
            output.WriteByte((byte)(inverse >> 8));
            output.Write(payload.Slice(offset, blockLength));
            offset += blockLength;
        }
        while (offset < payload.Length);

        Span<byte> adler = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(adler, Adler32(payload));
        output.Write(adler);
        return output.ToArray();
    }

    private static uint Adler32(ReadOnlySpan<byte> payload)
    {
        const uint modulo = 65521;
        uint a = 1;
        uint b = 0;
        foreach (var value in payload)
        {
            a = (a + value) % modulo;
            b = (b + a) % modulo;
        }
        return (b << 16) | a;
    }

    private static uint Crc32(ReadOnlySpan<byte> kind, ReadOnlySpan<byte> payload)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in kind)
        {
            crc = Crc32Byte(crc, value);
        }
        foreach (var value in payload)
        {
            crc = Crc32Byte(crc, value);
        }
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint Crc32Byte(uint crc, byte value)
    {
        crc ^= value;
        for (var i = 0; i < 8; i++)
        {
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc;
    }

    private static int LoRomDistance(int start, int end)
    {
        var count = 0;
        var cursor = start;
        while (cursor != end)
        {
            cursor = SmwRom.IncrementLoRomAddress(cursor, 1);
            count++;
            if (count > 0x20000)
            {
                throw new InvalidOperationException($"LoROM range is unexpectedly large: {Hex24(start)}..{Hex24(end)}");
            }
        }
        return count;
    }

    private static string WriteBinary(string path, byte[] payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, payload);
        return SmwRom.Sha1Hex(payload);
    }

    private static string WriteJson(string path, object payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, JsonOptions))
        {
            WriteJsonValue(writer, payload);
        }

        var text = Encoding.UTF8.GetString(stream.ToArray()) + "\n";
        File.WriteAllText(path, text);
        return SmwRom.Sha1Hex(Encoding.UTF8.GetBytes(text));
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                break;
            case sbyte or byte or short or ushort or int or uint or long:
                writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case ulong unsigned:
                writer.WriteNumberValue(unsigned);
                break;
            case float single:
                writer.WriteNumberValue(single);
                break;
            case double number:
                writer.WriteNumberValue(number);
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case System.Collections.IDictionary dictionary:
                writer.WriteStartObject();
                foreach (System.Collections.DictionaryEntry entry in dictionary)
                {
                    writer.WritePropertyName(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty);
                    WriteJsonValue(writer, entry.Value);
                }
                writer.WriteEndObject();
                break;
            case System.Collections.IEnumerable enumerable:
                writer.WriteStartArray();
                foreach (var item in enumerable)
                {
                    WriteJsonValue(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                property => property.Name,
                property => JsonElementToObject(property.Value),
                StringComparer.Ordinal),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static Dictionary<string, object?> Dict(params (string Key, object? Value)[] items)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in items)
        {
            dictionary[key] = value;
        }
        return dictionary;
    }

    private static int ParseLevelId(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }
        return int.Parse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static string FormatLevelId(int levelId) => $"{levelId & 0x1FF:X3}";
    private static string Hex24(int value) => $"0x{value & 0xFFFFFF:X6}";
    private static string Hex16(int value) => $"0x{value & 0xFFFF:X4}";
    private static string Rel(string path, string root) => Path.GetRelativePath(root, path).Replace('\\', '/');
    private static int Signed16(int word) => (word & 0x8000) != 0 ? word - 0x10000 : word;

    private sealed record PlayerTableSpec(string Name, int Address, int Count, string Format);
    private sealed record PaletteTableSpec(string Name, int Address, int WordCount);

    public sealed record SmwLevelIndex(
        string TitleSource,
        string Status,
        string? Error,
        Dictionary<int, string> Titles,
        List<Dictionary<string, object?>> Levels,
        int InvalidCount)
    {
        public Dictionary<string, object?> ToSerializable()
        {
            return Dict(
                ("source", TitleSource),
                ("status", Status),
                ("error", Error),
                ("count", Levels.Count),
                ("invalid_count", InvalidCount),
                ("levels", Levels));
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
        public Dictionary<string, object?> ToSerializable() => Dict(
            ("raw", Raw),
            ("screens", Screens),
            ("bg_palette", BgPalette),
            ("level_mode", LevelMode),
            ("background_color", BackgroundColor),
            ("sprite_graphics", SpriteGraphics),
            ("music_index", MusicIndex),
            ("layer3_priority", Layer3Priority),
            ("fg_palette", FgPalette),
            ("sprite_palette", SpritePalette),
            ("timer_index", TimerIndex),
            ("tileset", Tileset),
            ("layer1_scroll", Layer1Scroll),
            ("item_memory", ItemMemory),
            ("layout_flags", LayoutFlags),
            ("vertical", Vertical),
            ("width_tiles", WidthTiles),
            ("height_tiles", HeightTiles));
    }

    private sealed record ParsedObjects(List<LevelObject> Objects, List<ScreenExit> ScreenExits);

    private sealed record LevelObject(int Sequence, int Offset, int[] Raw, int ObjectId, int SizeOrType, int[] Extra, ObjectPlacement Placement)
    {
        public Dictionary<string, object?> ToSerializable() => Dict(
            ("sequence", Sequence),
            ("offset", Offset),
            ("raw", Raw),
            ("object_id", ObjectId),
            ("size_or_type", SizeOrType),
            ("extra", Extra),
            ("placement", Placement.ToSerializable()));
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
        public Dictionary<string, object?> ToSerializable() => Dict(
            ("layer", Layer),
            ("screen_cursor", ScreenCursor),
            ("screen_increment", ScreenIncrement),
            ("sub_x", SubX),
            ("sub_y", SubY),
            ("high_subscreen", HighSubscreen),
            ("map16_offset", Map16Offset),
            ("x_tile", XTile),
            ("y_tile", YTile),
            ("x_px", XPx),
            ("y_px", YPx),
            ("adjusted_raw", AdjustedRaw));
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
        public Dictionary<string, object?> ToSerializable() => Dict(
            ("screen", Screen),
            ("screen_cursor", ScreenCursor),
            ("exit_low", ExitLow),
            ("raw_r11", RawR11),
            ("vanilla_properties", VanillaProperties),
            ("vanilla_destination_property_bits", VanillaDestinationPropertyBits),
            ("vanilla_secondary_property_bits", VanillaSecondaryPropertyBits),
            ("lunar_magic_properties", LunarMagicProperties),
            ("lunar_magic_secondary", LunarMagicSecondary),
            ("vanilla_destination_low", VanillaDestinationLow),
            ("vanilla_source_map_high", VanillaSourceMapHigh),
            ("vanilla_destination", VanillaDestination),
            ("vanilla_secondary", VanillaSecondary));
    }

    private sealed record ParsedSprites(int Header, List<SpriteRecord> Sprites);

    private sealed record SpriteRecord(int Offset, int ScreenY, int XId, int Screen, int XPx, int YPx, int ExtraBits, int SpriteId, int[] Raw)
    {
        public Dictionary<string, object?> ToSerializable() => Dict(
            ("offset", Offset),
            ("screen_y", ScreenY),
            ("x_id", XId),
            ("screen", Screen),
            ("x_px", XPx),
            ("y_px", YPx),
            ("extra_bits", ExtraBits),
            ("sprite_id", SpriteId),
            ("raw", Raw),
            ("format", "yyyyEESY_XXXXssss_NNNNNNNN"));
    }
}
