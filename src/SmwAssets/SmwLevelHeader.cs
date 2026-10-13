using System;

namespace OpenPlatformerRuntime.SmwAssets;

/// <summary>The five original level header bytes and their ROM-driven layout.</summary>
public sealed class SmwLevelHeader
{
    private readonly byte[] _bytes;
    private SmwLevelHeader(int id, int address, byte[] bytes, byte layoutFlags)
    {
        Id = id;
        SourceAddress = address;
        _bytes = bytes;
        LayoutFlags = layoutFlags;
    }

    public int Id { get; }
    public int SourceAddress { get; }
    public ReadOnlySpan<byte> Bytes => _bytes;
    public int Screens => (_bytes[0] & 31) + 1;
    public int BackgroundPalette => _bytes[0] >> 5;
    public int Mode => _bytes[1] & 31;
    public int Sky => _bytes[1] >> 5;
    public int SpriteGraphics => _bytes[2] & 15;
    public int Music => _bytes[2] >> 4 & 7;
    public int Layer3Priority => _bytes[2] >> 7;
    public int ForegroundPalette => _bytes[3] & 7;
    public int SpritePalette => _bytes[3] >> 3 & 7;
    public int Timer => _bytes[3] >> 6;
    public int Tileset => _bytes[4] & 15;
    public int Layer1Scroll => _bytes[4] >> 4 & 3;
    public int ItemMemory => _bytes[4] >> 6;
    public byte LayoutFlags { get; }
    public bool Vertical => (LayoutFlags & 1) != 0;
    public SmwPaletteSelection Palette => new(Sky, BackgroundPalette, ForegroundPalette, SpritePalette);

    public static SmwLevelHeader Read(SmwRom rom, int id)
    {
        if ((uint)id >= 512) throw new ArgumentOutOfRangeException(nameof(id));
        var address = rom.Data.ReadUInt24(0x05E000 + id * 3);
        var bytes = rom.Data.ReadBytes(address, 5);
        var flags = rom.Data.ReadByte(0x058417 + (bytes[1] & 31));
        return new SmwLevelHeader(id, address, bytes, flags);
    }
}
