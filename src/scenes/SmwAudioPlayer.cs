using Godot;
using System;
using OpenPlatformerRuntime.Audio;

namespace OpenPlatformerRuntime;

/// <summary>Standalone listening and diagnostics screen; shares gameplay's native audio node.</summary>
public partial class SmwAudioPlayer : Control
{
    public static readonly Vector2I ViewportSize = new(640, 480);
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

    private OptionButton _banks = null!;
    private ItemList _songs = null!;
    private Button _play = null!, _pause = null!, _stop = null!;
    private Label _nowPlaying = null!, _activity = null!, _diagnostics = null!, _fidelity = null!;
    private ProgressBar _left = null!, _right = null!;
    private readonly System.Collections.Generic.Dictionary<string, Button> _sfx = [];
    private double _refreshTime;
    private int _smokeFrame;
    private bool _heardAudio;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = new Theme { DefaultFontSize = 14 };
        AddChild(new ColorRect { Color = new Color("101c2d"), MouseFilter = MouseFilterEnum.Ignore });
        GetChild<Control>(0).SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride($"margin_{edge}", 16);
        AddChild(margin);
        var layout = new VBoxContainer(); layout.AddThemeConstantOverride("separation", 8); margin.AddChild(layout);
        var heading = new HBoxContainer(); layout.AddChild(heading);
        var title = new Label { Text = "SMW Audio Player", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 24); heading.AddChild(title);
        Button(heading, "Course Select", ReturnToMenu).SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        layout.AddChild(new Label { Text = "Original songs and sound effects from your ROM" });

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 12); layout.AddChild(columns);
        var music = Panel(columns);
        music.AddChild(new Label { Text = "Music bank" });
        _banks = new OptionButton { Name = "MusicBank" };
        foreach (var name in new[] { "Level — 29 songs", "Overworld — 9 songs", "Credits — 12 songs" }) _banks.AddItem(name);
        _banks.ItemSelected += _ => PopulateSongs(); music.AddChild(_banks);
        _songs = new ItemList
        {
            Name = "Songs", CustomMinimumSize = new Vector2(260, 125), SizeFlagsVertical = SizeFlags.ExpandFill,
            AllowReselect = true,
        };
        _songs.ItemActivated += _ => PlaySelection(); music.AddChild(_songs);
        var transport = new HBoxContainer(); music.AddChild(transport);
        _play = Button(transport, "Play / Restart", PlaySelection);
        _pause = Button(transport, "Pause", TogglePause);
        _stop = Button(transport, "Stop", Audio.ResetPlayback);
        PopulateSongs();

        var effects = Panel(columns);
        effects.AddChild(new Label { Text = "Sound effects" });
        var grid = new GridContainer { Columns = 3 }; effects.AddChild(grid);
        AddSfx(grid, "jump", "Jump", Audio.PlayJump);
        AddSfx(grid, "spin_jump", "Spin jump", Audio.PlaySpinJump);
        AddSfx(grid, "coin", "Coin", Audio.PlayCoin);
        AddSfx(grid, "dragon_coin", "Dragon coin", Audio.PlayDragonCoin);
        AddSfx(grid, "one_up", "1-UP", Audio.PlayOneUp);
        AddSfx(grid, "stomp", "Stomp", () => Audio.PlayStomp(0));
        AddSfx(grid, "block_break", "Block break", Audio.PlayBlockBreak);
        AddSfx(grid, "powerup_reward", "Power-up", Audio.PlayBlockReward);
        AddSfx(grid, "fireball", "Fireball", Audio.PlayFireball);
        AddSfx(grid, "hurt", "Hurt", Audio.PlayPlayerHurt);
        AddSfx(grid, "death", "Death", Audio.PlayDeath);
        AddSfx(grid, "course_clear", "Course clear", Audio.PlayCourseClear);
        _nowPlaying = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(240, 34) };
        _nowPlaying.AddThemeFontSizeOverride("font_size", 14); effects.AddChild(_nowPlaying);
        effects.AddChild(new Label { Text = "Stereo source levels" });
        var meters = new HBoxContainer(); effects.AddChild(meters);
        _left = Meter(meters, "L"); _right = Meter(meters, "R");
        _activity = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; effects.AddChild(_activity);

        var volumeRow = new HBoxContainer(); layout.AddChild(volumeRow);
        volumeRow.AddChild(new Label { Text = "Playback volume" });
        var volume = new HSlider { MinValue = -48, MaxValue = 0, Step = 1, Value = -12, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var volumeLabel = new Label { Text = "−12 dB", CustomMinimumSize = new Vector2(52, 0) };
        volume.ValueChanged += db => { Audio.PlaybackVolumeDb = (float)db; volumeLabel.Text = $"{db:0} dB"; };
        volumeRow.AddChild(volume); volumeRow.AddChild(volumeLabel); Audio.PlaybackVolumeDb = (float)volume.Value;
        _diagnostics = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _diagnostics.AddThemeFontSizeOverride("font_size", 12); layout.AddChild(_diagnostics);
        _fidelity = new Label
        {
            Text = "DSP reference tests match. Original firmware's detailed song timing is still being validated.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _fidelity.AddThemeFontSizeOverride("font_size", 12);
        _fidelity.AddThemeColorOverride("font_color", new Color("a8bacf")); layout.AddChild(_fidelity);
        RefreshStatus(); _play.GrabFocus();
        GD.Print($"smw-audio-player: ready=1 songs=50 sfx=12 loaded={(Audio.HasNativeData ? 1 : 0)}");
        if (Autoplay && !RunSmokeTest) PlaySelection();
    }

    private static VBoxContainer Panel(HBoxContainer parent)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var style = new StyleBoxFlat { BgColor = new Color("1c2c43"), CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 12, ContentMarginBottom = 12 };
        panel.AddThemeStyleboxOverride("panel", style); parent.AddChild(panel);
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 6); panel.AddChild(content);
        return content;
    }

    private static Button Button(Node parent, string title, Action action)
    {
        var button = new Button { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        button.Pressed += action; parent.AddChild(button); return button;
    }

    private void AddSfx(GridContainer grid, string id, string title, Action action)
    {
        var button = Button(grid, title, action);
        button.AddThemeFontSizeOverride("font_size", 12);
        button.CustomMinimumSize = new Vector2(76, 25);
        button.Disabled = !Audio.HasNativeData;
        _sfx.Add(id, button);
    }

    private static ProgressBar Meter(Node parent, string title)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(row); row.AddChild(new Label { Text = title });
        var meter = new ProgressBar { MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddChild(meter); return meter;
    }

    private void PopulateSongs()
    {
        _songs.Clear();
        var names = SongNames[_banks.Selected];
        for (var i = 0; i < names.Length; i++) _songs.AddItem($"{i + 1:X2}   {names[i]}");
        _songs.Select(_banks.Selected == 0 ? 1 : 0);
        _play.Disabled = !Audio.HasNativeData;
    }

    private void PlaySelection()
    {
        var selection = _songs.GetSelectedItems();
        if (selection.Length == 0 || !Audio.HasNativeData) return;
        Audio.ResetPlayback();
        Audio.PlaySong((SmwMusicBank)_banks.Selected, (byte)(selection[0] + 1));
    }

    private void TogglePause() => Audio.SetMusicPaused(!Audio.GetPlaybackStatus().Paused);
    private void ReturnToMenu() { Audio.ResetPlayback(); CourseSelectRequested?.Invoke(); }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!input.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled(); ReturnToMenu();
    }

    public override void _Process(double delta)
    {
        var state = Audio.GetPlaybackStatus();
        _left.Value = state.LeftPeak; _right.Value = state.RightPeak;
        _heardAudio |= state.LeftPeak > 0 || state.RightPeak > 0;
        _refreshTime += delta;
        if (_refreshTime < 0.15) return;
        _refreshTime = 0; RefreshStatus();
    }

    private void RefreshStatus()
    {
        var state = Audio.GetPlaybackStatus();
        var names = SongNames[(int)state.Bank];
        _nowPlaying.Text = state.Failed ? $"Playback stopped: {state.Error}" : !state.Loaded ? "Load a compatible SMW ROM to listen." :
            state.Song == 0 ? "Choose a song and press Play." : $"{state.Bank} · {state.Song:X2}\n{names[Math.Min(state.Song, names.Length) - 1]}";
        _pause.Text = state.Paused ? "Resume" : "Pause";
        _pause.Disabled = state.Song == 0 || state.Failed;
        _stop.Disabled = !state.Loaded;
        var seconds = state.GeneratedFrames / 32000;
        _activity.Text = $"{seconds / 60:00}:{seconds % 60:00}   {(state.Paused ? "Music paused" : "Last SFX: " + state.LastSfx)}";
        _diagnostics.Text = $"32 kHz stereo · 8 voices · buffer {(state.QueuedFrames + state.DeviceQueuedFrames) / 32.0:0.0} ms\n" +
            $"Playback underruns: {state.Skips} · Dropped frames: {state.DroppedFrames}";
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!RunSmokeTest) return;
        switch (++_smokeFrame)
        {
            case 1: _play.EmitSignal(BaseButton.SignalName.Pressed); break;
            case 30: _sfx["jump"].EmitSignal(BaseButton.SignalName.Pressed); break;
            case 45: _sfx["coin"].EmitSignal(BaseButton.SignalName.Pressed); break;
            case 75: _pause.EmitSignal(BaseButton.SignalName.Pressed); break;
            case 115:
                if (!Audio.GetPlaybackStatus().Paused)
                {
                    GD.PrintErr($"smw-audio-player: pause_failed {Audio.DebugStatus()}");
                    GetTree().Quit(1); return;
                }
                _pause.EmitSignal(BaseButton.SignalName.Pressed); break;
            case 130: _stop.EmitSignal(BaseButton.SignalName.Pressed); break;
            case 150: SelectAndPlay(SmwMusicBank.Overworld, 2); break;
            case 180: _sfx["spin_jump"].EmitSignal(BaseButton.SignalName.Pressed); break;
            case 210: SelectAndPlay(SmwMusicBank.Credits, 1); break;
            case 250: _stop.EmitSignal(BaseButton.SignalName.Pressed); break;
            case 270: SelectAndPlay(SmwMusicBank.Level, 2); break;
            case 300:
                var state = Audio.GetPlaybackStatus();
                if (!state.Loaded || state.Failed || !_heardAudio || state.Song != 2 || state.DroppedFrames != 0 ||
                    _fidelity.GetGlobalRect().End.Y > GetViewportRect().End.Y)
                {
                    GD.PrintErr($"smw-audio-player: smoke_failed {Audio.DebugStatus()}");
                    GetTree().Quit(1); return;
                }
                GD.Print("smw-audio-player: smoke=passed banks=3 jump=1 coin=1 spin_jump=1 pause_resume=1 restart=1 nonzero_pcm=1");
                GetTree().Quit(); break;
        }
    }

    private void SelectAndPlay(SmwMusicBank bank, int song)
    {
        _banks.Select((int)bank); _banks.EmitSignal(OptionButton.SignalName.ItemSelected, (long)bank);
        _songs.Select(song - 1); _play.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
