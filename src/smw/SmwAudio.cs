using Godot;
using System;
using System.Diagnostics;
using System.IO;
using FileAccess = Godot.FileAccess;
using OpenPlatformerRuntime.Audio;
using OpenPlatformerRuntime.SmwAssets;

namespace OpenPlatformerRuntime;

/// <summary>Godot device sink for native, integer SMW sequencing and S-DSP PCM.</summary>
public partial class SmwAudio : Node
{
    private const int SampleRate = 32000;
    private const int PlaybackChunk = 512;
    private const int QueueFrames = 16384;
    private readonly short[] _pcm = new short[1068];
    private readonly Vector2[] _queue = new Vector2[QueueFrames];
    private readonly Vector2[] _playbackChunk = new Vector2[PlaybackChunk];
    private readonly byte[] _pendingCommands = new byte[4];
    private SmwAudioData? _data;
    private NativeSmwAudio? _engine;
    private AudioStreamPlayer? _player;
    private AudioStreamGeneratorPlayback? _playback;
    private int _read, _write, _queued;
    private long _frames, _droppedFrames, _underruns, _renderedFrames;
    private long _lastTriggerClock, _lastKeyOnClock = -1;
    private int _playbackCapacity;
    private double _renderMilliseconds;
    private bool _loaded, _failed;
    private SmwMusicBank? _pendingBank;
    private int? _sampleProbe;
    private string _source = "none", _lastSfx = "none";
    private string _lastError = "";
    private int _leftPeak, _rightPeak;
    private int _lastPort, _lastCommand;

    public SmwRom? Rom { get; set; }
    public bool ClockedByGameplay { get; set; }
    public static readonly int[] ProbeSampleIds = [7, 8, 9, 14, 16];
    public int LoadedProbeSampleCount { get { EnsureLoaded(); return _engine != null ? ProbeSampleIds.Length : 0; } }
    public bool HasNativeData { get { EnsureLoaded(); return _engine != null; } }

    public readonly record struct PlaybackStatus(bool Loaded, bool Failed, string Error, SmwMusicBank Bank,
        byte Song, bool Paused, long GeneratedFrames, long Skips, long DroppedFrames,
        int QueuedFrames, int DeviceQueuedFrames, float LeftPeak, float RightPeak, string LastSfx);

    public PlaybackStatus GetPlaybackStatus() => new(_engine != null, _failed, _lastError,
        _engine?.Bank ?? SmwMusicBank.Level, _engine?.Driver.Song ?? 0, (_engine?.Driver.smw_pause_music ?? 0) != 0,
        _renderedFrames, _underruns, _droppedFrames, _queued,
        Math.Max(0, _playbackCapacity - (_playback?.GetFramesAvailable() ?? 0)),
        _leftPeak / 32768f, _rightPeak / 32768f, _lastSfx);

    // Application volume is deliberately outside the raw, reference-tested PCM boundary.
    public float PlaybackVolumeDb
    {
        get => _player?.VolumeDb ?? 0;
        set { EnsureLoaded(); if (_player != null) _player.VolumeDb = Math.Clamp(value, -60, 6); }
    }

    public override void _Ready()
    {
        EnsureLoaded();
        GD.Print($"smw-audio: native_dsp=1 source={_source} samples={LoadedProbeSampleCount} voices=8 samplerate={SampleRate} cpu_emulation=0");
    }

    // The physics frame defines event timestamps and sample count. Process delta
    // and device consumption never alter the native audio simulation.
    public override void _PhysicsProcess(double delta)
    {
        if (!ClockedByGameplay) AdvanceSimulationFrame();
    }
    public override void _Process(double delta) => DrainPlaybackQueue();
    public override void _ExitTree()
    {
        _player?.Stop();
        _playback?.Dispose();
        _playback = null;
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (Rom != null) { _data = SmwAudioData.FromRom(Rom); _source = "rom"; }
            else
            {
                var path = SmwAssetPaths.Path("audio/native_audio_data.bin");
                if (FileAccess.FileExists(path)) { _data = SmwAudioData.FromBinary(FileAccess.GetFileAsBytes(path)); _source = "native-cache"; }
                else
                {
                    string[] names = ["spc_engine", "spc_samples", "spc_level_music_bank", "spc_overworld_music_bank", "spc_credits_music_bank"];
                    var banks = new byte[5][];
                    for (var i = 0; i < names.Length; i++)
                    {
                        var bankPath = SmwAssetPaths.Path($"audio/{names[i]}.bin");
                        if (!FileAccess.FileExists(bankPath)) return;
                        banks[i] = FileAccess.GetFileAsBytes(bankPath);
                    }
                    _data = SmwAudioData.FromUploadBanks("legacy-unverified-bank-cache", banks[0], banks[1], banks[2], banks[3], banks[4]);
                    _source = "legacy-bank-cache";
                }
            }
            _engine = new NativeSmwAudio(_data);
            _engine.Driver.RegisterWritten += ObserveRegisterWrite;
            var stream = new AudioStreamGenerator
            {
                MixRateMode = AudioStreamGenerator.AudioStreamGeneratorMixRate.Custom,
                MixRate = SampleRate,
                BufferLength = 0.1f,
            };
            _player = new AudioStreamPlayer { Name = "NativeSmwPcm", Stream = stream, Bus = "Master" };
            AddChild(_player); _player.Play();
            _playback = _player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
            _playbackCapacity = _playback?.GetFramesAvailable() ?? 0;
        }
        catch (Exception exception)
        {
            _failed = true;
            _lastError = exception.Message;
            GD.PrintErr($"smw-audio: load_failed={exception.Message}");
        }
    }

    public void AdvanceSimulationFrame()
    {
        EnsureLoaded();
        if (_engine == null || _failed) return;
        try
        {
            var rate = Engine.PhysicsTicksPerSecond;
            var clock = NativeSmwAudio.FrameToClock(_frames, rate);
            if (_pendingBank != null)
            {
                _engine.ScheduleMusic(clock, _pendingBank.Value, _pendingCommands[2]);
                _pendingBank = null;
            }
            else _engine.ScheduleCommand(clock, 2, _pendingCommands[2]);
            _engine.ScheduleCommand(clock, 0, _pendingCommands[0]);
            _engine.ScheduleCommand(clock, 1, _pendingCommands[1]);
            _engine.ScheduleCommand(clock, 3, _pendingCommands[3]);
            Array.Clear(_pendingCommands);
            if (_sampleProbe is int sample)
            {
                _sampleProbe = null;
                int[] registers = [0x74,0x72,0x73,0x75,0x77,0x70,0x71,0x5C,0x4C];
                byte[] values = [(byte)sample,0,16,0,127,56,56,0,128];
                for (var i=0;i<registers.Length;i++) _engine.ScheduleRegisterWrite(clock,registers[i],values[i]);
            }
            var target = (++_frames * SampleRate) / rate;
            var count = checked((int)(target - _engine.Dsp.GeneratedFrames));
            if (count > _pcm.Length / 2) throw new NotSupportedException("Audio frame rate is below the supported PCM block capacity");
            var started = Stopwatch.GetTimestamp();
            _engine.Generate(_pcm.AsSpan(0, count * 2));
            _renderMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            _renderedFrames += count;
            _leftPeak = _rightPeak = 0;
            for (var i = 0; i < count; i++)
            {
                _leftPeak = Math.Max(_leftPeak, Math.Abs((int)_pcm[i * 2]));
                _rightPeak = Math.Max(_rightPeak, Math.Abs((int)_pcm[i * 2 + 1]));
                if (_queued == QueueFrames) { _read = (_read + 1) % QueueFrames; _queued--; _droppedFrames++; }
                // Every s16 / 32768 value is exactly representable as float32.
                // Device resampling, bus effects and volume are outside the raw PCM boundary.
                _queue[_write] = new Vector2(_pcm[i*2] / 32768.0f, _pcm[i*2+1] / 32768.0f);
                _write = (_write + 1) % QueueFrames; _queued++;
            }
        }
        catch (NotSupportedException exception)
        {
            _failed = true;
            _lastError = exception.Message;
            GD.PrintErr($"smw-audio: unsupported={exception.Message} {_engine.Driver.DescribeState()}");
        }
    }

    private void DrainPlaybackQueue()
    {
        if (_playback == null) return;
        _underruns = _playback.GetSkips();
        while (_queued >= PlaybackChunk && _playback.GetFramesAvailable() >= PlaybackChunk)
        {
            for (var i=0;i<PlaybackChunk;i++) _playbackChunk[i] = _queue[(_read+i) % QueueFrames];
            if (!_playback.PushBuffer(_playbackChunk)) break;
            _read = (_read + PlaybackChunk) % QueueFrames; _queued -= PlaybackChunk;
        }
    }

    private void Trigger(string name, int port, byte command)
    {
        EnsureLoaded();
        if (_engine == null || _failed) return;
        _pendingCommands[port] = command;
        _lastSfx = name; _lastPort = port; _lastCommand = command;
        _lastTriggerClock = NativeSmwAudio.FrameToClock(_frames, Engine.PhysicsTicksPerSecond);
        _lastKeyOnClock = -1;
        GD.Print($"smw-audio: sfx={name} port={port} command={command:X2} native=1 frame={_frames} scheduled_clock={NativeSmwAudio.FrameToClock(_frames, Engine.PhysicsTicksPerSecond)}");
    }

    // Raw driver ports: 0=$1DF9, 1=$1DFA (jump), 2=$1DFB (music), 3=$1DFC.
    public void PlayMenuStart() => Trigger("menu_start",3,0x29);
    public void PlayJump() => Trigger("jump",1,1);
    public void PlaySpinJump() => Trigger("spin_jump",3,4);
    public void PlayCoin() => Trigger("coin",3,1);
    public void PlayDragonCoin() => Trigger("dragon_coin",0,0x1C);
    public void PlayOneUp() => Trigger("one_up",3,5);
    public void PlayStomp(int rewardIndex) => Trigger("stomp",0,(byte)(0x13+Math.Clamp(rewardIndex,0,6)));
    public void PlayBlockBreak() => Trigger("block_break",0,8);
    public void PlayBlockReward() => Trigger("powerup_reward",3,0x0A);
    public void PlayFireball() => Trigger("fireball",3,6);
    public void PlayPlayerHurt() => Trigger("hurt",0,4);
    public void PlayDeath() { _lastSfx="death"; PlaySong(SmwMusicBank.Level,9); }
    public void PlayCourseClear() { _lastSfx="course_clear"; PlaySong(SmwMusicBank.Level,12); }

    public bool PlayNamedSfx(string name)
    {
        switch(name.ToLowerInvariant())
        {
            case "coin": PlayCoin(); break;
            case "dragon": case "dragon_coin": PlayDragonCoin(); break;
            case "oneup": case "one-up": case "1up": PlayOneUp(); break;
            case "stomp": PlayStomp(0); break;
            case "block": case "break": case "block_break": PlayBlockBreak(); break;
            case "reward": case "powerup": PlayBlockReward(); break;
            case "fireball": case "fire": PlayFireball(); break;
            case "hurt": PlayPlayerHurt(); break;
            case "death": PlayDeath(); break;
            case "clear": case "course_clear": PlayCourseClear(); break;
            default: return false;
        }
        return true;
    }

    public bool HasSample(int sampleId) { EnsureLoaded(); return _engine != null && (uint)sampleId < 20; }
    public void PlaySample(int sampleId) { if (HasSample(sampleId)) _sampleProbe = sampleId; }
    public string PlaySampleProbe(int sampleId)
    {
        var available=HasSample(sampleId); if(available) PlaySample(sampleId);
        return $"sample={sampleId:X2} available={(available?1:0)} samples={LoadedProbeSampleCount}";
    }
    public void PlaySong(SmwMusicBank bank, byte song)
    {
        EnsureLoaded(); if (_engine == null || _failed) return;
        if (_engine.Driver.smw_pause_music != 0) _pendingCommands[0] = 0x12;
        _pendingBank=bank; _pendingCommands[2]=song;
        GD.Print($"smw-audio: song bank={bank} id={song:X2} source={_source}");
    }
    public void PlayLevelMusic(int headerMusicIndex)
    {
        EnsureLoaded();
        if (_data == null) return;
        try { PlaySong(SmwMusicBank.Level,_data.LevelSong(headerMusicIndex)); }
        catch (NotSupportedException exception) { GD.PrintErr($"smw-audio: unsupported={exception.Message}"); }
    }
    public void PlayMusicPreview(string bankName)
    {
        switch(bankName)
        {
            case "Overworld": PlaySong(SmwMusicBank.Overworld,2); break;
            case "Credits": PlaySong(SmwMusicBank.Credits,1); break;
            case "Star": PlaySong(SmwMusicBank.Level,13); break;
            default: PlaySong(SmwMusicBank.Level,2); break;
        }
        GD.Print($"smw-audio: music_preview={bankName} source=original-sequence");
    }
    public void StopMusicPreview()
    {
        EnsureLoaded(); if (_engine == null) return;
        Trigger("pause_music",0,0x11);
    }
    public void SetMusicPaused(bool paused) => Trigger(paused ? "pause_music" : "resume_music",0,paused ? (byte)0x11 : (byte)0x12);

    /// <summary>Stop all voices and reset the standalone player, including failed sequence diagnostics.</summary>
    public void ResetPlayback()
    {
        EnsureLoaded();
        if (_data == null) return;
        _engine = new NativeSmwAudio(_data);
        _engine.Driver.RegisterWritten += ObserveRegisterWrite;
        Array.Clear(_pendingCommands);
        _pendingBank = null; _sampleProbe = null;
        _frames = _renderedFrames = _droppedFrames = _underruns = 0;
        _read = _write = _queued = _leftPeak = _rightPeak = 0;
        _renderMilliseconds = 0; _failed = false; _lastError = ""; _lastSfx = "none";
        _lastKeyOnClock = -1;
        _player?.Stop(); _playback?.Dispose(); _player?.Play();
        _playback = _player?.GetStreamPlayback() as AudioStreamGeneratorPlayback;
        _playbackCapacity = _playback?.GetFramesAvailable() ?? 0;
    }
    private void ObserveRegisterWrite(long clock, int register, byte value)
    {
        var mask = _lastPort == 0 ? 0x10 : _lastPort == 1 ? 0x80 : _lastPort == 3 ? 0x40 : 0;
        if (_lastKeyOnClock < 0 && clock >= _lastTriggerClock && register == 0x4C && (value & mask) != 0 &&
            _engine?.Driver.CurrentCommand(_lastPort) == _lastCommand) _lastKeyOnClock = clock;
    }
    public string DebugStatus()
    {
        EnsureLoaded();
        return $"loaded={(_engine!=null?1:0)} failed={(_failed?1:0)} source={_source} backend=native_dsp samples={LoadedProbeSampleCount} voices=8 " +
            $"music={(_engine?.Driver.Song??0):X2} simulation_frames={_frames} generated_frames={_renderedFrames} queued_frames={_queued} " +
            $"playback_skips={_underruns} dropped_frames={_droppedFrames} queued_ms={_queued*1000.0/SampleRate:0.00} " +
            $"device_queued_frames={Math.Max(0,_playbackCapacity-(_playback?.GetFramesAvailable()??0))} " +
            $"dsp_command_latency_ms={(_lastKeyOnClock < 0 ? -1 : (_lastKeyOnClock-_lastTriggerClock)/1024.0):0.000} " +
            $"render_ms={_renderMilliseconds:0.000} last_sfx={_lastSfx} last_sfx_port={_lastPort} last_sfx_cmd={_lastCommand:X2} last_sfx_native=1 " +
            (_engine == null ? "" : _engine.Driver.DescribeState()+" "+_engine.Dsp.DescribeState());
    }

    public byte[]? CaptureNativeState()
    {
        if (_engine == null) return null;
        using var buffer=new MemoryStream(); using var writer=new BinaryWriter(buffer);
        writer.Write(1); writer.Write(_frames); writer.Write(Engine.PhysicsTicksPerSecond);
        writer.Write(_pendingCommands); writer.Write(_pendingBank is null ? -1 : (int)_pendingBank.Value);
        writer.Write(_sampleProbe??-1);
        var state=_engine.CaptureState(); writer.Write(state.Length); writer.Write(state);
        return buffer.ToArray();
    }

    public void RestoreNativeState(byte[] state)
    {
        EnsureLoaded(); if(_engine==null) throw new InvalidOperationException("Checkpoint requires native audio data");
        using var buffer=new MemoryStream(state); using var reader=new BinaryReader(buffer);
        if(reader.ReadInt32()!=1) throw new InvalidDataException("Native Godot audio checkpoint schema mismatch");
        var frames=reader.ReadInt64();
        if(reader.ReadInt32()!=Engine.PhysicsTicksPerSecond) throw new InvalidDataException("Checkpoint uses a different game/audio timebase");
        reader.ReadBytes(4).CopyTo(_pendingCommands,0);
        var bank=reader.ReadInt32(); _pendingBank=bank<0?null:(SmwMusicBank)bank;
        var probe=reader.ReadInt32(); _sampleProbe=probe<0?null:probe;
        var length=reader.ReadInt32(); if(length<=0||length>150000) throw new InvalidDataException("Invalid native audio checkpoint length");
        _engine.RestoreState(reader.ReadBytes(length));
        if(buffer.Position!=buffer.Length) throw new InvalidDataException("Trailing native Godot audio checkpoint bytes");
        _frames=frames; _renderedFrames=_engine.Dsp.GeneratedFrames; _read=_write=_queued=0; _failed=false;
        _player?.Stop(); _playback?.Dispose(); _player?.Play();
        _playback=_player?.GetStreamPlayback() as AudioStreamGeneratorPlayback;
        _playbackCapacity=_playback?.GetFramesAvailable()??0;
    }
}
