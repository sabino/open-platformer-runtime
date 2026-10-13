using System;
using System.IO;

namespace OpenPlatformerRuntime.SmwAssets;

/// <summary>Original object dispatch data, used to select compiled C# game routines.</summary>
public static class SmwObjectDispatch
{
    // These are data tables in the validated vanilla ROM. Nothing here
    // fetches or executes CPU instructions. Tables are pinned against the
    // galaxyhaxz/smw-src bank-D disassembly.
    public static int Routine(LoRomData rom, int tileset, SmwLevelObject obj)
    {
        if ((uint)tileset >= 15) throw new InvalidDataException("Invalid vanilla object tileset");
        if (obj.Id == 0) return rom.ReadUInt24(0x0DA10F + obj.SizeOrType * 3);
        var family = rom.ReadUInt24(0x0DA41E + tileset * 3);
        var table = family switch
        {
            0x0DA44B => 0x0DA455,
            0x0DC190 => 0x0DC19A,
            0x0DCD90 => 0x0DCD9A,
            0x0DD990 => 0x0DD99A,
            0x0DE890 => 0x0DE89A,
            _ => throw new InvalidDataException($"Unknown vanilla object dispatch family ${family:X6}"),
        };
        var routine = rom.ReadUInt24(table + (obj.Id - 1) * 3);
        // $0DAB3E folds kinds 10..15 into the first six slope entries.
        return routine == 0x0DAB3E ? rom.ReadUInt24(0x0DAB50 + (obj.SizeOrType & 15) % 10 * 3) : routine;
    }
}

public sealed class SmwTerrainUnsupportedObjectException : NotSupportedException
{
    internal SmwTerrainUnsupportedObjectException(int level, int tileset, SmwLevelObject obj, int routine)
        : base($"Native terrain: level {level:X3}, tileset {tileset:X}, layer {obj.Placement.Layer}, object {obj.Sequence}, ID {obj.Id:X2}, type {obj.SizeOrType:X2}, routine ${routine:X6} is not implemented")
    {
        Level = level; Tileset = tileset; Layer = obj.Placement.Layer;
        Sequence = obj.Sequence; ObjectId = obj.Id; Type = obj.SizeOrType; Routine = routine;
    }
    public int Level { get; }
    public int Tileset { get; }
    public int Layer { get; }
    public int Sequence { get; }
    public int ObjectId { get; }
    public int Type { get; }
    public int Routine { get; }
}
