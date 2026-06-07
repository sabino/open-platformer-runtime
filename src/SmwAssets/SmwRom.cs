using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace OpenPlatformerRuntime.SmwAssets;

public sealed class SmwRom
{
    private SmwRom(string fileName, byte[] data, SmwRomInspection inspection)
    {
        FileName = fileName;
        Data = data;
        Inspection = inspection;
        Sha1 = inspection.Sha1;
    }

    public string FileName { get; }
    public byte[] Data { get; }
    public SmwRomInspection Inspection { get; }
    public string Sha1 { get; }

    public static SmwRom Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"ROM does not exist: {fullPath}", fullPath);
        }

        return FromBytes(File.ReadAllBytes(fullPath), Path.GetFileName(fullPath));
    }

    public static SmwRom FromBytes(ReadOnlySpan<byte> selectedBytes, string? fileName = null)
    {
        var data = selectedBytes.ToArray();
        if (SmwRomInspector.HasCopierHeader(data.Length))
        {
            data = data.Skip(SmwRomInspector.CopierHeaderSize).ToArray();
        }

        var inspection = SmwRomInspector.Inspect(data);
        if (inspection.HasCopierHeader)
        {
            throw new InvalidOperationException($"headered ROMs are not supported after canonicalization: size={inspection.Size} sha1={inspection.Sha1}");
        }
        if (!inspection.IsExpectedSize)
        {
            throw new InvalidOperationException($"unsupported ROM size={inspection.Size}; expected {SmwRomInspector.ExpectedUnheaderedRomSize}");
        }
        if (!inspection.IsExpectedSha1)
        {
            throw new InvalidOperationException(
                $"unsupported ROM sha1={inspection.Sha1}; expected {SmwRomInspector.ExpectedRomLabel} {SmwRomInspector.ExpectedUnheaderedSha1}");
        }

        return new SmwRom(string.IsNullOrWhiteSpace(fileName) ? "selected.sfc" : fileName!, data, inspection);
    }

    public int LoRomIndex(int address)
    {
        try
        {
            return SmwRomInspector.LoRomIndex(address, Data.Length);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    public bool HasLoRomRange(int address, int length)
    {
        if (length < 0)
        {
            return false;
        }

        try
        {
            for (var i = 0; i < length; i++)
            {
                _ = LoRomIndex(IncrementLoRomAddress(address, i));
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public int GetByte(int address)
    {
        return Data[LoRomIndex(address)];
    }

    public int GetWord(int address)
    {
        return GetByte(address) | (GetByte(IncrementLoRomAddress(address, 1)) << 8);
    }

    public int[] GetWords(int address, int count)
    {
        var words = new int[count];
        for (var i = 0; i < count; i++)
        {
            words[i] = GetWord(IncrementLoRomAddress(address, i * 2));
        }

        return words;
    }

    public int Get24(int address)
    {
        return GetWord(address) | (GetByte(IncrementLoRomAddress(address, 2)) << 16);
    }

    public byte[] GetBytes(int address, int count)
    {
        var output = new byte[count];
        for (var i = 0; i < count; i++)
        {
            output[i] = (byte)GetByte(address);
            address = IncrementLoRomAddress(address, 1);
        }

        return output;
    }

    public static string Sha1Hex(ReadOnlySpan<byte> payload)
    {
        return Convert.ToHexString(SHA1.HashData(payload));
    }

    public static int IncrementLoRomAddress(int address, int delta)
    {
        var result = address;
        for (var i = 0; i < delta; i++)
        {
            result++;
            if ((result & 0x8000) == 0)
            {
                result += 0x8000;
            }
        }

        return result;
    }
}

internal sealed class SmwRomReader
{
    private readonly SmwRom _rom;

    public SmwRomReader(SmwRom rom, int address)
    {
        _rom = rom;
        Address = address;
    }

    public int Address { get; private set; }

    public int Next()
    {
        var value = _rom.GetByte(Address);
        Address = SmwRom.IncrementLoRomAddress(Address, 1);
        return value;
    }
}
