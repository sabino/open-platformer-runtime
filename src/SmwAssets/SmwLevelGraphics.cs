using System;

namespace OpenPlatformerRuntime.SmwAssets;

/// <summary>Vanilla level graphics windows and Map16 data read directly from ROM tables.</summary>
public sealed class SmwLevelGraphics
{
    private readonly SmwRom _rom;
    private readonly SmwGraphicsDecoder _graphics;
    public SmwLevelGraphics(SmwRom rom, int level)
    {
        _rom = rom;
        _graphics = new SmwGraphicsDecoder(rom);
        Header = SmwLevelHeader.Read(rom, level);
    }

    public SmwLevelHeader Header { get; }

    public byte[] ReadSpriteWindow(bool specialWorld = false) => ReadWindow(0x00A8C3, Header.SpriteGraphics, specialWorld);
    public byte[] ReadBackgroundWindow() => ReadWindow(0x00A92B, Header.Tileset, false);

    private byte[] ReadWindow(int table, int setting, bool specialWorld)
    {
        if ((uint)setting >= 26) throw new ArgumentOutOfRangeException(nameof(setting));
        var output = new byte[0x4000];
        var scratch = new byte[0xC00];
        // The original loads slot 0 first, then 1..3. Short GFX30 leaves
        // decompression scratch bytes from the preceding upload untouched.
        for (var slot = 0; slot < 4; slot++)
        {
            var id = _rom.Data.ReadByte(table + setting * 4 + slot);
            var file = _graphics.Decode(id);
            if (file.Format != SmwGraphicsFormat.Planar3Bpp || file.Bytes.Length > scratch.Length)
                throw new InvalidOperationException("Unexpected format in a vanilla graphics slot");
            file.Bytes.CopyTo(scratch);
            if (id == 1 && specialWorld) _graphics.Decode(0x31).Bytes.CopyTo(scratch);
            var upload = SmwGraphicsDecoder.ExpandNormalUpload(scratch, id, Header.Tileset);
            upload.CopyTo(output, slot * 0x1000);
        }
        return output;
    }

    // InitializeMap16Pointers ($0581FB), snesrev/smw src/smw_05.c.
    // Read pointer/mask tables from the ROM, retaining TL/BL/TR/BR word order.
    public ushort[] ReadForegroundMap16()
    {
        if (Header.Tileset >= 15) throw new InvalidOperationException("Tileset 15 has no vanilla Map16 table");
        var tilesetPointer = _rom.Data.ReadUInt16(0x058000 + Header.Tileset * 2);
        var globalPointer = 0x8000;
        var pointers = new int[512];
        for (var tile = 0; tile < pointers.Length; tile++)
        {
            var mask = _rom.Data.ReadByte(0x0581BB + tile / 8);
            if ((mask & (128 >> (tile % 8))) != 0)
            {
                pointers[tile] = globalPointer;
                globalPointer += 8;
            }
            else
            {
                pointers[tile] = tilesetPointer;
                tilesetPointer += 8;
            }
        }
        if (Header.Tileset is 0 or 7)
        {
            for (var i = 0; i < 4; i++)
            {
                pointers[452 + i] = 0x8A70 + i * 8;
                pointers[492 + i] = 0x8A90 + i * 8;
            }
        }
        var words = new ushort[2048];
        for (var tile = 0; tile < pointers.Length; tile++)
            for (var sub = 0; sub < 4; sub++) words[tile * 4 + sub] = _rom.Data.ReadUInt16(0x0D0000 | (pointers[tile] + sub * 2));
        return words;
    }

    public ushort[] ReadBackgroundMap16()
    {
        var words = new ushort[2048];
        for (var i = 0; i < words.Length; i++) words[i] = _rom.Data.ReadUInt16(0x0D9100 + i * 2);
        return words;
    }

    public SmwBackgroundTiles? ReadBackgroundTiles()
    {
        var address = _rom.Data.ReadUInt24(0x05E600 + Header.Id * 3);
        if ((address >> 16) != 0xFF) return null;
        address = 0x0C0000 | (address & 0xFFFF);
        var decoded = Rle1Decoder.Decode(_rom.Data.ReadFrom(address));
        return new SmwBackgroundTiles(address, decoded.CompressedLength, address % 0x10000 >= 0xE8FE ? 1 : 0, decoded.Bytes);
    }
}

public sealed record SmwBackgroundTiles(int SourceAddress, int CompressedLength, int Page, byte[] Bytes);
