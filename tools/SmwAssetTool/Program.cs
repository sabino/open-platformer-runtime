using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class Program
{
    private const string SmwUsSha1 = "6B47BB75D16514B6A476AA0C73A683A2A4C18765";
    private const int SmwUsRomSize = 0x80000;

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
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record AudioBank(string Name, int SourceAddress, int Length, byte[] Suffix);

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
