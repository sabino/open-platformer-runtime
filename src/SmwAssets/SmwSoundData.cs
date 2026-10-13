using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace OpenPlatformerRuntime.SmwAssets;

public sealed record SmwAudioSample(int Id, int SourceStart, int SourceLoop, BrrDecodedSample Waveform, int? LoopStartFrame)
{
    public const int SampleRate = 32000;
}

public readonly record struct SmwSfxInstrument(
    int Id, byte LeftVolume, byte RightVolume, byte SampleId,
    byte AttackDecay, byte SustainRelease, byte Gain, byte PitchBase);

public readonly record struct SmwSfxPreviewNote(
    int Instrument, int Note, int DurationFrames, float Volume, int DelayFrames, float Pan);

public sealed record SmwSfxPreview(
    int SourcePointer, IReadOnlyList<SmwSfxPreviewNote> Notes, bool HasUnsupportedCommands);

/// <summary>
/// Immutable, typed sound assets. Upload addresses are resolved only while
/// reading the ROM's storage format; playback never maintains SPC RAM or runs
/// the sound firmware. SFX previews remain approximate until P8 is implemented.
/// </summary>
public sealed class SmwSoundData
{
    private readonly Dictionary<(int Port, int Command), SmwSfxPreview> _effects;

    private SmwSoundData(UploadSegments engine, UploadSegments samples)
    {
        var decoded = new List<SmwAudioSample>();
        var directory = samples.ReadRange(0x8000, 20 * 4);
        for (var id = 0; id < 20; id++)
        {
            var start = BinaryPrimitives.ReadUInt16LittleEndian(directory.Slice(id * 4, 2));
            var loop = BinaryPrimitives.ReadUInt16LittleEndian(directory.Slice(id * 4 + 2, 2));
            var waveform = BrrDecoder.Decode(samples.ReadFrom(start));
            int? loopFrame = null;
            if (waveform.Loops)
            {
                var relative = loop - start;
                if (relative < 0 || relative >= waveform.EncodedLength || relative % 9 != 0)
                    throw new InvalidDataException($"Invalid BRR loop for sample {id}");
                loopFrame = relative / 9 * 16;
            }
            decoded.Add(new SmwAudioSample(id, start, loop, waveform, loopFrame));
        }
        Samples = decoded.AsReadOnly();

        var instruments = new List<SmwSfxInstrument>();
        var table = engine.ReadRange(0x5570, 19 * 9);
        for (var id = 0; id < 19; id++)
        {
            var entry = table.Slice(id * 9, 9);
            if (entry[4] >= Samples.Count) throw new InvalidDataException("SFX instrument refers to a missing sample");
            instruments.Add(new SmwSfxInstrument(id, entry[0], entry[1], entry[4], entry[5], entry[6], entry[7], entry[8]));
        }
        SfxInstruments = instruments.AsReadOnly();

        _effects = [];
        ReadEffects(engine, port: 1, tableAddress: 0x5681, count: 42);
        ReadEffects(engine, port: 3, tableAddress: 0x5619, count: 52);
    }

    public ReadOnlyCollection<SmwAudioSample> Samples { get; }
    public ReadOnlyCollection<SmwSfxInstrument> SfxInstruments { get; }
    public int SoundEffectCount => _effects.Count;

    public bool TryGetSfxPreview(int port, int command, out SmwSfxPreview preview) =>
        _effects.TryGetValue((port, command), out preview!);

    public static SmwSoundData FromRom(SmwRom rom)
    {
        // The engine's stored payload omits the upload terminator. Only the data
        // segment is used below; its executable segment is never interpreted.
        var engine = new byte[6323];
        rom.Data.ReadRange(0x0E8000, 6321).CopyTo(engine);
        return FromUploadBanks(engine, rom.Data.ReadRange(0x0F8000, 28538));
    }

    public static SmwSoundData FromUploadBanks(ReadOnlySpan<byte> engine, ReadOnlySpan<byte> samples) =>
        new(UploadSegments.Parse(engine), UploadSegments.Parse(samples));

    private void ReadEffects(UploadSegments engine, int port, int tableAddress, int count)
    {
        // Command zero is silence; each real command starts at index one.
        var pointers = engine.ReadRange(tableAddress + 2, count * 2);
        for (var command = 1; command <= count; command++)
        {
            var pointer = BinaryPrimitives.ReadUInt16LittleEndian(pointers.Slice((command - 1) * 2, 2));
            var preview = ReadPreview(engine.ReadFrom(pointer), port, pointer);
            _effects.Add((port, command), preview);
        }
    }

    private static SmwSfxPreview ReadPreview(ReadOnlySpan<byte> stream, int port, int pointer)
    {
        var notes = new List<SmwSfxPreviewNote>();
        var offset = 0;
        var length = 8;
        var instrument = 7;
        var volume = 0.34f;
        var pan = 0.0f;
        var delay = port == 3 ? 2 : 0;
        var unsupported = false;
        while (offset < stream.Length && offset < 192 && notes.Count < 16)
        {
            var command = stream[offset++];
            if (command == 0) return new SmwSfxPreview(pointer, notes.AsReadOnly(), unsupported);
            if ((command & 0x80) == 0)
            {
                length = Math.Max(1, (int)command);
                if (offset >= stream.Length) break;
                command = stream[offset++];
                if ((command & 0x80) == 0)
                {
                    var left = command;
                    if (offset >= stream.Length) break;
                    command = stream[offset++];
                    var right = left;
                    if ((command & 0x80) == 0)
                    {
                        right = command;
                        if (offset >= stream.Length) break;
                        command = stream[offset++];
                    }
                    var l = Math.Clamp(left / 127.0f, 0, 1);
                    var r = Math.Clamp(right / 127.0f, 0, 1);
                    volume = Math.Clamp((l + r) * 0.22f, 0.06f, 0.5f);
                    pan = Math.Clamp(r - l, -0.8f, 0.8f);
                }
            }

            if (command == 0xDA)
            {
                do
                {
                    if (offset >= stream.Length) return new SmwSfxPreview(pointer, notes.AsReadOnly(), true);
                    command = stream[offset++];
                    if (port == 3 && (command & 0x80) != 0) unsupported = true; // Noise is not previewed.
                } while (port == 3 && (command & 0x80) != 0);
                instrument = command;
                if (instrument >= 19) return new SmwSfxPreview(pointer, notes.AsReadOnly(), true);
                continue;
            }
            if (command == 0xDD)
            {
                if (offset >= stream.Length) break;
                var note = stream[offset++];
                notes.Add(new SmwSfxPreviewNote(instrument, note, length, volume, delay, pan));
                delay += length;
                offset += 3;
                unsupported = true; // Pitch envelopes are not previewed.
                continue;
            }
            if (command == 0xEB)
            {
                offset += 3;
                unsupported = true;
                continue;
            }
            if (command == 0xFF) break; // Native repetition needs the full sequencer.
            if ((command & 0x80) != 0)
            {
                notes.Add(new SmwSfxPreviewNote(instrument, command, length, volume, delay, pan));
                delay += length;
            }
        }
        return new SmwSfxPreview(pointer, notes.AsReadOnly(), true);
    }

    /// <summary>Bounded immutable file chunks used only during asset decoding.</summary>
    private sealed class UploadSegments
    {
        private sealed record Segment(int SourceAddress, byte[] Bytes);
        private readonly List<Segment> _segments = [];

        public static UploadSegments Parse(ReadOnlySpan<byte> payload)
        {
            var result = new UploadSegments();
            var offset = 0;
            while (offset + 2 <= payload.Length)
            {
                var count = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset, 2));
                offset += 2;
                if (count == 0) return result;
                if (payload.Length - offset < count + 2) throw new InvalidDataException("Truncated sound asset block");
                var source = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset, 2));
                offset += 2;
                if (source + count > 0x10000) throw new InvalidDataException("Sound asset block exceeds its source address range");
                result._segments.Add(new Segment(source, payload.Slice(offset, count).ToArray()));
                offset += count;
            }
            throw new InvalidDataException("Sound asset bank has no terminator");
        }

        public ReadOnlySpan<byte> ReadRange(int source, int length)
        {
            var tail = ReadFrom(source);
            if (length > tail.Length) throw new InvalidDataException("Sound asset table crosses a file block boundary");
            return tail[..length];
        }

        public ReadOnlySpan<byte> ReadFrom(int source)
        {
            for (var i = _segments.Count - 1; i >= 0; i--)
            {
                var segment = _segments[i];
                var offset = source - segment.SourceAddress;
                if ((uint)offset < (uint)segment.Bytes.Length) return segment.Bytes.AsSpan(offset);
            }
            throw new InvalidDataException($"Sound asset pointer ${source:X4} is outside stored data blocks");
        }
    }
}
