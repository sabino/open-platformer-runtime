using System;
using System.Collections.Generic;

namespace OpenPlatformerRuntime.SmwAssets;

public readonly record struct SmwEntrance(int Level, int SourceId, bool Secondary, int X, int Y, int Action,
    int Layer1Y, int Layer2Y, byte LayoutFlags, byte Layer3Settings, bool DisableIntro);

/// <summary>Vanilla main/secondary entrance data, in original pixel coordinates.</summary>
public sealed class SmwEntranceDecoder
{
    private readonly SmwRom _rom;
    public SmwEntranceDecoder(SmwRom rom) => _rom = rom;

    public IReadOnlyDictionary<string, int> TableAddresses { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["level_info_05f000"] = 0x05F000,
        ["level_info_05f200"] = 0x05F200,
        ["level_info_05f400"] = 0x05F400,
        ["level_info_05f600"] = 0x05F600,
        ["secondary_level_low_05f800"] = 0x05F800,
        ["secondary_y_05fa00"] = 0x05FA00,
        ["secondary_x_05fc00"] = 0x05FC00,
        ["secondary_entrance_type_05fe00"] = 0x05FE00,
    };

    public byte ReadTable(string name, int index)
    {
        RequireId(index);
        return _rom.Data.ReadByte(TableAddresses[name] + index);
    }

    // Data portion of LoadLevel ($05D796). Midway/intro/world selection remains
    // game-state logic. See pinned snesrev/smw and THIRD-PARTY-NOTICES.md.
    public SmwEntrance Main(int level)
    {
        RequireId(level);
        var f000 = _rom.Data.ReadByte(0x05F000 + level);
        var f200 = _rom.Data.ReadByte(0x05F200 + level);
        var f400 = _rom.Data.ReadByte(0x05F400 + level);
        var f600 = _rom.Data.ReadByte(0x05F600 + level);
        return Resolve(level, level, false, X(f200 & 7), Y(f000 & 15), f600 & 31,
            f200 >> 3 & 7, LayerY(0x05D708, f400 >> 2 & 3), LayerY(0x05D70C, f400 & 3));
    }

    public SmwEntrance Secondary(int id)
    {
        RequireId(id);
        var level = (id & 0x100) | _rom.Data.ReadByte(0x05F800 + id);
        var y = _rom.Data.ReadByte(0x05FA00 + id);
        var x = _rom.Data.ReadByte(0x05FC00 + id);
        var action = _rom.Data.ReadByte(0x05FE00 + id) & 7;
        return Resolve(level, id, true, X(x >> 5), Y(y & 15), x & 31,
            action, LayerY(0x05D708, y >> 4 & 3), LayerY(0x05D70C, y >> 6));
    }

    private SmwEntrance Resolve(int level, int source, bool secondary, int x, int y, int screen, int action, int layer1Y, int layer2Y)
    {
        var f600 = _rom.Data.ReadByte(0x05F600 + level);
        var flags = (byte)(f600 >> 5 & 3);
        if ((flags & 1) != 0)
        {
            y = screen * 256 + (y & 255);
            layer1Y = screen * 256 + (layer1Y & 255);
            // Flag 3 locks Layer 2 vertical scrolling; other modes share the
            // entrance screen. Read its flag from the original scroll table.
            var scrollIndex = _rom.Data.ReadByte(0x05F000 + level) >> 4;
            var layer2Scroll = _rom.Data.ReadByte(0x05D710 + scrollIndex);
            if (layer2Scroll != 3) layer2Y = screen * 256 + (layer2Y & 255);
        }
        else x = screen * 256 + (x & 255);
        return new SmwEntrance(level, source, secondary, x, y, action, layer1Y, layer2Y, flags,
            (byte)(_rom.Data.ReadByte(0x05F200 + level) >> 6), (f600 & 0x80) != 0);
    }

    private int X(int index) => _rom.Data.ReadByte(0x05D750 + (index & 7)) | _rom.Data.ReadByte(0x05D758 + (index & 7)) << 8;
    private int Y(int index) => _rom.Data.ReadByte(0x05D730 + (index & 15)) | _rom.Data.ReadByte(0x05D740 + (index & 15)) << 8;
    private int LayerY(int table, int index) => _rom.Data.ReadByte(table + index);
    private static void RequireId(int id)
    {
        if ((uint)id >= 512) throw new ArgumentOutOfRangeException(nameof(id));
    }
}
