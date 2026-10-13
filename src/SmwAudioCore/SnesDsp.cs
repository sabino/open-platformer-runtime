// Signal-processing translation of ares/sfc/dsp (ISC), revision
// 9408cb43d4948fc3ea6e152a307a34348df3fe04. See THIRD-PARTY-NOTICES.md.
// This component contains no SPC700 instruction interpreter or console CPU.
using System;
using System.Text;

namespace OpenPlatformerRuntime.Audio;

/// <summary>Integer S-DSP pipeline. One clock is 1/1,024,000s; one stereo frame is 32 clocks.</summary>
public sealed partial class SnesDsp
{
    public const int SampleRate = 32000;
    public const int ClocksPerSample = 32;
    public const int VoiceCount = 8;
    private readonly byte[] _memory = new byte[65536];
    private readonly byte[] _registers = new byte[128];
    private readonly Voice[] _voices = new Voice[8];
    private readonly int[] _main = new int[2];
    private readonly int[] _echoIn = new int[2];
    private readonly int[] _echoOut = new int[2];
    private readonly short[] _history = new short[16];
    private int _phase, _counter, _noise = 0x4000, _historyOffset;
    private int _echoPage, _echoAddress, _echoOffset, _echoLength;
    private bool _echoReadOnly, _alternate = true;
    private int _directory, _source, _directoryAddress, _nextAddress, _brrHeader, _brrByte;
    private int _adsr, _envx, _outx, _pitch, _output, _keyOn, _keyOff, _pendingKeyOn, _end;
    private short _left, _right;

    private sealed class Voice
    {
        public readonly short[] Buffer = new short[12];
        public int BufferOffset, Fraction, Address, BrrOffset = 1, KeyDelay;
        public int EnvelopeMode, Envelope, HiddenEnvelope, Envx;
        public bool Modulate, Noise, Echo, End, Looped;
    }

    public SnesDsp()
    {
        for (var i = 0; i < _voices.Length; i++) _voices[i] = new Voice();
        _registers[0x6C] = 0xE0;
    }

    public long Clock { get; private set; }
    public long GeneratedFrames { get; private set; }
    public int Phase => _phase;
    public Span<byte> SampleAndEchoMemory => _memory;
    public ReadOnlySpan<byte> Registers => _registers;
    public short LastLeft => _left;
    public short LastRight => _right;

    public void Initialize(ReadOnlySpan<byte> memory, ReadOnlySpan<byte> registers)
    {
        if (memory.Length != _memory.Length || registers.Length != _registers.Length)
            throw new ArgumentException("DSP initial state requires 65536 data bytes and 128 register bytes");
        memory.CopyTo(_memory);
        registers.CopyTo(_registers);
        Array.Clear(_main); Array.Clear(_echoIn); Array.Clear(_echoOut); Array.Clear(_history);
        ClearMonitorTaps();
        foreach (var v in _voices)
        {
            Array.Clear(v.Buffer);
            v.BufferOffset = v.Fraction = v.Address = v.KeyDelay = 0;
            v.BrrOffset = 1;
            v.EnvelopeMode = v.Envelope = v.HiddenEnvelope = v.Envx = 0;
            v.Modulate = v.Noise = v.Echo = v.End = v.Looped = false;
        }
        _phase = _counter = _historyOffset = _echoAddress = _echoOffset = _echoLength = 0;
        _echoPage = registers[0x6D];
        _echoReadOnly = true;
        _alternate = true; _noise = 0x4000;
        _directory = registers[0x5D];
        _source = _directoryAddress = _nextAddress = _brrHeader = _brrByte = 0;
        _adsr = _envx = _outx = _pitch = _output = _keyOn = _keyOff = _end = 0;
        _pendingKeyOn = registers[0x4C];
        Clock = GeneratedFrames = 0; _left = _right = 0;
    }

    public byte ReadRegister(int address)
    {
        if ((uint)address >= 128) throw new ArgumentOutOfRangeException(nameof(address));
        return _registers[address];
    }

    public void WriteRegister(int address, byte value)
    {
        if ((uint)address >= 128) throw new ArgumentOutOfRangeException(nameof(address));
        _registers[address] = value;
        if (address == 0x4C) _pendingKeyOn = value;
        else if (address == 0x7C)
        {
            _end = _registers[0x7C] = 0;
            foreach (var voice in _voices) voice.End = false;
        }
        else if ((address & 15) == 8) _envx = value;
        else if ((address & 15) == 9) _outx = value;
    }

    /// <summary>Run one pipeline clock; true when a raw signed stereo frame is ready.</summary>
    public bool StepClock()
    {
        var output = _phase == 27;
        RunPhase();
        _phase = (_phase + 1) & 31;
        Clock++;
        if (output) GeneratedFrames++;
        return output;
    }

    public void Generate(Span<short> interleaved)
    {
        if ((interleaved.Length & 1) != 0) throw new ArgumentException("Stereo PCM must contain pairs", nameof(interleaved));
        var offset = 0;
        while (offset < interleaved.Length)
        {
            if (!StepClock()) continue;
            interleaved[offset++] = _left;
            interleaved[offset++] = _right;
        }
    }

    private int ReadWord(int address) => _memory[address & 65535] | _memory[(address + 1) & 65535] << 8;
    private static int Clamp16(int value) => Math.Clamp(value, short.MinValue, short.MaxValue);
    private static int Signed16(int value) => unchecked((short)value);
    private int Reg(int voice, int field) => _registers[voice * 16 + field];
    private bool PollCounter(int rate) => rate != 0 && (_counter + CounterOffsets[rate]) % CounterRates[rate] == 0;

    private void Voice1(int n)
    {
        _directoryAddress = (_directory << 8) + (_source << 2);
        _source = Reg(n, 4);
    }

    private void Voice2(int n)
    {
        _nextAddress = ReadWord(_directoryAddress + (_voices[n].KeyDelay == 0 ? 2 : 0));
        _adsr = Reg(n, 5);
        _pitch = Reg(n, 2);
    }

    private void Voice3(int n) { Voice3a(n); Voice3b(n); Voice3c(n); }
    private void Voice3a(int n) => _pitch |= (Reg(n, 3) & 63) << 8;
    private void Voice3b(int n)
    {
        var v = _voices[n];
        _brrByte = _memory[(v.Address + v.BrrOffset) & 65535];
        _brrHeader = _memory[v.Address & 65535];
    }

    private void Voice3c(int n)
    {
        var v = _voices[n];
        if (v.Modulate) _pitch = (_pitch + ((_output >> 5) * _pitch >> 10)) & 0x7FFF;
        if (v.KeyDelay != 0)
        {
            if (v.KeyDelay == 5)
            {
                v.Address = _nextAddress; v.BrrOffset = 1; v.BufferOffset = 0; _brrHeader = 0;
            }
            v.Envelope = v.HiddenEnvelope = v.Fraction = 0;
            v.KeyDelay--;
            if ((v.KeyDelay & 3) != 0) v.Fraction = 0x4000;
            _pitch = 0;
        }
        var sample = v.Noise ? Signed16(_noise << 1) : Interpolate(v);
        _output = (sample * v.Envelope >> 11) & ~1;
        v.Envx = v.Envelope >> 4;
        if ((_registers[0x6C] & 0x80) != 0 || (_brrHeader & 3) == 1)
        {
            v.EnvelopeMode = 0; v.Envelope = 0;
        }
        if (_alternate)
        {
            if ((_keyOff & (1 << n)) != 0) v.EnvelopeMode = 0;
            if ((_keyOn & (1 << n)) != 0) { v.KeyDelay = 5; v.EnvelopeMode = 1; }
        }
        if (v.KeyDelay == 0) RunEnvelope(n, v);
    }

    private void Voice4(int n)
    {
        var v = _voices[n]; v.Looped = false;
        if (v.Fraction >= 0x4000)
        {
            DecodeFour(v); v.BrrOffset += 2;
            if (v.BrrOffset >= 9)
            {
                v.Address = (v.Address + 9) & 65535;
                if ((_brrHeader & 1) != 0) { v.Address = _nextAddress; v.Looped = true; }
                v.BrrOffset = 1;
            }
        }
        v.Fraction = Math.Min((v.Fraction & 0x3FFF) + _pitch, 0x7FFF);
        MixVoice(n, 0);
    }

    private void Voice5(int n)
    {
        MixVoice(n, 1);
        var v = _voices[n]; v.End |= v.Looped;
        if (v.KeyDelay == 5) v.End = false;
    }
    private void Voice6(int n) => _outx = (_output >> 8) & 255;
    private void Voice7(int n)
    {
        _end = 0;
        for (var i = 0; i < 8; i++) if (_voices[i].End) _end |= 1 << i;
        _registers[0x7C] = (byte)_end;
        _envx = _voices[n].Envx;
    }
    private void Voice8(int n) => _registers[n * 16 + 9] = (byte)_outx;
    private void Voice9(int n) => _registers[n * 16 + 8] = (byte)_envx;

    private void MixVoice(int n, int channel)
    {
        var value = _output * unchecked((sbyte)Reg(n, channel)) >> 7;
        if (channel == 0) _monitorLeft[n] = value;
        else _monitorRight[n] = value;
        _monitorSource[n] = (byte)Reg(n, 4);
        _main[channel] = Clamp16(_main[channel] + value);
        if (_voices[n].Echo) _echoOut[channel] = Clamp16(_echoOut[channel] + value);
    }

    private int Interpolate(Voice v)
    {
        var fraction = (v.Fraction >> 4) & 255;
        var offset = (v.BufferOffset + (v.Fraction >> 12)) % 12;
        var value = Gaussian[255 - fraction] * v.Buffer[offset] >> 11;
        offset = (offset + 1) % 12;
        value += Gaussian[511 - fraction] * v.Buffer[offset] >> 11;
        offset = (offset + 1) % 12;
        value += Gaussian[256 + fraction] * v.Buffer[offset] >> 11;
        offset = (offset + 1) % 12;
        value = Signed16(value);
        value += Gaussian[fraction] * v.Buffer[offset] >> 11;
        return Clamp16(value) & ~1;
    }

    private void DecodeFour(Voice v)
    {
        var packed = (_brrByte << 8) | _memory[(v.Address + v.BrrOffset + 1) & 65535];
        var shift = _brrHeader >> 4;
        var filter = (_brrHeader >> 2) & 3;
        for (var i = 0; i < 4; i++)
        {
            var sample = Signed16(packed) >> 12; packed <<= 4;
            sample = shift <= 12 ? (sample << shift) >> 1 : sample & ~0x7FF;
            var previous = v.Buffer[(v.BufferOffset + 11) % 12];
            var older = v.Buffer[(v.BufferOffset + 10) % 12] >> 1;
            sample += filter switch
            {
                1 => (previous >> 1) + ((-previous) >> 5),
                2 => previous - older + (older >> 4) + ((previous * -3) >> 6),
                3 => previous - older + ((previous * -13) >> 7) + ((older * 3) >> 4),
                _ => 0,
            };
            v.Buffer[v.BufferOffset] = unchecked((short)(Clamp16(sample) << 1));
            v.BufferOffset = (v.BufferOffset + 1) % 12;
        }
    }

    private void RunEnvelope(int n, Voice v)
    {
        var value = v.Envelope;
        if (v.EnvelopeMode == 0) { v.Envelope = Math.Max(0, value - 8); return; }
        int rate;
        var data = Reg(n, 6);
        if ((_adsr & 0x80) != 0)
        {
            if (v.EnvelopeMode >= 2)
            {
                value--; value -= value >> 8;
                rate = v.EnvelopeMode == 2 ? ((_adsr >> 4) & 7) * 2 + 16 : data & 31;
            }
            else { rate = (_adsr & 15) * 2 + 1; value += rate < 31 ? 32 : 1024; }
        }
        else
        {
            data = Reg(n, 7);
            var mode = data >> 5;
            rate = mode < 4 ? 31 : data & 31;
            if (mode < 4) value = data << 4;
            else if (mode == 4) value -= 32;
            else if (mode == 5) { value--; value -= value >> 8; }
            else value += mode == 7 && (uint)v.HiddenEnvelope >= 0x600 ? 8 : 32;
        }
        if ((value >> 8) == (data >> 5) && v.EnvelopeMode == 2) v.EnvelopeMode = 3;
        v.HiddenEnvelope = value;
        if ((uint)value > 0x7FF)
        {
            value = Math.Clamp(value, 0, 0x7FF);
            if (v.EnvelopeMode == 1) v.EnvelopeMode = 2;
        }
        if (PollCounter(rate)) v.Envelope = value;
    }

    private void Misc27()
    {
        for (var n = 0; n < 8; n++) _voices[n].Modulate = n != 0 && (_registers[0x2D] & (1 << n)) != 0;
    }
    private void Misc28()
    {
        for (var n = 0; n < 8; n++)
        {
            _voices[n].Noise = (_registers[0x3D] & (1 << n)) != 0;
            _voices[n].Echo = (_registers[0x4D] & (1 << n)) != 0;
        }
        _directory = _registers[0x5D];
    }
    private void Misc29()
    {
        _alternate = !_alternate;
        if (_alternate) _pendingKeyOn &= ~_keyOn;
    }
    private void Misc30()
    {
        if (_alternate) { _keyOn = _pendingKeyOn; _keyOff = _registers[0x5C]; }
        _counter = _counter == 0 ? 30719 : _counter - 1;
        if (PollCounter(_registers[0x6C] & 31))
            _noise = (((_noise << 13) ^ (_noise << 14)) & 0x4000) | (_noise >> 1);
    }

    private int Fir(int channel, int tap) => _history[channel * 8 + ((_historyOffset + tap + 1) & 7)] *
        unchecked((sbyte)_registers[tap * 16 + 15]) >> 6;
    private void ReadEcho(int channel) => _history[channel * 8 + _historyOffset] = (short)(Signed16(ReadWord(_echoAddress + channel * 2)) >> 1);
    private void WriteEcho(int channel)
    {
        if (!_echoReadOnly)
        {
            var address = (_echoAddress + channel * 2) & 65535;
            _memory[address] = (byte)_echoOut[channel];
            _memory[(address + 1) & 65535] = (byte)(_echoOut[channel] >> 8);
        }
        _echoOut[channel] = 0;
    }
    private int EchoOutput(int channel) => Clamp16(Signed16(_main[channel] * unchecked((sbyte)_registers[channel * 16 + 12]) >> 7) +
        Signed16(_echoIn[channel] * unchecked((sbyte)_registers[(channel + 2) * 16 + 12]) >> 7));
    private void Echo22()
    {
        _historyOffset = (_historyOffset + 1) & 7;
        _echoAddress = ((_echoPage << 8) + _echoOffset) & 65535;
        ReadEcho(0);
        _echoIn[0] = Fir(0, 0); _echoIn[1] = Fir(1, 0);
    }
    private void Echo23()
    {
        for (var c = 0; c < 2; c++) _echoIn[c] += Fir(c, 1) + Fir(c, 2);
        ReadEcho(1);
    }
    private void Echo24()
    {
        for (var c = 0; c < 2; c++) _echoIn[c] += Fir(c, 3) + Fir(c, 4) + Fir(c, 5);
    }
    private void Echo25()
    {
        for (var c = 0; c < 2; c++) _echoIn[c] = Clamp16(Signed16(_echoIn[c] + Fir(c, 6)) + Signed16(Fir(c, 7))) & ~1;
    }
    private void Echo26()
    {
        _monitorEchoLeft = Signed16(_echoIn[0] * unchecked((sbyte)_registers[0x2C]) >> 7);
        _main[0] = EchoOutput(0);
        for (var c = 0; c < 2; c++) _echoOut[c] = Clamp16(_echoOut[c] + Signed16(_echoIn[c] * unchecked((sbyte)_registers[13]) >> 7)) & ~1;
    }
    private void Echo27()
    {
        _monitorEchoRight = Signed16(_echoIn[1] * unchecked((sbyte)_registers[0x3C]) >> 7);
        _left = (short)_main[0]; _right = (short)EchoOutput(1);
        _main[0] = _main[1] = 0;
        if ((_registers[0x6C] & 64) != 0) _left = _right = 0;
    }
    private void Echo28() => _echoReadOnly = (_registers[0x6C] & 32) != 0;
    private void Echo29()
    {
        _echoPage = _registers[0x6D];
        if (_echoOffset == 0) _echoLength = (_registers[0x7D] & 15) << 11;
        _echoOffset += 4;
        if (_echoOffset >= _echoLength) _echoOffset = 0;
        WriteEcho(0);
        _echoReadOnly = (_registers[0x6C] & 32) != 0;
    }
    private void Echo30() => WriteEcho(1);

    public string DescribeState()
    {
        var result = new StringBuilder($"clock={Clock} frame={GeneratedFrames} phase={_phase} counter={_counter} noise={_noise:X4} " +
            $"echo={_echoAddress:X4}/{_echoOffset:X4}/{_echoLength:X4} pitch={_pitch:X4} output={_output}");
        for (var n = 0; n < 8; n++)
        {
            var v = _voices[n];
            result.Append($" voice{n}_env={v.Envelope:X3}/{v.EnvelopeMode} voice{n}_brr={v.Address:X4}:{v.BrrOffset} " +
                $"voice{n}_fraction={v.Fraction:X4} voice{n}_keydelay={v.KeyDelay}");
        }
        return result.ToString();
    }
}
