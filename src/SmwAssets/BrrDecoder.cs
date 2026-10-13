using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace OpenPlatformerRuntime.SmwAssets;

public sealed record BrrDecodedSample(ReadOnlyMemory<byte> Pcm16, int EncodedLength, bool Loops)
{
    public int FrameCount => Pcm16.Length / 2;
}

/// <summary>BRR file-format decoding to signed mono PCM, without an audio hardware core.</summary>
public static class BrrDecoder
{
    public static BrrDecodedSample Decode(ReadOnlySpan<byte> encoded)
    {
        var pcm = new List<short>();
        var previous = 0;
        var older = 0;
        for (var offset = 0; offset + 9 <= encoded.Length; offset += 9)
        {
            var header = encoded[offset];
            var shift = header >> 4;
            var filter = (header >> 2) & 3;
            for (var i = 0; i < 16; i++)
            {
                var packed = encoded[offset + 1 + i / 2];
                var nibble = (packed >> ((i & 1) == 0 ? 4 : 0)) & 15;
                var sample = (nibble & 7) - (nibble & 8);
                sample = shift <= 12 ? (sample << shift) >> 1 : (sample >> 3) << 11;
                sample += filter switch
                {
                    1 => previous + ((-previous) >> 4),
                    2 => previous * 2 + ((-previous * 3) >> 5) - older + (older >> 4),
                    3 => previous * 2 + ((-previous * 13) >> 6) - older + ((older * 3) >> 4),
                    _ => 0,
                };
                sample = Math.Clamp(sample, -0x8000, 0x7FFF);
                sample = (sample & 0x3FFF) - (sample & 0x4000);
                older = previous;
                previous = sample;
                pcm.Add((short)(sample * 2));
            }

            if ((header & 1) == 0) continue;
            var bytes = new byte[pcm.Count * 2];
            for (var i = 0; i < pcm.Count; i++)
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2, 2), pcm[i]);
            return new BrrDecodedSample(bytes, offset + 9, (header & 2) != 0);
        }
        throw new InvalidDataException("BRR sample is truncated or has no end block");
    }
}
