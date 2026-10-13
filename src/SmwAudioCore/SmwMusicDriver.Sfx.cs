// Native control-flow translation of the MIT SMW driver reference.
using System;
using static OpenPlatformerRuntime.Audio.DspRegisters;

namespace OpenPlatformerRuntime.Audio;

public sealed partial class SmwMusicDriver
{
    private static void Sfx0_Process(SmwMusicDriver p)
    {
        var incoming = p.new_value_from_snes[0];
        if (incoming == 0x12 || incoming == 0x11 || (p.CurrentCommands[0] != 0x11 && p.CurrentCommands[0] != 0x1D))
        {
            if ((incoming & 0x80) != 0)
            {
                p.smw_tempo_increase = 10;
                p.tempo = (ushort)((p.tempo + 2560) & ~255);
                p.new_value_from_snes[3] = 0x1D; p.CurrentCommands[0] = 0x1D;
                p.BeginSfx0(); return;
            }
            if (incoming != 0) { p.CurrentCommands[0] = incoming; p.BeginSfx0(); return; }
        }
        var parse = false;
        if (p.sfx0_countdown != 0)
        {
            if (--p.sfx0_countdown != 0) return;
            p.sfx0_sound_ptr_cur = p.ReadWord(0x5681 + (byte)(p.CurrentCommands[0] * 2)); parse = true;
        }
        else if (p.CurrentCommands[0] == 0) return;
        else parse = --p.sfx0_note_length_left == 0;
        if (parse && !p.ReadSfxNote(0)) return;
        p.did_affect_volumepitch_flag = 0;
        if (p.channel[4].pitch_slide_length != 0) Sfx_WritePitchSweep(p, p.channel[4]);
        else if (p.sfx0_note_length_left == 2) Dsp_Write(p, KOF, 0x10);
    }

    private void BeginSfx0()
    {
        if (smw_pause_music != 0)
        {
            smw_pause_music = 0;
            if (echo_channels == 0) Dsp_Write(this, FLG, 0x20);
            else { echo_volume_left = echo_volume_right = 22 << 8; SetEchoVolume(this); Dsp_Write(this, FLG, 0); }
        }
        sfx0_countdown = 2;
        if (CurrentCommands[0] == 0x11 && echo_channels != 0) SetEchoOff(this);
        Dsp_Write(this, KOF, 0x10); is_chan_on |= 0x10; channel[4].pitch_envelope_num_ticks = 0;
    }

    private static void Sfx3_Process(SmwMusicDriver p)
    {
        var current = p.CurrentCommands[3]; var incoming = p.new_value_from_snes[3];
        if (current != 0x24 && (incoming == 0x24 || (current != 0x1D && current != 5)) && incoming != 0)
        {
            p.CurrentCommands[3] = incoming; p.sfx3_timer = 2;
            Dsp_Write(p, KOF, 0x40); p.is_chan_on |= 0x40; p.channel[6].pitch_envelope_num_ticks = 0; return;
        }
        var parse = false;
        if (p.sfx3_timer != 0)
        {
            if (--p.sfx3_timer != 0) return;
            p.sfx3_sound_ptr_cur = p.ReadWord(0x5619 + (byte)(p.CurrentCommands[3] * 2)); parse = true;
        }
        else if (current == 0) return;
        else parse = --p.sfx3_countdown == 0;
        if (parse && !p.ReadSfxNote(3)) return;
        p.did_affect_volumepitch_flag = 0;
        if (p.channel[6].pitch_slide_length != 0) Sfx_WritePitchSweep(p, p.channel[6]);
        else if (p.sfx3_countdown == 2) Dsp_Write(p, KOF, 0x40);
    }

    private bool ReadSfxNote(int port)
    {
        var n = port == 0 ? 4 : 6; var c = channel[n]; var mask = 1 << n;
        ref var pointer = ref (port == 0 ? ref sfx0_sound_ptr_cur : ref sfx3_sound_ptr_cur);
        ref var duration = ref (port == 0 ? ref sfx0_note_length : ref sfx3_length);
        for (var guard = 0; guard < 1024; guard++)
        {
            int command = ram[pointer++];
            if (command == 0)
            {
                if (port == 0) Sfx0_TurnOffChannel(this); else Sfx3_TurnOffChannel(this);
                return false;
            }
            if ((command & 0x80) == 0)
            {
                duration = (byte)command; command = ram[pointer++];
                if ((command & 0x80) == 0)
                {
                    var volume = command; Dsp_Write(this, n * 16, volume);
                    command = ram[pointer++];
                    if ((command & 0x80) != 0) Dsp_Write(this, n * 16 + 1, volume);
                    else { Dsp_Write(this, n * 16 + 1, command); command = ram[pointer++]; }
                }
            }
            if (command == 0xDA)
            {
                if (port == 3) { chan_bit_flags = 0; Dsp_Write(this, NON, 0); }
                do
                {
                    command = ram[pointer++];
                    if (port == 3 && (command & 0x80) != 0)
                    {
                        sfx3_unused = 0x1F; Dsp_Write(this, FLG, 0x1F); chan_bit_flags = 0x40; Dsp_Write(this, NON, 0x40);
                    }
                } while (port == 3 && (command & 0x80) != 0);
                var instrument = 0x5570 + command * 9;
                if (command >= 19) throw new NotSupportedException($"SFX instrument {command:X2}");
                for (var i = 0; i < 8; i++) Dsp_Write(this, n * 16 + i, ram[instrument + i]);
                c.instrument_pitch_base = ram[instrument + 8]; continue;
            }
            if (command == 0xFF)
            {
                if (port == 0) pointer = ReadWord(0x5681 + (byte)(CurrentCommands[0] * 2));
                else pointer -= 2;
                continue;
            }
            if (command == 0xDD)
            {
                PlayNote(this, c, ram[pointer++]); Write_KeyOn(this, mask);
            }
            if (command == 0xDD || command == 0xEB)
            {
                c.pitch_slide_delay_left = ram[pointer++]; c.pitch_slide_length = ram[pointer++];
                ComputePitchAdd(c, ram[pointer++]);
            }
            else { PlayNote(this, c, command); Write_KeyOn(this, mask); }
            if (port == 0) sfx0_note_length_left = duration; else sfx3_countdown = duration;
            return true;
        }
        throw new NotSupportedException("SFX control flow does not reach a timed note");
    }

    private static void Sfx1_Process(SmwMusicDriver p)
    {
        var incoming = p.new_value_from_snes[1];
        if (incoming == 255) throw new NotSupportedException("SMW jump-channel command FF");
        if (incoming == 2)
        {
            if ((p.CurrentCommands[2] == 6 || (p.CurrentCommands[2] & ~3) == 0) && p.smw_player_on_yoshi == 0)
            { Port1_WriteInstrument(p, 9); p.smw_player_on_yoshi = 1; }
        }
        else if (incoming == 3) p.smw_player_on_yoshi = 0;
        else if (incoming == 1 || (p.CurrentCommands[1] != 1 && incoming == 4))
        {
            p.CurrentCommands[1] = incoming; p.sfx1_countdown = 4;
            Dsp_Write(p, KOF, 0x80); p.is_chan_on |= 0x80;
            foreach (var c in p.channel) c.pitch_envelope_num_ticks = c.pitch_envelope_delay = 0;
            return;
        }
        var voice = p.channel[7];
        var first = false;
        if (p.sfx1_countdown != 0)
        {
            if (--p.sfx1_countdown != 0) return;
            first = true;
        }
        if (p.CurrentCommands[1] == 1)
        {
            if (first)
            {
                p.chan7_countdown_2 = 0x30; Chan7_WriteInstrumentData(p, 8); PlayNote(p, voice, 0xB2);
                voice.pitch_slide_delay_left = 0; voice.pitch_slide_length = 5; ComputePitchAdd(voice, 0xB5);
                Dsp_Write(p, V7VOLL, 0x38); Dsp_Write(p, V7VOLR, 0x38); Write_KeyOn(p, 0x80);
            }
            else if (--p.chan7_countdown_2 == 0) { p.EndJump(); return; }
            else if (p.chan7_countdown_2 == 0x2A)
            { voice.pitch_slide_delay_left = 0; voice.pitch_slide_length = 0x12; ComputePitchAdd(voice, 0xB9); }
            if (p.chan7_countdown_2 == 2) Dsp_Write(p, KOF, 0x80);
            p.did_affect_volumepitch_flag = 0;
            if (voice.pitch_slide_length != 0) Sfx_WritePitchSweep(p, voice);
        }
        else if (p.CurrentCommands[1] == 4)
        {
            if (first) p.chan7_countdown_2 = 0x18;
            else if (--p.chan7_countdown_2 == 0) { p.EndJump(); return; }
            if (first || p.chan7_countdown_2 == 0x0C)
            {
                Chan7_WriteInstrumentData(p, 7); PlayNote(p, voice, 0xA4);
                Dsp_Write(p, V7VOLL, 0x28); Dsp_Write(p, V7VOLR, 0x28); Write_KeyOn(p, 0x80);
            }
            if (p.chan7_countdown_2 == 2) Dsp_Write(p, KOF, 0x80);
        }
    }

    private void EndJump()
    {
        CurrentCommands[1] = 0; is_chan_on &= 0x7F;
        if (channel[7].instrument_id != 0) Channel_SetInstrument(this, channel[7], channel[7].instrument_id);
    }
}
