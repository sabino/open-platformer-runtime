using System;
using System.Collections.Generic;

namespace OpenPlatformerRuntime.SmwAssets;

public sealed record SmwRomTable(string Name, int SourceAddress, string Format, int[] Values);

/// <summary>Player pose, animation and OAM tables, loaded from the validated ROM.</summary>
public sealed class SmwPlayerGraphicsData
{
    private readonly Dictionary<string, SmwRomTable> _tables = new(StringComparer.Ordinal);
    public SmwPlayerGraphicsData(SmwRom rom)
    {
        Add(rom.Data, "walking_pose_count", 0x00DC78, 4);
        Add(rom.Data, "animation_speed_table", 0x00DC7C, 112);
        Add(rom.Data, "player_xy_disp_index_index", 0x00DCEC, 70);
        Add(rom.Data, "player_xy_disp_index", 0x00DD32, 28);
        Add(rom.Data, "x_disp", 0x00DD4E, 114, signedWords: true);
        Add(rom.Data, "y_disp", 0x00DE32, 114, signedWords: true);
        Add(rom.Data, "powerup_tileset_index", 0x00DF16, 4);
        Add(rom.Data, "tiles_index", 0x00DF4C, 192);
        Add(rom.Data, "tiles", 0x00DFDA, 50);
        Add(rom.Data, "head_tile_pointer_index", 0x00E00C, 192);
        Add(rom.Data, "body_tile_pointer_index", 0x00E0CC, 192);
        Add(rom.Data, "tile_x_flip", 0x00E18C, 2);
    }

    public IReadOnlyDictionary<string, SmwRomTable> Tables => _tables;
    public IReadOnlyList<int> Values(string name) => _tables[name].Values;

    private void Add(LoRomData data, string name, int address, int count, bool signedWords = false)
    {
        var values = new int[count];
        for (var i = 0; i < count; i++)
            values[i] = signedWords ? unchecked((short)data.ReadUInt16(address + i * 2)) : data.ReadByte(address + i);
        _tables.Add(name, new SmwRomTable(name, address, signedWords ? "s16" : "u8", values));
    }
}
