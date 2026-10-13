using System;
using System.Buffers.Binary;

namespace OpenPlatformerRuntime.SmwAssets;

/// <summary>
/// Immutable ROM data access. Sequential reads follow the ROM file across
/// LoROM banks; this is not a CPU bus or an instruction execution facility.
/// </summary>
public sealed class LoRomData
{
    private readonly byte[] _bytes;

    public LoRomData(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty || bytes.Length > 0x400000 || bytes.Length % 0x8000 != 0)
        {
            throw new ArgumentException("LoROM data must contain complete 32 KiB banks, at most 4 MiB", nameof(bytes));
        }
        _bytes = bytes.ToArray();
    }

    public int Length => _bytes.Length;

    public byte ReadByte(int address) => _bytes[SmwRomInspector.LoRomIndex(address, Length)];

    public ushort ReadUInt16(int address)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(ReadRange(address, 2));
    }

    public int ReadUInt24(int address)
    {
        var bytes = ReadRange(address, 3);
        return bytes[0] | bytes[1] << 8 | bytes[2] << 16;
    }

    public byte[] ReadBytes(int address, int count) => ReadRange(address, count).ToArray();

    public ReadOnlySpan<byte> ReadFrom(int address) => _bytes.AsSpan(SmwRomInspector.LoRomIndex(address, Length));

    public ReadOnlySpan<byte> ReadRange(int address, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        var offset = SmwRomInspector.LoRomIndex(address, Length);
        if (count > Length - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "ROM data range extends beyond the image");
        }
        return _bytes.AsSpan(offset, count);
    }

    public static int NextAddress(int address)
    {
        if ((uint)address > 0xFFFFFF || (address & 0x8000) == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }
        var next = (address & 0xFFFF) == 0xFFFF ? address + 0x8001 : address + 1;
        if (next > 0xFFFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(address), "ROM data address exceeds 24 bits");
        }
        return next;
    }
}
