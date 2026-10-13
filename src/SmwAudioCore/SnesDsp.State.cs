using System;
using System.IO;

namespace OpenPlatformerRuntime.Audio;

public sealed partial class SnesDsp
{
    // Native DSP checkpoint. Contains no CPU registers or SPC snapshot format.
    public byte[] CaptureState()
    {
        using var buffer = new MemoryStream(); using var writer = new BinaryWriter(buffer);
        writer.Write(0x50534453); writer.Write(1); writer.Write(Clock); writer.Write(GeneratedFrames);
        writer.Write(_memory); writer.Write(_registers);
        writer.Write(_phase);
        writer.Write(_counter);
        writer.Write(_noise);
        writer.Write(_historyOffset);
        writer.Write(_echoPage);
        writer.Write(_echoAddress);
        writer.Write(_echoOffset);
        writer.Write(_echoLength);
        writer.Write(_directory);
        writer.Write(_source);
        writer.Write(_directoryAddress);
        writer.Write(_nextAddress);
        writer.Write(_brrHeader);
        writer.Write(_brrByte);
        writer.Write(_adsr);
        writer.Write(_envx);
        writer.Write(_outx);
        writer.Write(_pitch);
        writer.Write(_output);
        writer.Write(_keyOn);
        writer.Write(_keyOff);
        writer.Write(_pendingKeyOn);
        writer.Write(_end);
        writer.Write(_echoReadOnly);
        writer.Write(_alternate);
        writer.Write(_left);
        writer.Write(_right);
        foreach (var value in _main) writer.Write(value);
        foreach (var value in _echoIn) writer.Write(value);
        foreach (var value in _echoOut) writer.Write(value);
        foreach (var value in _history) writer.Write(value);
        foreach (var voice in _voices)
        {
            writer.Write(voice.BufferOffset);
            writer.Write(voice.Fraction);
            writer.Write(voice.Address);
            writer.Write(voice.BrrOffset);
            writer.Write(voice.KeyDelay);
            writer.Write(voice.EnvelopeMode);
            writer.Write(voice.Envelope);
            writer.Write(voice.HiddenEnvelope);
            writer.Write(voice.Envx);
            writer.Write(voice.Modulate);
            writer.Write(voice.Noise);
            writer.Write(voice.Echo);
            writer.Write(voice.End);
            writer.Write(voice.Looped);
            foreach (var value in voice.Buffer) writer.Write(value);
        }
        return buffer.ToArray();
    }

    public void RestoreState(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 66362) throw new InvalidDataException("Native DSP checkpoint size mismatch");
        using var buffer = new MemoryStream(bytes.ToArray()); using var reader = new BinaryReader(buffer);
        if (reader.ReadInt32() != 0x50534453 || reader.ReadInt32() != 1) throw new InvalidDataException("Native DSP checkpoint schema mismatch");
        Clock = reader.ReadInt64(); GeneratedFrames = reader.ReadInt64();
        reader.ReadBytes(65536).CopyTo(_memory, 0); reader.ReadBytes(128).CopyTo(_registers, 0);
        _phase = reader.ReadInt32();
        _counter = reader.ReadInt32();
        _noise = reader.ReadInt32();
        _historyOffset = reader.ReadInt32();
        _echoPage = reader.ReadInt32();
        _echoAddress = reader.ReadInt32();
        _echoOffset = reader.ReadInt32();
        _echoLength = reader.ReadInt32();
        _directory = reader.ReadInt32();
        _source = reader.ReadInt32();
        _directoryAddress = reader.ReadInt32();
        _nextAddress = reader.ReadInt32();
        _brrHeader = reader.ReadInt32();
        _brrByte = reader.ReadInt32();
        _adsr = reader.ReadInt32();
        _envx = reader.ReadInt32();
        _outx = reader.ReadInt32();
        _pitch = reader.ReadInt32();
        _output = reader.ReadInt32();
        _keyOn = reader.ReadInt32();
        _keyOff = reader.ReadInt32();
        _pendingKeyOn = reader.ReadInt32();
        _end = reader.ReadInt32();
        _echoReadOnly = reader.ReadBoolean();
        _alternate = reader.ReadBoolean();
        _left = reader.ReadInt16(); _right = reader.ReadInt16();
        for (var i = 0; i < _main.Length; i++) _main[i] = reader.ReadInt32();
        for (var i = 0; i < _echoIn.Length; i++) _echoIn[i] = reader.ReadInt32();
        for (var i = 0; i < _echoOut.Length; i++) _echoOut[i] = reader.ReadInt32();
        for (var i = 0; i < _history.Length; i++) _history[i] = reader.ReadInt16();
        foreach (var voice in _voices)
        {
            voice.BufferOffset = reader.ReadInt32();
            voice.Fraction = reader.ReadInt32();
            voice.Address = reader.ReadInt32();
            voice.BrrOffset = reader.ReadInt32();
            voice.KeyDelay = reader.ReadInt32();
            voice.EnvelopeMode = reader.ReadInt32();
            voice.Envelope = reader.ReadInt32();
            voice.HiddenEnvelope = reader.ReadInt32();
            voice.Envx = reader.ReadInt32();
            voice.Modulate = reader.ReadBoolean();
            voice.Noise = reader.ReadBoolean();
            voice.Echo = reader.ReadBoolean();
            voice.End = reader.ReadBoolean();
            voice.Looped = reader.ReadBoolean();
            for (var i = 0; i < voice.Buffer.Length; i++) voice.Buffer[i] = reader.ReadInt16();
        }
        if (buffer.Position != buffer.Length || (uint)_phase >= 32 || (uint)_counter >= 30720)
            throw new InvalidDataException("Invalid native DSP checkpoint state");
    }
}
