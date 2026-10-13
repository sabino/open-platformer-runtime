// Native SMW sound-driver routine translation from MIT-licensed snesrev/smw
// eae20c65c58930c8b62c76188d259579ad4130f1, src/smw_spc_player.c.
// No SPC700 instructions are executed. See THIRD-PARTY-NOTICES.md.
using System;
using System.IO;
using static OpenPlatformerRuntime.Audio.DspRegisters;

namespace OpenPlatformerRuntime.Audio;

public sealed partial class SmwMusicDriver
{
    private sealed class Channel
    {
        public ushort pattern_cur_ptr;
        public byte index;
        public byte note_ticks_left;
        public byte volume_fade_ticks;
        public byte pan_num_ticks;
        public byte pitch_slide_length;
        public byte pitch_slide_delay_left;
        public byte vibrato_hold_count;
        public byte vib_depth;
        public byte tremolo_hold_count;
        public byte tremolo_depth;
        public byte subroutine_num_loops;
        public byte instrument_id;
        public byte note_keyoff_ticks_left;
        public byte vibrato_change_count;
        public byte note_length;
        public byte note_gate_off_fixedpt;
        public byte instrument_pitch_base;
        public byte channel_volume_master;
        public ushort channel_volume;
        public ushort volume_fade_addpertick;
        public byte volume_fade_target;
        public ushort pan_value;
        public ushort pan_add_per_tick;
        public byte pan_target_value;
        public byte pan_flag_with_phase_invert;
        public ushort pitch;
        public ushort pitch_add_per_tick;
        public byte pitch_target;
        public byte fine_tune;
        public byte pitch_envelope_num_ticks;
        public byte pitch_envelope_delay;
        public byte pitch_envelope_direction;
        public byte pitch_envelope_slide_value;
        public byte vibrato_count;
        public byte vibrato_rate;
        public byte vibrato_delay_ticks;
        public byte vibrato_fade_num_ticks;
        public byte vibrato_fade_add_per_tick;
        public byte vib_depth_orig;
        public byte tremolo_count;
        public byte tremolo_delay_ticks;
        public byte final_volume;
        public ushort saved_pattern_ptr;
        public ushort pattern_start_ptr;
        public Channel[] Owner = null!;
        public static Channel operator ++(Channel c) => c.Owner[Math.Min(8, c.index + 1)];
        public static Channel operator --(Channel c) => c.Owner[Math.Max(0, c.index - 1)];
    }
        public byte[] new_value_from_snes = new byte[4];
        public byte[] last_value_from_snes = new byte[4];
        public byte timer_cycles;
        public byte counter_sf0c;
        public byte sfx3_timer;
        public ushort temp_accum;
        public byte ttt;
        public byte did_affect_volumepitch_flag;
        public ushort addr0;
        public ushort addr1;
        public ushort sfx0_sound_ptr_cur;
        public ushort sfx3_sound_ptr_cur;
        public byte chan7_countdown_2;
        public byte is_chan_on;
        public byte sfx3_unused;
        public byte chan_bit_flags;
        public ushort music_ptr_toplevel;
        public byte block_count;
        public byte global_transposition;
        public byte sfx_timer_accum;
        public byte sfx_tick_counter;
        public byte chn;
        public byte key_ON;
        public byte cur_chan_bit;
        public byte main_tempo_accum;
        public ushort tempo;
        public byte tempo_fade_num_ticks;
        public byte tempo_fade_final;
        public ushort tempo_fade_add;
        public ushort master_volume;
        public byte master_volume_fade_ticks;
        public byte master_volume_fade_target;
        public ushort master_volume_fade_add_per_tick;
        public byte vol_dirty;
        public byte echo_volume_fade_ticks;
        public ushort echo_volume_left;
        public ushort echo_volume_right;
        public ushort echo_volume_fade_add_left;
        public ushort echo_volume_fade_add_right;
        public byte echo_volume_fade_target_left;
        public byte echo_volume_fade_target_right;
        public byte byte_2FF;
        public byte sfx0_note_length_left;
        public byte sfx0_note_length;
        public byte sfx0_countdown;
        public byte sfx1_countdown;
        public byte sfx3_countdown;
        public byte sfx3_length;
        public byte smw_player_on_yoshi;
        public byte smw_tempo_increase;
        public byte smw_pause_music;
        public byte echo_channels;
        public ushort pattern_start_ptr;
    private readonly Channel[] channel = new Channel[9];
    private readonly byte[] ram = new byte[65536];
    private readonly byte[] IncomingCommands = new byte[4];
    private readonly byte[] CurrentCommands = new byte[4];
    private ushort ReadWord(int offset) => (ushort)(ram[offset & 65535] | ram[(offset + 1) & 65535] << 8);
    private static void Not_Implemented() => throw new NotSupportedException("Unimplemented original SMW sound-driver command");
    private static readonly ushort[] kBaseNoteFreqs = [4286, 4541, 4811, 5097, 5400, 5721, 6061, 6422, 6804, 7208, 7637, 8091];
    private static readonly byte[] kNoteVol = [8, 18, 27, 36, 44, 53, 62, 71, 81, 90, 98,107,125,143,161,179];
    private static readonly byte[] kNoteGateOffPct = [51,102,128,153,179,204,230,255];
    private static readonly byte[] kVolumeTable = [0, 1, 3, 7, 13, 21, 30, 41, 52, 66, 81, 94, 103, 110, 115, 119, 122, 124, 125, 126, 127, 127];
    private static readonly byte[] kEffectByteLength = [2,  2,  3,  4,  4,  1,  2,  3,  2,  3,  2,  4,  1,  2,  3,  4,
  2,  4,  4,  1,  2,  4,  1,  4,  4];
    private static void Spc_Loop_Part2(SmwMusicDriver p, int ticks) {
  ticks &= 255;
  int t = p.sfx_timer_accum + (byte)(ticks * 0x38);
  p.sfx_timer_accum = unchecked((byte)(t));
  if (t >= 256) {
    p.sfx_tick_counter++;

    Sfx0_Process(p);
    ReadPortFromSnes(p, 0);

    Sfx1_Process(p);
    ReadPortFromSnes(p, 1);

    Sfx3_Process(p);
    ReadPortFromSnes(p, 3);
  }
  t = p.main_tempo_accum + (byte)(ticks * (p.tempo >> 8));
  p.main_tempo_accum = unchecked((byte)(t));
  if (t >= 256) {
    if (!((p.smw_pause_music) != 0))
      Music_Process(p);
    ReadPortFromSnes(p, 2);
  } else if ((p.CurrentCommands[2]) != 0) {
    Channel c = p.channel[7];
    for (p.cur_chan_bit = 0x80; p.cur_chan_bit != 0; p.cur_chan_bit >>= 1, c--) {
      if ((c.pattern_cur_ptr >> 8) != 0)
        HandlePanAndSweep(p, c);
    }
  }
}

    private static void ReadPortFromSnes(SmwMusicDriver p, int port) {
  byte old = unchecked((byte)(p.last_value_from_snes[port]));
  p.last_value_from_snes[port] = p.IncomingCommands[port];

  if (p.IncomingCommands[port] != old) {
    p.new_value_from_snes[port] = p.IncomingCommands[port];
  } else
    p.new_value_from_snes[port] = 0;
}

    private static void PlayNote(SmwMusicDriver p, Channel c, int note) {
  note &= 255;
  if (note >= 0xd0) {
    c.instrument_id = unchecked((byte)(note));
    ushort addr = unchecked((ushort)((note - 0xd0) * 6 + 0x5fa5));
    if (Channel_SetInstrumentEx(p, c, addr))
      return;
    note = p.ram[addr + 5];
  } else if (note >= 0xc6) {
    return;
  }
  c.pitch = unchecked((ushort)(((note & 0x7f) + p.global_transposition) << 8 | c.fine_tune));
  c.vibrato_count = unchecked((byte)(0));
  c.tremolo_count = unchecked((byte)(0));
  c.vibrato_hold_count = unchecked((byte)(0));
  c.vibrato_change_count = unchecked((byte)(0));
  c.tremolo_hold_count = unchecked((byte)(0));
  p.vol_dirty = unchecked((byte)(p.vol_dirty | (p.cur_chan_bit)));
  p.key_ON = unchecked((byte)(p.key_ON | (p.cur_chan_bit)));
  c.pitch_slide_length = unchecked((byte)(c.pitch_envelope_num_ticks));
  if ((c.pitch_slide_length) != 0) {
    c.pitch_slide_delay_left = unchecked((byte)(c.pitch_envelope_delay));
    if (!((c.pitch_envelope_direction) != 0))
      c.pitch = unchecked((ushort)(c.pitch - (c.pitch_envelope_slide_value << 8)));
    ComputePitchAdd(c, (c.pitch >> 8) + c.pitch_envelope_slide_value);
  }
  WritePitch(p, c, c.pitch);
}

    private static ushort ComputePeriod(int v) {
  v &= 255;

  byte pp = unchecked((byte)(v & 0x7f));
  byte q = (byte)(pp / 12); byte r = (byte)(pp % 12);
  ushort t = unchecked((ushort)(kBaseNoteFreqs[r]));
  while (q != 6)
    { t >>= 1; q++; }
  return t;
}

    private static void WritePitch(SmwMusicDriver p, Channel c, int pitch) {
  pitch &= 65535;
  if ((pitch >> 8) >= 0x34) {
    pitch = pitch + ((pitch >> 8) - 0x34);
  } else if ((pitch >> 8) < 0x13) {
    pitch = pitch + ((byte)(((pitch >> 8) - 0x13) * 2) - 256);
  }

  ushort t = unchecked((ushort)(ComputePeriod(pitch >> 8)));
  ushort t1 = unchecked((ushort)(ComputePeriod((pitch >> 8) + 1)));

  ushort td = unchecked((ushort)(t1 - t));
  t = unchecked((ushort)(t + ((td >> 8) * (byte)pitch)));
  t = unchecked((ushort)(t + (((byte)td * (byte)pitch) >> 8)));
  t = unchecked((ushort)(t * (c.instrument_pitch_base)));
  if (!(((p.cur_chan_bit & p.is_chan_on) != 0))) {
    byte reg = unchecked((byte)(c.index * 16));
    Dsp_Write(p, reg + V0PITCHL, t & 0xff);
    Dsp_Write(p, reg + V0PITCHH, t >> 8);
  }
}

    private static void Sfx0_TurnOffChannel(SmwMusicDriver p) {
  if (p.CurrentCommands[0] == 0x11) {
    p.smw_pause_music = unchecked((byte)(0x60));
    Dsp_Write(p, FLG, 0x60);
  }
  p.CurrentCommands[0] = 0;
  p.is_chan_on = unchecked((byte)(p.is_chan_on & (~0x10)));
  if ((p.channel[4].instrument_id) != 0)
    Channel_SetInstrument(p, p.channel[4], p.channel[4].instrument_id);
}

    private static void SetEchoVolume(SmwMusicDriver p) {
  Dsp_Write(p, EVOLL, p.echo_volume_left >> 8);
  Dsp_Write(p, EVOLR, p.echo_volume_right >> 8);
}

    private static void SetEchoOff(SmwMusicDriver p) {
  p.echo_volume_left = unchecked((ushort)(p.echo_volume_right = 0));
  SetEchoVolume(p);
  p.sfx3_unused = unchecked((byte)(0));
  Dsp_Write(p, FLG, 0x20);
}



    private static void Sfx3_TurnOffChannel(SmwMusicDriver p) {
  p.CurrentCommands[3] = 0;
  p.is_chan_on = unchecked((byte)(p.is_chan_on & (~0x40)));
  p.chan_bit_flags = unchecked((byte)(0));
  Dsp_Write(p, NON, 0);
  if ((p.channel[6].instrument_id) != 0)
    Channel_SetInstrument(p, p.channel[6], p.channel[6].instrument_id);
}



    private static void Port1_WriteInstrument(SmwMusicDriver p, int cmd) {
  cmd &= 255;
  var instrumentOffset = 0x5570 + cmd * 9;
  for (int i = 0; i != 8; i++)
    Dsp_Write(p, V5VOLL + i, p.ram[instrumentOffset + i]);
  p.channel[5].instrument_pitch_base = p.ram[instrumentOffset + 8];
}

    private static void Chan_DoAnyFade(ref ushort value, int add, int target, int cont) {
    value = (ushort)(cont == 0 ? target << 8 : value + add);
}

    private static void Sfx_WritePitchSweep(SmwMusicDriver p, Channel c) {
  Chan_DoAnyFade(ref c.pitch, c.pitch_add_per_tick, c.pitch_target, --c.pitch_slide_length);
  p.cur_chan_bit = 0;  // force change through
  WritePitch(p, c, c.pitch);
}

    private static void Chan7_WriteInstrumentData(SmwMusicDriver p, int cmd) {
  cmd &= 255;
  var instrumentOffset = 0x5570 + cmd * 9;
  for (int i = 0; i != 8; i++)
    Dsp_Write(p, V7VOLL + i, p.ram[instrumentOffset + i]);
  p.channel[7].instrument_pitch_base = p.ram[instrumentOffset + 8];
}



    private static void Music_Process(SmwMusicDriver p) {
  byte cmd;
  int t;

  if (p.CurrentCommands[2] != 0) {
    if ((p.CurrentCommands[2] == 6 || !(((p.CurrentCommands[2] & ~3) != 0))) && !((p.smw_player_on_yoshi) != 0)) {
      Dsp_Write(p, KOF, 0x20);
      p.is_chan_on = unchecked((byte)(p.is_chan_on | (0x20)));
    } else {
      p.is_chan_on = unchecked((byte)(p.is_chan_on & (~0x20)));
    }
  }
  cmd = unchecked((byte)(p.new_value_from_snes[2]));
  if ((sbyte)cmd <= 0) {
    if ((sbyte)cmd < 0) {
      p.master_volume_fade_ticks = unchecked((byte)(0xf0));
      p.master_volume_fade_target = unchecked((byte)(0));
      p.master_volume_fade_add_per_tick = unchecked((ushort)(SpcDivHelper(-(p.master_volume >> 8), 0xf0)));
    }
    if ((p.counter_sf0c) != 0)
      goto music_keep_running;
    if ((p.CurrentCommands[2]) != 0)
      goto label_a;
    return;
  }

  if (cmd == 0x16 || cmd == 0x10 || cmd == 0xf || cmd >= 9 && cmd < 13)
    p.smw_tempo_increase = unchecked((byte)(0));

  p.CurrentCommands[2] = cmd;
  p.counter_sf0c = unchecked((byte)(2));
  p.music_ptr_toplevel = unchecked((ushort)(p.ReadWord(0x1360 + (cmd - 1) * 2)));

  Channel c = p.channel[7];
  for (int i = 7; i >= 0; i--, c--) {
    c.pan_value = unchecked((ushort)((ushort)((c.pan_value & 255) | ((10) << 8))));
    c.channel_volume = unchecked((ushort)((ushort)((c.channel_volume & 255) | ((0xff) << 8))));
    c.fine_tune = unchecked((byte)(0));
    c.pan_num_ticks = unchecked((byte)(0));
    c.volume_fade_ticks = unchecked((byte)(0));
    c.vib_depth = unchecked((byte)(0));
    c.tremolo_depth = unchecked((byte)(0));
    c.subroutine_num_loops = unchecked((byte)(0));
    c.instrument_id = unchecked((byte)(0));
  }
  p.master_volume_fade_ticks = unchecked((byte)(0));
  p.echo_volume_fade_ticks = unchecked((byte)(0));
  p.tempo_fade_num_ticks = unchecked((byte)(0));
  p.global_transposition = unchecked((byte)(0));
  p.master_volume = unchecked((ushort)((ushort)((p.master_volume & 255) | ((0xc0) << 8))));
  p.tempo = unchecked((ushort)((ushort)((p.tempo & 255) | ((0x36) << 8))));
  for (int i = 0; i < 8; i++)
    p.channel[i].pitch_envelope_num_ticks = p.channel[i].pitch_envelope_delay = 0;

  Dsp_Write(p, KOF, ~p.is_chan_on);
  return;

music_keep_running:
  if ((--p.counter_sf0c) != 0)
    return;
Music_loop_1:
  if (p.music_ptr_toplevel == 0)
    return;
  for(;;) {
    t = p.ReadWord(p.music_ptr_toplevel);
    p.music_ptr_toplevel = unchecked((ushort)(p.music_ptr_toplevel + (2)));
    if ((t >> 8) != 0)
      break;
    if (t == 0) {
      p.CurrentCommands[2] = 0;
      Dsp_Write(p, KOF, ~p.is_chan_on);
      return;
    }
    if (--p.block_count == 0) {
      p.music_ptr_toplevel = unchecked((ushort)(p.music_ptr_toplevel + (2)));
    } else {
      if ((p.block_count & 0x80) != 0)
        p.block_count = unchecked((byte)(t));
      p.music_ptr_toplevel = unchecked((ushort)(p.ReadWord(p.music_ptr_toplevel)));
    }
  }
  var patternOffset = t;
  for (int i = 7; i >= 0; i--) {
    p.channel[i].pattern_cur_ptr = p.ReadWord(patternOffset + (i) * 2);
  }

  p.cur_chan_bit = unchecked((byte)(0x80));
  for (int i = 7; i >= 0; i--, p.cur_chan_bit >>= 1) {
    if ((p.channel[i].pattern_cur_ptr >> 8) != 0) {
      p.channel[i].note_ticks_left = 1;
      if (p.channel[i].instrument_id == 0)
        Channel_SetInstrument(p, p.channel[i], 1);
    }
  }
label_a:

  p.key_ON = unchecked((byte)(0));
  p.cur_chan_bit = unchecked((byte)(1));
  c = p.channel[0];
  for (; p.cur_chan_bit != 0; c++, p.cur_chan_bit <<= 1) {
    if ((c.pattern_cur_ptr >> 8) == 0)
      continue;
    if ((--c.note_ticks_left) != 0) {
      HandleNoteTick(p, c);
      continue;
    }
    for(;;) {
      cmd = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
      if (cmd == 0) {
        if (!((c.subroutine_num_loops) != 0))
          goto Music_loop_1;
        c.pattern_cur_ptr = unchecked((ushort)((--c.subroutine_num_loops == 0) ? c.saved_pattern_ptr : c.pattern_start_ptr));
        continue;
      }
      if (!(((cmd & 0x80) != 0))) {
        c.note_length = unchecked((byte)(cmd));
        cmd = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
        if (!(((cmd & 0x80) != 0))) {


          c.note_gate_off_fixedpt = unchecked((byte)(kNoteGateOffPct[cmd >> 4 & 7]));
          c.channel_volume_master = unchecked((byte)(kNoteVol[cmd & 0xf]));
          p.vol_dirty = unchecked((byte)(p.vol_dirty | (p.cur_chan_bit)));
          cmd = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
        }
      }
      if (cmd >= 0xda) {
        HandleEffect(p, c, cmd);
        continue;
      }
      if (!(((p.is_chan_on & p.cur_chan_bit) != 0)))
        PlayNote(p, c, cmd);
      c.note_ticks_left = unchecked((byte)(c.note_length));
      t = c.note_ticks_left * c.note_gate_off_fixedpt >> 8;
      c.note_keyoff_ticks_left = unchecked((byte)((t != 0) ? t : 1));
      break;
    }
  }

  if ((p.tempo_fade_num_ticks) != 0)
    p.tempo = unchecked((ushort)((--p.tempo_fade_num_ticks == 0) ? p.tempo_fade_final << 8 : p.tempo + p.tempo_fade_add));

  if ((p.echo_volume_fade_ticks) != 0) {
    if (--p.echo_volume_fade_ticks == 0) {
      p.echo_volume_left = unchecked((ushort)(p.echo_volume_left & (~0xff)));
      p.echo_volume_right = unchecked((ushort)(p.echo_volume_right & (~0xff)));
    } else {
      p.echo_volume_left = unchecked((ushort)(p.echo_volume_left + (p.echo_volume_fade_add_left)));
      p.echo_volume_right = unchecked((ushort)(p.echo_volume_right + (p.echo_volume_fade_add_right)));
    }
    SetEchoVolume(p);
  }

  if ((p.master_volume_fade_ticks) != 0) {
    p.master_volume = unchecked((ushort)((--p.master_volume_fade_ticks == 0) ? p.master_volume_fade_target << 8 : p.master_volume + p.master_volume_fade_add_per_tick));
    p.vol_dirty = unchecked((byte)(0xff));
  }
  c = p.channel[7];
  p.cur_chan_bit = unchecked((byte)(0x80));
  for (; p.cur_chan_bit != 0; c--, p.cur_chan_bit >>= 1) {
    if ((c.pattern_cur_ptr >> 8) != 0)
      Chan_HandleTick(p, c);
  }

  p.vol_dirty = unchecked((byte)(0));
  Write_KeyOn(p, p.key_ON & ~p.is_chan_on);
}

    private static void Write_KeyOn(SmwMusicDriver p, int a) {
  a &= 255;
  Dsp_Write(p, KOF, 0);
  Dsp_Write(p, KON, a);
}

    private static void Channel_SetInstrument(SmwMusicDriver p, Channel c, int instrument) {
  instrument &= 255;
  c.instrument_id = unchecked((byte)(instrument));
  Channel_SetInstrumentEx(p, c, 0x5F46 + (instrument - 1) * 5);
}

    private static bool Channel_SetInstrumentEx(SmwMusicDriver p, Channel c, int addr) {
  addr &= 65535;
  if ((p.is_chan_on & p.cur_chan_bit) != 0)
    return true;
  p.chan_bit_flags = unchecked((byte)(p.chan_bit_flags & (~p.cur_chan_bit)));
  Dsp_Write(p, NON, p.chan_bit_flags);
  byte reg = unchecked((byte)(c.index * 16));
  var instrumentOffset = addr;
  Dsp_Write(p, reg + V0SRCN, p.ram[instrumentOffset + 0]);
  Dsp_Write(p, reg + V0ADSR1, p.ram[instrumentOffset + 1]);
  Dsp_Write(p, reg + V0ADSR2, p.ram[instrumentOffset + 2]);
  Dsp_Write(p, reg + V0GAIN, p.ram[instrumentOffset + 3]);
  c.instrument_pitch_base = unchecked((byte)(p.ram[instrumentOffset + 4]));
  return false;
}


    private static void HandleEffect(SmwMusicDriver p, Channel c, int effect) {
  effect &= 255;

  switch (effect) {
  case 0xda:
    Channel_SetInstrument(p, c, p.ram[c.pattern_cur_ptr++] + 1);
    break;
  case 0xdb: {
    byte arg = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    c.pan_value = unchecked((ushort)((arg & 0x1f) << 8));
    c.pan_flag_with_phase_invert = unchecked((byte)(arg & 0xc0));
    p.vol_dirty = unchecked((byte)(p.vol_dirty | (p.cur_chan_bit)));
    break;
  }
  case 0xdc:
    c.pan_num_ticks = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    c.pan_target_value = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    c.pan_add_per_tick = unchecked((ushort)(SpcDivHelper(c.pan_target_value - (c.pan_value >> 8), c.pan_num_ticks)));
    break;
  case 0xde: // vibrato on
    c.vibrato_delay_ticks = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    c.vibrato_fade_num_ticks = unchecked((byte)(0));
    c.vibrato_rate = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    c.vib_depth = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    break;
  case 0xdf: // vibrato off
    c.vib_depth = unchecked((byte)(0));
    break;
  case 0xe0:  // master volume
    p.master_volume = unchecked((ushort)(p.ram[c.pattern_cur_ptr++] << 8));
    p.vol_dirty = unchecked((byte)(0xff));
    break;
  case 0xe1:  // master volume fade
    p.master_volume_fade_ticks = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    p.master_volume_fade_target = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    p.master_volume_fade_add_per_tick = unchecked((ushort)(SpcDivHelper(p.master_volume_fade_target - (p.master_volume >> 8), p.master_volume_fade_ticks)));
    break;
  case 0xe2:  // tempo
    p.tempo = unchecked((ushort)((p.ram[c.pattern_cur_ptr++] + p.smw_tempo_increase + 1) << 8));
    break;
  case 0xe3:  // tempo fade
    p.tempo_fade_num_ticks = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    p.tempo_fade_final = unchecked((byte)(p.ram[c.pattern_cur_ptr++] + p.smw_tempo_increase + 1));
    p.tempo_fade_add = unchecked((ushort)(SpcDivHelper(p.tempo_fade_final - (p.tempo >> 8), p.tempo_fade_num_ticks)));
    break;
  case 0xe4:
    p.global_transposition = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    break;
  case 0xe5:  // tremolo on
    throw new NotSupportedException($"SMW tremolo command E5 at ${c.pattern_cur_ptr - 1:X4} requires original-driver validation");
  case 0xe6:  // tremolo off
    c.tremolo_depth = unchecked((byte)(0));
    break;
  case 0xe7:  // volume
    c.channel_volume = unchecked((ushort)(p.ram[c.pattern_cur_ptr++] << 8));
    p.vol_dirty = unchecked((byte)(p.vol_dirty | (p.cur_chan_bit)));
    break;
  case 0xe8:  // volume fade
    c.volume_fade_ticks = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    c.volume_fade_target = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    c.volume_fade_addpertick = unchecked((ushort)(SpcDivHelper(c.volume_fade_target - (c.channel_volume >> 8), c.volume_fade_ticks)));
    break;
  case 0xe9: { // subroutine
    ushort t = unchecked((ushort)(p.ram[c.pattern_cur_ptr++]));
    t = unchecked((ushort)(t | (p.ram[c.pattern_cur_ptr++] << 8)));
    c.subroutine_num_loops = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    c.saved_pattern_ptr = unchecked((ushort)(c.pattern_cur_ptr));
    c.pattern_cur_ptr = unchecked((ushort)(c.pattern_start_ptr = t));
    break;
  }
  case 0xea:  // vibrato fade
    c.vibrato_fade_num_ticks = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    c.vib_depth_orig = unchecked((byte)(c.vib_depth));
    c.vibrato_fade_add_per_tick = unchecked((byte)(c.vibrato_fade_num_ticks != 0 ? c.vib_depth / c.vibrato_fade_num_ticks : 0xff));
    break;
  case 0xeb:  // pitch envelope release
    c.pitch_envelope_direction = unchecked((byte)(1));
    goto pitch_env;
  case 0xec:  // pitch envelope attack
    c.pitch_envelope_direction = unchecked((byte)(0));
pitch_env:
    c.pitch_envelope_delay = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    c.pitch_envelope_num_ticks = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    c.pitch_envelope_slide_value = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    break;
  case 0xee:  // fine tune
    c.fine_tune = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    break;
  case 0xef:  // echo enable bits and volume
    p.echo_channels = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    Dsp_Write(p, EON, p.echo_channels);
    p.echo_volume_left = unchecked((ushort)(p.ram[c.pattern_cur_ptr++] << 8));
    p.echo_volume_right = unchecked((ushort)(p.ram[c.pattern_cur_ptr++] << 8));
    p.sfx3_unused = unchecked((byte)(0));
    Dsp_Write(p, FLG, 0);
    SetEchoVolume(p);
    break;
  case 0xf0:  // echo off
    p.echo_channels = unchecked((byte)(0));
    SetEchoOff(p);
    break;
  case 0xf1: { // set echo properties
    ReadOnlySpan<sbyte> kEchoFirParameters = [
       -1,   8,  23,  36,  36,  23,   8,  -1,
      127,   0,   0,   0,   0,   0,   0,   0,
    ];
    Dsp_Write(p, EDL, p.ram[c.pattern_cur_ptr++]);
    Dsp_Write(p, EFB, p.ram[c.pattern_cur_ptr++]);
    var firOffset = p.ram[c.pattern_cur_ptr++] * 8;
    for (int i = 0; i < 8; i++)
      Dsp_Write(p, FIR0 + i * 16, kEchoFirParameters[firOffset++]);
    break;
  }
  case 0xf2:
    p.echo_volume_fade_ticks = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    p.echo_volume_fade_target_left = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    p.echo_volume_fade_target_right = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
    p.echo_volume_fade_add_left = unchecked((ushort)(SpcDivHelper(p.echo_volume_fade_target_left - (p.echo_volume_left >> 8), p.echo_volume_fade_ticks)));
    p.echo_volume_fade_add_right = unchecked((ushort)(SpcDivHelper(p.echo_volume_fade_target_right - (p.echo_volume_right >> 8), p.echo_volume_fade_ticks)));
    break;
  default:
    Not_Implemented();
    break;
  }
}

    private static void ComputePitchAdd(Channel c, int pitch) {
  pitch &= 255;
  c.pitch_target = unchecked((byte)(pitch & 0x7f));
  c.pitch_add_per_tick = unchecked((ushort)(SpcDivHelper(c.pitch_target - (c.pitch >> 8), c.pitch_slide_length)));
}

    private static void Chan_HandleTick(SmwMusicDriver p, Channel c) {
  if ((c.volume_fade_ticks) != 0) {
    p.vol_dirty = unchecked((byte)(p.vol_dirty | (p.cur_chan_bit)));
    Chan_DoAnyFade(ref c.channel_volume, c.volume_fade_addpertick, c.volume_fade_target, --c.volume_fade_ticks);
  }
  if ((c.tremolo_depth) != 0) {
    if (c.tremolo_delay_ticks == c.tremolo_hold_count) {
      p.vol_dirty = unchecked((byte)(p.vol_dirty | (p.cur_chan_bit)));
      if ((c.tremolo_count & 0x80) != 0 && c.tremolo_depth == 0xff) {
        c.tremolo_count = unchecked((byte)(0x80));
      } else {
        //c.tremolo_count += c.tremolo_rate;
      }
      CalcTremolo(p, c);
    } else {
      c.tremolo_hold_count++;
      CalcFinalVolume(p, c, c.channel_volume_master);
    }
  } else {
    CalcFinalVolume(p, c, c.channel_volume_master);
  }

  if ((c.pan_num_ticks) != 0) {
    Chan_DoAnyFade(ref c.pan_value, c.pan_add_per_tick, c.pan_target_value, --c.pan_num_ticks);
  } else if (!(((p.vol_dirty & p.cur_chan_bit) != 0))) {
    return;
  }
  WriteVolumeToDsp(p, c, c.pan_value);
}

    private static void WriteVolumeToDsp(SmwMusicDriver p, Channel c, int volume) {
  volume &= 65535;

  if ((p.is_chan_on & p.cur_chan_bit) != 0)
    return;
  for (int i = 0; i < 2; i++) {
    int j = volume >> 8;
    if (!((j < 21))) throw new InvalidOperationException("Native sound-driver invariant failed");
    byte t = unchecked((byte)(kVolumeTable[j] + ((kVolumeTable[j + 1] - kVolumeTable[j]) * (byte)volume >> 8)));
    t = unchecked((byte)(t * c.final_volume >> 8));
    if (((c.pan_flag_with_phase_invert << i) & 0x80) != 0)
      t = unchecked((byte)(-t));
    Dsp_Write(p, V0VOLL + i + c.index * 16, t);
    volume = 0x1400 - volume;
  }
}




    private static bool WantWriteKof(SmwMusicDriver p, Channel c) {
  int ptr = c.pattern_cur_ptr;
  for (;;) {
    byte cmd = unchecked((byte)(p.ram[ptr++]));
    if (cmd == 0)
      return true;

    while (!(((cmd & 0x80) != 0)))
      cmd = unchecked((byte)(p.ram[ptr++]));

    if (cmd == 0xc6)
      return false;
    if (cmd < 0xda)
      return true;
    ptr += kEffectByteLength[cmd - 0xda] - 1;
  }
}

    private static void HandleNoteTick(SmwMusicDriver p, Channel c) {
  if (--c.note_keyoff_ticks_left == 0 || c.note_ticks_left == 2) {
    if (WantWriteKof(p, c) && !(((p.cur_chan_bit & p.is_chan_on) != 0)))
      Dsp_Write(p, KOF, p.cur_chan_bit);
  }

  p.did_affect_volumepitch_flag = unchecked((byte)(0));
  if (c.pitch_slide_length == 0 || ((p.is_chan_on & p.cur_chan_bit) != 0)) {
    if (p.ram[c.pattern_cur_ptr] != 0xdd)
      goto after_pitch_thing;
    if ((p.cur_chan_bit & p.is_chan_on) != 0) {
      c.pattern_cur_ptr = unchecked((ushort)(c.pattern_cur_ptr + (4)));
    } else {
      c.pattern_cur_ptr++;
      c.pitch_slide_delay_left = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
      c.pitch_slide_length = unchecked((byte)(p.ram[c.pattern_cur_ptr++]));
      ComputePitchAdd(c, p.ram[c.pattern_cur_ptr++] + p.global_transposition);
    }
  }

  if ((c.pitch_slide_delay_left) != 0) {
    c.pitch_slide_delay_left--;
  } else if (!(((p.is_chan_on & p.cur_chan_bit) != 0))) {
    p.did_affect_volumepitch_flag = unchecked((byte)(0x80));
    Chan_DoAnyFade(ref c.pitch, c.pitch_add_per_tick, c.pitch_target, --c.pitch_slide_length);
  }
after_pitch_thing:;
  ushort pitch = unchecked((ushort)(c.pitch));
  if ((c.vib_depth) != 0) {
    if (c.vibrato_delay_ticks == c.vibrato_hold_count) {
      if ((c.vibrato_fade_num_ticks) != 0) {
        if (c.vibrato_change_count == c.vibrato_fade_num_ticks) {
          c.vib_depth = unchecked((byte)(c.vib_depth_orig));
        } else {
          c.vib_depth = unchecked((byte)((c.vibrato_change_count++ == 0 ? 0 : c.vib_depth) + c.vibrato_fade_add_per_tick));
        }
      }
      c.vibrato_count = unchecked((byte)(c.vibrato_count + (c.vibrato_rate)));
      CalcVibratoAddPitch(p, c, pitch, c.vibrato_count);
      return;
    }
    c.vibrato_hold_count++;
  }
  if ((p.did_affect_volumepitch_flag) != 0)
    WritePitch(p, c, pitch);
}

    private static void CalcVibratoAddPitch(SmwMusicDriver p, Channel c, int pitch, int value) {
  pitch &= 65535;
  value &= 255;
  int t = value << 2;
  t = t ^ ((t & 0x100) != 0 ? 0xff : 0);
  int r = (c.vib_depth >= 0xf1) ?
    (byte)t * (c.vib_depth & 0xf) :
    (byte)t * c.vib_depth >> 8;
  WritePitch(p, c, pitch + ((value & 0x80) != 0 ? -r : r));
}

    private static void HandleTremolo(SmwMusicDriver p, Channel c) {
  Not_Implemented();
}

    private static void CalcTremolo(SmwMusicDriver p, Channel c) {
  Not_Implemented();
}

    private static void HandlePanAndSweep(SmwMusicDriver p, Channel c) {
  p.did_affect_volumepitch_flag = unchecked((byte)(0));
  if ((c.tremolo_depth) != 0 && c.tremolo_hold_count == c.tremolo_delay_ticks)
    HandleTremolo(p, c);

  ushort volume = unchecked((ushort)(c.pan_value));
  if (c.pan_num_ticks != 0) {
    p.did_affect_volumepitch_flag = unchecked((byte)(0x80));
    volume = unchecked((ushort)(volume + (p.main_tempo_accum * (short)c.pan_add_per_tick / 256)));
  }

  if ((p.did_affect_volumepitch_flag) != 0)
    WriteVolumeToDsp(p, c, volume);
  p.did_affect_volumepitch_flag = unchecked((byte)(0));

  ushort pitch = unchecked((ushort)(c.pitch));
  if ((c.pitch_slide_length) != 0 && !((c.pitch_slide_delay_left) != 0)) {
    p.did_affect_volumepitch_flag = unchecked((byte)(0x80));
    pitch = unchecked((ushort)(pitch + (p.main_tempo_accum * (short)c.pitch_add_per_tick / 256)));
  }
  if ((c.vib_depth) != 0 && c.vibrato_delay_ticks == c.vibrato_hold_count) {
    CalcVibratoAddPitch(p, c, pitch, (p.main_tempo_accum * c.vibrato_rate >> 8) + c.vibrato_count);
    return;
  }
  if ((p.did_affect_volumepitch_flag) != 0)
    WritePitch(p, c, pitch);
}

    private static void CalcFinalVolume(SmwMusicDriver p, Channel c, int vol) {
  vol &= 255;
  int t = vol * (c.channel_volume >> 8) >> 8;
  t = (p.master_volume >> 8) * t >> 8;
  c.final_volume = unchecked((byte)(t * t >> 8));
}


}
