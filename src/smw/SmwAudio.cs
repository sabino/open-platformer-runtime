using Godot;
using System;
using System.Collections.Generic;

public partial class SmwAudio : Node
{
    private const int SampleRate = 32000;
    private const int FramesPerSecond = 60;
    private const int InstrumentTable = 0x5570;
    private readonly Dictionary<int, DecodedSample> _samples = [];
    private readonly List<Voice> _voices = [];
    private byte[] _spcRam = new byte[0x10000];
    private AudioStreamPlayer? _player;
    private AudioStreamGeneratorPlayback? _playback;
    private bool _loaded;
    private IReadOnlyList<MusicEvent> _musicPattern = Array.Empty<MusicEvent>();
    private int _musicLoopFrames;
    private int _musicFrame;
    private int _musicEventIndex;
    private double _musicFrameAccumulator;
    private bool _musicPlaying;

    public override void _Ready()
    {
        EnsureLoaded();
        GD.Print($"smw-audio: internal_apu=1 samples={LoadedProbeSampleCount}");
    }

    public override void _Process(double delta)
    {
        StepMusicSequencer(delta);
        FillGeneratorBuffer();
    }

    public override void _ExitTree()
    {
        _voices.Clear();
        _player?.Stop();
    }

    public int LoadedProbeSampleCount =>
        (HasSample(9) ? 1 : 0) + (HasSample(14) ? 1 : 0) + (HasSample(16) ? 1 : 0);

    public void PlayMenuStart()
    {
        PlaySample(9);
    }

    public void PlayJump()
    {
        PlayPort1JumpCommand();
    }

    public void PlaySpinJump()
    {
        PlayPort1TwoNoteCommand();
    }

    public bool HasSample(int sampleId)
    {
        EnsureLoaded();
        return _samples.ContainsKey(sampleId);
    }

    public void PlaySample(int sampleId)
    {
        EnsureLoaded();
        if (!_samples.TryGetValue(sampleId, out var sample))
        {
            return;
        }

        _voices.Add(new Voice(sample, step: 1.0, volume: 0.42f, pan: 0.0f, durationSamples: 0, delaySamples: 0));
    }

    public void PlayMusicPreview(string bankName)
    {
        EnsureLoaded();
        (_musicPattern, _musicLoopFrames) = bankName switch
        {
            "Overworld" => (OverworldPreviewPattern, 96),
            "Credits" => (CreditsPreviewPattern, 128),
            _ => (LevelPreviewPattern, 96),
        };

        _voices.Clear();
        _musicFrame = 0;
        _musicEventIndex = 0;
        _musicFrameAccumulator = 0.0;
        _musicPlaying = true;
        TriggerMusicEventsForCurrentFrame();
        GD.Print($"smw-audio: music_preview={bankName} events={_musicPattern.Count} loop_frames={_musicLoopFrames}");
    }

    public void StopMusicPreview()
    {
        _musicPlaying = false;
        _voices.Clear();
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        SetupGenerator();
        LoadSpcSamples();
    }

    private void SetupGenerator()
    {
        var stream = new AudioStreamGenerator
        {
            MixRateMode = AudioStreamGenerator.AudioStreamGeneratorMixRate.Custom,
            MixRate = SampleRate,
            BufferLength = 0.25f,
        };
        _player = new AudioStreamPlayer
        {
            Name = "SmwInternalApu",
            Stream = stream,
            Bus = "Master",
        };
        AddChild(_player);
        _player.Play();
        _playback = _player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
    }

    private void LoadSpcSamples()
    {
        LoadSpcUploadIntoRam("res://generated/smw/audio/spc_engine.bin", _spcRam);
        LoadSpcUploadIntoRam("res://generated/smw/audio/spc_samples.bin", _spcRam);
        foreach (var sampleId in new[] { 7, 8, 9, 14, 16 })
        {
            var decoded = DecodeSampleFromDirectory(_spcRam, sampleId);
            if (decoded.Pcm.Length > 0)
            {
                _samples[sampleId] = decoded;
            }
        }
    }

    private static void LoadSpcUploadIntoRam(string resourcePath, byte[] ram)
    {
        if (!FileAccess.FileExists(resourcePath))
        {
            return;
        }

        using var file = FileAccess.Open(resourcePath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return;
        }

        ParseSpcUploadIntoRam(file.GetBuffer((long)file.GetLength()), ram);
    }

    private static void ParseSpcUploadIntoRam(byte[] payload, byte[] ram)
    {
        var offset = 0;
        while (offset + 2 <= payload.Length)
        {
            var count = payload[offset] | (payload[offset + 1] << 8);
            offset += 2;
            if (count == 0)
            {
                return;
            }

            if (offset + 2 + count > payload.Length)
            {
                return;
            }

            var target = payload[offset] | (payload[offset + 1] << 8);
            offset += 2;
            for (var i = 0; i < count; i++)
            {
                ram[(target + i) & 0xFFFF] = payload[offset + i];
            }

            offset += count;
        }
    }

    private static DecodedSample DecodeSampleFromDirectory(byte[] ram, int sampleId)
    {
        var entry = 0x8000 + sampleId * 4;
        var start = ram[entry] | (ram[entry + 1] << 8);
        var loop = ram[entry + 2] | (ram[entry + 3] << 8);
        return DecodeBrrSample(ram, sampleId, start, loop);
    }

    private static DecodedSample DecodeBrrSample(byte[] ram, int sampleId, int start, int loop)
    {
        var samples = new List<float>();
        var old = 0;
        var older = 0;
        var offset = start;
        while (offset + 9 <= ram.Length)
        {
            var command = ram[offset];
            var shift = command >> 4;
            var filterId = (command >> 2) & 0x03;
            for (var i = 0; i < 16; i++)
            {
                var packed = ram[offset + 1 + i / 2];
                var nibble = (packed >> ((i & 1) == 1 ? 0 : 4)) & 0x0F;
                var sample = (nibble & 7) - (nibble & 8);
                if (shift <= 12)
                {
                    sample = (sample << shift) >> 1;
                }
                else
                {
                    sample = (sample >> 3) << 12;
                }

                if (filterId == 1)
                {
                    sample += old + ((-old) >> 4);
                }
                else if (filterId == 2)
                {
                    sample += old * 2 + ((-old * 3) >> 5) - older + (older >> 4);
                }
                else if (filterId == 3)
                {
                    sample += old * 2 + ((-old * 13) >> 6) - older + ((older * 3) >> 4);
                }

                sample = Math.Clamp(sample, -0x8000, 0x7FFF);
                sample = (sample & 0x3FFF) - (sample & 0x4000);
                older = old;
                old = sample;
                samples.Add(Math.Clamp(sample / 16384.0f, -1.0f, 1.0f));
            }

            offset += 9;
            if ((command & 1) != 0)
            {
                break;
            }
        }

        return new DecodedSample(sampleId, start, loop, samples.ToArray());
    }

    private void PlayPort1JumpCommand()
    {
        PlayInstrumentNote(instrument: 8, note: 0xB2, durationFrames: 0x30, volume: 0.52f, delayFrames: 4);
    }

    private void PlayPort1TwoNoteCommand()
    {
        PlayInstrumentNote(instrument: 7, note: 0xA4, durationFrames: 0x0C, volume: 0.42f, delayFrames: 4);
        PlayInstrumentNote(instrument: 7, note: 0xA4, durationFrames: 0x0C, volume: 0.42f, delayFrames: 0x10);
    }

    private void PlayInstrumentNote(int instrument, int note, int durationFrames, float volume, int delayFrames, float pan = 0.0f)
    {
        EnsureLoaded();
        var instrumentOffset = InstrumentTable + instrument * 9;
        if (instrumentOffset + 8 >= _spcRam.Length)
        {
            return;
        }

        var sampleId = _spcRam[instrumentOffset + 4];
        var pitchBase = _spcRam[instrumentOffset + 8];
        if (!_samples.TryGetValue(sampleId, out var sample))
        {
            sample = DecodeSampleFromDirectory(_spcRam, sampleId);
            if (sample.Pcm.Length == 0)
            {
                return;
            }

            _samples[sampleId] = sample;
        }

        var step = ComputeDspPitchStep(note, pitchBase);
        _voices.Add(new Voice(
            sample,
            step,
            volume,
            pan,
            durationSamples: FramesToSamples(durationFrames),
            delaySamples: FramesToSamples(delayFrames)));
    }

    private void StepMusicSequencer(double delta)
    {
        if (!_musicPlaying || _musicPattern.Count == 0 || _musicLoopFrames <= 0)
        {
            return;
        }

        _musicFrameAccumulator += delta * FramesPerSecond;
        while (_musicFrameAccumulator >= 1.0)
        {
            _musicFrameAccumulator -= 1.0;
            _musicFrame++;
            if (_musicFrame >= _musicLoopFrames)
            {
                _musicFrame = 0;
                _musicEventIndex = 0;
            }

            TriggerMusicEventsForCurrentFrame();
        }
    }

    private void TriggerMusicEventsForCurrentFrame()
    {
        while (_musicEventIndex < _musicPattern.Count && _musicPattern[_musicEventIndex].Frame == _musicFrame)
        {
            var note = _musicPattern[_musicEventIndex];
            PlayInstrumentNote(note.Instrument, note.Note, note.DurationFrames, note.Volume, delayFrames: 0, note.Pan);
            _musicEventIndex++;
        }
    }

    private static int FramesToSamples(int frames)
    {
        return Math.Max(0, frames) * SampleRate / FramesPerSecond;
    }

    private static double ComputeDspPitchStep(int note, int instrumentPitchBase)
    {
        var pitch = note << 8;
        if ((pitch >> 8) >= 0x34)
        {
            pitch += (pitch >> 8) - 0x34;
        }
        else if ((pitch >> 8) < 0x13)
        {
            pitch += (byte)(((pitch >> 8) - 0x13) * 2) - 256;
        }

        var basePeriod = ComputePeriod((pitch >> 8) & 0xFF);
        var nextPeriod = ComputePeriod(((pitch >> 8) + 1) & 0xFF);
        var delta = nextPeriod - basePeriod;
        var interpolated = basePeriod + ((delta >> 8) * (pitch & 0xFF)) + (((delta & 0xFF) * (pitch & 0xFF)) >> 8);
        var dspPitch = interpolated * instrumentPitchBase;
        return Math.Clamp(dspPitch / 4096.0, 0.05, 8.0);
    }

    private static int ComputePeriod(int note)
    {
        int[] baseNoteFreqs = [4286, 4541, 4811, 5097, 5400, 5721, 6061, 6422, 6804, 7208, 7637, 8091];
        var pp = note & 0x7F;
        var q = pp / 12;
        var r = pp % 12;
        var value = baseNoteFreqs[r];
        while (q != 6)
        {
            value >>= 1;
            q++;
        }

        return value;
    }

    private void FillGeneratorBuffer()
    {
        if (_playback == null)
        {
            return;
        }

        var framesAvailable = _playback.GetFramesAvailable();
        if (framesAvailable <= 0)
        {
            return;
        }

        var frameCount = Math.Min(framesAvailable, 1024);
        var frames = new Vector2[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            frames[i] = RenderFrame();
        }

        _playback.PushBuffer(frames);
    }

    private Vector2 RenderFrame()
    {
        var left = 0.0f;
        var right = 0.0f;
        for (var i = _voices.Count - 1; i >= 0; i--)
        {
            var voice = _voices[i];
            if (!voice.Active)
            {
                _voices.RemoveAt(i);
                continue;
            }

            var sample = voice.NextSample();
            left += sample * voice.LeftGain;
            right += sample * voice.RightGain;
        }

        return new Vector2(Math.Clamp(left, -1.0f, 1.0f), Math.Clamp(right, -1.0f, 1.0f));
    }

    private sealed record DecodedSample(int Id, int StartAddress, int LoopAddress, float[] Pcm);

    private readonly record struct MusicEvent(
        int Frame,
        int Instrument,
        int Note,
        int DurationFrames,
        float Volume,
        float Pan);

    private static readonly MusicEvent[] LevelPreviewPattern =
    [
        new(0, 9, 0x68, 18, 0.22f, -0.15f),
        new(0, 12, 0x58, 22, 0.14f, 0.18f),
        new(12, 9, 0x6C, 18, 0.22f, -0.10f),
        new(24, 9, 0x70, 18, 0.22f, -0.05f),
        new(24, 12, 0x5C, 22, 0.14f, 0.18f),
        new(36, 9, 0x74, 18, 0.22f, 0.00f),
        new(48, 9, 0x77, 18, 0.22f, 0.05f),
        new(48, 12, 0x60, 22, 0.14f, 0.18f),
        new(60, 9, 0x74, 18, 0.22f, 0.00f),
        new(72, 9, 0x70, 18, 0.22f, -0.05f),
        new(72, 12, 0x5C, 22, 0.14f, 0.18f),
        new(84, 9, 0x6C, 18, 0.22f, -0.10f),
    ];

    private static readonly MusicEvent[] OverworldPreviewPattern =
    [
        new(0, 12, 0x60, 14, 0.18f, -0.12f),
        new(8, 12, 0x64, 14, 0.18f, -0.08f),
        new(16, 12, 0x67, 14, 0.18f, 0.00f),
        new(24, 12, 0x6C, 20, 0.20f, 0.08f),
        new(32, 9, 0x54, 22, 0.12f, 0.18f),
        new(40, 12, 0x6C, 14, 0.18f, 0.08f),
        new(48, 12, 0x67, 14, 0.18f, 0.00f),
        new(56, 12, 0x64, 14, 0.18f, -0.08f),
        new(64, 12, 0x60, 20, 0.20f, -0.12f),
        new(72, 9, 0x58, 22, 0.12f, 0.18f),
        new(80, 12, 0x64, 14, 0.18f, -0.08f),
        new(88, 12, 0x67, 14, 0.18f, 0.00f),
    ];

    private static readonly MusicEvent[] CreditsPreviewPattern =
    [
        new(0, 12, 0x64, 28, 0.18f, -0.10f),
        new(0, 9, 0x50, 32, 0.10f, 0.18f),
        new(24, 12, 0x68, 28, 0.18f, -0.05f),
        new(40, 9, 0x54, 32, 0.10f, 0.18f),
        new(48, 12, 0x6C, 28, 0.18f, 0.00f),
        new(72, 12, 0x70, 28, 0.18f, 0.05f),
        new(80, 9, 0x58, 32, 0.10f, 0.18f),
        new(96, 12, 0x6C, 28, 0.18f, 0.00f),
        new(112, 12, 0x68, 24, 0.18f, -0.05f),
    ];

    private sealed class Voice
    {
        private readonly DecodedSample _sample;
        private readonly int _durationSamples;
        private int _delaySamples;
        private int _ageSamples;
        private double _position;
        private readonly double _step;

        public Voice(DecodedSample sample, double step, float volume, float pan, int durationSamples, int delaySamples)
        {
            _sample = sample;
            _step = step;
            _durationSamples = durationSamples;
            _delaySamples = delaySamples;
            var leftPan = Math.Clamp(1.0f - pan, 0.0f, 1.0f);
            var rightPan = Math.Clamp(1.0f + pan, 0.0f, 1.0f);
            LeftGain = volume * leftPan;
            RightGain = volume * rightPan;
        }

        public float LeftGain { get; }
        public float RightGain { get; }
        public bool Active => _delaySamples > 0 ||
            (_durationSamples > 0 ? _ageSamples < _durationSamples : _position < _sample.Pcm.Length);

        public float NextSample()
        {
            if (_delaySamples > 0)
            {
                _delaySamples--;
                return 0.0f;
            }

            if (!Active)
            {
                return 0.0f;
            }

            var index = (int)_position;
            if (index >= _sample.Pcm.Length)
            {
                if (_durationSamples > 0)
                {
                    _position = 0.0;
                    index = 0;
                }
                else
                {
                    return 0.0f;
                }
            }

            _position += _step;
            _ageSamples++;
            return _sample.Pcm[index];
        }
    }
}
