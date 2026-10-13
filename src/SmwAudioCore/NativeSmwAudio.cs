using System;
using System.IO;

namespace OpenPlatformerRuntime.Audio;

public readonly record struct SmwAudioCommand(long Clock, int Port, byte Value);

/// <summary>Sample-driven native sequencing and DSP. Host buffering never advances this clock.</summary>
public sealed class NativeSmwAudio
{
    private readonly SmwAudioData _data;
    private readonly SmwAudioCommand[] _queue = new SmwAudioCommand[1024];
    private int _read, _write, _queued;
    private long _lastEventClock = -1;
    public SnesDsp Dsp { get; }
    public SmwMusicDriver Driver { get; }
    public SmwMusicBank Bank { get; private set; }
    public int QueuedEvents => _queued;

    public NativeSmwAudio(SmwAudioData data, SmwMusicBank bank = SmwMusicBank.Level, Action<long, int, byte>? trace = null)
    {
        _data = data; Bank = bank;
        var image = data.CreateDataImage(bank);
        Dsp = new SnesDsp();
        var registers = new byte[128]; registers[0x6C] = 0xE0;
        Dsp.Initialize(image, registers);
        Driver = new SmwMusicDriver(Dsp, image, trace);
    }

    /// <summary>Equal-clock events preserve caller order. Late events are explicitly rejected.</summary>
    public void ScheduleCommand(long clock, int port, byte value)
    {
        if (clock < Dsp.Clock || clock < _lastEventClock) throw new ArgumentException("Audio event is late or out of order", nameof(clock));
        if ((uint)port >= 4) throw new ArgumentOutOfRangeException(nameof(port));
        Enqueue(new SmwAudioCommand(clock, port, value));
    }

    public void ScheduleMusic(long clock, SmwMusicBank bank, byte song)
    {
        if ((uint)bank > 2) throw new ArgumentOutOfRangeException(nameof(bank));
        if (clock < Dsp.Clock || clock < _lastEventClock) throw new ArgumentException("Audio event is late or out of order", nameof(clock));
        if (_queued > _queue.Length - 2) throw new InvalidOperationException("Audio event queue is full");
        Enqueue(new SmwAudioCommand(clock, 4, (byte)bank));
        ScheduleCommand(clock, 2, song);
    }

    public void ScheduleRegisterWrite(long clock, int register, byte value)
    {
        if ((uint)register >= 128) throw new ArgumentOutOfRangeException(nameof(register));
        if (clock < Dsp.Clock || clock < _lastEventClock) throw new ArgumentException("DSP event is late or out of order", nameof(clock));
        Enqueue(new SmwAudioCommand(clock, register + 5, value));
    }

    private void Enqueue(SmwAudioCommand command)
    {
        if (_queued == _queue.Length) throw new InvalidOperationException("Audio event queue is full");
        _queue[_write] = command; _write = (_write + 1) % _queue.Length; _queued++;
        _lastEventClock = command.Clock;
    }

    public void Generate(Span<short> stereo)
    {
        if ((stereo.Length & 1) != 0) throw new ArgumentException("Stereo PCM must contain pairs", nameof(stereo));
        var index = 0;
        while (index < stereo.Length)
        {
            while (_queued != 0 && _queue[_read].Clock == Dsp.Clock)
            {
                var command = _queue[_read]; _read = (_read + 1) % _queue.Length; _queued--;
                if (command.Port == 4)
                {
                    var bank = (SmwMusicBank)command.Value;
                    if (bank != Bank) { Bank = bank; Driver.LoadBankData(_data, Bank); }
                }
                else if (command.Port >= 5) Dsp.WriteRegister(command.Port - 5, command.Value);
                else Driver.SetCommand(command.Port, command.Value);
            }
            if (Dsp.Clock != 0 && Dsp.Clock % 2048 == 0) Driver.Tick();
            if (!Dsp.StepClock()) continue;
            stereo[index++] = Dsp.LastLeft; stereo[index++] = Dsp.LastRight;
        }
    }

    public static long FrameToClock(long frame, int framesPerSecondNumerator = 60, int framesPerSecondDenominator = 1)
    {
        if (frame < 0 || framesPerSecondNumerator <= 0 || framesPerSecondDenominator <= 0) throw new ArgumentOutOfRangeException(nameof(frame));
        return checked(frame * SnesDsp.SampleRate * SnesDsp.ClocksPerSample * framesPerSecondDenominator / framesPerSecondNumerator);
    }

    public byte[] CaptureState()
    {
        using var buffer = new MemoryStream(); using var writer = new BinaryWriter(buffer);
        writer.Write(0x41574D53); writer.Write(1); writer.Write(_data.RomSha1); writer.Write((byte)Bank);
        var dsp = Dsp.CaptureState(); var driver = Driver.CaptureState();
        writer.Write(dsp.Length); writer.Write(dsp); writer.Write(driver.Length); writer.Write(driver);
        writer.Write(_lastEventClock); writer.Write(_queued);
        for (var i = 0; i < _queued; i++)
        {
            var e = _queue[(_read+i)%_queue.Length]; writer.Write(e.Clock); writer.Write(e.Port); writer.Write(e.Value);
        }
        return buffer.ToArray();
    }

    public void RestoreState(ReadOnlySpan<byte> bytes)
    {
        using var buffer = new MemoryStream(bytes.ToArray()); using var reader = new BinaryReader(buffer);
        if (reader.ReadInt32() != 0x41574D53 || reader.ReadInt32() != 1 || reader.ReadString() != _data.RomSha1)
            throw new InvalidDataException("Native audio checkpoint schema or ROM mismatch");
        var bank = (SmwMusicBank)reader.ReadByte();
        if ((uint)bank > 2) throw new InvalidDataException("Native audio checkpoint bank mismatch");
        var length = reader.ReadInt32(); if (length <= 0 || length > 70000) throw new InvalidDataException("Native DSP checkpoint length mismatch");
        var dsp = reader.ReadBytes(length);
        length = reader.ReadInt32(); if (length <= 0 || length > 70000) throw new InvalidDataException("Native sequencer checkpoint length mismatch");
        var driver = reader.ReadBytes(length);
        var last = reader.ReadInt64(); var count = reader.ReadInt32();
        if ((uint)count > (uint)_queue.Length) throw new InvalidDataException("Native audio checkpoint event count mismatch");
        var queue = new SmwAudioCommand[count];
        for (var i=0;i<count;i++) queue[i] = new SmwAudioCommand(reader.ReadInt64(),reader.ReadInt32(),reader.ReadByte());
        if (buffer.Position != buffer.Length) throw new InvalidDataException("Trailing native audio checkpoint bytes");
        Dsp.RestoreState(dsp); Driver.RestoreState(driver); Bank=bank;
        _read = _write = _queued = 0; _lastEventClock = -1;
        foreach(var e in queue) Enqueue(e);
        _lastEventClock = last;
    }
}
