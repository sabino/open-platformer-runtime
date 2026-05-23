using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public partial class Main : Node2D
{
    private static readonly Vector2I DefaultWindowSize = new(768, 672);

    private Control? _menu;
    private GameScene? _game;
    private SmwAudio? _audio;
    private ColorRect? _gameBackground;
    private bool _debugOverlays;

    public override void _Ready()
    {
        if (!DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase))
        {
            GetWindow().Transparent = false;
            GetViewport().TransparentBg = false;
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Transparent, false);
            DisplayServer.WindowSetSize(DefaultWindowSize);
        }

        SetupInputMap();
        _audio = new SmwAudio { Name = "SmwAudio" };
        AddChild(_audio);
        ShowMenu();

        var autostart = false;
        string? testLevel = null;
        string? capturePath = null;
        string? audioPreview = null;
        Vector2? testSpawn = null;
        int? testPowerup = null;
        int? testScreenExit = null;
        var captureFrames = 8;
        foreach (var arg in OS.GetCmdlineArgs())
        {
            if (arg == "--smw-test-autostart")
            {
                autostart = true;
            }
            else if (arg == "--smw-debug-overlays")
            {
                _debugOverlays = true;
            }
            else if (arg.StartsWith("--smw-test-level=", StringComparison.Ordinal))
            {
                testLevel = arg["--smw-test-level=".Length..].ToUpperInvariant();
            }
            else if (arg.StartsWith("--smw-capture=", StringComparison.Ordinal))
            {
                capturePath = arg["--smw-capture=".Length..];
                autostart = true;
            }
            else if (arg.StartsWith("--smw-audio-preview=", StringComparison.Ordinal))
            {
                audioPreview = arg["--smw-audio-preview=".Length..];
            }
            else if (arg.StartsWith("--smw-test-spawn=", StringComparison.Ordinal))
            {
                testSpawn = ParseTestSpawn(arg["--smw-test-spawn=".Length..]);
                autostart = true;
            }
            else if (arg.StartsWith("--smw-test-powerup=", StringComparison.Ordinal))
            {
                testPowerup = ParseTestPowerup(arg["--smw-test-powerup=".Length..]);
                autostart = true;
            }
            else if (arg.StartsWith("--smw-test-screen-exit=", StringComparison.Ordinal))
            {
                testScreenExit = ParseHexOrDecimal(arg["--smw-test-screen-exit=".Length..]);
                autostart = true;
            }
            else if (arg.StartsWith("--smw-capture-frames=", StringComparison.Ordinal) &&
                int.TryParse(arg["--smw-capture-frames=".Length..], out var parsedFrames))
            {
                captureFrames = Math.Max(1, parsedFrames);
            }
        }

        if (autostart || testLevel != null || capturePath != null)
        {
            StartGame();
        }
        if (testLevel != null)
        {
            _game?.DebugEnterLevel(testLevel);
        }
        if (testSpawn != null)
        {
            _game?.DebugSetPlayerPosition(testSpawn.Value);
        }
        if (testPowerup != null)
        {
            _game?.DebugSetPlayerPowerup(testPowerup.Value);
        }
        if (testScreenExit != null)
        {
            _game?.DebugEnterScreenExit(testScreenExit.Value);
        }
        if (capturePath != null)
        {
            _game?.DebugCaptureViewport(capturePath, captureFrames, quitAfterCapture: true);
        }
        if (audioPreview != null)
        {
            _audio?.PlayMusicPreview(audioPreview);
        }
    }

    private static Vector2? ParseTestSpawn(string value)
    {
        var parts = value.Split(',', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            GD.PrintErr($"smw-test-spawn: expected x,y but got {value}");
            return null;
        }
        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            GD.PrintErr($"smw-test-spawn: invalid coordinates {value}");
            return null;
        }

        return new Vector2(x, y);
    }

    private static int? ParseHexOrDecimal(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
            return int.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)
                ? hex
                : null;
        }

        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec)
            ? dec
            : int.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bareHex)
                ? bareHex
                : null;
    }

    private static int? ParseTestPowerup(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var powerup))
        {
            return powerup;
        }

        return normalized switch
        {
            "small" => SmwPhysics.SmallPowerup,
            "big" => SmwPhysics.BigPowerup,
            "cape" => SmwPhysics.CapePowerup,
            "fire" => SmwPhysics.FirePowerup,
            _ => null,
        };
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
            Position = new Vector2(12, 10),
            CustomMinimumSize = new Vector2(232, 196),
        };
        _menu.AddChild(panel);

        var title = new Label { Text = "Open Platformer Runtime" };
        title.AddThemeFontSizeOverride("font_size", 14);
        panel.AddChild(title);

        var status = new Label { Text = AssetStatusText() };
        status.AddThemeFontSizeOverride("font_size", 8);
        panel.AddChild(status);

        var start = new Button { Text = "Start YI1 Slice" };
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
        title.AddThemeFontSizeOverride("font_size", 10);
        panel.AddChild(title);

        var samples = new HBoxContainer();
        panel.AddChild(samples);
        AddCommandButton(samples, "Jump", () => _audio?.PlayJump());
        AddCommandButton(samples, "2-note", () => _audio?.PlaySpinJump());
        AddSampleButton(samples, 9, "BRR09");

        var musicTitle = new Label { Text = "Music Banks" };
        musicTitle.AddThemeFontSizeOverride("font_size", 10);
        panel.AddChild(musicTitle);

        foreach (var line in MusicBankLines())
        {
            var label = new Label { Text = line };
            label.AddThemeFontSizeOverride("font_size", 8);
            panel.AddChild(label);
        }

        var musicButtons = new HBoxContainer();
        panel.AddChild(musicButtons);
        foreach (var name in new[] { "Level", "Overworld", "Credits" })
        {
            var bankPath = $"res://generated/smw/audio/spc_{name.ToLowerInvariant()}_music_bank.bin";
            var button = new Button
            {
                Text = name,
                Disabled = !FileAccess.FileExists(bankPath),
                TooltipText = "Internal BRR sequencer preview; exact SPC/DSP song playback is still pending.",
            };
            button.Pressed += () => _audio?.PlayMusicPreview(name);
            musicButtons.AddChild(button);
        }

        var stop = new Button
        {
            Text = "Stop",
            TooltipText = "Stop the internal sequencer preview.",
        };
        stop.Pressed += () => _audio?.StopMusicPreview();
        musicButtons.AddChild(stop);
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
        EnsureGameBackground();
        _audio?.PlayMenuStart();

        _game = new GameScene
        {
            Name = "GameScene",
            DebugOverlays = _debugOverlays,
        };
        AddChild(_game);
    }

    private void EnsureGameBackground()
    {
        if (_gameBackground != null)
        {
            return;
        }

        _gameBackground = new ColorRect
        {
            Name = "OpaqueGameBackground",
            Color = new Color(0.0f, 0.39f, 0.74f, 1.0f),
            Position = new Vector2(-4096, -4096),
            Size = new Vector2(16384, 16384),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = -4000,
        };
        AddChild(_gameBackground);
    }
}
