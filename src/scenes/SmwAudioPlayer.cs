using Godot;
using System;
using System.Collections.Generic;
using OpenPlatformerRuntime.Audio;

namespace OpenPlatformerRuntime;

/// <summary>Standalone listening and diagnostics screen; shares gameplay's native audio node.</summary>
public partial class SmwAudioPlayer : Control
{
    public static readonly Vector2I ViewportSize = new(760, 620);
    public SmwAudio Audio { get; set; } = null!;
    public bool Autoplay { get; set; }
    public bool RunSmokeTest { get; set; }
    public event Action? CourseSelectRequested;

    // Labels only. All notes, instruments, timing and samples come from the supplied ROM.
    private static readonly string[][] SongNames =
    [
        ["Piano", "Here We Go!", "Water", "Bowser battle 1", "Boss battle", "Cave", "Ghost house", "Castle",
         "Player died", "Game over", "Boss clear", "Course clear", "Star", "Directional coins", "Keyhole",
         "Keyhole (alternate ID)", "Zoom in", "Switch palace", "Welcome", "Bonus game clear", "Rescue egg",
         "Bowser battle 1 (alternate ID)", "Bowser zoom out", "Bowser zoom in", "Bowser battle 2",
         "Bowser battle 3", "Bowser defeated", "Princess kiss", "Bowser interlude"],
        ["Title screen", "Main overworld", "Yoshi's Island", "Vanilla Dome", "Star Road", "Forest of Illusion",
         "Valley of Bowser", "Valley revealed", "Special World"],
        ["Credits", "Ending fanfare", "Credits continuation", "The end", "Credits (05)", "Ending fanfare (06)",
         "Credits continuation (07)", "The end (08)", "Credits (09)", "Ending fanfare (0A)",
         "Credits continuation (0B)", "The end (0C)"]
    ];

    private OptionButton _banks = null!, _soundGroup = null!, _outputMode = null!;
    private ItemList _songs = null!, _sounds = null!;
    private LineEdit _soundSearch = null!;
    private Button _play = null!, _pause = null!, _stop = null!, _playSound = null!;
    private Label _nowPlaying = null!, _activity = null!, _diagnostics = null!, _fidelity = null!;
    private ProgressBar _left = null!, _right = null!;
    private CheckBox _yoshi = null!;
    private TabContainer _tabs = null!;
    private readonly List<SmwSoundCommand> _visibleSounds = [];
    private double _refreshTime;
    private int _smokeFrame, _pendingDismount;
    private uint _dismountRandom = 0x534D57;
    private bool _heardAudio, _courseSequence, _courseSeen;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = new Theme { DefaultFontSize = 14 };
        var background = new ColorRect { Color = new Color("101c2d"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride($"margin_{edge}", 16);
        AddChild(margin);
        var layout = new VBoxContainer(); layout.AddThemeConstantOverride("separation", 8); margin.AddChild(layout);
        var heading = new HBoxContainer(); layout.AddChild(heading);
        var title = new Label { Text = "SMW Audio Player", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 24); heading.AddChild(title);
        Button(heading, "Course Select", ReturnToMenu).SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        layout.AddChild(new Label { Text = "Original ROM songs, sound commands, percussion and listening controls" });
        _tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; layout.AddChild(_tabs);
        BuildMusicTab(); BuildSoundsTab();
        _tabs.AddChild(new SmwAudioMixer { Name = "Mixer", Audio = Audio });
        BuildOutputControls(layout);
        _diagnostics = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _diagnostics.AddThemeFontSizeOverride("font_size", 12); layout.AddChild(_diagnostics);
        _fidelity = new Label { Text = "DSP fixtures match. Original firmware write timing still differs; full-song identity is unverified.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _fidelity.AddThemeFontSizeOverride("font_size", 12); _fidelity.AddThemeColorOverride("font_color", new Color("a8bacf"));
        layout.AddChild(_fidelity);
        ApplyStartupSelection();
        RefreshStatus(); _play.GrabFocus();
        GD.Print($"smw-audio-player: ready=1 songs=50 sound_commands={SmwSoundCatalog.Commands.Count} mixer=1 stereo_controls=1 yoshi=1 loaded={(Audio.HasNativeData ? 1 : 0)}");
        if (Autoplay && !RunSmokeTest) PlaySelection();
    }

    private void BuildMusicTab()
    {
        var columns = new HBoxContainer { Name = "Music" }; columns.AddThemeConstantOverride("separation", 12); _tabs.AddChild(columns);
        var music = Panel(columns);
        music.AddChild(new Label { Text = "Music bank" });
        _banks = new OptionButton { Name = "MusicBank" };
        foreach (var name in new[] { "Level — 29 songs", "Overworld — 9 songs", "Credits — 12 songs" }) _banks.AddItem(name);
        _banks.ItemSelected += _ => PopulateSongs(); music.AddChild(_banks);
        _songs = new ItemList { Name = "Songs", CustomMinimumSize = new Vector2(288, 150), SizeFlagsVertical = SizeFlags.ExpandFill, AllowReselect = true };
        _songs.ItemActivated += _ => PlaySelection(); music.AddChild(_songs);
        var transport = new HBoxContainer(); music.AddChild(transport);
        _play = Button(transport, "Play / Restart", PlaySelection);
        _pause = Button(transport, "Pause", TogglePause);
        _stop = Button(transport, "Stop all", StopAll);
        PopulateSongs();
        var listening = Panel(columns, scroll: true);
        _nowPlaying = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(285, 50) };
        _nowPlaying.AddThemeFontSizeOverride("font_size", 18); listening.AddChild(_nowPlaying);
        listening.AddChild(new Label { Text = "Stereo source levels" });
        _left = Meter(listening, "L"); _right = Meter(listening, "R");
        _activity = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; listening.AddChild(_activity);
        _yoshi = new CheckBox { Text = "On Yoshi — percussion", TooltipText = "Original Yoshi drum commands. Percussion exists in level songs Piano, Here We Go!, Water and Cave." };
        _yoshi.Toggled += ToggleYoshi; listening.AddChild(_yoshi);
        var help = new Label { Text = "Mount: Yoshi sound. Dismount: jump or hurt.\nPercussion follows the original song data.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        help.AddThemeFontSizeOverride("font_size", 12); listening.AddChild(help);
    }

    private void ApplyStartupSelection()
    {
        int? song = null;
        foreach (var argument in OS.GetCmdlineArgs())
        {
            const string bankPrefix = "--smw-audio-player-bank=";
            const string songPrefix = "--smw-audio-player-song=";
            if (argument.StartsWith(bankPrefix, StringComparison.Ordinal) &&
                Enum.TryParse<SmwMusicBank>(argument[bankPrefix.Length..], true, out var bank) && (uint)bank < 3)
            { _banks.Select((int)bank); PopulateSongs(); }
            else if (argument.StartsWith(songPrefix, StringComparison.Ordinal) && int.TryParse(argument[songPrefix.Length..], out var parsed)) song = parsed;
        }
        if (song is >= 1 && song <= _songs.ItemCount) _songs.Select(song.Value - 1);
    }

    private void BuildSoundsTab()
    {
        var panel = new VBoxContainer { Name = "Sound effects" }; panel.AddThemeConstantOverride("separation", 8); _tabs.AddChild(panel);
        panel.AddChild(new Label { Text = "Original command catalog — effects share the game's eight voices and priority rules", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var filters = new HBoxContainer(); panel.AddChild(filters);
        _soundSearch = new LineEdit { PlaceholderText = "Search coin, circle, Yoshi, firework…", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _soundSearch.TextChanged += _ => PopulateSounds(); filters.AddChild(_soundSearch);
        _soundGroup = new OptionButton();
        foreach (var group in new[] { "All commands", "$1DF9 effects", "$1DFA jump / Yoshi", "$1DFC effects", "Music / transitions" }) _soundGroup.AddItem(group);
        _soundGroup.ItemSelected += _ => PopulateSounds(); filters.AddChild(_soundGroup);
        _sounds = new ItemList { Name = "Sounds", SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 150), AllowReselect = true };
        _sounds.ItemActivated += _ => PlaySoundSelection(); panel.AddChild(_sounds);
        var actions = new HBoxContainer(); panel.AddChild(actions);
        _playSound = Button(actions, "Play selected sound", PlaySoundSelection);
        Button(actions, "Stop all sounds", StopAll);
        panel.AddChild(new Label { Text = "Course clear → circle close previews both original music cues in sequence.\nRemoved IDs are preserved and labeled silent.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        PopulateSounds();
    }

    private void BuildOutputControls(VBoxContainer parent)
    {
        var row = new HBoxContainer(); parent.AddChild(row);
        row.AddChild(new Label { Text = "Output" });
        _outputMode = new OptionButton();
        foreach (var name in new[] { "Stereo", "Mono (L + R)", "Left → both", "Right → both" }) _outputMode.AddItem(name);
        _outputMode.TooltipText = "Mono averages L/R. Opposite-phase surround can cancel; Left/Right → both mirrors one side without that cancellation.";
        _outputMode.ItemSelected += id => Audio.OutputChannels = (SmwAudio.ChannelMode)id; row.AddChild(_outputMode);
        OutputSlider(row, "L", 0, 200, 100, v => Audio.LeftOutputGain = (float)v / 100, "%");
        OutputSlider(row, "R", 0, 200, 100, v => Audio.RightOutputGain = (float)v / 100, "%");
        var volume = new HBoxContainer(); parent.AddChild(volume);
        OutputSlider(volume, "Playback volume", -48, 0, -12, v => Audio.PlaybackVolumeDb = (float)v, " dB");
        Audio.PlaybackVolumeDb = -12;
    }

    private static void OutputSlider(Node parent, string title, double min, double max, double value, Action<double> action, string unit)
    {
        var label = new Label { Text = $"{title} {value:0}{unit}", CustomMinimumSize = new Vector2(title.Length > 2 ? 155 : 60, 0) }; parent.AddChild(label);
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = 1, Value = value, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(85, 0) };
        slider.ValueChanged += v => { label.Text = $"{title} {v:0}{unit}"; action(v); }; parent.AddChild(slider);
    }
    private static VBoxContainer Panel(HBoxContainer parent, bool scroll = false)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var style = new StyleBoxFlat { BgColor = new Color("1c2c43"), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 12, ContentMarginBottom = 12 };
        panel.AddThemeStyleboxOverride("panel", style); parent.AddChild(panel);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; content.AddThemeConstantOverride("separation", 6);
        if (scroll)
        {
            var viewport = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
            panel.AddChild(viewport); viewport.AddChild(content);
        }
        else panel.AddChild(content);
        return content;
    }
    private static Button Button(Node parent, string title, Action action)
    {
        var button = new Button { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        button.Pressed += action; parent.AddChild(button); return button;
    }
    private static ProgressBar Meter(Node parent, string title)
    {
        var row = new HBoxContainer(); parent.AddChild(row); row.AddChild(new Label { Text = title });
        var meter = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter }; row.AddChild(meter); return meter;
    }
    private void PopulateSongs()
    {
        _songs.Clear(); var names = SongNames[_banks.Selected];
        for (var i = 0; i < names.Length; i++) _songs.AddItem($"{i + 1:X2}   {names[i]}");
        _songs.Select(_banks.Selected == 0 ? 1 : 0); _play.Disabled = !Audio.HasNativeData;
    }
    private void PopulateSounds()
    {
        _sounds.Clear(); _visibleSounds.Clear(); int[] ports = [-1, 0, 1, 3, 2];
        foreach (var sound in SmwSoundCatalog.Commands)
        {
            if (_soundGroup.Selected != 0 && sound.Port != ports[_soundGroup.Selected]) continue;
            var text = $"{sound.Name}    ${0x1DF9 + sound.Port:X4}:{sound.Command:X2}";
            if (!text.Contains(_soundSearch.Text, StringComparison.OrdinalIgnoreCase)) continue;
            _sounds.AddItem(text); _visibleSounds.Add(sound);
        }
        if (_visibleSounds.Count > 0) _sounds.Select(0);
        _playSound.Disabled = !Audio.HasNativeData || _visibleSounds.Count == 0;
    }
    private void PlaySelection()
    {
        var selection = _songs.GetSelectedItems(); if (selection.Length == 0 || !Audio.HasNativeData) return;
        _courseSequence = _courseSeen = false; Audio.ResetPlayback();
        Audio.PlaySong((SmwMusicBank)_banks.Selected, (byte)(selection[0] + 1));
        if (_yoshi.ButtonPressed) Audio.SetYoshiMounted(true);
    }
    private void PlaySoundSelection()
    {
        var selected = _sounds.GetSelectedItems(); if (selected.Length != 0) PlaySound(_visibleSounds[selected[0]]);
    }
    private void PlaySound(SmwSoundCommand sound)
    {
        if (sound.MusicCue)
        {
            _courseSequence = sound.CourseSequence; _courseSeen = false;
            Audio.PlaySong(SmwMusicBank.Level, sound.Command);
        }
        else Audio.PlayRawSfx(sound.Port, sound.Command, sound.Name);
    }
    private void StopAll() { _courseSequence = _courseSeen = false; _pendingDismount = 0; Audio.ResetPlayback(); _yoshi.SetPressedNoSignal(false); }
    private void TogglePause() => Audio.SetMusicPaused(!Audio.GetPlaybackStatus().Paused);
    private void ReturnToMenu() { StopAll(); CourseSelectRequested?.Invoke(); }
    private void ToggleYoshi(bool mounted)
    {
        Audio.SetYoshiMounted(mounted);
        if (mounted) { _pendingDismount = 0; Audio.PlayRawSfx(3, 0x1F, "yoshi_mount"); }
        else _pendingDismount = 1; // Drum-off and jump must be separate frames on $1DFA.
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if (!input.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled(); ReturnToMenu();
    }
    public override void _Process(double delta)
    {
        var state = Audio.GetPlaybackStatus(); _left.Value = state.LeftPeak; _right.Value = state.RightPeak;
        _heardAudio |= state.LeftPeak > 0 || state.RightPeak > 0;
        _refreshTime += delta; if (_refreshTime < 0.15) return; _refreshTime = 0; RefreshStatus();
    }
    private void RefreshStatus()
    {
        var state = Audio.GetPlaybackStatus(); var names = SongNames[(int)state.Bank];
        _nowPlaying.Text = state.Failed ? $"Playback stopped: {state.Error}" : !state.Loaded ? "Load a compatible SMW ROM to listen." :
            state.Song == 0 ? "Choose a song and press Play." : $"{state.Bank} · {state.Song:X2}\n{names[Math.Min(state.Song, names.Length) - 1]}";
        _pause.Text = state.Paused ? "Resume" : "Pause"; _pause.Disabled = state.Song == 0 || state.Failed; _stop.Disabled = !state.Loaded;
        var seconds = state.GeneratedFrames / 32000;
        _activity.Text = $"{seconds / 60:00}:{seconds % 60:00}   {(state.Paused ? "Music paused" : "Last SFX: " + state.LastSfx)}\nYoshi drums: {(state.OnYoshi ? "on" : "off")}";
        _diagnostics.Text = $"32 kHz stereo · 8 voices · buffer {(state.QueuedFrames + state.DeviceQueuedFrames) / 32.0:0.0} ms · underruns {state.Skips} · dropped {state.DroppedFrames}\n" +
            $"Listening: {(Audio.Monitor.Enabled ? "monitor mix" : "original DSP mix")} · output {Audio.OutputChannels}";
    }
    public override void _PhysicsProcess(double delta)
    {
        if (_pendingDismount > 0 && --_pendingDismount == 0)
        {
            _dismountRandom ^= _dismountRandom << 13; _dismountRandom ^= _dismountRandom >> 17; _dismountRandom ^= _dismountRandom << 5;
            if ((_dismountRandom & 1) == 0) Audio.PlayJump();
            else { Audio.PlayPlayerHurt(); Audio.PlayRawSfx(3, 0x13, "lose_yoshi"); }
        }
        if (_courseSequence)
        {
            var song = Audio.GetPlaybackStatus().Song;
            if (song == 12) _courseSeen = true;
            else if (song == 0 && _courseSeen) { _courseSequence = false; Audio.PlayCircleClose(); }
        }
        if (RunSmokeTest) RunControlsSmoke();
    }
    private void RunControlsSmoke()
    {
        switch (++_smokeFrame)
        {
            case 1: _play.EmitSignal(BaseButton.SignalName.Pressed); break;
            case 30: Audio.PlayJump(); break;
            case 45: Audio.PlayCoin(); break;
            case 60: _yoshi.ButtonPressed = true; break;
            case 75:
                if (!Audio.GetPlaybackStatus().OnYoshi) { FailSmoke("yoshi_on_failed"); return; }
                _yoshi.ButtonPressed = false; break;
            case 85: _pause.EmitSignal(BaseButton.SignalName.Pressed); break;
            case 130:
                if (!Audio.GetPlaybackStatus().Paused || Audio.GetPlaybackStatus().OnYoshi) { FailSmoke("pause_or_yoshi_off_failed"); return; }
                _pause.EmitSignal(BaseButton.SignalName.Pressed); break;
            case 160: _stop.EmitSignal(BaseButton.SignalName.Pressed); break;
            case 180: SelectAndPlay(SmwMusicBank.Overworld, 2); break;
            case 210: Audio.PlaySpinJump(); break;
            case 240: SelectAndPlay(SmwMusicBank.Credits, 1); break;
            case 275: _stop.EmitSignal(BaseButton.SignalName.Pressed); break;
            case 295: SelectAndPlay(SmwMusicBank.Level, 2); break;
            case 310:
                _tabs.CurrentTab = 1; _soundSearch.Text = "circle";
                _soundSearch.EmitSignal(LineEdit.SignalName.TextChanged, "circle");
                if (_visibleSounds.Count != 2 || _visibleSounds[0].Command != 0x11) { FailSmoke("sound_search_failed"); return; }
                _sounds.Select(0); _playSound.EmitSignal(BaseButton.SignalName.Pressed); break;
            case 325: _outputMode.Select(1); _outputMode.EmitSignal(OptionButton.SignalName.ItemSelected, 1L); _tabs.CurrentTab = 2; break;
            case 340: Audio.Monitor.SetSolo(true, 4, true); Audio.Monitor.Enabled = true; break;
            case 360:
                var state = Audio.GetPlaybackStatus();
                if (!state.Loaded || state.Failed || !_heardAudio || state.DroppedFrames != 0 || Audio.OutputChannels != SmwAudio.ChannelMode.Mono ||
                    _fidelity.GetGlobalRect().End.Y > GetViewportRect().End.Y) { FailSmoke("controls_or_layout_failed"); return; }
                GD.Print("smw-audio-player: smoke=passed banks=3 jump=1 coin=1 spin_jump=1 pause_resume=1 yoshi=1 circle=1 mixer=1 mono=1 nonzero_pcm=1");
                GetTree().Quit(); break;
        }
    }
    private void FailSmoke(string reason) { GD.PrintErr($"smw-audio-player: {reason} {Audio.DebugStatus()}"); GetTree().Quit(1); }
    private void SelectAndPlay(SmwMusicBank bank, int song)
    {
        _banks.Select((int)bank); _banks.EmitSignal(OptionButton.SignalName.ItemSelected, (long)bank);
        _songs.Select(song - 1); _play.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
