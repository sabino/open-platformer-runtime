using System;

namespace OpenPlatformerRuntime.SmwAssets;

/// <summary>Data-only tile decoding. Pixels are palette indices in tile/row order.</summary>
public static class SnesGraphics
{
    public static byte[] DecodePlanar(ReadOnlySpan<byte> bytes, int bitsPerPixel)
    {
        var tileSize = TileSize(bitsPerPixel);
        RequireTiles(bytes, tileSize);
        var pixels = new byte[checked(bytes.Length / tileSize * 64)];
        for (var tile = 0; tile < bytes.Length / tileSize; tile++)
        {
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    var color = 0;
                    for (var plane = 0; plane < bitsPerPixel; plane++)
                    {
                        // The final 3bpp plane uses eight contiguous row bytes.
                        var offset = plane == 2 && bitsPerPixel == 3
                            ? 16 + y : plane / 2 * 16 + y * 2 + plane % 2;
                        color |= ((bytes[tile * tileSize + offset] >> (7 - x)) & 1) << plane;
                    }
                    pixels[tile * 64 + y * 8 + x] = (byte)color;
                }
            }
        }
        return pixels;
    }

    public static byte[] ExpandTo4Bpp(ReadOnlySpan<byte> bytes, int bitsPerPixel)
    {
        var tileSize = TileSize(bitsPerPixel);
        RequireTiles(bytes, tileSize);
        var output = new byte[checked(bytes.Length / tileSize * 32)];
        for (var tile = 0; tile < bytes.Length / tileSize; tile++)
        {
            var source = bytes.Slice(tile * tileSize, tileSize);
            var destination = output.AsSpan(tile * 32, 32);
            source[..16].CopyTo(destination);
            if (bitsPerPixel == 3)
            {
                for (var y = 0; y < 8; y++) destination[16 + y * 2] = source[16 + y];
            }
            else if (bitsPerPixel == 4)
            {
                source[16..].CopyTo(destination[16..]);
            }
        }
        return output;
    }

    // ConvertGFX27IntoNormallFormat ($00AB42): each three bytes hold eight
    // consecutive pixels, most significant three bits first. See the pinned
    // snesrev/smw src/smw_00.c and THIRD-PARTY-NOTICES.md.
    public static byte[] DecodePacked3Bpp(ReadOnlySpan<byte> bytes)
    {
        RequireTiles(bytes, 24);
        var output = new byte[checked(bytes.Length / 3 * 8)];
        for (var i = 0; i < bytes.Length; i += 3)
        {
            var word = bytes[i] << 16 | bytes[i + 1] << 8 | bytes[i + 2];
            for (var x = 0; x < 8; x++) output[i / 3 * 8 + x] = (byte)((word >> (21 - x * 3)) & 7);
        }
        return output;
    }

    private static int TileSize(int bitsPerPixel) => bitsPerPixel switch
    {
        2 => 16,
        3 => 24,
        4 => 32,
        _ => throw new ArgumentOutOfRangeException(nameof(bitsPerPixel), "Only 2, 3 and 4bpp planar graphics are supported"),
    };

    private static void RequireTiles(ReadOnlySpan<byte> bytes, int tileSize)
    {
        if (bytes.Length % tileSize != 0)
            throw new ArgumentException($"Graphics length {bytes.Length} is not a whole number of {tileSize}-byte tiles", nameof(bytes));
    }
}
