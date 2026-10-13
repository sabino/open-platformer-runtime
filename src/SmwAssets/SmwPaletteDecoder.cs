using System;

namespace OpenPlatformerRuntime.SmwAssets;

public readonly record struct SmwPaletteSelection(int Sky, int Background, int Foreground, int Sprites);

/// <summary>Original palette data and game palette assembly, independent of a renderer.</summary>
public sealed class SmwPaletteDecoder
{
    private readonly LoRomData _data;
    public SmwPaletteDecoder(SmwRom rom) => _data = rom.Data;

    // BufferPalettesRoutines_Levels ($00ABED), snesrev/smw src/smw_00.c.
    // These writes deliberately preserve unused entries from the prior mode.
    // See THIRD-PARTY-NOTICES.md for source attribution.
    public ushort ApplyLevelBase(Span<ushort> palette, SmwPaletteSelection selection)
    {
        RequirePalette(palette);
        for (var row = 0; row < 16; row++) palette[row * 16 + 1] = (ushort)(row < 8 ? 0x7FDD : 0x7FFF);
        LoadColors(palette, 0x00B170, 0, 8, 8, 2);
        LoadColors(palette, 0x00B250, 4, 2, 6, 10);
        LoadColors(palette, 0x00B190 + SelectionOffset(selection.Foreground), 2, 2, 6, 2);
        LoadColors(palette, 0x00B318 + SelectionOffset(selection.Sprites), 14, 2, 6, 2);
        LoadColors(palette, 0x00B0B0 + SelectionOffset(selection.Background), 0, 2, 6, 2);
        LoadColors(palette, 0x00B674, 2, 9, 7, 3);
        LoadColors(palette, 0x00B674, 9, 9, 7, 3);
        return _data.ReadUInt16(0x00B0A0 + (selection.Sky & 15) * 2);
    }

    public void ApplyPlayerVariant(Span<ushort> palette, int variant)
    {
        RequirePalette(palette);
        if ((uint)variant > 3) throw new ArgumentOutOfRangeException(nameof(variant));
        LoadColors(palette, 0x00B2C8 + variant * 20, 8, 6, 10, 1);
    }

    /// <summary>Explicit zero-based development entry, with player and initial flashing color.</summary>
    public ushort[] CreateLevelEntry(SmwPaletteSelection selection, int playerVariant = 0)
    {
        var palette = new ushort[256];
        ApplyLevelBase(palette, selection);
        ApplyPlayerVariant(palette, playerVariant);
        palette[6 * 16 + 4] = _data.ReadUInt16(0x00B60C);
        return palette;
    }

    public ushort[] CreatePlayerRow(int variant)
    {
        var palette = new ushort[256];
        palette[8 * 16 + 1] = 0x7FFF;
        LoadColors(palette, 0x00B280, 8, 2, 6, 1);
        ApplyPlayerVariant(palette, variant);
        return palette.AsSpan(128, 16).ToArray();
    }

    // BufferPalettesRoutines_Overworld ($00AD25). Region selection and offsets
    // are ROM tables, not copies of the palette data in source code.
    public void ApplyOverworld(Span<ushort> palette, int region, bool specialWorld)
    {
        RequirePalette(palette);
        if (region is < 1 or > 7) throw new ArgumentOutOfRangeException(nameof(region));
        var index = _data.ReadByte(0x00AD1E + region - 1);
        var offset = _data.ReadUInt16(0x00ABDF + index * 2);
        LoadColors(palette, (specialWorld ? 0x00B732 : 0x00B3D8) + offset, 4, 1, 7, 4);
        LoadColors(palette, 0x00B528, 2, 9, 7, 6);
        LoadColors(palette, 0x00B57C, 8, 1, 7, 8);
        LoadColors(palette, 0x00B5EC, 0, 8, 8, 2);
    }

    public void ApplyTitle(Span<ushort> palette)
    {
        RequirePalette(palette);
        LoadColors(palette, 0x00B63C, 0, 8, 8, 1);
        LoadColors(palette, 0x00B62C, 1, 8, 8, 1);
    }

    public ushort ApplyIggyLarry(Span<ushort> palette, SmwPaletteSelection selection)
    {
        ApplyLevelBase(palette, selection);
        LoadColors(palette, 0x00B65C, 0, 0, 8, 1);
        return 23;
    }

    public ushort ApplyMode7Boss(Span<ushort> palette, SmwPaletteSelection selection)
    {
        ApplyLevelBase(palette, selection with { Sprites = 2, Foreground = 7 });
        LoadColors(palette, 0x00B5F4, 0, 12, 4, 1);
        return 23;
    }

    public static (byte R, byte G, byte B) ToRgb(ushort word) =>
        (Expand5(word & 31), Expand5(word >> 5 & 31), Expand5(word >> 10 & 31));

    private static byte Expand5(int value) => (byte)((value << 3) | (value >> 2));
    private int SelectionOffset(int index) => _data.ReadByte(0x00ABD3 + (index & 15));

    private void LoadColors(Span<ushort> palette, int address, int row, int color, int count, int rows)
    {
        for (var r = 0; r < rows; r++)
        {
            for (var i = 0; i < count; i++)
            {
                palette[(row + r) * 16 + color + i] = _data.ReadUInt16(address);
                address = LoRomData.NextAddress(LoRomData.NextAddress(address));
            }
        }
    }

    private static void RequirePalette(Span<ushort> palette)
    {
        if (palette.Length != 256) throw new ArgumentException("A game palette must contain 256 color words", nameof(palette));
    }
}
