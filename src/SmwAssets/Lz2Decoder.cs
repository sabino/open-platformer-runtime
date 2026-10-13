using System;
using System.Collections.Generic;
using System.IO;

namespace OpenPlatformerRuntime.SmwAssets;

/// <summary>
/// Native LC_LZ2 data decoding, shared by desktop, browser and extraction tools.
/// Format/control-flow reference: snesrev/smw assets/util.py, MIT licensed;
/// see THIRD-PARTY-NOTICES.md. No ROM data or machine instructions are embedded.
/// </summary>
public static class Lz2Decoder
{
    public static Lz2Decoded Decode(ReadOnlySpan<byte> source, int maximumOutputLength = 0x10000)
    {
        if (maximumOutputLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOutputLength));
        }
        var output = new List<byte>(Math.Min(maximumOutputLength, 4096));
        var position = 0;
        while (true)
        {
            var header = Next(source, ref position);
            if (header == 0xFF)
            {
                return new Lz2Decoded(output.ToArray(), position);
            }
            var operation = header & 0xE0;
            var length = header & 0x1F;
            if (operation == 0xE0)
            {
                operation = (header << 3) & 0xE0;
                length = ((header & 3) << 8) | Next(source, ref position);
            }
            length++;
            if (length > maximumOutputLength - output.Count)
            {
                throw new InvalidDataException("LC_LZ2 output exceeds the declared data limit");
            }
            if (operation == 0)
            {
                for (var i = 0; i < length; i++) output.Add(Next(source, ref position));
            }
            else if ((operation & 0x80) != 0)
            {
                // Vanilla LC_LZ2 treats all 1xx commands as forward copies.
                // LC_LZ3 bit-reversal/backwards-copy rules do not apply here.
                var offset = Next(source, ref position) << 8 | Next(source, ref position);
                for (var i = 0; i < length; i++, offset++)
                {
                    if (offset >= output.Count)
                    {
                        throw new InvalidDataException("LC_LZ2 copy references unavailable output");
                    }
                    output.Add(output[offset]);
                }
            }
            else if ((operation & 0x40) == 0)
            {
                var value = Next(source, ref position);
                for (var i = 0; i < length; i++) output.Add(value);
            }
            else if ((operation & 0x20) == 0)
            {
                var first = Next(source, ref position);
                var second = Next(source, ref position);
                for (var i = 0; i < length; i++) output.Add((i & 1) == 0 ? first : second);
            }
            else
            {
                var value = Next(source, ref position);
                for (var i = 0; i < length; i++) output.Add(unchecked((byte)(value + i)));
            }
        }
    }

    private static byte Next(ReadOnlySpan<byte> source, ref int position)
    {
        if (position >= source.Length)
        {
            throw new InvalidDataException("LC_LZ2 input ended before its terminator");
        }
        return source[position++];
    }
}

public sealed record Lz2Decoded(byte[] Bytes, int CompressedLength);
