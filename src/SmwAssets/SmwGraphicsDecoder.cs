using System;
using System.Collections.Generic;

namespace OpenPlatformerRuntime.SmwAssets;

public enum SmwGraphicsFormat
{
    Planar2Bpp,
    Planar3Bpp,
    Planar4Bpp,
    Packed3Bpp,
}

/// <summary>A decompressed file, retaining its original storage format.</summary>
public sealed class SmwGraphicsFile
{
    private readonly byte[] _bytes;

    internal SmwGraphicsFile(int id, int address, Lz2Decoded data, SmwGraphicsFormat format)
    {
        Id = id;
        SourceAddress = address;
        CompressedLength = data.CompressedLength;
        Format = format;
        _bytes = data.Bytes;
    }

    public int Id { get; }
    public int SourceAddress { get; }
    public int CompressedLength { get; }
    public SmwGraphicsFormat Format { get; }
    public ReadOnlySpan<byte> Bytes => _bytes;
    public int TileCount => _bytes.Length / (Format == SmwGraphicsFormat.Planar2Bpp ? 16 : Format == SmwGraphicsFormat.Planar4Bpp ? 32 : 24);

    public byte[] DecodePixels() => Format switch
    {
        SmwGraphicsFormat.Planar2Bpp => SnesGraphics.DecodePlanar(_bytes, 2),
        SmwGraphicsFormat.Planar3Bpp => SnesGraphics.DecodePlanar(_bytes, 3),
        SmwGraphicsFormat.Planar4Bpp => SnesGraphics.DecodePlanar(_bytes, 4),
        SmwGraphicsFormat.Packed3Bpp => SnesGraphics.DecodePacked3Bpp(_bytes),
        _ => throw new InvalidOperationException("Unknown graphics format"),
    };

    /// <summary>Planar expansion with zero additional planes, before upload color rules.</summary>
    public byte[] ExpandTo4Bpp() => Format switch
    {
        SmwGraphicsFormat.Planar2Bpp => SnesGraphics.ExpandTo4Bpp(_bytes, 2),
        SmwGraphicsFormat.Planar3Bpp => SnesGraphics.ExpandTo4Bpp(_bytes, 3),
        SmwGraphicsFormat.Planar4Bpp => Bytes.ToArray(),
        _ => throw new InvalidOperationException("Packed Mode 7 graphics must be decoded as pixels"),
    };
}

/// <summary>All 52 vanilla graphics files; no Godot, filesystem or CPU dependency.</summary>
public sealed class SmwGraphicsDecoder
{
    public const int FileCount = 0x34;
    private readonly LoRomData _data;

    public SmwGraphicsDecoder(SmwRom rom) => _data = rom.Data;

    public SmwGraphicsFile Decode(int id)
    {
        if ((uint)id >= FileCount) throw new ArgumentOutOfRangeException(nameof(id));
        var address = id switch
        {
            0x32 => 0x080000 | _data.ReadUInt16(0x00B8D8),
            0x33 => 0x080000 | _data.ReadUInt16(0x00B88B),
            _ => _data.ReadByte(0x00B992 + id) | _data.ReadByte(0x00B9C4 + id) << 8 | _data.ReadByte(0x00B9F6 + id) << 16,
        };
        var format = id switch
        {
            0x27 => SmwGraphicsFormat.Packed3Bpp,
            >= 0x28 and <= 0x2B or 0x2F => SmwGraphicsFormat.Planar2Bpp,
            0x32 => SmwGraphicsFormat.Planar4Bpp,
            _ => SmwGraphicsFormat.Planar3Bpp,
        };
        return new SmwGraphicsFile(id, address, Lz2Decoder.Decode(_data.ReadFrom(address)), format);
    }

    public IReadOnlyList<SmwGraphicsFile> DecodeAll()
    {
        var files = new SmwGraphicsFile[FileCount];
        for (var id = 0; id < files.Length; id++) files[id] = Decode(id);
        return files;
    }

    // UploadGraphicsFiles_UploadGFXFile ($00AA6B), vanilla 3bpp branch.
    // Color plane 3 depends on the requested file and active level tileset.
    // The Special World replacement overwrites only GFX01's first 64 tiles;
    // the remaining 64 tiles stay from GFX01. GFX31 is not a full upload itself.
    // Reference: pinned snesrev/smw src/smw_00.c; see THIRD-PARTY-NOTICES.md.
    public byte[] DecodeLevelUpload(int requestedId, int levelTileset, bool specialWorld = false)
    {
        if ((uint)levelTileset > 0xFF) throw new ArgumentOutOfRangeException(nameof(levelTileset));
        var file = Decode(requestedId);
        if (file.Format != SmwGraphicsFormat.Planar3Bpp || file.TileCount != 128)
            throw new ArgumentException("A normal vanilla level upload requires 128 tiles of 3bpp data", nameof(requestedId));
        var source = file.Bytes.ToArray();
        if (requestedId == 1 && specialWorld) Decode(0x31).Bytes.CopyTo(source);
        return ExpandNormalUpload(source, requestedId, levelTileset);
    }

    internal static byte[] ExpandNormalUpload(ReadOnlySpan<byte> source, int requestedId, int levelTileset)
    {
        var output = SnesGraphics.ExpandTo4Bpp(source, 3);
        for (var tile = 0; tile < 128; tile++)
        {
            var mask = requestedId == 0x1E || (requestedId == 8 && levelTileset >= 0x11) ||
                ((requestedId == 1 || requestedId == 0x17) && (tile < 2 || tile is 16 or 17));
            if (!mask) continue;
            for (var y = 0; y < 8; y++)
            {
                var offset = tile * 32 + y * 2;
                output[offset + 17] = (byte)(output[offset] | output[offset + 1] | output[offset + 16]);
            }
        }
        return output;
    }
}
