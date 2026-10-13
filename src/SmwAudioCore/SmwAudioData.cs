using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using OpenPlatformerRuntime.SmwAssets;

namespace OpenPlatformerRuntime.Audio;

public enum SmwMusicBank { Level, Overworld, Credits }

/// <summary>Versioned ROM-derived upload data. Executable firmware is retained as provenance only.</summary>
public sealed class SmwAudioData
{
    public const int SchemaVersion = 1;
    private readonly byte[][] _banks;
    private readonly byte[] _levelMusicIds;
    public string RomSha1 { get; }

    private SmwAudioData(string romSha1, byte[][] banks, byte[]? levelMusicIds = null)
    {
        RomSha1 = romSha1; _banks = banks;
        _levelMusicIds = levelMusicIds ?? Array.Empty<byte>();
        foreach (var bank in banks) ValidateUpload(bank);
    }

    public static SmwAudioData FromRom(SmwRom rom)
    {
        var engine = new byte[6323];
        rom.Data.ReadRange(0x0E8000, 6321).CopyTo(engine);
        return new SmwAudioData(rom.Inspection.Sha1,
            [engine, rom.Data.ReadBytes(0x0F8000, 28538), rom.Data.ReadBytes(0x0EAED6, 16899),
             rom.Data.ReadBytes(0x0E98B1, 5667), rom.Data.ReadBytes(0x03E400, 6624)], rom.Data.ReadBytes(0x0584DB, 8));
    }

    public static SmwAudioData FromUploadBanks(string romSha1, byte[] engine, byte[] samples, byte[] level, byte[] overworld, byte[] credits) =>
        new(romSha1, [engine.AsSpan().ToArray(), samples.AsSpan().ToArray(), level.AsSpan().ToArray(), overworld.AsSpan().ToArray(), credits.AsSpan().ToArray()]);

    public byte[] CreateDataImage(SmwMusicBank bank)
    {
        var result = new byte[65536];
        ApplyUpload(_banks[0], result, skipFirmware: true);
        ApplyUpload(_banks[1], result, skipFirmware: false);
        ApplyUpload(_banks[2 + (int)bank], result, skipFirmware: false);
        return result;
    }

    public ReadOnlyMemory<byte> UploadPayload(int index) => _banks[index];

    public byte LevelSong(int headerMusicIndex)
    {
        if ((uint)headerMusicIndex >= 8) throw new ArgumentOutOfRangeException(nameof(headerMusicIndex));
        if (_levelMusicIds.Length != 8) throw new NotSupportedException("Legacy raw banks do not contain the level music mapping; select a song explicitly or load the ROM");
        return _levelMusicIds[headerMusicIndex];
    }

    public void ApplyMusicBank(SmwMusicBank bank, Span<byte> image)
    {
        if (image.Length != 65536) throw new ArgumentException("Sound image length mismatch", nameof(image));
        ApplyUpload(_banks[2 + (int)bank], image, false);
    }

    private static void ValidateUpload(ReadOnlySpan<byte> payload) => ApplyUpload(payload, Span<byte>.Empty, false);

    private static void ApplyUpload(ReadOnlySpan<byte> payload, Span<byte> destination, bool skipFirmware)
    {
        var offset = 0;
        while (offset + 2 <= payload.Length)
        {
            var count = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset, 2)); offset += 2;
            if (count == 0) return;
            if (payload.Length - offset < count + 2) throw new InvalidDataException("Truncated native sound-data bank");
            var address = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset, 2)); offset += 2;
            if (address + count > 65536) throw new InvalidDataException("Native sound-data block crosses its 16-bit storage range");
            if (!destination.IsEmpty && !(skipFirmware && address < 0x1360))
                payload.Slice(offset, count).CopyTo(destination.Slice(address, count));
            offset += count;
        }
        throw new InvalidDataException("Native sound-data bank has no terminator");
    }

    public byte[] ToBinary()
    {
        using var buffer = new MemoryStream(); using var writer = new BinaryWriter(buffer);
        writer.Write("SMWAUDIO"u8); writer.Write(SchemaVersion); writer.Write(RomSha1);
        writer.Write(_banks.Length);
        writer.Write(_levelMusicIds.Length); writer.Write(_levelMusicIds);
        foreach (var bank in _banks) { writer.Write(bank.Length); writer.Write(bank); }
        return buffer.ToArray();
    }

    public static SmwAudioData FromBinary(ReadOnlySpan<byte> bytes)
    {
        using var buffer = new MemoryStream(bytes.ToArray()); using var reader = new BinaryReader(buffer);
        if (!reader.ReadBytes(8).AsSpan().SequenceEqual("SMWAUDIO"u8) || reader.ReadInt32() != SchemaVersion)
            throw new InvalidDataException("Unsupported native sound-data schema");
        var hash = reader.ReadString();
        if (hash != SmwRomInspector.ExpectedUnheaderedSha1) throw new InvalidDataException("Native sound-data ROM fingerprint mismatch");
        if (reader.ReadInt32() != 5) throw new InvalidDataException("Native sound-data bank count mismatch");
        var musicCount = reader.ReadInt32();
        if (musicCount != 8) throw new InvalidDataException("Native sound-data level music mapping is missing");
        var musicIds = reader.ReadBytes(musicCount);
        var banks = new byte[5][];
        for (var i = 0; i < banks.Length; i++)
        {
            var size = reader.ReadInt32();
            if (size < 2 || size > 65540 || size > buffer.Length - buffer.Position) throw new InvalidDataException("Invalid native sound-data bank length");
            banks[i] = reader.ReadBytes(size);
        }
        if (buffer.Position != buffer.Length) throw new InvalidDataException("Trailing native sound-data bytes");
        return new SmwAudioData(hash, banks, musicIds);
    }
}
