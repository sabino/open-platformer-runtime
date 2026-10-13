using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenPlatformerRuntime.SmwAssets;

public sealed record SmwLevelCatalogEntry(
    int Id, string Name, int Layer1Address, int Layer2Address, int SpriteAddress,
    int ObjectCount, int Layer2ObjectCount, int SpriteCount, int ExitCount,
    int Screens, bool Vertical, bool TerrainVerified, IReadOnlyList<int> LayoutAliases);

/// <summary>
/// ROM-derived directory shared by desktop, browser and inspection tools.
/// Pointer slots and name-table labels are not a list of reachable courses.
/// This directory does not expand objects or invent missing game behavior.
/// </summary>
public sealed class SmwLevelCatalog
{
    public const int SchemaVersion = 1;
    public const int SlotCount = 512;
    public const string TitleSource = "vanilla_overworld_level_name_tables";
    private readonly SmwLevelCatalogEntry[] _entries;

    public SmwLevelCatalog(SmwRom rom, Action<int, int>? progress = null)
    {
        var titles = ReadTitles(rom.Data);
        _entries = new SmwLevelCatalogEntry[SlotCount];
        var aliases = new Dictionary<int, List<int>>();
        for (var id = 0; id < SlotCount; id++)
        {
            var level = new SmwLevelData(rom, id);
            if (!aliases.TryGetValue(level.Header.SourceAddress, out var group))
                aliases[level.Header.SourceAddress] = group = [];
            group.Add(id);
            _entries[id] = new SmwLevelCatalogEntry(id, titles.GetValueOrDefault(id, ""),
                level.Header.SourceAddress, level.Layer2Address, level.Sprites.SourceAddress,
                level.Layer1.Objects.Count, level.Layer2?.Objects.Count ?? 0,
                level.Sprites.Sprites.Count, level.LoadedExits.Count(),
                level.Header.Screens, level.Header.Vertical, HasVerifiedTerrain(id), Array.Empty<int>());
            if ((id & 63) == 63) progress?.Invoke(id + 1, SlotCount);
        }
        for (var id = 0; id < SlotCount; id++)
            _entries[id] = _entries[id] with { LayoutAliases = aliases[_entries[id].Layer1Address].AsReadOnly() };
        Entries = Array.AsReadOnly(_entries);
        RomSha1 = rom.Inspection.Sha1;
    }

    public string RomSha1 { get; }
    public IReadOnlyList<SmwLevelCatalogEntry> Entries { get; }
    public int VerifiedTerrainCount => _entries.Count(entry => entry.TerrainVerified);
    public SmwLevelCatalogEntry this[int id] => (uint)id < SlotCount ? _entries[id] : throw new ArgumentOutOfRangeException(nameof(id));

    // Each entry requires complete Map16-buffer comparison with the original
    // loader. A verified layout does not claim complete gameplay or rendering parity.
    public static bool HasVerifiedTerrain(int id) => id is 0x0C7 or 0x105 or 0x106 or 0x1CB;

    private static Dictionary<int, string> ReadTitles(LoRomData rom)
    {
        // Port the vanilla portion of PR #1's name reader onto the shared
        // bounds-checked ROM API. Patched ROMs require a separate compatibility profile.
        var strings = rom.ReadBytes(0x049AC5, 460);
        var result = new Dictionary<int, string>();
        // The vanilla table has $5D words ($000-$024, $101-$138),
        // not 256. Reading past it interprets the next routine as names.
        for (var overworld = 1; overworld < 0x5D; overworld++)
        {
            var id = overworld >= 0x25 ? 0x100 + overworld - 0x24 : overworld;
            if (id >= SlotCount) continue;
            var word = rom.ReadUInt16(0x04A0FC + overworld * 2);
            var tiles = new List<byte>(19);
            var prefix = word >> 8 & 0x7F;
            var middle = word >> 4 & 15;
            var suffix = word & 15;
            if (prefix < 31)
            {
                var offset = rom.ReadUInt16(0x049C91 + prefix * 2);
                if (offset < strings.Length && (strings[offset] & 0x80) == 0) AppendSegment(tiles, strings, offset);
            }
            if (middle < 15)
            {
                var offset = rom.ReadUInt16(0x049CCF + middle * 2);
                if (offset < strings.Length && strings[offset] != 0x9F) AppendSegment(tiles, strings, offset);
            }
            if (suffix < 13) AppendSegment(tiles, strings, rom.ReadUInt16(0x049CED + suffix * 2));
            var text = string.Concat(tiles.Select(code => NameGlyph(code)));
            result[id] = string.Join(" ", text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
        return result;
    }

    private static void AppendSegment(List<byte> tiles, byte[] strings, int offset)
    {
        for (var i = offset; i < strings.Length; i++)
        {
            // UpdateLevelName_049D7F copies at most 19 tiles. Some tiles
            // represent more than one printed letter, so trim before decoding.
            if (tiles.Count < 19) tiles.Add((byte)(strings[i] & 127));
            if ((strings[i] & 128) != 0) return;
        }
        if (offset < strings.Length) throw new InvalidDataException("Unterminated ROM level-name segment");
    }

    private static string NameGlyph(int code) => code switch
    {
        >= 0 and <= 25 => ((char)('A' + code)).ToString(),
        0x1C => "-", 0x1F => " ", 0x5A => "#", 0x5D => "'", 0x62 => ".",
        >= 0x64 and <= 0x6A => ((char)('1' + code - 0x64)).ToString(),
        0x32 => "I", 0x33 or 0x34 => "L", 0x35 => "U", 0x36 => "S", 0x37 => "I",
        0x38 => "Y", 0x39 => "E", 0x3A or 0x3B => "L", 0x3C => "OW",
        _ => throw new NotSupportedException($"Unsupported ROM level-name glyph ${code:X2}")
    };
}
