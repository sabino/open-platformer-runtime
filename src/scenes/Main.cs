using Godot;
using System;
using System.Collections.Generic;

public partial class Main : Node2D
{
    private Control? _menu;
    private GameScene? _game;
    private SmwAudio? _audio;

    public override void _Ready()
    {
        SetupInputMap();
        _audio = new SmwAudio { Name = "SmwAudio" };
        AddChild(_audio);
        ShowMenu();

        foreach (var arg in OS.GetCmdlineArgs())
        {
            if (arg == "--smw-test-autostart")
            {
                StartGame();
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_game == null && @event.IsActionPressed("ui_accept"))
        {
            StartGame();
        }
    }

    private static void SetupInputMap()
    {
        AddKeyAction("smw_left", Key.Left, Key.A);
        AddKeyAction("smw_right", Key.Right, Key.D);
        AddKeyAction("smw_down", Key.Down, Key.S);
        AddKeyAction("smw_jump", Key.Z, Key.Space);
        AddKeyAction("smw_spin", Key.X);
        AddKeyAction("smw_run", Key.Shift, Key.C);
        AddKeyAction("smw_start", Key.Enter);
    }

    private static void AddKeyAction(StringName action, params Key[] keys)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }

        foreach (var key in keys)
        {
            var exists = false;
            foreach (var existing in InputMap.ActionGetEvents(action))
            {
                if (existing is InputEventKey keyEvent && keyEvent.PhysicalKeycode == key)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                var inputEvent = new InputEventKey { PhysicalKeycode = key };
                InputMap.ActionAddEvent(action, inputEvent);
            }
        }
    }

    private void ShowMenu()
    {
        _menu = new Control();
        _menu.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_menu);

        var background = new ColorRect
        {
            Color = new Color(0.04f, 0.06f, 0.08f, 1.0f),
        };
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _menu.AddChild(background);

        var panel = new VBoxContainer
        {
            Position = new Vector2(44, 44),
            CustomMinimumSize = new Vector2(600, 360),
        };
        _menu.AddChild(panel);

        var title = new Label { Text = "Open Platformer Runtime" };
        title.AddThemeFontSizeOverride("font_size", 28);
        panel.AddChild(title);

        var status = new Label { Text = AssetStatusText() };
        status.AddThemeFontSizeOverride("font_size", 14);
        panel.AddChild(status);

        var start = new Button { Text = "Start Yoshi Island 1 Slice" };
        start.Pressed += StartGame;
        panel.AddChild(start);

        AddAudioTester(panel);
    }

    private static string AssetStatusText()
    {
        return FileAccess.FileExists("res://generated/smw/manifest.json")
            ? "Generated SMW asset pack found."
            : "No generated asset pack found. The playable slice will use a placeholder level.";
    }

    private void AddAudioTester(VBoxContainer panel)
    {
        var title = new Label { Text = "Internal APU Probe" };
        title.AddThemeFontSizeOverride("font_size", 18);
        panel.AddChild(title);

        var samples = new HBoxContainer();
        panel.AddChild(samples);
        AddCommandButton(samples, "Jump 01", () => _audio?.PlayJump());
        AddCommandButton(samples, "Two-note 04", () => _audio?.PlaySpinJump());
        AddSampleButton(samples, 9, "BRR 09");

        var musicTitle = new Label { Text = "Music Banks" };
        musicTitle.AddThemeFontSizeOverride("font_size", 18);
        panel.AddChild(musicTitle);

        foreach (var line in MusicBankLines())
        {
            var label = new Label { Text = line };
            label.AddThemeFontSizeOverride("font_size", 12);
            panel.AddChild(label);
        }

        var musicButtons = new HBoxContainer();
        panel.AddChild(musicButtons);
        foreach (var name in new[] { "Level", "Overworld", "Credits" })
        {
            var button = new Button
            {
                Text = name,
                Disabled = true,
                TooltipText = "Imported, but exact SPC/DSP command sequencing is not ported yet.",
            };
            musicButtons.AddChild(button);
        }
    }

    private void AddSampleButton(HBoxContainer parent, int sampleId, string label)
    {
        var button = new Button
        {
            Text = label,
            Disabled = _audio == null || !_audio.HasSample(sampleId),
        };
        button.Pressed += () => _audio?.PlaySample(sampleId);
        parent.AddChild(button);
    }

    private static void AddCommandButton(HBoxContainer parent, string label, Action action)
    {
        var button = new Button { Text = label };
        button.Pressed += action;
        parent.AddChild(button);
    }

    private static IEnumerable<string> MusicBankLines()
    {
        var banks = new (string Label, string Path)[]
        {
            ("Level", "res://generated/smw/audio/spc_level_music_bank.bin"),
            ("Overworld", "res://generated/smw/audio/spc_overworld_music_bank.bin"),
            ("Credits", "res://generated/smw/audio/spc_credits_music_bank.bin"),
        };

        foreach (var bank in banks)
        {
            if (!FileAccess.FileExists(bank.Path))
            {
                yield return $"{bank.Label}: missing";
                continue;
            }

            using var file = FileAccess.Open(bank.Path, FileAccess.ModeFlags.Read);
            yield return $"{bank.Label}: {file?.GetLength() ?? 0} bytes imported";
        }
    }

    private void StartGame()
    {
        if (_game != null)
        {
            return;
        }

        _menu?.QueueFree();
        _menu = null;
        _audio?.PlayMenuStart();

        _game = new GameScene { Name = "GameScene" };
        AddChild(_game);
    }
}
