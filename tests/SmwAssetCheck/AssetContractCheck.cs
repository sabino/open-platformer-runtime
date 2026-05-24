using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

public static class AssetContractCheck
{
    private const int ExpectedYoshiIslandTileCount = 1616;
    private const int ExpectedYoshiIslandSpriteCount = 34;

    public static int Main(string[] args)
    {
        try
        {
            var generatedRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath("generated/smw");
            Check(Directory.Exists(generatedRoot), $"generated asset directory missing: {generatedRoot}");

            using var manifest = LoadJson(Path.Combine(generatedRoot, "manifest.json"));
            var manifestRoot = manifest.RootElement;

            CheckManifestFiles(generatedRoot, manifestRoot);
            CheckLevels(generatedRoot, manifestRoot);
            CheckPlayerGraphics(generatedRoot);
            CheckTilesets(generatedRoot);
            CheckPalettes(generatedRoot);
            CheckAudio(generatedRoot, manifestRoot);

            Console.WriteLine($"smw-godot C# asset contract: ok root={generatedRoot}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"smw-godot C# asset contract: failed: {ex.Message}");
            return 1;
        }
    }

    private static void CheckManifestFiles(string generatedRoot, JsonElement manifest)
    {
        var assets = Required(manifest, "assets");
        foreach (var asset in assets.EnumerateObject())
        {
            CheckAssetFiles(generatedRoot, asset.Value, $"assets.{asset.Name}");
        }

        var levels = Required(manifest, "levels");
        Check(levels.TryGetProperty("105", out _), "manifest missing level 105");
        Check(levels.TryGetProperty("1CB", out _), "manifest missing direct pipe target level 1CB");
        foreach (var level in levels.EnumerateObject())
        {
            CheckAssetFiles(generatedRoot, level.Value, $"levels.{level.Name}");
        }
    }

    private static void CheckAssetFiles(string generatedRoot, JsonElement element, string context)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("file", out var fileProperty) && fileProperty.ValueKind == JsonValueKind.String)
            {
                var relative = fileProperty.GetString() ?? "";
                var path = Path.Combine(generatedRoot, relative);
                Check(File.Exists(path), $"{context} references missing file {relative}");

                if (element.TryGetProperty("length", out var lengthProperty) && lengthProperty.ValueKind == JsonValueKind.Number)
                {
                    var actualLength = new FileInfo(path).Length;
                    var expectedLength = lengthProperty.GetInt64();
                    Check(actualLength == expectedLength, $"{context} length mismatch for {relative}: expected {expectedLength}, got {actualLength}");
                }

                if (element.TryGetProperty("sha1", out var sha1Property) && sha1Property.ValueKind == JsonValueKind.String)
                {
                    var expectedSha1 = sha1Property.GetString() ?? "";
                    var actualSha1 = Sha1Hex(path);
                    Check(string.Equals(actualSha1, expectedSha1, StringComparison.OrdinalIgnoreCase),
                        $"{context} sha1 mismatch for {relative}: expected {expectedSha1}, got {actualSha1}");
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                CheckAssetFiles(generatedRoot, property.Value, $"{context}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var child in element.EnumerateArray())
            {
                CheckAssetFiles(generatedRoot, child, $"{context}[{index}]");
                index++;
            }
        }
    }

    private static void CheckLevels(string generatedRoot, JsonElement manifest)
    {
        var levels = Required(manifest, "levels");
        var level105File = Required(Required(levels, "105"), "file").GetString() ?? "";
        var level1CbFile = Required(Required(levels, "1CB"), "file").GetString() ?? "";

        using var level105 = LoadJson(Path.Combine(generatedRoot, level105File));
        using var level1Cb = LoadJson(Path.Combine(generatedRoot, level1CbFile));
        CheckLevelHeader(level105.RootElement, "105", expectedTileset: 7, expectedSpriteGraphics: 8, expectedScreens: 20);
        CheckLevelHeader(level1Cb.RootElement, "1CB", expectedTileset: 3, expectedSpriteGraphics: 8, expectedScreens: 2);

        var exits105 = RequiredArray(level105.RootElement, "screen_exits");
        var pipeExit = exits105.FirstOrDefault(exit => Required(exit, "screen").GetInt32() == 7);
        Check(pipeExit.ValueKind != JsonValueKind.Undefined, "level 105 missing screen 07 pipe exit");
        Check(Required(pipeExit, "exit_low").GetInt32() == 0xCB, "level 105 screen 07 exit_low mismatch");
        Check(Required(pipeExit, "vanilla_destination").GetInt32() == 0x1CB, "level 105 screen 07 vanilla destination mismatch");
        Check(Required(pipeExit, "vanilla_secondary").GetInt32() == 0, "level 105 screen 07 should be primary/direct");

        var sprites = RequiredArray(Required(level105.RootElement, "sprite_layer"), "sprites");
        Check(sprites.Count() == ExpectedYoshiIslandSpriteCount, $"level 105 expected {ExpectedYoshiIslandSpriteCount} sprite records");
        var spriteCounts = new Dictionary<int, int>();
        foreach (var sprite in sprites)
        {
            var id = Required(sprite, "sprite_id").GetInt32();
            spriteCounts[id] = spriteCounts.GetValueOrDefault(id) + 1;
            Check((Required(sprite, "format").GetString() ?? "") == "yyyyEESY_XXXXssss_NNNNNNNN", "sprite format mismatch");
        }
        Check(spriteCounts.GetValueOrDefault(0xAB) == 18, "level 105 expected 18 Rex sprite records");

        CheckTilemap(generatedRoot, "levels/level_105_partial_tilemap.json");
    }

    private static void CheckLevelHeader(JsonElement level, string id, int expectedTileset, int expectedSpriteGraphics, int expectedScreens)
    {
        var header = Required(level, "header");
        Check(Required(header, "tileset").GetInt32() == expectedTileset, $"level {id} tileset mismatch");
        Check(Required(header, "sprite_graphics").GetInt32() == expectedSpriteGraphics, $"level {id} sprite graphics mismatch");
        Check(Required(header, "screens").GetInt32() == expectedScreens, $"level {id} screen count mismatch");
        Check(Required(header, "width_tiles").GetInt32() >= expectedScreens * 16, $"level {id} width too small");
    }

    private static void CheckTilemap(string generatedRoot, string relativePath)
    {
        using var tilemap = LoadJson(Path.Combine(generatedRoot, relativePath));
        var root = tilemap.RootElement;
        Check((Required(root, "status").GetString() ?? "") == "partial", "level 105 tilemap status mismatch");
        Check(Required(root, "placed_tile_count").GetInt32() == ExpectedYoshiIslandTileCount, "level 105 placed tile count mismatch");
        Check(Required(root, "unsupported_object_counts").EnumerateObject().Any() == false, "level 105 has unsupported object records");

        var placedTiles = RequiredArray(root, "placed_tiles").ToList();
        Check(placedTiles.Count == ExpectedYoshiIslandTileCount, "level 105 placed tile array length mismatch");

        var sources = placedTiles.Select(tile => Required(tile, "source").GetString() ?? "").ToHashSet(StringComparer.Ordinal);
        foreach (var expectedSource in new[]
        {
            "right_diagonal_pipe",
            "left_diagonal_ledge_edge",
            "steep_right_slope_surface",
            "extended_goal_marker",
            "extended_midway_bar",
            "extended_yellow_switch_block",
            "extended_question_block_flower",
        })
        {
            Check(sources.Contains(expectedSource), $"level 105 missing tile source {expectedSource}");
        }

        CheckHasTile(placedTiles, 56, 18, "right_diagonal_pipe", 0x01C4, "right diagonal pipe surface tile");
        CheckHasTile(placedTiles, 55, 19, "right_diagonal_pipe", 0x01C7, "right diagonal pipe body tile");
        CheckHasTile(placedTiles, 13, 18, "left_diagonal_ledge_edge", 0x01AA, "left ledge edge tile");
        CheckHasTile(placedTiles, 294, 22, "extended_goal_marker", 0x0066, "goal marker tile");
        CheckHasTile(placedTiles, 150, 21, "extended_midway_bar", 0x0035, "midway bar tile");
    }

    private static void CheckPlayerGraphics(string generatedRoot)
    {
        using var player = LoadJson(Path.Combine(generatedRoot, "player/player_graphics.json"));
        var root = player.RootElement;
        Check((Required(root, "status").GetString() ?? "") == "partial", "player graphics status mismatch");

        var pendingStates = RequiredArray(Required(root, "categories"), "states_pending_direct_oam_port")
            .Select(value => value.GetString() ?? "")
            .ToHashSet(StringComparer.Ordinal);
        foreach (var state in new[] { "idle", "walk", "run", "jump", "spin_jump", "duck" })
        {
            Check(pendingStates.Contains(state), $"player graphics pending-state list missing {state}");
        }

        var palette = Required(root, "palette");
        var variants = RequiredArray(palette, "variants").Select(variant => Required(variant, "name").GetString() ?? "").ToArray();
        Check(variants.SequenceEqual(["mario", "luigi", "fire_mario", "fire_luigi"]), "player palette variants mismatch");
        var basePalette = RequiredArray(palette, "snes_bgr555").Select(value => value.GetInt32()).Take(8).ToArray();
        Check(basePalette.SequenceEqual([0x0000, 0x7FDD, 0x0000, 0x0D71, 0x1E9B, 0x3B7F, 0x635F, 0x581D]), "player base palette prefix mismatch");

        var tables = Required(root, "oam_tables");
        CheckSourceAddress(tables, "player_xy_disp_index_index", "0x00DCEC");
        CheckSourceAddress(tables, "player_xy_disp_index", "0x00DD32");
        CheckSourceAddress(tables, "x_disp", "0x00DD4E");
        CheckSourceAddress(tables, "y_disp", "0x00DE32");
        CheckSourceAddress(tables, "powerup_tileset_index", "0x00DF16");
        CheckSourceAddress(tables, "tiles_index", "0x00DF4C");
        CheckSourceAddress(tables, "tiles", "0x00DFDA");
        CheckSourceAddress(tables, "head_tile_pointer_index", "0x00E00C");
        CheckSourceAddress(tables, "body_tile_pointer_index", "0x00E0CC");
        Check(RequiredArray(Required(tables, "x_disp"), "values").Count() == 114, "player x displacement table length mismatch");
        Check(RequiredArray(Required(tables, "tiles_index"), "values").Count() == 192, "player tile index table length mismatch");

        foreach (var png in Directory.EnumerateFiles(Path.Combine(generatedRoot, "player"), "gfx3*_player_palette*.png"))
        {
            CheckPngHeader(png);
        }
    }

    private static void CheckHasTile(
        List<JsonElement> placedTiles,
        int x,
        int y,
        string source,
        int map16,
        string description)
    {
        var hasTile = placedTiles.Any(tile =>
            Required(tile, "x").GetInt32() == x &&
            Required(tile, "y").GetInt32() == y &&
            (Required(tile, "source").GetString() ?? "") == source &&
            Required(tile, "map16").GetInt32() == map16);

        Check(hasTile, $"{description} missing at {x},{y} source={source} map16=0x{map16:X4}");
    }

    private static void CheckTilesets(string generatedRoot)
    {
        using var tileset = LoadJson(Path.Combine(generatedRoot, "tilesets/level_105_tileset7.json"));
        var root = tileset.RootElement;
        Check((Required(root, "status").GetString() ?? "") == "preview", "level 105 tileset status mismatch");
        Check((Required(Required(root, "atlas_png"), "file").GetString() ?? "") == "tilesets/level_105_tileset7_8x8.png", "level 105 tileset atlas mismatch");
        Check(Required(Required(root, "vram"), "tile_count").GetInt32() == 512, "level 105 BG VRAM tile count mismatch");
        var uploadIds = RequiredArray(root, "uploads").Select(upload => Required(upload, "gfx_id").GetString() ?? "").ToArray();
        Check(uploadIds.SequenceEqual(["15", "1B", "17", "14"]), "level 105 BG GFX upload IDs mismatch");

        using var spriteSet = LoadJson(Path.Combine(generatedRoot, "spritesets/level_105_spritegfx8.json"));
        var spriteRoot = spriteSet.RootElement;
        Check(Required(spriteRoot, "sprite_graphics").GetInt32() == 8, "level 105 sprite graphics set mismatch");
        var spriteUploads = RequiredArray(spriteRoot, "uploads").Select(upload => Required(upload, "gfx_id").GetString() ?? "").ToArray();
        Check(spriteUploads.SequenceEqual(["20", "13", "01", "00"]), "level 105 sprite GFX upload IDs mismatch");
        Check((Required(Required(spriteRoot, "vram"), "format").GetString() ?? "") == "snes_4bpp_tiles_in_sprite_vram_order_0x6000_to_0x7fff", "sprite VRAM format mismatch");
    }

    private static void CheckPalettes(string generatedRoot)
    {
        foreach (var relative in new[] { "palettes/level_105_palette.json", "palettes/level_1CB_palette.json" })
        {
            using var palette = LoadJson(Path.Combine(generatedRoot, relative));
            var root = palette.RootElement;
            Check((Required(root, "status").GetString() ?? "") == "preview", $"{relative} status mismatch");
            Check((Required(root, "source").GetString() ?? "") == "vanilla_header_tables", $"{relative} source mismatch");
            Check(RequiredArray(root, "snes_bgr555").Count() == 256, $"{relative} SNES palette length mismatch");
            Check(RequiredArray(root, "rgb888").Count() == 256, $"{relative} RGB palette length mismatch");
        }
    }

    private static void CheckAudio(string generatedRoot, JsonElement manifest)
    {
        var audio = Required(Required(manifest, "assets"), "audio");
        Check((Required(audio, "status").GetString() ?? "") == "partial", "audio status mismatch");
        Check(Required(audio, "sample_rate").GetInt32() == 32000, "audio sample rate mismatch");
        var banks = Required(audio, "banks");
        foreach (var bankName in new[] { "spc_engine", "spc_level_music_bank", "spc_overworld_music_bank", "spc_credits_music_bank", "spc_samples" })
        {
            Check(banks.TryGetProperty(bankName, out _), $"audio bank missing {bankName}");
        }

        using var audioManifest = LoadJson(Path.Combine(generatedRoot, Required(audio, "file").GetString() ?? ""));
        var decodedSamples = RequiredArray(audioManifest.RootElement, "decoded_samples").ToArray();
        Check(decodedSamples.Length >= 3, "audio decoded sample preview count too small");
        foreach (var sample in decodedSamples)
        {
            Check(Required(sample, "sample_rate").GetInt32() == 32000, "decoded sample rate mismatch");
            CheckPngOrWave(Path.Combine(generatedRoot, Required(sample, "file").GetString() ?? ""));
        }
    }

    private static void CheckSourceAddress(JsonElement tables, string tableName, string expectedAddress)
    {
        var table = Required(tables, tableName);
        Check((Required(table, "source_addr").GetString() ?? "") == expectedAddress, $"{tableName} source address mismatch");
    }

    private static void CheckPngOrWave(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase))
        {
            Check(bytes.Length >= 44 && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
                  bytes[8] == (byte)'W' && bytes[9] == (byte)'A' && bytes[10] == (byte)'V' && bytes[11] == (byte)'E',
                $"{path} is not a valid RIFF/WAVE preview");
            return;
        }

        CheckPngHeader(path);
    }

    private static void CheckPngHeader(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Check(bytes.Length >= 24, $"{path} too small for PNG header");
        Check(bytes[0] == 0x89 && bytes[1] == (byte)'P' && bytes[2] == (byte)'N' && bytes[3] == (byte)'G' &&
              bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
            $"{path} is not a PNG");
        Check(bytes[12] == (byte)'I' && bytes[13] == (byte)'H' && bytes[14] == (byte)'D' && bytes[15] == (byte)'R',
            $"{path} missing PNG IHDR");
        var width = ReadBigEndianInt32(bytes, 16);
        var height = ReadBigEndianInt32(bytes, 20);
        Check(width > 0 && height > 0, $"{path} has invalid PNG dimensions {width}x{height}");
    }

    private static JsonDocument LoadJson(string path)
    {
        Check(File.Exists(path), $"JSON file missing: {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
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

    private static string Sha1Hex(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA1.HashData(stream));
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24) |
               (bytes[offset + 1] << 16) |
               (bytes[offset + 2] << 8) |
               bytes[offset + 3];
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
