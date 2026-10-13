using System;

namespace OpenPlatformerRuntime.SmwAssets;

public sealed record SmwRgbaImage(int Width, int Height, byte[] Bytes);

/// <summary>ROM tiles assembled into exact RGBA images for native Godot textures.</summary>
public static class SmwGraphicsImages
{
    public static SmwRgbaImage CreateAtlas(ReadOnlySpan<byte> pixels, ReadOnlySpan<ushort> palette, int columns = 16)
    {
        if (pixels.Length == 0 || pixels.Length % 64 != 0) throw new ArgumentException("Expected whole decoded tiles", nameof(pixels));
        if (palette.Length < 16) throw new ArgumentException("Expected at least 16 palette colors", nameof(palette));
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        var count = pixels.Length / 64;
        var width = checked(columns * 8);
        var height = checked((count + columns - 1) / columns * 8);
        var bytes = new byte[checked(width * height * 4)];
        for (var tile = 0; tile < count; tile++)
        {
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    var index = pixels[tile * 64 + y * 8 + x];
                    if (index >= palette.Length) throw new ArgumentException("Pixel index exceeds palette", nameof(pixels));
                    var output = ((tile / columns * 8 + y) * width + tile % columns * 8 + x) * 4;
                    WritePixel(bytes, output, palette[index], index == 0 ? (byte)0 : (byte)255);
                }
            }
        }
        return new SmwRgbaImage(width, height, bytes);
    }

    public static SmwRgbaImage CreateMap16Atlas(ReadOnlySpan<ushort> map16, ReadOnlySpan<byte> planar, ReadOnlySpan<ushort> palette, int columns = 16)
    {
        if (map16.Length == 0 || map16.Length % 4 != 0) throw new ArgumentException("Expected whole Map16 entries", nameof(map16));
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        var tileCount = map16.Length / 4;
        var image = new SmwRgbaImage(checked(columns * 16), checked((tileCount + columns - 1) / columns * 16),
            new byte[checked(columns * 16 * ((tileCount + columns - 1) / columns * 16) * 4)]);
        var pixels = SnesGraphics.DecodePlanar(planar, 4);
        RequireLevelPalette(palette);
        for (var tile = 0; tile < tileCount; tile++)
            BlitMap16(image, tile % columns * 16, tile / columns * 16, map16.Slice(tile * 4, 4), pixels, palette);
        return image;
    }

    public static SmwRgbaImage CreateLayer2(SmwBackgroundTiles background, ReadOnlySpan<ushort> map16, ReadOnlySpan<byte> planar, ReadOnlySpan<ushort> palette)
    {
        if (background.Bytes.Length != 32 * 27) throw new ArgumentException("Vanilla background must contain two 16x27 halves", nameof(background));
        RequireLevelPalette(palette);
        var image = new SmwRgbaImage(512, 432, new byte[512 * 432 * 4]);
        var pixels = SnesGraphics.DecodePlanar(planar, 4);
        for (var i = 0; i < background.Bytes.Length; i++)
        {
            var tile = background.Page * 256 + background.Bytes[i];
            var x = i / 432 * 16 + i % 16;
            var y = i % 432 / 16;
            BlitMap16(image, x * 16, y * 16, map16.Slice(tile * 4, 4), pixels, palette);
        }
        return image;
    }

    private static void BlitMap16(SmwRgbaImage image, int x0, int y0, ReadOnlySpan<ushort> words, ReadOnlySpan<byte> pixels, ReadOnlySpan<ushort> palette)
    {
        for (var sub = 0; sub < 4; sub++)
        {
            // Original Map16 order is TL/BL/TR/BR.
            var word = words[sub];
            var tile = word & 0x03FF;
            if (tile * 64 >= pixels.Length) continue;
            var row = (word >> 10 & 7) * 16;
            var dx = sub / 2 * 8;
            var dy = sub % 2 * 8;
            for (var y = 0; y < 8; y++)
            {
                var sy = (word & 0x8000) != 0 ? 7 - y : y;
                for (var x = 0; x < 8; x++)
                {
                    var sx = (word & 0x4000) != 0 ? 7 - x : x;
                    var color = pixels[tile * 64 + sy * 8 + sx];
                    if (color == 0) continue;
                    var output = ((y0 + dy + y) * image.Width + x0 + dx + x) * 4;
                    WritePixel(image.Bytes, output, palette[row + color], 255);
                }
            }
        }
    }

    private static void WritePixel(Span<byte> bytes, int offset, ushort word, byte alpha)
    {
        var color = SmwPaletteDecoder.ToRgb(word);
        bytes[offset] = color.R;
        bytes[offset + 1] = color.G;
        bytes[offset + 2] = color.B;
        bytes[offset + 3] = alpha;
    }

    private static void RequireLevelPalette(ReadOnlySpan<ushort> palette)
    {
        if (palette.Length != 256) throw new ArgumentException("Expected a complete game palette", nameof(palette));
    }
}
