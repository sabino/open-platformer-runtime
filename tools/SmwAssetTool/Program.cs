using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPlatformerRuntime.SmwAssets;
using OpenPlatformerRuntime.Audio;

internal static class Program
{
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
    private static readonly EntranceTableSpec[] EntranceTableSpecs =
    [
        new("level_info_05f000", 0x05F000, 0x200),
        new("level_info_05f200", 0x05F200, 0x200),
        new("level_info_05f400", 0x05F400, 0x200),
        new("level_info_05f600", 0x05F600, 0x200),
        new("secondary_entrance_type_05fe00", 0x05FE00, 0x200),
        new("secondary_level_low_05f800", 0x05F800, 0x200),
        new("secondary_x_05fc00", 0x05FC00, 0x200),
        new("secondary_y_05fa00", 0x05FA00, 0x200),
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
    private static readonly int[] AudioPreviewSampleIds = [7, 8, 9, 14, 16];
    private static readonly string[] PlayerPaletteNames = ["mario", "luigi", "fire_mario", "fire_luigi"];

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
                "extract-graphics" when args.Length == 3 => ExtractGraphics(args[1], args[2]),
                "extract-level-graphics" when args.Length is 3 or 4 => ExtractLevelGraphics(args[1], args[2], args.Length == 4 ? args[3] : "105,1CB"),
                "extract-level-data" when args.Length is 3 or 4 => ExtractLevelData(args[1], args[2], args.Length == 4 ? args[3] : "all"),
                "extract-terrain" when args.Length is 3 or 4 => ExtractTerrain(args[1], args[2], args.Length == 4 ? args[3] : "0C7,105,106,1CB"),
                "audit-terrain" when args.Length == 3 => AuditTerrain(args[1], args[2]),
                "extract-levels" when args.Length == 3 => ExtractLevels(args[1], args[2]),
                "verify-levels" when args.Length == 3 => VerifyLevels(args[1], args[2]),
                "extract-audio-previews" when args.Length == 3 => ExtractAudioPreviews(args[1], args[2]),
                "extract-audio-native" when args.Length == 3 => ExtractNativeAudio(args[1], args[2]),
                "verify-audio-previews" when args.Length == 3 => VerifyAudioPreviews(args[1], args[2]),
                "extract-player-metadata" when args.Length == 3 => ExtractPlayerMetadata(args[1], args[2]),
                "verify-player-metadata" when args.Length == 3 => VerifyPlayerMetadata(args[1], args[2]),
                "extract-entrance-tables" when args.Length == 3 => ExtractEntranceTables(args[1], args[2]),
                "verify-entrance-tables" when args.Length == 3 => VerifyEntranceTables(args[1], args[2]),
                "extract-palettes" when args.Length == 3 => ExtractPalettes(args[1], args[2]),
                "verify-palettes" when args.Length == 3 => VerifyPalettes(args[1], args[2]),
                "inspect-rom" when args.Length == 2 => InspectRom(args[1]),
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

    private static int InspectRom(string romPath)
    {
        var fullPath = Path.GetFullPath(romPath);
        Check(File.Exists(fullPath), $"ROM does not exist: {fullPath}");
        var inspection = SmwRomInspector.Inspect(File.ReadAllBytes(fullPath));
        WriteJson(Console.OpenStandardOutput(), inspection.ToSerializable());
        return inspection.IsSupported ? 0 : 1;
    }

    private static int ExtractGraphics(string romPath, string outDir)
    {
        var rom = Rom.Load(romPath);
        var outputRoot = Path.GetFullPath(outDir);
        Directory.CreateDirectory(outputRoot);
        var decoder = new SmwGraphicsDecoder(rom.Native);
        var files = new List<object>();
        foreach (var file in decoder.DecodeAll())
        {
            var stem = $"gfx/full/gfx{file.Id:X2}";
            var rawPath = $"{stem}.raw.bin";
            var pixelsPath = $"{stem}.pixels.bin";
            WriteBinary(Path.Combine(outputRoot, rawPath), file.Bytes.ToArray());
            WriteBinary(Path.Combine(outputRoot, pixelsPath), file.DecodePixels());
            string? planarPath = null;
            if (file.Format != SmwGraphicsFormat.Packed3Bpp)
            {
                planarPath = $"{stem}.planar4.bin";
                WriteBinary(Path.Combine(outputRoot, planarPath), file.ExpandTo4Bpp());
            }
            files.Add(new
            {
                id = $"{file.Id:X2}", source_addr = $"0x{file.SourceAddress:X6}",
                compressed_length = file.CompressedLength, decompressed_length = file.Bytes.Length,
                format = GraphicsFormatName(file.Format), tile_count = file.TileCount,
                raw_file = rawPath, pixels_file = pixelsPath, planar4_file = planarPath,
                raw_sha1 = Convert.ToHexString(SHA1.HashData(file.Bytes)),
            });
            if (file.Format == SmwGraphicsFormat.Planar3Bpp && file.TileCount == 128)
                WriteBinary(Path.Combine(outputRoot, $"{stem}.upload-00.bin"), decoder.DecodeLevelUpload(file.Id, 0));
        }
        WriteBinary(Path.Combine(outputRoot, "gfx/full/gfx08.upload-11.bin"), decoder.DecodeLevelUpload(8, 0x11));
        WriteBinary(Path.Combine(outputRoot, "gfx/full/gfx01.special-world.bin"), decoder.DecodeLevelUpload(1, 0, specialWorld: true));
        WriteJson(Path.Combine(outputRoot, "native_graphics_manifest.json"), new
        {
            schema_version = 1, decoder_version = 1, source_rom_sha1 = rom.Sha1, files,
        });
        Console.WriteLine($"smw-asset-tool: extracted all {files.Count} vanilla graphics files to {outputRoot}");
        return 0;
    }

    private static string GraphicsFormatName(SmwGraphicsFormat format) => format switch
    {
        SmwGraphicsFormat.Planar2Bpp => "snes_2bpp_planar",
        SmwGraphicsFormat.Planar3Bpp => "snes_3bpp_planar",
        SmwGraphicsFormat.Planar4Bpp => "snes_4bpp_planar",
        SmwGraphicsFormat.Packed3Bpp => "smw_mode7_packed_3bpp",
        _ => throw new InvalidOperationException("Unknown graphics format"),
    };

    private static int ExtractLevelGraphics(string romPath, string outDir, string levelIds)
    {
        var rom = Rom.Load(romPath);
        var root = Path.GetFullPath(outDir);
        var palettes = new SmwPaletteDecoder(rom.Native);
        var playerPixels = new SmwGraphicsDecoder(rom.Native).Decode(0x32).DecodePixels();
        var bootPalette = new ushort[256];
        palettes.ApplyLevelBase(bootPalette, default);
        WriteBinary(Path.Combine(root, "boot-palette-base.bin"), WordBytes(bootPalette));
        var entries = new List<object>();
        foreach (var text in levelIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var id = int.Parse(text, System.Globalization.NumberStyles.HexNumber);
            var level = new SmwLevelGraphics(rom.Native, id);
            var header = level.Header;
            var dir = Path.Combine(root, $"level_{id:X3}");
            var paletteBase = new ushort[256];
            var sky = palettes.ApplyLevelBase(paletteBase, header.Palette);
            var palette = palettes.CreateLevelEntry(header.Palette);
            var foreground = level.ReadBackgroundWindow();
            var sprites = level.ReadSpriteWindow();
            var map16 = level.ReadForegroundMap16();
            WriteBinary(Path.Combine(dir, "header.bin"), header.Bytes.ToArray());
            WriteBinary(Path.Combine(dir, "palette_base.bin"), WordBytes(paletteBase));
            WriteBinary(Path.Combine(dir, "palette_entry.bin"), WordBytes(palette));
            WriteBinary(Path.Combine(dir, "foreground.bin"), foreground);
            WriteBinary(Path.Combine(dir, "sprites.bin"), sprites);
            WriteBinary(Path.Combine(dir, "map16.bin"), WordBytes(map16));
            var images = new List<object>();
            void SaveImage(string name, SmwRgbaImage image)
            {
                WriteBinary(Path.Combine(dir, name), image.Bytes);
                images.Add(new { file = name, width = image.Width, height = image.Height, sha256 = Convert.ToHexString(SHA256.HashData(image.Bytes)) });
            }
            SaveImage("map16.rgba", SmwGraphicsImages.CreateMap16Atlas(map16, foreground, palette));
            var background = level.ReadBackgroundTiles();
            if (background != null)
            {
                WriteBinary(Path.Combine(dir, "background.bin"), background.Bytes);
                SaveImage("background.rgba", SmwGraphicsImages.CreateLayer2(background, level.ReadBackgroundMap16(), foreground, palette));
            }
            for (var variant = 0; variant < 4; variant++)
            {
                var row = palettes.CreatePlayerRow(variant);
                WriteBinary(Path.Combine(dir, $"player-palette-{variant}.bin"), WordBytes(row));
                SaveImage($"player-{variant}.rgba", SmwGraphicsImages.CreateAtlas(playerPixels, row));
            }
            var spritePixels = SnesGraphics.DecodePlanar(sprites, 4);
            for (var row = 0; row < 8; row++)
                SaveImage($"sprites-{row}.rgba", SmwGraphicsImages.CreateAtlas(spritePixels, palette.AsSpan((8 + row) * 16, 16)));
            entries.Add(new
            {
                id = $"{id:X3}", source_addr = $"0x{header.SourceAddress:X6}", sky, screens = header.Screens, level_mode = header.Mode,
                tileset = header.Tileset, sprite_graphics = header.SpriteGraphics, layout_flags = header.LayoutFlags, images,
                background = background == null ? null : new { source_addr = $"0x{background.SourceAddress:X6}", compressed_length = background.CompressedLength, page = background.Page },
            });
        }
        WriteJson(Path.Combine(root, "native_level_graphics.json"), new { schema_version = 1, source_rom_sha1 = rom.Sha1, levels = entries });
        Console.WriteLine($"smw-asset-tool: extracted native level graphics for {entries.Count} levels to {root}");
        return 0;
    }

    private static byte[] WordBytes(ReadOnlySpan<ushort> words)
    {
        var bytes = new byte[words.Length * 2];
        for (var i = 0; i < words.Length; i++)
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2, 2), words[i]);
        return bytes;
    }

    private static int ExtractLevelData(string romPath, string outDir, string ids)
    {
        var rom = Rom.Load(romPath);
        var output = Path.GetFullPath(outDir);
        var entrances = new SmwEntranceDecoder(rom.Native);
        var levels = ids == "all" ? Enumerable.Range(0, 512) : ids.Split(',').Select(id => int.Parse(id, System.Globalization.NumberStyles.HexNumber));
        var summaries = new List<object>();
        var aliases = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var id in levels)
        {
            var data = new SmwLevelData(rom.Native, id);
            var key = $"{id:X3}";
            var dir = Path.Combine(output, key);
            WriteBinary(Path.Combine(dir, "layer1.bin"), data.Layer1.Bytes);
            WriteBinary(Path.Combine(dir, "layer2.bin"), data.Layer2?.Bytes ?? data.Background!.Bytes);
            WriteBinary(Path.Combine(dir, "sprites.bin"), data.Sprites.Bytes);
            object Objects(SmwObjectStream stream) => new
            {
                source_addr = $"0x{stream.SourceAddress:X6}", length = stream.Bytes.Length,
                objects = stream.Objects.Select(obj => new
                {
                    sequence = obj.Sequence, offset = obj.Offset, raw = new int[] { obj.B0, obj.B1, obj.SizeOrType },
                    object_id = obj.Id, size_or_type = obj.SizeOrType, exit_low = obj.ExitLow,
                    placement = new
                    {
                        layer = obj.Placement.Layer, screen_cursor = obj.Placement.Screen,
                        sub_x = obj.Placement.SubX, sub_y = obj.Placement.SubY, high_subscreen = obj.Placement.HighSubscreen,
                        vertical = obj.Placement.Vertical, map16_offset = obj.Placement.Map16Offset,
                        x_tile = obj.Placement.XTile, y_tile = obj.Placement.YTile,
                    },
                }).ToArray(),
                exits = stream.Exits.Select(exit => new { screen = exit.Screen, exit_low = exit.DestinationLow, raw_r11 = exit.Properties,
                    requested_secondary = exit.Secondary, destination_main_map = exit.ResolveId(false), destination_submap = exit.ResolveId(true) }).ToArray(),
            };
            WriteJson(Path.Combine(dir, "data.json"), new
            {
                id = key, header = data.Header.Bytes.ToArray().Select(value => (int)value).ToArray(), layout_flags = data.Header.LayoutFlags,
                layer1 = Objects(data.Layer1), layer2 = data.Layer2 == null ? null : Objects(data.Layer2),
                layer2_addr = $"0x{data.Layer2Address:X6}", layer2_kind = data.Layer2 == null ? "rle_background" : "object_stream",
                layer2_compressed_length = data.Layer2?.Bytes.Length ?? data.Background!.CompressedLength,
                sprites = new
                {
                    source_addr = $"0x{data.Sprites.SourceAddress:X6}", header = data.Sprites.Header,
                    memory_setting = data.Sprites.MemorySetting, buoyancy = data.Sprites.Buoyancy,
                    records = data.Sprites.Sprites.Select(sprite => new
                    {
                        index = sprite.Index, offset = sprite.Offset, raw = new int[] { sprite.Position, sprite.ScreenPosition, sprite.Id },
                        screen = sprite.Screen, along = sprite.Along, across = sprite.Across, x = sprite.X, y = sprite.Y,
                        vertical = sprite.Vertical,
                    }).ToArray(),
                },
                main_entrance = entrances.Main(id), secondary_entrance = entrances.Secondary(id),
            });
            var alias = $"{data.Header.SourceAddress:X6}:{data.Layer2Address:X6}:{data.Sprites.SourceAddress:X6}";
            if (!aliases.TryGetValue(alias, out var aliasIds)) aliases[alias] = aliasIds = [];
            aliasIds.Add(key);
            summaries.Add(new { id = key, objects = data.Layer1.Objects.Count, sprites = data.Sprites.Sprites.Count, vertical = data.Header.Vertical,
                layer2_kind = data.Layer2 == null ? "rle_background" : "object_stream", pointer_tuple = alias });
        }
        foreach (var pair in entrances.TableAddresses)
            WriteBinary(Path.Combine(output, "entrances", pair.Key + ".bin"), rom.Native.Data.ReadBytes(pair.Value, 512));
        WriteJson(Path.Combine(output, "native_level_data.json"), new
        {
            schema_version = 1, source_rom_sha1 = rom.Sha1, levels = summaries,
            pointer_alias_groups = aliases.Values.Where(group => group.Count > 1).ToArray(),
            classification = "Pointer aliases are inventory evidence, not proof that a level is unused; progression reachability remains to be audited",
        });
        Console.WriteLine($"smw-asset-tool: extracted {summaries.Count} vanilla level slots and entrance tables to {output}");
        return 0;
    }

    private static int AuditTerrain(string romPath, string outDir)
    {
        var rom = Rom.Load(romPath);
        var levels = new List<object>();
        var constructed = 0;
        var unsupported = 0;
        var errors = 0;
        for (var id = 0; id < 512; id++)
        {
            var data = new SmwLevelData(rom.Native, id);
            try
            {
                var terrain = new SmwMap16Terrain(rom.Native, data);
                constructed++;
                levels.Add(new { id = $"{id:X3}", status = "constructed", mode = data.Header.Mode, tileset = data.Header.Tileset,
                    layer1 = $"0x{data.Header.SourceAddress:X6}", layer2 = $"0x{data.Layer2Address:X6}",
                    low_sha256 = Convert.ToHexString(SHA256.HashData(terrain.LowBytes)), high_sha256 = Convert.ToHexString(SHA256.HashData(terrain.HighBytes)) });
            }
            catch (SmwTerrainUnsupportedObjectException error)
            {
                unsupported++;
                levels.Add(new { id = $"{id:X3}", status = "unsupported", mode = data.Header.Mode, tileset = data.Header.Tileset,
                    layer = error.Layer, sequence = error.Sequence, object_id = $"{error.ObjectId:X2}", type = $"{error.Type:X2}",
                    routine = $"0x{error.Routine:X6}", message = error.Message });
            }
            catch (Exception error) when (error is InvalidDataException or ArgumentOutOfRangeException or NotSupportedException)
            {
                errors++;
                levels.Add(new { id = $"{id:X3}", status = "error", mode = data.Header.Mode, tileset = data.Header.Tileset, message = error.Message });
            }
        }
        WriteJson(Path.Combine(Path.GetFullPath(outDir), "native_terrain_audit.json"), new
        {
            source_rom_sha1 = rom.Sha1, slots = 512, constructed, unsupported, errors, levels,
            scope = "Native expansion with fresh state; constructed does not establish original-buffer, rendering, gameplay, or reachability parity",
        });
        Console.WriteLine($"smw-asset-tool: terrain audit constructed={constructed} unsupported={unsupported} errors={errors} slots=512");
        return errors == 0 ? 0 : 1;
    }

    private static int ExtractTerrain(string romPath, string outDir, string ids)
    {
        var rom = Rom.Load(romPath);
        var root = Path.GetFullPath(outDir);
        var entries = new List<object>();
        foreach (var text in ids.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var id = int.Parse(text, System.Globalization.NumberStyles.HexNumber);
            var level = new SmwLevelData(rom.Native, id);
            var terrain = new SmwMap16Terrain(rom.Native, level);
            var dir = Path.Combine(root, $"{id:X3}");
            WriteBinary(Path.Combine(dir, "map16-low.bin"), terrain.LowBytes.ToArray());
            WriteBinary(Path.Combine(dir, "map16-high.bin"), terrain.HighBytes.ToArray());
            WriteJson(Path.Combine(dir, "tiles.json"), terrain.LayerTiles());
            entries.Add(new
            {
                id = $"{id:X3}", header = level.Header.Bytes.ToArray().Select(value => (int)value), level_mode = level.Header.Mode,
                tileset = level.Header.Tileset, buffer_size = SmwMap16Terrain.BufferSize,
                low_sha256 = Convert.ToHexString(SHA256.HashData(terrain.LowBytes)),
                high_sha256 = Convert.ToHexString(SHA256.HashData(terrain.HighBytes)),
            });
        }
        WriteJson(Path.Combine(root, "native_terrain.json"), new
        {
            schema_version = 1, source_rom_sha1 = rom.Sha1, levels = entries,
            initial_state = "zero item memory; moon, checkpoint, five-Yoshi-coin, midpoint, and palace-switch flags clear",
            unsupported_objects = "fail explicitly; never replace with approximate geometry",
        });
        Console.WriteLine($"smw-asset-tool: expanded native Map16 terrain for {entries.Count} levels to {root}");
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

    private static int ExtractNativeAudio(string romPath, string outDir)
    {
        var rom = Rom.Load(romPath);
        var data = SmwAudioData.FromRom(rom.Native);
        var bytes = data.ToBinary();
        var path = Path.Combine(Path.GetFullPath(outDir), "audio/native_audio_data.bin");
        WriteBinary(path, bytes);
        var roundtrip = SmwAudioData.FromBinary(File.ReadAllBytes(path));
        foreach (var bank in Enum.GetValues<SmwMusicBank>())
            Check(data.CreateDataImage(bank).SequenceEqual(roundtrip.CreateDataImage(bank)), "Native audio binary roundtrip differs");
        WriteJson(Path.Combine(Path.GetFullPath(outDir), "native_audio_manifest.json"), new
        {
            schema_version = SmwAudioData.SchemaVersion, source_rom_sha1 = rom.Sha1,
            file = "audio/native_audio_data.bin", sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            voices = 8, samples = 20, level_song_entries = 29, overworld_song_entries = 9, credits_song_entries = 12,
            sample_rate = 32000, normal_input = "ROM-derived native data; no SPC700 CPU or pre-rendered music",
            fidelity_scope = "DSP fixtures and translated-driver events; whole-firmware timing remains open",
        });
        Console.WriteLine($"smw-asset-tool: extracted native audio schema={SmwAudioData.SchemaVersion} bytes={bytes.Length}");
        return 0;
    }

    private static int ExtractAudioPreviews(string romPath, string outDir)
    {
        var rom = Rom.Load(romPath);
        var outputRoot = Path.GetFullPath(outDir);
        Directory.CreateDirectory(outputRoot);
        var manifest = BuildAudioPreviewManifest(rom);
        foreach (var sample in manifest.DecodedSamples)
        {
            WriteBinary(Path.Combine(outputRoot, sample.File), sample.WavData);
        }

        WriteJson(Path.Combine(outputRoot, "native_audio_preview_manifest.json"), manifest.ToSerializable());
        Console.WriteLine($"smw-asset-tool: extracted native audio previews to {outputRoot}");
        return 0;
    }

    private static int VerifyAudioPreviews(string romPath, string generatedDir)
    {
        var rom = Rom.Load(romPath);
        var generatedRoot = Path.GetFullPath(generatedDir);
        var manifest = BuildAudioPreviewManifest(rom);
        VerifyGeneratedAudioManifest(generatedRoot, manifest);
        foreach (var sample in manifest.DecodedSamples)
        {
            var path = Path.Combine(generatedRoot, sample.File);
            Check(File.Exists(path), $"generated audio preview missing: {sample.File}");
            var actual = File.ReadAllBytes(path);
            Check(actual.SequenceEqual(sample.WavData), $"generated audio preview differs from C# BRR decode: {sample.File}");
        }

        Console.WriteLine($"smw-asset-tool: verified native BRR audio previews for samples {string.Join(",", manifest.DecodedSamples.Select(sample => sample.Id))}");
        return 0;
    }

    private static int ExtractPlayerMetadata(string romPath, string outDir)
    {
        var rom = Rom.Load(romPath);
        var outputRoot = Path.GetFullPath(outDir);
        Directory.CreateDirectory(outputRoot);
        WriteJson(Path.Combine(outputRoot, "native_player_metadata.json"), BuildPlayerMetadata(rom).ToSerializable());
        Console.WriteLine($"smw-asset-tool: extracted native player metadata to {outputRoot}");
        return 0;
    }

    private static int VerifyPlayerMetadata(string romPath, string generatedDir)
    {
        var rom = Rom.Load(romPath);
        var generatedRoot = Path.GetFullPath(generatedDir);
        VerifyGeneratedPlayerMetadata(generatedRoot, BuildPlayerMetadata(rom));
        Console.WriteLine("smw-asset-tool: verified native player OAM tables and palettes");
        return 0;
    }

    private static int ExtractEntranceTables(string romPath, string outDir)
    {
        var rom = Rom.Load(romPath);
        var outputRoot = Path.GetFullPath(outDir);
        var tables = BuildEntranceTables(rom);
        WriteJson(Path.Combine(outputRoot, "levels", "secondary_tables.json"), tables.ToSerializable());
        Console.WriteLine($"smw-asset-tool: extracted native entrance tables to {outputRoot}");
        return 0;
    }

    private static int VerifyEntranceTables(string romPath, string generatedDir)
    {
        var rom = Rom.Load(romPath);
        var generatedRoot = Path.GetFullPath(generatedDir);
        VerifyGeneratedEntranceTables(generatedRoot, BuildEntranceTables(rom));
        Console.WriteLine("smw-asset-tool: verified native entrance and secondary-exit tables");
        return 0;
    }

    private static int ExtractPalettes(string romPath, string outDir)
    {
        var rom = Rom.Load(romPath);
        var outputRoot = Path.GetFullPath(outDir);
        var palettes = BuildPaletteAssets(rom);
        WriteJson(Path.Combine(outputRoot, "palettes", "global_palettes.json"), palettes.Global.ToSerializable());
        foreach (var level in palettes.Levels)
        {
            WriteJson(Path.Combine(outputRoot, "palettes", $"level_{level.LevelId}_palette.json"), level.ToSerializable());
        }

        Console.WriteLine($"smw-asset-tool: extracted native palette assets to {outputRoot}");
        return 0;
    }

    private static int VerifyPalettes(string romPath, string generatedDir)
    {
        var rom = Rom.Load(romPath);
        var generatedRoot = Path.GetFullPath(generatedDir);
        VerifyGeneratedPalettes(generatedRoot, BuildPaletteAssets(rom));
        Console.WriteLine("smw-asset-tool: verified native global and level palettes");
        return 0;
    }

    private static CoreManifest BuildCoreManifest(Rom rom)
    {
        var assets = new List<CoreAsset>();
        AddPlayerGfx(rom, assets, 0x32);
        AddPlayerGfx(rom, assets, 0x33);
        AddAudioBanks(rom, assets);
        AddGlobalMap16(rom, assets);

        return new CoreManifest(
            SchemaVersion: 1,
            SourceRomSha1: rom.Sha1,
            SourceRomSize: rom.Native.Data.Length,
            Assets: assets);
    }

    private static void AddPlayerGfx(Rom rom, List<CoreAsset> assets, int id)
    {
        var file = new SmwGraphicsDecoder(rom.Native).Decode(id);
        assets.Add(new CoreAsset(
            File: $"gfx/gfx{id:X2}.bin".ToLowerInvariant(),
            Kind: "player_gfx",
            SourceAddress: file.SourceAddress,
            Format: GraphicsFormatName(file.Format),
            Data: file.Bytes.ToArray(),
            Extra: new Dictionary<string, object>
            {
                ["compressed_length"] = file.CompressedLength,
                ["decompressed_length"] = file.Bytes.Length,
                ["tile_count"] = file.TileCount,
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
        foreach (var levelId in levelIds)
        {
            var data = new SmwLevelData(rom.Native, levelId);
            var layer1Address = data.Layer1.SourceAddress;
            var layer1Raw = data.Layer1.Bytes;
            var layer1Length = layer1Raw.Length;
            var header = ProjectLevelHeader(data.Header);
            var parsedLayer1 = ProjectObjects(data.Layer1);
            AnnotateVanillaScreenExits(parsedLayer1.ScreenExits, levelId);

            var layer2Address = data.Layer2Address;
            var layer2Kind = data.Layer2 == null ? "rle_background" : "object_stream";
            var layer2Raw = data.Layer2?.Bytes ?? data.Background!.Bytes;
            var layer2Length = data.Layer2?.Bytes.Length ?? data.Background!.CompressedLength;
            var spriteAddress = data.Sprites.SourceAddress;
            var spriteRaw = data.Sprites.Bytes;
            var spriteLength = spriteRaw.Length;
            var parsedSprites = new ParsedSprites(data.Sprites.Header, data.Sprites.Sprites.Select(sprite => new SpriteRecord(
                sprite.Offset, sprite.Position, sprite.ScreenPosition, sprite.Screen, sprite.X, sprite.Y,
                sprite.Position >> 2 & 3, sprite.Id, [sprite.Position, sprite.ScreenPosition, sprite.Id])).ToList());

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

    private static LevelHeader ProjectLevelHeader(SmwLevelHeader h) => new(
        h.Bytes.ToArray().Select(value => (int)value).ToArray(), h.Screens, h.BackgroundPalette, h.Mode, h.Sky,
        h.SpriteGraphics, h.Music, h.Layer3Priority, h.ForegroundPalette, h.SpritePalette, h.Timer,
        h.Tileset, h.Layer1Scroll, h.ItemMemory, h.LayoutFlags, h.Vertical,
        h.Vertical ? 32 : h.Screens * 16, h.Vertical ? h.Screens * 16 : 27);

    private static ParsedLayerObjects ProjectObjects(SmwObjectStream stream)
    {
        var objects = stream.Objects.Select(obj =>
        {
            var p = obj.Placement;
            var adjusted0 = p.SubY | (p.HighSubscreen ? 16 : 0) | (obj.B0 & 0xE0);
            var adjusted1 = p.SubX | (obj.B1 & 0xF0);
            return new LevelObject(obj.Sequence, obj.Offset, [obj.B0, obj.B1, obj.SizeOrType], obj.Id, obj.SizeOrType,
                obj.ExitLow is byte low ? [low] : [], new ObjectPlacement(p.Layer, p.Screen, obj.ScreenIncrement,
                p.SubX, p.SubY, p.HighSubscreen, p.Map16Offset, p.XTile, p.YTile, p.XTile * 16, p.YTile * 16,
                [adjusted0, adjusted1, obj.SizeOrType]));
        }).ToList();
        var exits = stream.Objects.Where(obj => obj.IsExit).Select(obj => new ScreenExit(obj.B0 & 31, obj.Placement.Screen,
            obj.ExitLow!.Value, obj.B1, obj.B1 & 3,
            ((obj.B1 & 1) << 8) | obj.ExitLow.Value, (obj.B1 >> 1) & 1,
            obj.B1 & 15, (obj.B1 >> 1) & 1, null, null, null, null)).ToList();
        return new ParsedLayerObjects(objects, exits);
    }

    private static void AnnotateVanillaScreenExits(List<ScreenExit> exits, int sourceLevelId)
    {
        // Historical development projection. Actual exit resolution uses the
        // current overworld state in SmwScreenExit.ResolveId.
        for (var i = 0; i < exits.Count; i++)
            exits[i] = exits[i] with { VanillaDestinationLow = exits[i].ExitLow,
                VanillaSourceMapHigh = sourceLevelId >> 8 & 1,
                VanillaDestination = (sourceLevelId & 0x100) | exits[i].ExitLow,
                VanillaSecondary = exits[i].RawR11 >> 1 };
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

    private static AudioPreviewManifest BuildAudioPreviewManifest(Rom rom)
    {
        var sampleBank = rom.GetBytes(0x0F8000, 28538);
        var (ram, uploadBlocks) = ParseSpcUpload(sampleBank);
        var decodedSamples = new List<AudioPreviewSample>();
        foreach (var sampleId in AudioPreviewSampleIds)
        {
            var entry = 0x8000 + sampleId * 4;
            var start = ram[entry] | (ram[entry + 1] << 8);
            var loop = ram[entry + 2] | (ram[entry + 3] << 8);
            var (samples, brrBytes) = DecodeBrrSample(ram, start);
            var wavData = WriteWavMono16(samples, sampleRate: 32000);
            decodedSamples.Add(new AudioPreviewSample(
                Id: sampleId,
                File: $"audio/sample_{sampleId:00}.wav",
                SpcStart: start,
                SpcLoop: loop,
                BrrBytes: brrBytes,
                SampleCount: samples.Count,
                SampleRate: 32000,
                WavData: wavData));
        }

        return new AudioPreviewManifest(
            SourceRomSha1: rom.Sha1,
            SampleUploadBlocks: uploadBlocks,
            DecodedSamples: decodedSamples);
    }

    private static (byte[] Ram, List<SpcUploadBlock> Blocks) ParseSpcUpload(byte[] payload)
    {
        var ram = new byte[0x10000];
        var blocks = new List<SpcUploadBlock>();
        var offset = 0;
        while (offset + 2 <= payload.Length)
        {
            var count = payload[offset] | (payload[offset + 1] << 8);
            offset += 2;
            if (count == 0)
            {
                return (ram, blocks);
            }

            Check(offset + 2 + count <= payload.Length, "truncated SPC upload block");
            var target = payload[offset] | (payload[offset + 1] << 8);
            offset += 2;
            Array.Copy(payload, offset, ram, target, count);
            blocks.Add(new SpcUploadBlock(target, count));
            offset += count;
        }

        throw new InvalidOperationException("SPC upload stream missing zero terminator");
    }

    private static (List<int> Samples, int BrrBytes) DecodeBrrSample(byte[] ram, int start)
    {
        var samples = new List<int>();
        var old = 0;
        var older = 0;
        var offset = start;
        while (offset + 9 <= ram.Length)
        {
            var command = ram[offset];
            var shift = command >> 4;
            var filterId = (command >> 2) & 0x03;
            for (var i = 0; i < 16; i++)
            {
                var packed = ram[offset + 1 + i / 2];
                var nibble = (packed >> (((i & 1) == 1) ? 0 : 4)) & 0x0F;
                var sample = (nibble & 7) - (nibble & 8);
                if (shift <= 12)
                {
                    sample = (sample << shift) >> 1;
                }
                else
                {
                    sample = (sample >> 3) << 12;
                }

                if (filterId == 1)
                {
                    sample += old + ((-old) >> 4);
                }
                else if (filterId == 2)
                {
                    sample += old * 2 + ((-old * 3) >> 5) - older + (older >> 4);
                }
                else if (filterId == 3)
                {
                    sample += old * 2 + ((-old * 13) >> 6) - older + ((older * 3) >> 4);
                }

                sample = Math.Clamp(sample, -0x8000, 0x7FFF);
                sample = (sample & 0x3FFF) - (sample & 0x4000);
                older = old;
                old = sample;
                samples.Add(sample * 2);
            }

            offset += 9;
            if ((command & 1) != 0)
            {
                break;
            }
        }

        return (samples, offset - start);
    }

    private static byte[] WriteWavMono16(IReadOnlyList<int> samples, int sampleRate)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var dataBytes = samples.Count * 2;
        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataBytes);
        foreach (var sample in samples)
        {
            writer.Write((short)Math.Clamp(sample, -32768, 32767));
        }

        return stream.ToArray();
    }

    private static void VerifyGeneratedAudioManifest(string generatedRoot, AudioPreviewManifest expected)
    {
        var path = Path.Combine(generatedRoot, "audio", "audio_manifest.json");
        Check(File.Exists(path), $"generated audio manifest missing: {path}");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Check(Required(root, "sample_rate").GetInt32() == 32000, "audio manifest sample rate mismatch");
        var blocks = RequiredArray(root, "sample_upload_blocks").ToArray();
        Check(blocks.Length == expected.SampleUploadBlocks.Count, "SPC sample upload block count mismatch");
        for (var i = 0; i < blocks.Length; i++)
        {
            Check((Required(blocks[i], "target").GetString() ?? "") == $"0x{expected.SampleUploadBlocks[i].Target:X4}", $"SPC upload block {i} target mismatch");
            Check(Required(blocks[i], "length").GetInt32() == expected.SampleUploadBlocks[i].Length, $"SPC upload block {i} length mismatch");
        }

        var samples = RequiredArray(root, "decoded_samples").ToArray();
        foreach (var expectedSample in expected.DecodedSamples)
        {
            var sample = samples.FirstOrDefault(candidate => Required(candidate, "id").GetInt32() == expectedSample.Id);
            Check(sample.ValueKind != JsonValueKind.Undefined, $"decoded sample {expectedSample.Id} missing from audio manifest");
            Check((Required(sample, "file").GetString() ?? "") == expectedSample.File, $"decoded sample {expectedSample.Id} file mismatch");
            Check((Required(sample, "spc_start").GetString() ?? "") == $"0x{expectedSample.SpcStart:X4}", $"decoded sample {expectedSample.Id} start mismatch");
            Check((Required(sample, "spc_loop").GetString() ?? "") == $"0x{expectedSample.SpcLoop:X4}", $"decoded sample {expectedSample.Id} loop mismatch");
            Check(Required(sample, "brr_bytes").GetInt32() == expectedSample.BrrBytes, $"decoded sample {expectedSample.Id} BRR length mismatch");
            Check(Required(sample, "sample_count").GetInt32() == expectedSample.SampleCount, $"decoded sample {expectedSample.Id} sample count mismatch");
            Check(Required(sample, "sample_rate").GetInt32() == expectedSample.SampleRate, $"decoded sample {expectedSample.Id} sample rate mismatch");
            Check((Required(sample, "sha1").GetString() ?? "") == Convert.ToHexString(SHA1.HashData(expectedSample.WavData)), $"decoded sample {expectedSample.Id} WAV sha1 mismatch");
        }
    }

    private static PlayerMetadata BuildPlayerMetadata(Rom rom)
    {
        var data = new SmwPlayerGraphicsData(rom.Native);
        var paletteVariants = new List<PlayerPaletteVariant>();
        for (var i = 0; i < PlayerPaletteNames.Length; i++)
        {
            paletteVariants.Add(new PlayerPaletteVariant(i, PlayerPaletteNames[i], BuildPlayerSpritePaletteWords(rom, i)));
        }

        var tilePointerTables = new Dictionary<string, int[]>(StringComparer.Ordinal)
        {
            ["head"] = data.Values("head_tile_pointer_index").ToArray(),
            ["body"] = data.Values("body_tile_pointer_index").ToArray(),
            ["walking_pose_count"] = data.Values("walking_pose_count").ToArray(),
            ["animation_speed_table"] = data.Values("animation_speed_table").ToArray(),
        };

        var oamTables = PlayerOamTableSpecs
            .Select(spec => new PlayerOamTable(
                Name: spec.Name,
                SourceAddress: spec.Address,
                Count: spec.Count,
                Format: spec.Format,
                Values: data.Values(spec.Name).ToArray()))
            .ToList();

        return new PlayerMetadata(rom.Sha1, paletteVariants, tilePointerTables, oamTables);
    }

    private static int[] BuildPlayerSpritePaletteWords(Rom rom, int player)
    {
        return new SmwPaletteDecoder(rom.Native).CreatePlayerRow(player).Select(value => (int)value).ToArray();
    }

    private static void VerifyGeneratedPlayerMetadata(string generatedRoot, PlayerMetadata expected)
    {
        var path = Path.Combine(generatedRoot, "player", "player_graphics.json");
        Check(File.Exists(path), $"generated player graphics metadata missing: {path}");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        var palette = Required(root, "palette");
        CheckJsonArrayEquals(Required(palette, "snes_bgr555"), expected.PaletteVariants[0].SnesBgr555, "player base palette");
        var variants = RequiredArray(palette, "variants").ToArray();
        Check(variants.Length == expected.PaletteVariants.Count, "player palette variant count mismatch");
        for (var i = 0; i < expected.PaletteVariants.Count; i++)
        {
            var expectedVariant = expected.PaletteVariants[i];
            var actualVariant = variants[i];
            Check(Required(actualVariant, "index").GetInt32() == expectedVariant.Index, $"player palette variant {i} index mismatch");
            Check((Required(actualVariant, "name").GetString() ?? "") == expectedVariant.Name, $"player palette variant {i} name mismatch");
            CheckJsonArrayEquals(Required(actualVariant, "snes_bgr555"), expectedVariant.SnesBgr555, $"player palette variant {i}");
        }

        var tilePointerTables = Required(root, "tile_pointer_tables");
        foreach (var (name, values) in expected.TilePointerTables)
        {
            CheckJsonArrayEquals(Required(tilePointerTables, name), values, $"player tile pointer table {name}");
        }

        var oamTables = Required(root, "oam_tables");
        foreach (var expectedTable in expected.OamTables)
        {
            var actualTable = Required(oamTables, expectedTable.Name);
            Check((Required(actualTable, "source_addr").GetString() ?? "") == $"0x{expectedTable.SourceAddress:X6}", $"player OAM table {expectedTable.Name} source address mismatch");
            Check(Required(actualTable, "count").GetInt32() == expectedTable.Count, $"player OAM table {expectedTable.Name} count mismatch");
            Check((Required(actualTable, "format").GetString() ?? "") == expectedTable.Format, $"player OAM table {expectedTable.Name} format mismatch");
            CheckJsonArrayEquals(Required(actualTable, "values"), expectedTable.Values, $"player OAM table {expectedTable.Name}");
        }
    }

    private static EntranceTables BuildEntranceTables(Rom rom)
    {
        var tables = EntranceTableSpecs
            .Select(spec => new EntranceTable(
                Name: spec.Name,
                SourceAddress: spec.Address,
                Values: rom.GetBytes(spec.Address, spec.Length).Select(value => (int)value).ToArray()))
            .ToList();

        return new EntranceTables(rom.Sha1, tables);
    }

    private static void VerifyGeneratedEntranceTables(string generatedRoot, EntranceTables expected)
    {
        var path = Path.Combine(generatedRoot, "levels", "secondary_tables.json");
        Check(File.Exists(path), $"generated entrance table file missing: {path}");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        foreach (var table in expected.Tables)
        {
            CheckJsonArrayEquals(Required(root, table.Name), table.Values, $"entrance table {table.Name}");
        }

        var main105Y = expected.Table("level_info_05f000")[0x105];
        var main105XAction = expected.Table("level_info_05f200")[0x105];
        var main1CbY = expected.Table("level_info_05f000")[0x1CB];
        var main1CbXAction = expected.Table("level_info_05f200")[0x1CB];
        var secondary1CbLevelLow = expected.Table("secondary_level_low_05f800")[0x1CB];
        var secondary1CbY = expected.Table("secondary_y_05fa00")[0x1CB];
        var secondary1CbX = expected.Table("secondary_x_05fc00")[0x1CB];
        var secondary1CbType = expected.Table("secondary_entrance_type_05fe00")[0x1CB];
        Check(main105Y == 0x5B && main105XAction == 0x00, "level 105 main entrance table anchor mismatch");
        Check(main1CbY == 0x19 && main1CbXAction == 0x20, "level 1CB main entrance table anchor mismatch");
        Check(secondary1CbLevelLow == 0x05 && secondary1CbY == 0xA9 && secondary1CbX == 0x08 && secondary1CbType == 0x06,
            "secondary entrance 1CB return-to-105 anchor mismatch");
    }

    private static PaletteAssets BuildPaletteAssets(Rom rom)
    {
        var globalSets = GlobalPaletteSpecs
            .Select(spec => new PaletteSet(spec.Name, rom.GetWords(spec.Address, spec.WordCount)))
            .ToList();
        var summaries = BuildLevelSummaries(rom, [0x105, 0x1CB]);
        var levels = summaries.Values
            .Select(summary => BuildLevelPalette(rom, summary.LevelId, summary.Header))
            .ToList();
        return new PaletteAssets(new GlobalPalettes(globalSets), levels);
    }

    private static LevelPalette BuildLevelPalette(Rom rom, string levelId, LevelHeader header)
    {
        var selection = new SmwPaletteSelection(header.BackgroundColor, header.BgPalette, header.FgPalette, header.SpritePalette);
        var decoder = new SmwPaletteDecoder(rom.Native);
        var backAreaColor = decoder.ApplyLevelBase(new ushort[256], selection);
        var words = decoder.CreateLevelEntry(selection).Select(value => (int)value).ToArray();

        return new LevelPalette(
            LevelId: levelId,
            Source: "vanilla_header_tables",
            BackAreaColor: backAreaColor,
            Words: words,
            HeaderIndexes: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["background_color"] = header.BackgroundColor,
                ["bg_palette"] = header.BgPalette,
                ["fg_palette"] = header.FgPalette,
                ["sprite_palette"] = header.SpritePalette,
            });
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
            colors[i] =
            [
                (r5 << 3) | (r5 >> 2),
                (g5 << 3) | (g5 >> 2),
                (b5 << 3) | (b5 >> 2),
            ];
        }

        return colors;
    }

    private static void VerifyGeneratedPalettes(string generatedRoot, PaletteAssets expected)
    {
        var globalPath = Path.Combine(generatedRoot, "palettes", "global_palettes.json");
        Check(File.Exists(globalPath), $"generated global palette file missing: {globalPath}");
        using (var document = JsonDocument.Parse(File.ReadAllText(globalPath)))
        {
            var root = document.RootElement;
            foreach (var set in expected.Global.Sets)
            {
                var actual = Required(root, set.Name);
                CheckJsonArrayEquals(Required(actual, "snes_bgr555"), set.Words, $"global palette {set.Name}");
                CheckRgbArrayEquals(Required(actual, "rgb888"), SnesWordsToRgb(set.Words), $"global palette {set.Name} rgb");
            }
        }

        foreach (var level in expected.Levels)
        {
            var path = Path.Combine(generatedRoot, "palettes", $"level_{level.LevelId}_palette.json");
            Check(File.Exists(path), $"generated level palette file missing: {path}");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            Check((Required(root, "status").GetString() ?? "") == "preview", $"level {level.LevelId} palette status mismatch");
            Check((Required(root, "source").GetString() ?? "") == level.Source, $"level {level.LevelId} palette source mismatch");
            Check((Required(root, "level_id").GetString() ?? "") == level.LevelId, $"level {level.LevelId} palette id mismatch");
            Check(Required(Required(root, "back_area_color"), "snes_bgr555").GetInt32() == level.BackAreaColor, $"level {level.LevelId} back area color mismatch");
            CheckJsonArrayEquals(Required(root, "snes_bgr555"), level.Words, $"level {level.LevelId} palette");
            CheckRgbArrayEquals(Required(root, "rgb888"), SnesWordsToRgb(level.Words), $"level {level.LevelId} palette rgb");
            var headerIndexes = Required(root, "header_palette_indexes");
            foreach (var (key, value) in level.HeaderIndexes)
            {
                Check(Required(headerIndexes, key).GetInt32() == value, $"level {level.LevelId} palette header index {key} mismatch");
            }
        }
    }

    private static void CheckRgbArrayEquals(JsonElement element, IReadOnlyList<int[]> expected, string description)
    {
        Check(element.ValueKind == JsonValueKind.Array, $"{description} should be an array");
        var actual = element.EnumerateArray().ToArray();
        Check(actual.Length == expected.Count, $"{description} length mismatch");
        for (var i = 0; i < actual.Length; i++)
        {
            var rgb = actual[i].EnumerateArray().Select(value => value.GetInt32()).ToArray();
            Check(rgb.SequenceEqual(expected[i]), $"{description} mismatch at color {i}");
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

    private static void WriteJson(Stream stream, object value)
    {
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = JsonOptions.WriteIndented });
        JsonSerializer.Serialize(writer, value, JsonOptions);
        writer.Flush();
        Console.WriteLine();
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
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-graphics <rom.sfc> <ignored-out-dir>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-level-graphics <rom.sfc> <ignored-out-dir> [105,1CB,...]");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-level-data <rom.sfc> <ignored-out-dir> [all|105,1CB,...]");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-terrain <rom.sfc> <ignored-out-dir> [105,1CB,...]");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- audit-terrain <rom.sfc> <ignored-out-dir>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-levels <rom.sfc> <out-dir>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-levels <rom.sfc> <generated/smw>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-audio-previews <rom.sfc> <out-dir>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-audio-native <rom.sfc> <ignored-out-dir>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-audio-previews <rom.sfc> <generated/smw>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-player-metadata <rom.sfc> <out-dir>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-player-metadata <rom.sfc> <generated/smw>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-entrance-tables <rom.sfc> <out-dir>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-entrance-tables <rom.sfc> <generated/smw>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- extract-palettes <rom.sfc> <out-dir>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-palettes <rom.sfc> <generated/smw>");
        Console.Error.WriteLine("  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- inspect-rom <rom.sfc>");
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

    private sealed record PlayerTableSpec(string Name, int Address, int Count, string Format);

    private sealed record EntranceTableSpec(string Name, int Address, int Length);

    private sealed record PaletteTableSpec(string Name, int Address, int WordCount);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record AudioBank(string Name, int SourceAddress, int Length, byte[] Suffix);

    private sealed record PlayerMetadata(
        string SourceRomSha1,
        List<PlayerPaletteVariant> PaletteVariants,
        Dictionary<string, int[]> TilePointerTables,
        List<PlayerOamTable> OamTables)
    {
        public object ToSerializable()
        {
            return new
            {
                schema_version = 1,
                source_rom_sha1 = SourceRomSha1,
                palette = new
                {
                    variants = PaletteVariants.Select(variant => variant.ToSerializable()).ToArray(),
                },
                tile_pointer_tables = TilePointerTables.ToDictionary(pair => pair.Key, pair => pair.Value),
                oam_tables = OamTables.ToDictionary(table => table.Name, table => table.ToSerializable()),
            };
        }
    }

    private sealed record PlayerPaletteVariant(int Index, string Name, int[] SnesBgr555)
    {
        public object ToSerializable()
        {
            return new
            {
                index = Index,
                name = Name,
                snes_bgr555 = SnesBgr555,
            };
        }
    }

    private sealed record PlayerOamTable(string Name, int SourceAddress, int Count, string Format, int[] Values)
    {
        public object ToSerializable()
        {
            return new
            {
                source_addr = $"0x{SourceAddress:X6}",
                count = Count,
                format = Format,
                values = Values,
            };
        }
    }

    private sealed record EntranceTables(string SourceRomSha1, List<EntranceTable> Tables)
    {
        public object ToSerializable()
        {
            return Tables.ToDictionary(table => table.Name, table => table.Values, StringComparer.Ordinal);
        }

        public int[] Table(string name)
        {
            var table = Tables.FirstOrDefault(candidate => candidate.Name == name);
            if (table == null)
            {
                throw new InvalidOperationException($"native entrance table missing: {name}");
            }

            return table.Values;
        }
    }

    private sealed record EntranceTable(string Name, int SourceAddress, int[] Values);

    private sealed record PaletteAssets(GlobalPalettes Global, List<LevelPalette> Levels);

    private sealed record GlobalPalettes(List<PaletteSet> Sets)
    {
        public object ToSerializable()
        {
            return Sets
                .OrderBy(set => set.Name, StringComparer.Ordinal)
                .ToDictionary(
                    set => set.Name,
                    set => new
                    {
                        rgb888 = SnesWordsToRgb(set.Words),
                        snes_bgr555 = set.Words,
                    },
                    StringComparer.Ordinal);
        }
    }

    private sealed record PaletteSet(string Name, int[] Words);

    private sealed record LevelPalette(
        string LevelId,
        string Source,
        int BackAreaColor,
        int[] Words,
        Dictionary<string, int> HeaderIndexes)
    {
        public object ToSerializable()
        {
            return new
            {
                back_area_color = new
                {
                    rgb888 = SnesWordsToRgb([BackAreaColor])[0],
                    snes_bgr555 = BackAreaColor,
                },
                file = $"palettes/level_{LevelId}_palette.json",
                header_palette_indexes = HeaderIndexes,
                layout = new
                {
                    colors_per_row = 16,
                    rows = 16,
                    sprite_rows = "rows 8-15 are used by OAM sprite palettes",
                    tilemap_palette_bits = "bits 10-12 select CGRAM rows 0-7 for BG/FG Map16 rendering",
                },
                level_id = LevelId,
                notes = new[]
                {
                    "Vanilla palette assembly follows the header-selected tables documented by SMW Central and the speedrunning level-data notes.",
                    "Lunar Magic custom palette pointers at $0EF600 are recognized when a supported ROM exposes them.",
                },
                rgb888 = SnesWordsToRgb(Words),
                snes_bgr555 = Words,
                source = Source,
                status = "preview",
            };
        }
    }

    private sealed record AudioPreviewManifest(
        string SourceRomSha1,
        List<SpcUploadBlock> SampleUploadBlocks,
        List<AudioPreviewSample> DecodedSamples)
    {
        public object ToSerializable()
        {
            return new
            {
                schema_version = 1,
                source_rom_sha1 = SourceRomSha1,
                sample_rate = 32000,
                sample_upload_blocks = SampleUploadBlocks.Select(block => block.ToSerializable()).ToArray(),
                decoded_samples = DecodedSamples.Select(sample => sample.ToSerializable()).ToArray(),
            };
        }
    }

    private sealed record SpcUploadBlock(int Target, int Length)
    {
        public object ToSerializable()
        {
            return new
            {
                target = $"0x{Target:X4}",
                length = Length,
            };
        }
    }

    private sealed record AudioPreviewSample(
        int Id,
        string File,
        int SpcStart,
        int SpcLoop,
        int BrrBytes,
        int SampleCount,
        int SampleRate,
        byte[] WavData)
    {
        public object ToSerializable()
        {
            return new
            {
                id = Id,
                file = File,
                source = "spc_samples directory",
                spc_start = $"0x{SpcStart:X4}",
                spc_loop = $"0x{SpcLoop:X4}",
                brr_bytes = BrrBytes,
                sample_count = SampleCount,
                sample_rate = SampleRate,
                format = "pcm_s16le_wav_from_brr",
                sha1 = Convert.ToHexString(SHA1.HashData(WavData)),
            };
        }
    }

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
        private Rom(string path, SmwRom native)
        {
            Path = path;
            Native = native;
        }

        public string Path { get; }
        public SmwRom Native { get; }
        public string Sha1 => Native.Inspection.Sha1;

        public static Rom Load(string path)
        {
            var fullPath = System.IO.Path.GetFullPath(path);
            Check(File.Exists(fullPath), $"ROM does not exist: {fullPath}");
            return new Rom(fullPath, SmwRom.FromBytes(File.ReadAllBytes(fullPath)));
        }

        public int GetByte(int address)
        {
            return Native.Data.ReadByte(address);
        }

        public int GetWord(int address)
        {
            return Native.Data.ReadUInt16(address);
        }

        public int[] GetWords(int address, int count)
        {
            var words = new int[count];
            for (var i = 0; i < count; i++)
            {
                words[i] = GetWord(address);
                address = LoRomData.NextAddress(LoRomData.NextAddress(address));
            }

            return words;
        }

        public int Get24(int address)
        {
            return Native.Data.ReadUInt24(address);
        }

        public byte[] GetBytes(int address, int count)
        {
            return Native.Data.ReadBytes(address, count);
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
            Address = LoRomData.NextAddress(Address);

            return value;
        }
    }
}
