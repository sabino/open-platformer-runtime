using System;
using System.Collections.Generic;
using System.IO;

namespace OpenPlatformerRuntime.SmwAssets;

public readonly record struct SmwObjectPlacement(int Layer, int Screen, int SubX, int SubY, bool HighSubscreen, bool Vertical)
{
    public int Map16Offset => (SubY + (HighSubscreen ? 16 : 0)) * 16 + SubX;
    public int XTile => Vertical ? SubY + (HighSubscreen ? 16 : 0) : Screen * 16 + SubX;
    public int YTile => Vertical ? Screen * 16 + SubX : SubY + (HighSubscreen ? 16 : 0);
}

public sealed record SmwLevelObject(int Sequence, int Offset, byte B0, byte B1, byte SizeOrType, byte? ExitLow, SmwObjectPlacement Placement)
{
    public int Id => B1 >> 4 | (B0 & 0x60) >> 1;
    public bool ScreenIncrement => (B0 & 0x80) != 0;
    public bool IsExit => Id == 0 && SizeOrType == 0;
    public bool IsScreenJump => Id == 0 && SizeOrType == 1;
}

public readonly record struct SmwScreenExit(int Screen, byte DestinationLow, byte Properties)
{
    // The stream record requests this selector; the game applies the global
    // last-record value exposed by SmwLevelData.ExitSecondarySelector.
    public bool Secondary => Properties >> 1 != 0;
    // LoadLevel ($05D796): the current overworld map selects the bank.
    // An editor level ID is not authoritative world state.
    public int ResolveId(bool overworldSubmap) => (overworldSubmap ? 0x100 : 0) | DestinationLow;
}

public sealed record SmwObjectStream(int SourceAddress, byte[] Bytes, IReadOnlyList<SmwLevelObject> Objects, IReadOnlyList<SmwScreenExit> Exits);

public readonly record struct SmwSpriteRecord(int Index, int Offset, byte Position, byte ScreenPosition, byte Id, bool Vertical)
{
    public int Screen => (Position << 3 & 0x10) | (ScreenPosition & 15);
    public int Along => Screen * 256 + (ScreenPosition & 0xF0);
    // Vanilla LoadOneSprite ($02A82E) retains bits 0, 2, 3 in the high byte.
    // Those bits are not Lunar Magic extra-bit flags in the vanilla loader.
    public int Across => (Position & 0xF0) | (Position & 0x0D) << 8;
    public int X => Vertical ? Across : Along;
    public int Y => Vertical ? Along : Across;
    public int ScrollParameter => Position >> 2;
}

public sealed record SmwSpriteStream(int SourceAddress, byte[] Bytes, byte Header, IReadOnlyList<SmwSpriteRecord> Sprites)
{
    public int MemorySetting => Header & 31;
    public int Buoyancy => Header & 0xC0;
}

/// <summary>Bounded vanilla streams. No Lunar Magic dispatch or CPU execution.</summary>
public static class SmwLevelStreams
{
    // LoadLevelDataObject ($0585FF), ExtObj00/01 ($0DA512/$0DA53D),
    // and LoadOneSprite ($02A82E). Pinned snesrev/smw; see THIRD-PARTY-NOTICES.md.
    public static SmwObjectStream ReadObjects(LoRomData data, int address, byte layoutFlags, int layer = 0)
    {
        var result = ParseObjects(data.ReadFrom(address), layoutFlags, layer);
        return result with { SourceAddress = address };
    }

    public static SmwObjectStream ParseObjects(ReadOnlySpan<byte> source, byte layoutFlags, int layer = 0)
    {
        if ((uint)layer > 1) throw new ArgumentOutOfRangeException(nameof(layer));
        if (source.Length < 6) throw new InvalidDataException("Object stream lacks a header and terminator");
        var objects = new List<SmwLevelObject>();
        var exits = new List<SmwScreenExit>();
        var vertical = ((layoutFlags >> layer) & 1) != 0;
        var index = 5;
        var screen = 0;
        while (index < source.Length && index < 0x8000)
        {
            var offset = index;
            var b0 = source[index++];
            if (b0 == 0xFF) return new SmwObjectStream(0, source[..index].ToArray(), objects, exits);
            if (source.Length - index < 2) throw new InvalidDataException("Truncated vanilla object record");
            var b1 = source[index++];
            var b2 = source[index++];
            var id = b1 >> 4 | (b0 & 0x60) >> 1;
            screen = (screen + ((b0 & 0x80) != 0 ? 1 : 0)) & 255;
            var adjusted0 = b0;
            var adjusted1 = b1;
            if (vertical && (id << 8 | b2) >= 2)
            {
                adjusted0 = (byte)((b0 & 0xF0) | (b1 & 15));
                adjusted1 = (byte)((b1 & 0xF0) | (b0 & 15));
            }
            byte? exit = null;
            if (id == 0 && b2 == 0)
            {
                if (index >= source.Length) throw new InvalidDataException("Truncated screen exit");
                exit = source[index++];
                exits.Add(new SmwScreenExit(b0 & 31, exit.Value, b1));
            }
            var placement = new SmwObjectPlacement(layer + 1, screen, adjusted1 & 15, adjusted0 & 15, (adjusted0 & 16) != 0, vertical);
            objects.Add(new SmwLevelObject(objects.Count, offset, b0, b1, b2, exit, placement));
            if (id == 0 && b2 == 1) screen = b0 & 31;
        }
        throw new InvalidDataException("Vanilla object stream lacks a bounded terminator");
    }

    public static SmwSpriteStream ReadSprites(LoRomData data, int address, bool vertical)
    {
        var result = ParseSprites(data.ReadFrom(address), vertical);
        return result with { SourceAddress = address };
    }

    public static SmwSpriteStream ParseSprites(ReadOnlySpan<byte> source, bool vertical)
    {
        if (source.Length < 2) throw new InvalidDataException("Sprite stream lacks a header and terminator");
        var sprites = new List<SmwSpriteRecord>();
        var index = 1;
        while (index < source.Length && index < 0x8000)
        {
            if (source[index] == 0xFF) return new SmwSpriteStream(0, source[..(index + 1)].ToArray(), source[0], sprites);
            if (source.Length - index < 3) throw new InvalidDataException("Truncated sprite record");
            sprites.Add(new SmwSpriteRecord(sprites.Count, index, source[index], source[index + 1], source[index + 2], vertical));
            index += 3;
        }
        throw new InvalidDataException("Sprite stream lacks a bounded terminator");
    }
}
