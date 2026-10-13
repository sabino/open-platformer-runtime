using System;
using System.Collections.Generic;
using System.IO;

namespace OpenPlatformerRuntime.SmwAssets;

/// <summary>Vanilla Layer 2 LC_RLE1 data, terminated by FF FF at a command boundary.</summary>
public static class Rle1Decoder
{
    // BufferBGTilemap ($058126), snesrev/smw src/smw_05.c.
    // See THIRD-PARTY-NOTICES.md. No emulated hardware is involved.
    public static Rle1Decoded Decode(ReadOnlySpan<byte> source, int maximumOutputLength = 0x10000)
    {
        if (maximumOutputLength < 0) throw new ArgumentOutOfRangeException(nameof(maximumOutputLength));
        var output = new List<byte>();
        var position = 0;
        while (true)
        {
            if (source.Length - position < 2) throw new InvalidDataException("LC_RLE1 stream lacks its terminator");
            if (source[position] == 0xFF && source[position + 1] == 0xFF)
                return new Rle1Decoded(output.ToArray(), position + 2);
            var control = source[position++];
            var count = (control & 127) + 1;
            if (count > maximumOutputLength - output.Count) throw new InvalidDataException("LC_RLE1 output exceeds the declared limit");
            if ((control & 128) != 0)
            {
                var value = source[position++];
                for (var i = 0; i < count; i++) output.Add(value);
            }
            else
            {
                if (count > source.Length - position) throw new InvalidDataException("Truncated LC_RLE1 literal");
                for (var i = 0; i < count; i++) output.Add(source[position++]);
            }
        }
    }
}

public sealed record Rle1Decoded(byte[] Bytes, int CompressedLength);
