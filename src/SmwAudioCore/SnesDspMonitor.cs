using System;

namespace OpenPlatformerRuntime.Audio;

/// <summary>Optional listening mix. It never writes DSP registers or changes authentic PCM/state.</summary>
public sealed class SnesDspMonitor
{
    private readonly float[] _voiceGain = new float[8], _sampleGain = new float[20];
    private readonly float[] _voiceLeft = new float[8], _voiceRight = new float[8];
    private readonly float[] _sampleLeft = new float[20], _sampleRight = new float[20];
    private readonly bool[] _voiceMute = new bool[8], _voiceSolo = new bool[8];
    private readonly bool[] _sampleMute = new bool[20], _sampleSolo = new bool[20];
    private bool _anyVoiceSolo, _anySampleSolo;
    public bool Enabled { get; set; }
    public bool IncludeOriginalEcho { get; set; }

    public SnesDspMonitor() => Reset();
    public void Reset()
    {
        Array.Fill(_voiceGain, 1); Array.Fill(_sampleGain, 1);
        Array.Fill(_voiceLeft, 1); Array.Fill(_voiceRight, 1);
        Array.Fill(_sampleLeft, 1); Array.Fill(_sampleRight, 1);
        Array.Clear(_voiceMute); Array.Clear(_voiceSolo); Array.Clear(_sampleMute); Array.Clear(_sampleSolo);
        _anyVoiceSolo = _anySampleSolo = Enabled = IncludeOriginalEcho = false;
    }
    public void SetGain(bool sample, int id, float db) => (sample ? _sampleGain : _voiceGain)[id] = MathF.Pow(10, Math.Clamp(db, -60, 12) / 20);
    public void SetSideGain(bool sample, int id, bool right, float gain) =>
        (sample ? (right ? _sampleRight : _sampleLeft) : (right ? _voiceRight : _voiceLeft))[id] = Math.Clamp(gain, 0, 2);
    public void SetMuted(bool sample, int id, bool muted) => (sample ? _sampleMute : _voiceMute)[id] = muted;
    public void SetSolo(bool sample, int id, bool solo)
    {
        var flags = sample ? _sampleSolo : _voiceSolo; flags[id] = solo;
        var any = false; foreach (var flag in flags) any |= flag;
        if (sample) _anySampleSolo = any; else _anyVoiceSolo = any;
    }

    public void Render(SnesDsp dsp, out short left, out short right)
    {
        if (!Enabled) { left = dsp.LastLeft; right = dsp.LastRight; return; }
        if ((dsp.ReadRegister(0x6C) & 64) != 0) { left = right = 0; return; }
        float l = 0, r = 0;
        for (var voice = 0; voice < 8; voice++)
        {
            var sample = dsp.MonitorSourceIds[voice];
            if (_voiceMute[voice] || _anyVoiceSolo && !_voiceSolo[voice] || sample >= 20) continue;
            if (_sampleMute[sample] || _anySampleSolo && !_sampleSolo[sample]) continue;
            var gain = _voiceGain[voice] * _sampleGain[sample];
            l += dsp.MonitorVoiceLeft[voice] * gain * _voiceLeft[voice] * _sampleLeft[sample];
            r += dsp.MonitorVoiceRight[voice] * gain * _voiceRight[voice] * _sampleRight[sample];
        }
        l *= unchecked((sbyte)dsp.ReadRegister(0x0C)) / 128f;
        r *= unchecked((sbyte)dsp.ReadRegister(0x1C)) / 128f;
        if (IncludeOriginalEcho) { l += dsp.MonitorEchoLeft; r += dsp.MonitorEchoRight; }
        left = (short)Math.Clamp((int)l, short.MinValue, short.MaxValue);
        right = (short)Math.Clamp((int)r, short.MinValue, short.MaxValue);
    }
}
