using System;
using System.Security.Cryptography;

namespace OpenPlatformerRuntime.SmwAssets;

public static class SmwRomInspector
{
    public const int ExpectedUnheaderedRomSize = 0x80000;
    public const int CopierHeaderSize = 0x200;
    public const string ExpectedUnheaderedSha1 = "6B47BB75D16514B6A476AA0C73A683A2A4C18765";
    public const string ExpectedRomLabel = "unheadered SMW USA";

    public static SmwRomInspection Inspect(ReadOnlySpan<byte> data)
    {
        var sha1 = Convert.ToHexString(SHA1.HashData(data));
        var hasCopierHeader = HasCopierHeader(data.Length);
        var isExpectedSize = data.Length == ExpectedUnheaderedRomSize;
        var isExpectedSha1 = string.Equals(sha1, ExpectedUnheaderedSha1, StringComparison.OrdinalIgnoreCase);
        return new SmwRomInspection(data.Length, sha1, hasCopierHeader, isExpectedSize, isExpectedSha1);
    }

    public static bool HasCopierHeader(int byteLength)
    {
        return byteLength > CopierHeaderSize && byteLength % 0x8000 == CopierHeaderSize;
    }

    public static int LoRomIndex(int address, int romByteLength)
    {
        if ((address & 0x8000) == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address), $"LoROM address must have bit 0x8000 set: 0x{address:X6}");
        }

        var index = ((address >> 16) & 0x7F) * 0x8000 + (address & 0x7FFF);
        if (index < 0 || index >= romByteLength)
        {
            throw new ArgumentOutOfRangeException(nameof(address), $"LoROM address out of range: 0x{address:X6}");
        }

        return index;
    }
}

public sealed record SmwRomInspection(
    long Size,
    string Sha1,
    bool HasCopierHeader,
    bool IsExpectedSize,
    bool IsExpectedSha1)
{
    public bool IsSupported => !HasCopierHeader && IsExpectedSize && IsExpectedSha1;

    public string Status =>
        IsSupported ? "supported" :
        HasCopierHeader ? "headered" :
        !IsExpectedSize ? "wrong-size" :
        "wrong-sha1";

    public object ToSerializable()
    {
        return new
        {
            size = Size,
            sha1 = Sha1,
            has_copier_header = HasCopierHeader,
            is_expected_size = IsExpectedSize,
            is_expected_sha1 = IsExpectedSha1,
            is_supported = IsSupported,
            status = Status,
            expected_size = SmwRomInspector.ExpectedUnheaderedRomSize,
            expected_sha1 = SmwRomInspector.ExpectedUnheaderedSha1,
            expected_rom = SmwRomInspector.ExpectedRomLabel,
        };
    }
}
