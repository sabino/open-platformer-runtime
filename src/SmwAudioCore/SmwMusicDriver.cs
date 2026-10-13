using System;
using static OpenPlatformerRuntime.Audio.DspRegisters;

namespace OpenPlatformerRuntime.Audio;

/// <summary>Native game-specific sequencer with typed state, not an SPC700 CPU.</summary>
public sealed partial class SmwMusicDriver
{
    private readonly SnesDsp _dsp;
    public event Action<long, int, byte>? RegisterWritten;
    public long SequencerTicks { get; private set; }
    public byte Song => CurrentCommands[2];
    public byte ClaimedVoiceMask => is_chan_on;
    public byte CurrentCommand(int port) => (uint)port < 4 ? CurrentCommands[port] : (byte)0;

    public SmwMusicDriver(SnesDsp dsp, ReadOnlySpan<byte> data, Action<long, int, byte>? trace = null)
    {
        _dsp = dsp;
        RegisterWritten = trace;
        if (data.Length != ram.Length) throw new ArgumentException("Sound data image must contain 65536 bytes", nameof(data));
        data.CopyTo(ram);
        for (var i = 0; i < channel.Length; i++) channel[i] = new Channel { index = (byte)i, Owner = channel };
        tempo = 0x3600;
        int[] addresses = [MVOLL,MVOLR,EVOLL,EVOLR,FLG,EFB,PMON,NON,EON,DIR,ESA,EDL];
        byte[] values = [127,127,0,0,0x2F,0x60,0,0,0,0x80,0x60,2];
        for (var i = 11; i >= 0; i--) Dsp_Write(this, addresses[i], values[i]);
    }

    public void SetCommand(int port, byte value)
    {
        if ((uint)port >= 4) throw new ArgumentOutOfRangeException(nameof(port));
        IncomingCommands[port] = value;
    }

    public void Tick()
    {
        Spc_Loop_Part2(this, 1);
        SequencerTicks++;
    }

    public void LoadBankData(SmwAudioData data, SmwMusicBank bank)
    {
        Dsp_Write(this, FLG, 0x60); Dsp_Write(this, KOF, 255);
        data.ApplyMusicBank(bank, ram);
        data.ApplyMusicBank(bank, _dsp.SampleAndEchoMemory);
        Array.Clear(CurrentCommands); Array.Clear(IncomingCommands);
        Array.Clear(last_value_from_snes); Array.Clear(new_value_from_snes);
        is_chan_on = smw_tempo_increase = smw_pause_music = echo_channels = 0;
        music_ptr_toplevel = 0;
        foreach (var c in channel) c.pattern_cur_ptr = 0;
        Dsp_Write(this, FLG, 0x20);
    }

    private static void Dsp_Write(SmwMusicDriver p, int register, int value)
    {
        var data = unchecked((byte)value);
        p._dsp.WriteRegister(register, data);
        p.RegisterWritten?.Invoke(p._dsp.Clock, register, data);
    }

    private static int SpcDivHelper(int value, byte divisor)
    {
        var original = value;
        if ((value & 0x100) != 0) value = -value;
        var q = divisor != 0 ? (value & 255) / divisor : 255;
        var r = divisor != 0 ? (value & 255) % divisor : value & 255;
        var result = (q << 8) + (divisor != 0 ? ((r << 8) / divisor & 255) : 255);
        return (original & 0x100) != 0 ? -result : result;
    }

    public string DescribeState() => $"ticks={SequencerTicks} song={Song:X2} tempo={tempo:X4} " +
        $"music_accumulator={main_tempo_accum:X2} sfx_accumulator={sfx_timer_accum:X2} claimed={is_chan_on:X2} " +
        $"paused={smw_pause_music:X2} arrangement={music_ptr_toplevel:X4}";
}
