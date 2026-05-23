using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class SmwAudio : Node
{
    private const int SampleRate = 32000;
    private const int FramesPerSecond = 60;
    private const int InstrumentTable = 0x5570;
    private const int MaxVoices = 10;
    private const float GeneratorBufferLengthSeconds = 0.25f;
    private const int GeneratorBufferFrames = (int)(SampleRate * GeneratorBufferLengthSeconds);
    private const int TargetQueuedAudioFrames = 1536;
    private const int AudioMixChunkFrames = 512;
    private const int MaxAudioMixChunksPerProcess = 4;
    private readonly Dictionary<int, DecodedSample> _samples = [];
    private readonly List<Voice> _voices = [];
    private byte[] _spcRam = new byte[0x10000];
    private AudioStreamPlayer? _player;
    private AudioStreamGeneratorPlayback? _playback;
    private Vector2[] _mixBuffer = [];
    private bool _loaded;
    private IReadOnlyList<MusicEvent> _musicPattern = Array.Empty<MusicEvent>();
    private int _musicLoopFrames;
    private int _musicFrame;
    private int _musicEventIndex;
    private double _musicFrameAccumulator;
    private bool _musicPlaying;
    private int _debugMixFrames;
    private int _debugMixCalls;
    private double _debugMixMilliseconds;
    private int _debugLastMixFrames;
    private double _debugLastMixMilliseconds;
    private string _lastSfxName = "none";
    private int _lastSfxPort;
    private int _lastSfxCommand;
    private bool _lastSfxNativeStream;
    private int _lastSfxPointer;
    private int _lastSfxNoteCount;

    public static readonly int[] ProbeSampleIds = [9, 14, 16];

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
        CountLoadedProbeSamples();

    public void PlayMenuStart()
    {
        PlayNativeSfxProbe(
            "menu_start",
            port: 3,
            command: 0x29,
            [new SfxProbeNote(9, 0x80, 0x0C, 0.38f, 0, 0.0f)]);
    }

    public void PlayJump()
    {
        PlayNativeSfxProbe(
            "jump",
            port: 1,
            command: 0x01,
            [new SfxProbeNote(8, 0xB2, 0x30, 0.52f, 4, 0.0f)]);
    }

    public void PlaySpinJump()
    {
        PlayNativeSfxProbe(
            "spin_jump",
            port: 1,
            command: 0x04,
            [
                new SfxProbeNote(7, 0xA4, 0x0C, 0.42f, 4, 0.0f),
                new SfxProbeNote(7, 0xA4, 0x0C, 0.42f, 0x10, 0.0f),
            ]);
    }

    public void PlayCoin()
    {
        PlayNativeSfxProbe(
            "coin",
            port: 3,
            command: 0x01,
            [new SfxProbeNote(7, 0x9E, 0x08, 0.30f, 0, 0.0f)]);
    }

    public void PlayDragonCoin()
    {
        PlayNativeSfxProbe(
            "dragon_coin",
            port: 3,
            command: 0x01,
            [
                new SfxProbeNote(7, 0x9E, 0x08, 0.26f, 0, -0.10f),
                new SfxProbeNote(7, 0xA7, 0x0A, 0.24f, 4, 0.10f),
            ]);
    }

    public void PlayOneUp()
    {
        PlayNativeSfxProbe(
            "one_up",
            port: 3,
            command: 0x02,
            [
                new SfxProbeNote(12, 0x60, 0x08, 0.24f, 0, -0.12f),
                new SfxProbeNote(12, 0x64, 0x08, 0.24f, 5, -0.04f),
                new SfxProbeNote(12, 0x67, 0x08, 0.24f, 10, 0.04f),
                new SfxProbeNote(12, 0x70, 0x10, 0.26f, 15, 0.12f),
            ]);
    }

    public void PlayStomp(int rewardIndex)
    {
        var clamped = Math.Clamp(rewardIndex, 0, 6);
        PlayNativeSfxProbe(
            "stomp",
            port: 1,
            command: 0x13 + clamped,
            [new SfxProbeNote(7, 0x78 + clamped * 2, 0x08, 0.36f, 0, 0.0f)]);
    }

    public void PlayBlockBreak()
    {
        PlayNativeSfxProbe(
            "block_break",
            port: 1,
            command: 0x08,
            [
                new SfxProbeNote(7, 0x84, 0x06, 0.30f, 0, -0.18f),
                new SfxProbeNote(7, 0x78, 0x06, 0.28f, 3, 0.18f),
            ]);
    }

    public void PlayBlockReward()
    {
        PlayNativeSfxProbe(
            "powerup_reward",
            port: 3,
            command: 0x0A,
            [
                new SfxProbeNote(12, 0x68, 0x08, 0.22f, 0, -0.08f),
                new SfxProbeNote(12, 0x70, 0x10, 0.24f, 6, 0.08f),
            ]);
    }

    public void PlayFireball()
    {
        PlayNativeSfxProbe(
            "fireball",
            port: 3,
            command: 0x06,
            [
                new SfxProbeNote(7, 0x8E, 0x08, 0.28f, 0, -0.10f),
                new SfxProbeNote(7, 0x84, 0x06, 0.24f, 4, 0.12f),
            ]);
    }

    public void PlayPlayerHurt()
    {
        PlayNativeSfxProbe(
            "hurt",
            port: 1,
            command: 0x04,
            [new SfxProbeNote(7, 0x72, 0x0E, 0.32f, 0, 0.0f)]);
    }

    public void PlayDeath()
    {
        PlayNativeSfxProbe(
            "death",
            port: 3,
            command: 0x09,
            [
                new SfxProbeNote(12, 0x54, 0x12, 0.20f, 0, -0.08f),
                new SfxProbeNote(12, 0x50, 0x18, 0.18f, 14, 0.08f),
            ]);
    }

    public void PlayCourseClear()
    {
        PlayNativeSfxProbe(
            "course_clear",
            port: 3,
            command: 0x17,
            [new SfxProbeNote(12, 0x70, 0x18, 0.20f, 0, 0.0f)]);
    }

    public bool PlayNamedSfx(string name)
    {
        switch (name.ToLowerInvariant())
        {
            case "coin":
                PlayCoin();
                return true;
            case "dragon":
            case "dragon_coin":
                PlayDragonCoin();
                return true;
            case "oneup":
            case "one-up":
            case "1up":
                PlayOneUp();
                return true;
            case "stomp":
                PlayStomp(0);
                return true;
            case "block":
            case "break":
            case "block_break":
                PlayBlockBreak();
                return true;
            case "reward":
            case "powerup":
                PlayBlockReward();
                return true;
            case "fireball":
            case "fire":
                PlayFireball();
                return true;
            case "hurt":
                PlayPlayerHurt();
                return true;
            case "death":
                PlayDeath();
                return true;
            case "clear":
            case "course_clear":
                PlayCourseClear();
                return true;
            default:
                return false;
        }
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

        AddVoice(new Voice(sample, step: 1.0, volume: 0.42f, pan: 0.0f, durationSamples: 0, delaySamples: 0));
    }

    public string PlaySampleProbe(int sampleId)
    {
        EnsureLoaded();
        var available = _samples.ContainsKey(sampleId);
        if (available)
        {
            PlaySample(sampleId);
        }

        return $"sample={sampleId:X2} available={(available ? 1 : 0)} samples={LoadedProbeSampleCount}";
    }

    public void PlayMusicPreview(string bankName)
    {
        EnsureLoaded();
        (_musicPattern, _musicLoopFrames) = bankName switch
        {
            "Overworld" => (OverworldPreviewPattern, 96),
            "Credits" => (CreditsPreviewPattern, 128),
            "Star" => (StarPreviewPattern, 64),
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

    public string DebugStatus()
    {
        var framesAvailable = _playback?.GetFramesAvailable() ?? -1;
        var averageMixMilliseconds = _debugMixCalls > 0 ? _debugMixMilliseconds / _debugMixCalls : 0.0;
        return $"loaded={(_loaded ? 1 : 0)} samples={LoadedProbeSampleCount} voices={_voices.Count} " +
            $"music={(_musicPlaying ? 1 : 0)} music_frame={_musicFrame} events={_musicPattern.Count} " +
            $"loop_frames={_musicLoopFrames} frames_available={framesAvailable} " +
            $"mix_chunk={AudioMixChunkFrames} mix_max_chunks={MaxAudioMixChunksPerProcess} " +
            $"mix_frames={_debugMixFrames} mix_calls={_debugMixCalls} mix_ms={_debugMixMilliseconds:0.000} " +
            $"mix_last_frames={_debugLastMixFrames} mix_last_ms={_debugLastMixMilliseconds:0.000} mix_avg_ms={averageMixMilliseconds:0.000} " +
            $"last_sfx={_lastSfxName} last_sfx_port={_lastSfxPort} last_sfx_cmd={_lastSfxCommand:X2} " +
            $"last_sfx_native={(_lastSfxNativeStream ? 1 : 0)} last_sfx_ptr={_lastSfxPointer:X4} last_sfx_notes={_lastSfxNoteCount}";
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

    private int CountLoadedProbeSamples()
    {
        EnsureLoaded();
        var count = 0;
        foreach (var sampleId in ProbeSampleIds)
        {
            if (_samples.ContainsKey(sampleId))
            {
                count++;
            }
        }

        return count;
    }

    private void SetupGenerator()
    {
        var stream = new AudioStreamGenerator
        {
            MixRateMode = AudioStreamGenerator.AudioStreamGeneratorMixRate.Custom,
            MixRate = SampleRate,
            BufferLength = GeneratorBufferLengthSeconds,
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

    private void PlayNativeSfxProbe(string name, int port, int command, IReadOnlyList<SfxProbeNote> fallbackNotes)
    {
        EnsureLoaded();
        var nativeStream = TryDecodeNativeSfxCommand(port, command, out var notes, out var pointer);
        if (!nativeStream || notes.Count == 0)
        {
            notes = fallbackNotes;
            pointer = 0;
            nativeStream = false;
        }

        RecordSfx(name, port, command, nativeStream, pointer, notes.Count);
        foreach (var note in notes)
        {
            PlayInstrumentNote(note.Instrument, note.Note, note.DurationFrames, note.Volume, note.DelayFrames, note.Pan);
        }
    }

    private void RecordSfx(string name, int port, int command, bool nativeStream, int pointer, int noteCount)
    {
        _lastSfxName = name;
        _lastSfxPort = port;
        _lastSfxCommand = command & 0xFF;
        _lastSfxNativeStream = nativeStream;
        _lastSfxPointer = pointer & 0xFFFF;
        _lastSfxNoteCount = noteCount;
        GD.Print(
            $"smw-audio: sfx={name} port={port} command={_lastSfxCommand:X2} " +
            $"native={(nativeStream ? 1 : 0)} ptr={_lastSfxPointer:X4} notes={noteCount}");
    }

    private bool TryDecodeNativeSfxCommand(int port, int command, out IReadOnlyList<SfxProbeNote> notes, out int pointer)
    {
        notes = Array.Empty<SfxProbeNote>();
        pointer = 0;
        var table = port == 3 ? 0x5619 : 0x5681;
        var commandIndex = command & 0xFF;
        var tableOffset = table + commandIndex * 2;
        if (tableOffset + 1 >= _spcRam.Length)
        {
            return false;
        }

        pointer = _spcRam[tableOffset] | (_spcRam[tableOffset + 1] << 8);
        if (pointer <= 0 || pointer >= _spcRam.Length)
        {
            return false;
        }

        var decoded = new List<SfxProbeNote>();
        var offset = pointer;
        var length = 8;
        var instrument = 7;
        var volume = 0.34f;
        var pan = 0.0f;
        var delay = port == 3 ? 2 : 0;
        var guard = 0;
        while (offset >= 0 && offset < _spcRam.Length && guard++ < 192 && decoded.Count < 16)
        {
            var streamCommand = _spcRam[offset++];
            if (streamCommand == 0)
            {
                break;
            }

            if ((streamCommand & 0x80) == 0)
            {
                length = Math.Max(1, (int)streamCommand);
                if (offset >= _spcRam.Length)
                {
                    break;
                }

                streamCommand = _spcRam[offset++];
                if ((streamCommand & 0x80) == 0)
                {
                    var left = streamCommand;
                    if (offset >= _spcRam.Length)
                    {
                        break;
                    }

                    streamCommand = _spcRam[offset++];
                    var right = left;
                    if ((streamCommand & 0x80) == 0)
                    {
                        right = streamCommand;
                        if (offset >= _spcRam.Length)
                        {
                            break;
                        }

                        streamCommand = _spcRam[offset++];
                    }

                    var leftNorm = Math.Clamp(left / 127.0f, 0.0f, 1.0f);
                    var rightNorm = Math.Clamp(right / 127.0f, 0.0f, 1.0f);
                    volume = Math.Clamp((leftNorm + rightNorm) * 0.22f, 0.06f, 0.50f);
                    pan = Math.Clamp(rightNorm - leftNorm, -0.8f, 0.8f);
                }
            }

            if (streamCommand == 0xDA)
            {
                if (port == 3)
                {
                    do
                    {
                        if (offset >= _spcRam.Length)
                        {
                            return decoded.Count > 0;
                        }

                        streamCommand = _spcRam[offset++];
                    } while ((streamCommand & 0x80) != 0);

                    instrument = streamCommand;
                }
                else
                {
                    if (offset >= _spcRam.Length)
                    {
                        break;
                    }

                    instrument = _spcRam[offset++];
                }

                continue;
            }

            if (streamCommand == 0xDD)
            {
                if (offset >= _spcRam.Length)
                {
                    break;
                }

                var note = _spcRam[offset++];
                decoded.Add(new SfxProbeNote(instrument, note, length, volume, delay, pan));
                delay += length;
                offset = Math.Min(_spcRam.Length, offset + 3);
                continue;
            }

            if (streamCommand == 0xEB)
            {
                offset = Math.Min(_spcRam.Length, offset + 3);
                continue;
            }

            if (streamCommand == 0xFF)
            {
                break;
            }

            if ((streamCommand & 0x80) != 0)
            {
                decoded.Add(new SfxProbeNote(instrument, streamCommand, length, volume, delay, pan));
                delay += length;
            }
        }

        notes = decoded;
        return decoded.Count > 0;
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
        AddVoice(new Voice(
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
        while (q < 6)
        {
            value >>= 1;
            q++;
        }
        while (q > 6)
        {
            value <<= 1;
            q--;
        }

        return value;
    }

    private void FillGeneratorBuffer()
    {
        if (_playback == null)
        {
            return;
        }

        PruneInactiveVoices();
        if (_voices.Count == 0)
        {
            return;
        }

        var framesAvailable = _playback.GetFramesAvailable();
        if (framesAvailable <= 0)
        {
            return;
        }

        var targetFreeFrames = Math.Max(0, GeneratorBufferFrames - TargetQueuedAudioFrames);
        var framesNeeded = Math.Max(0, framesAvailable - targetFreeFrames);
        if (framesNeeded < AudioMixChunkFrames)
        {
            return;
        }

        var chunkCount = Math.Min(framesNeeded / AudioMixChunkFrames, MaxAudioMixChunksPerProcess);
        if (_mixBuffer.Length != AudioMixChunkFrames)
        {
            _mixBuffer = new Vector2[AudioMixChunkFrames];
        }

        var started = Stopwatch.GetTimestamp();
        var mixedFrames = 0;
        for (var chunk = 0; chunk < chunkCount; chunk++)
        {
            for (var i = 0; i < AudioMixChunkFrames; i++)
            {
                _mixBuffer[i] = RenderFrame();
            }

            _playback.PushBuffer(_mixBuffer);
            mixedFrames += AudioMixChunkFrames;
        }

        _debugMixFrames += mixedFrames;
        _debugMixCalls++;
        _debugLastMixFrames = mixedFrames;
        _debugLastMixMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        _debugMixMilliseconds += _debugLastMixMilliseconds;
    }

    private void AddVoice(Voice voice)
    {
        PruneInactiveVoices();
        while (_voices.Count >= MaxVoices)
        {
            _voices.RemoveAt(0);
        }

        _voices.Add(voice);
    }

    private void PruneInactiveVoices()
    {
        for (var i = _voices.Count - 1; i >= 0; i--)
        {
            if (!_voices[i].Active)
            {
                _voices.RemoveAt(i);
            }
        }
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

    private readonly record struct SfxProbeNote(
        int Instrument,
        int Note,
        int DurationFrames,
        float Volume,
        int DelayFrames,
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

    private static readonly MusicEvent[] StarPreviewPattern =
    [
        new(0, 12, 0x70, 8, 0.20f, -0.12f),
        new(4, 9, 0x58, 12, 0.13f, 0.12f),
        new(8, 12, 0x74, 8, 0.20f, -0.08f),
        new(16, 12, 0x77, 8, 0.20f, 0.00f),
        new(20, 9, 0x5C, 12, 0.13f, 0.12f),
        new(24, 12, 0x7C, 8, 0.20f, 0.08f),
        new(32, 12, 0x77, 8, 0.20f, 0.00f),
        new(36, 9, 0x60, 12, 0.13f, 0.12f),
        new(40, 12, 0x74, 8, 0.20f, -0.08f),
        new(48, 12, 0x70, 8, 0.20f, -0.12f),
        new(52, 9, 0x5C, 12, 0.13f, 0.12f),
        new(56, 12, 0x74, 8, 0.20f, -0.08f),
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
