using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using IOException = System.IO.IOException;
using IoPath = System.IO.Path;

namespace OpenPlatformerRuntime;

public partial class Main : Node2D
{
    private static readonly Vector2I LogicalViewportSize = new(256, 224);
    private static readonly Vector2I DefaultWindowSize = new(LogicalViewportSize.X * 3, LogicalViewportSize.Y * 3);
    private const string DefaultLevelId = "105";
    private static string ManifestPath => SmwAssetPaths.ManifestPath;
    private static string DefaultMenuLevelPreviewPath => SmwAssetPaths.Path("levels/level_105_partial_layout.png");
    private static string MenuPlayerPreviewPath => SmwAssetPaths.Path("player/gfx32_player_palette0.png");

    private Control? _menu;
    private GameScene? _game;
    private SmwAudio? _audio;
    private ColorRect? _gameBackground;
    private TextureRect? _menuLevelPreview;
    private TextureRect? _selectedLevelPreview;
    private Label? _selectedLevelLabel;
    private Label? _selectedLevelTitle;
    private Label? _levelSearchStatus;
    private Label? _assetStatusLabel;
    private Button? _startButton;
    private OptionButton? _levelSelect;
    private LineEdit? _levelSearch;
    private ItemList? _levelList;
    private CheckBox? _audioToggle;
    private CheckBox? _debugToggle;
    private CheckBox? _actorsToggle;
    private CheckBox? _actorVisualsToggle;
    private readonly List<ImportedLevelEntry> _importedLevels = [];
    private readonly List<ImportedLevelEntry> _visibleLevels = [];
    private readonly Dictionary<string, ImportedLevelEntry> _webIndexedLevels = new(StringComparer.Ordinal);
    private string _selectedLevelId = DefaultLevelId;
    private string _menuLevelPreviewPath = DefaultMenuLevelPreviewPath;
    private bool _debugOverlays = true;
    private bool _audioEnabled;
    private bool _actorsEnabled = true;
    private bool _actorVisualsEnabled = true;
    private JavaScriptObject? _webBridgeCallback;
    private int _webImportedFileCount;

    private sealed record ImportedLevelEntry(
        string Id,
        string Name,
        string DisplayName,
        string TitleSource,
        string? PreviewPath,
        int ObjectCount,
        int SpriteCount,
        int ScreenExitCount,
        bool IsGenerated);

    public override void _Ready()
    {
        Engine.MaxFps = 60;
        var webDisplay = IsWebDisplay();
        if (!webDisplay &&
            !DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase))
        {
            GetWindow().Transparent = false;
            GetViewport().TransparentBg = false;
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Transparent, false);
            DisplayServer.WindowSetSize(DefaultWindowSize);
        }

        SetupInputMap();
        SetupWebAssetBridge(webDisplay);
        LoadImportedLevelList();
        _audioEnabled = ShouldEnableAudio();
        ApplyInitialMenuArgs();
        ApplyInitialLevelArg();
        if (_audioEnabled)
        {
            _audio = new SmwAudio { Name = "SmwAudio" };
            AddChild(_audio);
        }
        else
        {
            GD.Print("smw-audio: disabled=1");
        }

        ShowMenu();

        var autostart = false;
        string? testLevel = null;
        string? capturePath = null;
        string? audioPreview = null;
        int? audioSamplePreview = null;
        string? inputScriptPath = null;
        string? autoplayMode = null;
        string? debugCommandPath = null;
        int? debugRconPort = null;
        var titleStart = false;
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
            else if (arg == "--smw-title-start")
            {
                titleStart = true;
            }
            else if (arg == "--smw-no-audio" ||
                arg.Equals("--smw-audio=off", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--smw-audio=0", StringComparison.OrdinalIgnoreCase))
            {
                _audioEnabled = false;
            }
            else if (arg == "--smw-audio" ||
                arg.Equals("--smw-audio=on", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--smw-audio=1", StringComparison.OrdinalIgnoreCase))
            {
                _audioEnabled = true;
            }
            else if (ApplyStartupToggleArg(arg))
            {
                continue;
            }
            else if (arg.StartsWith("--smw-test-level=", StringComparison.Ordinal))
            {
                testLevel = NormalizeLevelId(arg["--smw-test-level=".Length..]);
                SelectMenuLevel(testLevel, updateUi: false);
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
            else if (arg.StartsWith("--smw-audio-sample=", StringComparison.Ordinal))
            {
                audioSamplePreview = ParseHexOrDecimal(arg["--smw-audio-sample=".Length..]);
            }
            else if (arg.StartsWith("--smw-input-script=", StringComparison.Ordinal))
            {
                inputScriptPath = arg["--smw-input-script=".Length..];
                autostart = true;
            }
            else if (arg.StartsWith("--smw-autoplay=", StringComparison.Ordinal))
            {
                autoplayMode = arg["--smw-autoplay=".Length..];
                autostart = true;
            }
            else if (arg.StartsWith("--smw-debug-command-file=", StringComparison.Ordinal))
            {
                debugCommandPath = arg["--smw-debug-command-file=".Length..];
                autostart = true;
            }
            else if (arg.StartsWith("--smw-debug-rcon=", StringComparison.Ordinal) &&
                int.TryParse(arg["--smw-debug-rcon=".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedRconPort))
            {
                debugRconPort = parsedRconPort;
                autostart = true;
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
        if (titleStart && !autostart && testLevel == null && capturePath == null)
        {
            CallDeferred(nameof(StartGameFromTitleStartProbe));
        }
        if (testPowerup != null)
        {
            _game?.DebugSetPlayerPowerup(testPowerup.Value);
        }
        if (testSpawn != null)
        {
            _game?.DebugSetPlayerPosition(testSpawn.Value);
        }
        if (testScreenExit != null)
        {
            _game?.DebugEnterScreenExit(testScreenExit.Value);
        }
        if (inputScriptPath != null)
        {
            _game?.DebugLoadInputScript(inputScriptPath);
        }
        if (autoplayMode != null)
        {
            _game?.DebugSetAutoplayMode(autoplayMode);
        }
        if (debugCommandPath != null)
        {
            _game?.DebugUseCommandFile(debugCommandPath);
        }
        if (debugRconPort != null)
        {
            _game?.DebugStartRcon(debugRconPort.Value);
        }
        if (capturePath != null)
        {
            _game?.DebugCaptureViewport(capturePath, captureFrames, quitAfterCapture: true);
        }
        if (audioPreview != null)
        {
            _audio?.PlayMusicPreview(audioPreview);
        }
        if (audioSamplePreview != null)
        {
            GD.Print($"smw-audio: sample_preview {_audio?.PlaySampleProbe(audioSamplePreview.Value) ?? $"sample={audioSamplePreview.Value:X2} available=0 samples=0"}");
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

    private void LoadImportedLevelList()
    {
        _importedLevels.Clear();
        if (!FileAccess.FileExists(ManifestPath))
        {
            FinalizeImportedLevelList();
            return;
        }

        using var file = FileAccess.Open(ManifestPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            FinalizeImportedLevelList();
            return;
        }

        try
        {
            var parsed = Json.ParseString(file.GetAsText(false));
            if (parsed.VariantType != Variant.Type.Dictionary)
            {
                FinalizeImportedLevelList();
                return;
            }

            var manifest = parsed.AsGodotDictionary();
            MergeManifestLevelIndex(manifest);
            if (!manifest.TryGetValue("levels", out var levelsVariant) ||
                levelsVariant.VariantType != Variant.Type.Dictionary)
            {
                FinalizeImportedLevelList();
                return;
            }

            var levels = levelsVariant.AsGodotDictionary();
            foreach (var property in levels)
            {
                if (property.Value.VariantType != Variant.Type.Dictionary)
                {
                    continue;
                }

                var level = property.Value.AsGodotDictionary();
                var id = NormalizeLevelId(property.Key.AsString());
                string? previewPath = null;
                if (level.TryGetValue("layout_preview", out var layoutPreview) &&
                    layoutPreview.VariantType == Variant.Type.Dictionary)
                {
                    var layout = layoutPreview.AsGodotDictionary();
                    if (layout.TryGetValue("preview_png", out var previewPng) &&
                        previewPng.VariantType == Variant.Type.String)
                    {
                        var relativePath = previewPng.AsString();
                        if (!string.IsNullOrWhiteSpace(relativePath))
                        {
                            previewPath = SmwAssetPaths.Path(relativePath);
                        }
                    }
                }

                var screenExitCount = 0;
                if (level.TryGetValue("screen_exits", out var screenExits) &&
                    screenExits.VariantType == Variant.Type.Array)
                {
                    screenExitCount = screenExits.AsGodotArray().Count;
                }

                _importedLevels.Add(new ImportedLevelEntry(
                    id,
                    GetStringProperty(level, "name"),
                    GetStringProperty(level, "display_name"),
                    GetStringProperty(level, "title_source"),
                    previewPath,
                    GetIntProperty(level, "object_count"),
                    GetIntProperty(level, "sprite_count"),
                    screenExitCount,
                    IsGenerated: true));
            }
        }
        catch (InvalidOperationException exc)
        {
            GD.PrintErr($"smw-menu: manifest parse failed path={ManifestPath} error={exc.Message}");
            _importedLevels.Clear();
        }

        FinalizeImportedLevelList();
    }

    private void FinalizeImportedLevelList()
    {
        MergeWebIndexedLevels();
        _importedLevels.Sort((left, right) => LevelSortKey(left.Id).CompareTo(LevelSortKey(right.Id)));
        if (_importedLevels.Count > 0)
        {
            SelectMenuLevel(FindImportedLevel(DefaultLevelId)?.Id ?? _importedLevels[0].Id, updateUi: false);
        }
    }

    private void MergeManifestLevelIndex(Godot.Collections.Dictionary manifest)
    {
        if (!manifest.TryGetValue("level_index", out var indexVariant) ||
            indexVariant.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var index = indexVariant.AsGodotDictionary();
        if (!index.TryGetValue("levels", out var levelsVariant) ||
            levelsVariant.VariantType != Variant.Type.Array)
        {
            return;
        }

        var count = AddWebIndexedLevels(levelsVariant.AsGodotArray(), clearExisting: false);
        GD.Print($"smw-menu: manifest_level_index={count}");
    }

    private void MergeWebIndexedLevels()
    {
        if (_webIndexedLevels.Count == 0)
        {
            return;
        }

        var generatedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var level in _importedLevels)
        {
            generatedIds.Add(level.Id);
        }

        foreach (var level in _webIndexedLevels.Values)
        {
            if (!generatedIds.Contains(level.Id))
            {
                _importedLevels.Add(level);
            }
        }
    }

    private static int GetIntProperty(Godot.Collections.Dictionary element, string key)
    {
        return element.TryGetValue(key, out var value) &&
            (value.VariantType == Variant.Type.Int || value.VariantType == Variant.Type.Float)
            ? value.AsInt32()
            : 0;
    }

    private static string GetStringProperty(Godot.Collections.Dictionary element, string key)
    {
        return element.TryGetValue(key, out var value) && value.VariantType == Variant.Type.String
            ? value.AsString()
            : string.Empty;
    }

    private static int LevelSortKey(string levelId)
    {
        var normalized = NormalizeLevelId(levelId);
        return int.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : int.MaxValue;
    }

    private ImportedLevelEntry? FindImportedLevel(string levelId)
    {
        var normalized = NormalizeLevelId(levelId);
        foreach (var level in _importedLevels)
        {
            if (level.Id == normalized)
            {
                return level;
            }
        }

        return null;
    }

    private void SelectMenuLevel(string levelId, bool updateUi = true)
    {
        var normalized = NormalizeLevelId(levelId);
        var level = FindImportedLevel(normalized);
        _selectedLevelId = level?.Id ?? normalized;
        _menuLevelPreviewPath = level?.PreviewPath ?? DefaultMenuLevelPreviewPath;

        if (!updateUi)
        {
            return;
        }

        if (_menuLevelPreview != null)
        {
            SetTextureAndDispose(_menuLevelPreview, LoadTexture(_menuLevelPreviewPath));
        }
        if (_selectedLevelPreview != null)
        {
            SetTextureAndDispose(_selectedLevelPreview, LoadTexture(_menuLevelPreviewPath));
        }
        if (_selectedLevelLabel != null)
        {
            _selectedLevelLabel.Text = level switch
            {
                { IsGenerated: true } => $"Objects {level.ObjectCount}    Sprites {level.SpriteCount}    Exits {level.ScreenExitCount}",
                { IsGenerated: false } => "ROM indexed. Press Start to import this level.",
                _ => $"Level {_selectedLevelId} is not in the generated manifest",
            };
        }
        if (_selectedLevelTitle != null)
        {
            _selectedLevelTitle.Text = level != null
                ? $"{level.Id} {ShortenMenuText(LevelDisplayName(level), 18)}"
                : $"Level {_selectedLevelId}";
        }
        if (_startButton != null)
        {
            _startButton.Text = level != null && !level.IsGenerated
                ? $"Import {level.Id}"
                : level != null
                    ? $"Start {level.Id}"
                : $"Start Level {_selectedLevelId}";
        }
        SelectVisibleLevelRow(_selectedLevelId);
    }

    private static string NormalizeLevelId(string levelId)
    {
        var trimmed = levelId.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        return int.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed) &&
            parsed >= 0 &&
            parsed < 0x200
            ? parsed.ToString("X3", CultureInfo.InvariantCulture)
            : trimmed.ToUpperInvariant();
    }

    private void ApplyInitialMenuArgs()
    {
        foreach (var arg in OS.GetCmdlineArgs())
        {
            ApplyStartupToggleArg(arg);
        }
    }

    private void ApplyInitialLevelArg()
    {
        foreach (var arg in OS.GetCmdlineArgs())
        {
            if (arg.StartsWith("--smw-test-level=", StringComparison.Ordinal))
            {
                SelectMenuLevel(NormalizeLevelId(arg["--smw-test-level=".Length..]), updateUi: false);
            }
        }
    }

    private bool ApplyStartupToggleArg(string arg)
    {
        if (arg == "--smw-debug-overlays")
        {
            _debugOverlays = true;
            return true;
        }
        if (arg == "--smw-actors-off")
        {
            _actorsEnabled = false;
            return true;
        }
        if (arg.StartsWith("--smw-actors=", StringComparison.Ordinal))
        {
            _actorsEnabled = ParseToggleArg(arg["--smw-actors=".Length..], _actorsEnabled);
            return true;
        }
        if (arg == "--smw-actor-visuals-off")
        {
            _actorVisualsEnabled = false;
            return true;
        }
        if (arg.StartsWith("--smw-actor-visuals=", StringComparison.Ordinal))
        {
            _actorVisualsEnabled = ParseToggleArg(arg["--smw-actor-visuals=".Length..], _actorVisualsEnabled);
            return true;
        }

        return false;
    }

    private static bool ParseToggleArg(string value, bool fallback)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "on" or "true" or "yes" or "enabled" => true,
            "0" or "off" or "false" or "no" or "disabled" => false,
            _ => fallback,
        };
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_game != null)
        {
            return;
        }

        if (HandleMenuTypeSearch(@event))
        {
            return;
        }

        if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("smw_start"))
        {
            StartGame();
        }
    }

    private bool HandleMenuTypeSearch(InputEvent @event)
    {
        if (_levelSearch == null ||
            @event is not InputEventKey keyEvent ||
            !keyEvent.Pressed ||
            keyEvent.Echo)
        {
            return false;
        }

        if (keyEvent.Keycode == Key.Escape)
        {
            if (_levelSearch.Text.Length == 0)
            {
                return false;
            }
            _levelSearch.Text = string.Empty;
            _levelSearch.CaretColumn = 0;
            FilterMenuLevels(_levelSearch.Text);
            GetViewport().SetInputAsHandled();
            return true;
        }

        if (keyEvent.Keycode == Key.Backspace)
        {
            if (_levelSearch.Text.Length == 0)
            {
                return false;
            }
            _levelSearch.Text = _levelSearch.Text[..^1];
            _levelSearch.CaretColumn = _levelSearch.Text.Length;
            FilterMenuLevels(_levelSearch.Text);
            GetViewport().SetInputAsHandled();
            return true;
        }

        var unicode = keyEvent.Unicode;
        if (unicode < 0x20 || unicode > 0x7E)
        {
            return false;
        }

        _levelSearch.GrabFocus();
        _levelSearch.Text += (char)unicode;
        _levelSearch.CaretColumn = _levelSearch.Text.Length;
        FilterMenuLevels(_levelSearch.Text);
        GetViewport().SetInputAsHandled();
        return true;
    }

    private static void SetupInputMap()
    {
        AddKeyAction("smw_left", Key.Left, Key.A);
        AddKeyAction("smw_right", Key.Right, Key.D);
        AddKeyAction("smw_up", Key.Up, Key.W);
        AddKeyAction("smw_down", Key.Down, Key.S);
        AddKeyAction("smw_jump", Key.Z, Key.Space);
        AddKeyAction("smw_spin", Key.X);
        AddKeyAction("smw_run", Key.Shift, Key.C);
        AddKeyAction("smw_start", Key.Enter);
        AddKeyAction("smw_back", Key.Escape, Key.Backspace);
        AddKeyAction("ui_accept", Key.Enter);

        AddJoyButtonAction("smw_left", JoyButton.DpadLeft);
        AddJoyButtonAction("smw_right", JoyButton.DpadRight);
        AddJoyButtonAction("smw_up", JoyButton.DpadUp);
        AddJoyButtonAction("smw_down", JoyButton.DpadDown);
        AddJoyAxisAction("smw_left", JoyAxis.LeftX, -1.0f);
        AddJoyAxisAction("smw_right", JoyAxis.LeftX, 1.0f);
        AddJoyAxisAction("smw_up", JoyAxis.LeftY, -1.0f);
        AddJoyAxisAction("smw_down", JoyAxis.LeftY, 1.0f);
        AddJoyButtonAction("smw_jump", JoyButton.A);
        AddJoyButtonAction("smw_spin", JoyButton.B);
        AddJoyButtonAction("smw_run", JoyButton.X, JoyButton.RightShoulder);
        AddJoyButtonAction("smw_start", JoyButton.Start);
        AddJoyButtonAction("smw_back", JoyButton.Back, JoyButton.Guide);
        AddJoyButtonAction("ui_accept", JoyButton.A, JoyButton.Start);
        GD.Print("smw-input-map: keyboard=1 gamepad=1 buttons=11 axes=4");
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

    private static void AddJoyButtonAction(StringName action, params JoyButton[] buttons)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }

        foreach (var button in buttons)
        {
            var exists = false;
            foreach (var existing in InputMap.ActionGetEvents(action))
            {
                if (existing is InputEventJoypadButton buttonEvent &&
                    buttonEvent.ButtonIndex == button)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                var inputEvent = new InputEventJoypadButton { ButtonIndex = button };
                InputMap.ActionAddEvent(action, inputEvent);
            }
        }
    }

    private static void AddJoyAxisAction(StringName action, JoyAxis axis, float axisValue)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }

        foreach (var existing in InputMap.ActionGetEvents(action))
        {
            if (existing is InputEventJoypadMotion motionEvent &&
                motionEvent.Axis == axis &&
                MathF.Abs(motionEvent.AxisValue - axisValue) < 0.001f)
            {
                return;
            }
        }

        var inputEvent = new InputEventJoypadMotion
        {
            Axis = axis,
            AxisValue = axisValue,
        };
        InputMap.ActionAddEvent(action, inputEvent);
    }

    private void ShowMenu()
    {
        _menu = new Control();
        _menu.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_menu);

        var background = new ColorRect
        {
            Color = new Color(0.04f, 0.18f, 0.30f, 1.0f),
        };
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _menu.AddChild(background);

        AddMenuLevelPreview(_menu);

        var shade = new ColorRect
        {
            Color = new Color(0.0f, 0.0f, 0.0f, 0.34f),
        };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _menu.AddChild(shade);

        var root = new MarginContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.OffsetLeft = 5;
        root.OffsetTop = 5;
        root.OffsetRight = -5;
        root.OffsetBottom = -5;
        root.AddThemeConstantOverride("margin_left", 5);
        root.AddThemeConstantOverride("margin_top", 5);
        root.AddThemeConstantOverride("margin_right", 5);
        root.AddThemeConstantOverride("margin_bottom", 5);
        _menu.AddChild(root);

        var layout = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        layout.AddThemeConstantOverride("separation", 5);
        root.AddChild(layout);

        var browserFrame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(124, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        browserFrame.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0.02f, 0.10f, 0.18f, 0.88f), new Color(0.78f, 0.93f, 1.0f, 0.85f), 1, 3));
        layout.AddChild(browserFrame);

        var panel = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(114, 0),
        };
        panel.AddThemeConstantOverride("separation", 2);
        browserFrame.AddChild(MarginWrap(panel, 4));

        var title = new Label { Text = "Course Select" };
        title.AddThemeFontSizeOverride("font_size", 9);
        title.AddThemeColorOverride("font_color", new Color(1.0f, 0.95f, 0.62f, 1.0f));
        title.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 1.0f));
        title.AddThemeConstantOverride("shadow_offset_x", 2);
        title.AddThemeConstantOverride("shadow_offset_y", 2);
        panel.AddChild(title);

        _assetStatusLabel = new Label { Text = AssetStatusText() };
        _assetStatusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _assetStatusLabel.CustomMinimumSize = new Vector2(112, 10);
        _assetStatusLabel.AddThemeFontSizeOverride("font_size", 5);
        _assetStatusLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.96f, 1.0f, 1.0f));
        panel.AddChild(_assetStatusLabel);

        AddMenuLevelSelect(panel);

        PrintMenuAudioProbeStatus();

        var previewFrame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(116, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        previewFrame.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0.06f, 0.18f, 0.15f, 0.78f), new Color(1.0f, 0.95f, 0.62f, 0.78f), 1, 3));
        layout.AddChild(previewFrame);

        var previewPanel = new VBoxContainer();
        previewPanel.AddThemeConstantOverride("separation", 3);
        previewFrame.AddChild(MarginWrap(previewPanel, 4));
        AddMenuSelectedPreview(previewPanel);
        AddMenuToggles(previewPanel);
        _levelSearch?.GrabFocus();
        GD.Print($"smw-menu: assets={(HasGeneratedAssetPack() ? 1 : 0)} audio={(_audioEnabled ? 1 : 0)} actors={(_actorsEnabled ? 1 : 0)} actor_visuals={(_actorVisualsEnabled ? 1 : 0)} levels={_importedLevels.Count} selected_level={_selectedLevelId} level_preview={(FileAccess.FileExists(_menuLevelPreviewPath) ? 1 : 0)} player_preview={(FileAccess.FileExists(MenuPlayerPreviewPath) ? 1 : 0)}");
    }

    private static bool HasGeneratedAssetPack()
    {
        return FileAccess.FileExists(ManifestPath);
    }

    private string AssetStatusText()
    {
        if (_webIndexedLevels.Count > 0)
        {
            return $"{_webIndexedLevels.Count} ROM levels indexed. Missing assets generate on demand.";
        }

        return HasGeneratedAssetPack()
            ? "Generated asset pack found."
            : "No generated asset pack found. The playable slice will use a placeholder level.";
    }

    private static MarginContainer MarginWrap(Control child, int margin)
    {
        var wrapper = new MarginContainer();
        wrapper.AddThemeConstantOverride("margin_left", margin);
        wrapper.AddThemeConstantOverride("margin_top", margin);
        wrapper.AddThemeConstantOverride("margin_right", margin);
        wrapper.AddThemeConstantOverride("margin_bottom", margin);
        wrapper.AddChild(child);
        return wrapper;
    }

    private static StyleBoxFlat MakePanelStyle(Color fill, Color border, int borderWidth, int cornerRadius)
    {
        var style = new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
        };
        style.SetContentMarginAll(0);
        return style;
    }

    private void AddMenuLevelPreview(Control menu)
    {
        var texture = LoadTexture(_menuLevelPreviewPath);
        if (texture == null)
        {
            return;
        }

        _menuLevelPreview = new TextureRect
        {
            Name = "MenuLevelPreview",
            Position = Vector2.Zero,
            Size = new Vector2(LogicalViewportSize.X, LogicalViewportSize.Y),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        SetTextureAndDispose(_menuLevelPreview, texture);
        menu.AddChild(_menuLevelPreview);
    }

    private void AddMenuSelectedPreview(VBoxContainer panel)
    {
        var previewTitle = new Label { Text = "Preview" };
        previewTitle.AddThemeFontSizeOverride("font_size", 7);
        previewTitle.AddThemeColorOverride("font_color", new Color(1.0f, 0.95f, 0.62f, 1.0f));
        previewTitle.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 1.0f));
        previewTitle.AddThemeConstantOverride("shadow_offset_x", 1);
        previewTitle.AddThemeConstantOverride("shadow_offset_y", 1);
        panel.AddChild(previewTitle);

        var texture = LoadTexture(_menuLevelPreviewPath);
        if (texture == null)
        {
            SelectMenuLevel(_selectedLevelId);
            return;
        }

        var playerCard = new PanelContainer();
        playerCard.ClipContents = true;
        playerCard.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0.0f, 0.0f, 0.0f, 0.24f), new Color(1.0f, 1.0f, 1.0f, 0.30f), 1, 3));
        panel.AddChild(playerCard);

        var frame = new TextureRect
        {
            Name = "SelectedLevelPreview",
            Size = new Vector2(108, 88),
            CustomMinimumSize = new Vector2(108, 88),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        _selectedLevelPreview = frame;
        SetTextureAndDispose(frame, texture);
        playerCard.AddChild(frame);
        SelectMenuLevel(_selectedLevelId);
    }

    private void AddMenuToggles(VBoxContainer panel)
    {
        var row = new HBoxContainer();
        panel.AddChild(row);

        _audioToggle = new CheckBox
        {
            Text = "Audio",
            ButtonPressed = _audioEnabled,
            Disabled = true,
            TooltipText = "Audio is intentionally disabled from the selector for now.",
        };
        _audioToggle.AddThemeFontSizeOverride("font_size", 5);
        _audioToggle.AddThemeColorOverride("font_disabled_color", new Color(0.62f, 0.68f, 0.70f, 1.0f));
        _audioToggle.Toggled += SetAudioEnabled;
        row.AddChild(_audioToggle);

        _debugToggle = new CheckBox
        {
            Text = "Gizmos",
            ButtonPressed = _debugOverlays,
        };
        _debugToggle.AddThemeFontSizeOverride("font_size", 5);
        _debugToggle.Toggled += enabled => _debugOverlays = enabled;
        row.AddChild(_debugToggle);

        var actorRow = new HBoxContainer();
        panel.AddChild(actorRow);

        _actorsToggle = new CheckBox
        {
            Text = "Actors",
            ButtonPressed = _actorsEnabled,
        };
        _actorsToggle.AddThemeFontSizeOverride("font_size", 5);
        _actorsToggle.Toggled += enabled => _actorsEnabled = enabled;
        actorRow.AddChild(_actorsToggle);

        _actorVisualsToggle = new CheckBox
        {
            Text = "Sprites",
            ButtonPressed = _actorVisualsEnabled,
        };
        _actorVisualsToggle.AddThemeFontSizeOverride("font_size", 5);
        _actorVisualsToggle.Toggled += enabled => _actorVisualsEnabled = enabled;
        actorRow.AddChild(_actorVisualsToggle);
    }

    private static Texture2D? LoadTexture(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            return null;
        }

        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        if (image == null || image.IsEmpty())
        {
            image?.Dispose();
            return null;
        }

        var texture = ImageTexture.CreateFromImage(image);
        image.Dispose();
        return texture;
    }

    private static void SetTextureAndDispose(TextureRect rect, Texture2D? texture)
    {
        rect.Texture = texture;
        texture?.Dispose();
    }

    private void AddMenuLevelSelect(VBoxContainer panel)
    {
        _selectedLevelTitle = new Label { Text = $"Level {_selectedLevelId}" };
        _selectedLevelTitle.AddThemeFontSizeOverride("font_size", 7);
        _selectedLevelTitle.AddThemeColorOverride("font_color", new Color(1.0f, 0.95f, 0.62f, 1.0f));
        _selectedLevelTitle.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 1.0f));
        _selectedLevelTitle.AddThemeConstantOverride("shadow_offset_x", 1);
        _selectedLevelTitle.AddThemeConstantOverride("shadow_offset_y", 1);
        _selectedLevelTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        panel.AddChild(_selectedLevelTitle);

        _selectedLevelLabel = new Label();
        _selectedLevelLabel.AddThemeFontSizeOverride("font_size", 5);
        _selectedLevelLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.96f, 1.0f, 1.0f));
        panel.AddChild(_selectedLevelLabel);

        _startButton = new Button
        {
            Text = $"Start {_selectedLevelId}",
            CustomMinimumSize = new Vector2(112, 16),
        };
        _startButton.AddThemeFontSizeOverride("font_size", 6);
        _startButton.AddThemeColorOverride("font_color", new Color(1.0f, 0.95f, 0.62f, 1.0f));
        _startButton.AddThemeColorOverride("font_focus_color", new Color(1.0f, 0.95f, 0.62f, 1.0f));
        _startButton.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 1.0f, 1.0f));
        _startButton.AddThemeColorOverride("font_pressed_color", new Color(0.18f, 0.12f, 0.04f, 1.0f));
        _startButton.AddThemeStyleboxOverride("normal", MakePanelStyle(new Color(0.40f, 0.16f, 0.07f, 0.95f), new Color(1.0f, 0.95f, 0.62f, 1.0f), 1, 3));
        _startButton.AddThemeStyleboxOverride("hover", MakePanelStyle(new Color(0.56f, 0.23f, 0.08f, 0.95f), new Color(1.0f, 0.98f, 0.70f, 1.0f), 1, 3));
        _startButton.AddThemeStyleboxOverride("pressed", MakePanelStyle(new Color(1.0f, 0.78f, 0.24f, 0.95f), new Color(1.0f, 0.98f, 0.70f, 1.0f), 1, 3));
        _startButton.AddThemeStyleboxOverride("focus", MakePanelStyle(new Color(0.40f, 0.16f, 0.07f, 0.95f), new Color(1.0f, 0.98f, 0.70f, 1.0f), 1, 3));
        _startButton.Pressed += StartGame;
        panel.AddChild(_startButton);

        _levelSearch = new LineEdit
        {
            PlaceholderText = "Search",
            CustomMinimumSize = new Vector2(112, 16),
        };
        _levelSearch.AddThemeFontSizeOverride("font_size", 6);
        _levelSearch.TextChanged += FilterMenuLevels;
        panel.AddChild(_levelSearch);

        _levelSearchStatus = new Label();
        _levelSearchStatus.AddThemeFontSizeOverride("font_size", 5);
        _levelSearchStatus.AddThemeColorOverride("font_color", new Color(0.82f, 0.94f, 1.0f, 1.0f));
        panel.AddChild(_levelSearchStatus);

        _levelList = new ItemList
        {
            CustomMinimumSize = new Vector2(112, 106),
            AutoHeight = false,
            FixedColumnWidth = 112,
            MaxTextLines = 1,
            SameColumnWidth = true,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _levelList.AddThemeFontSizeOverride("font_size", 5);
        _levelList.ItemSelected += index =>
        {
            if (index >= 0 && index < _visibleLevels.Count)
            {
                SelectMenuLevel(_visibleLevels[(int)index].Id);
            }
        };
        _levelList.ItemActivated += index =>
        {
            if (index >= 0 && index < _visibleLevels.Count)
            {
                SelectMenuLevel(_visibleLevels[(int)index].Id);
                StartGame();
            }
        };
        panel.AddChild(_levelList);
        FilterMenuLevels(string.Empty);
    }

    private void FilterMenuLevels(string query)
    {
        if (_levelList == null)
        {
            return;
        }

        _levelList.Clear();
        _visibleLevels.Clear();
        foreach (var level in _importedLevels)
        {
            if (!LevelMatchesSearch(level, query))
            {
                continue;
            }
            _visibleLevels.Add(level);
            _levelList.AddItem(MenuLevelRowText(level));
        }

        if (_levelSearchStatus != null)
        {
            var noun = _webIndexedLevels.Count > 0 ? "valid levels" : "imported levels";
            _levelSearchStatus.Text = _visibleLevels.Count == _importedLevels.Count
                ? $"{_importedLevels.Count} {noun}"
                : $"{_visibleLevels.Count} of {_importedLevels.Count} levels";
        }

        if (_visibleLevels.Count == 0)
        {
            if (_selectedLevelLabel != null)
            {
                _selectedLevelLabel.Text = _webIndexedLevels.Count > 0
                    ? "No matching valid levels"
                    : "No matching imported levels";
            }
            return;
        }

        if (FindVisibleLevel(_selectedLevelId) == null)
        {
            SelectMenuLevel(_visibleLevels[0].Id);
        }
        else
        {
            SelectVisibleLevelRow(_selectedLevelId);
        }
    }

    private static bool LevelMatchesSearch(ImportedLevelEntry level, string query)
    {
        var normalized = query.Trim().ToUpperInvariant();
        if (normalized.Length == 0)
        {
            return true;
        }
        if (normalized.StartsWith("0X", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }
        var haystack = $"{level.Id} {level.Name} {level.DisplayName}".ToUpperInvariant();
        foreach (var token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!haystack.Contains(token, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private ImportedLevelEntry? FindVisibleLevel(string levelId)
    {
        var normalized = NormalizeLevelId(levelId);
        foreach (var level in _visibleLevels)
        {
            if (level.Id == normalized)
            {
                return level;
            }
        }
        return null;
    }

    private void SelectVisibleLevelRow(string levelId)
    {
        if (_levelList == null || _visibleLevels.Count == 0)
        {
            return;
        }

        var normalized = NormalizeLevelId(levelId);
        for (var index = 0; index < _visibleLevels.Count; index++)
        {
            if (_visibleLevels[index].Id == normalized)
            {
                _levelList.Select(index);
                return;
            }
        }
    }

    private static string LevelDisplayName(ImportedLevelEntry level)
    {
        if (!string.IsNullOrWhiteSpace(level.DisplayName) && level.DisplayName != $"Level {level.Id}")
        {
            return level.DisplayName;
        }
        if (!string.IsNullOrWhiteSpace(level.Name))
        {
            return level.Name;
        }
        return $"Level {level.Id}";
    }

    private static string MenuLevelRowText(ImportedLevelEntry level)
    {
        var name = LevelDisplayName(level);
        return name == $"Level {level.Id}"
            ? $"{level.Id}"
            : $"{level.Id} {ShortenMenuText(name, 18)}";
    }

    private static string ShortenMenuText(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }
        return text[..Math.Max(0, maxChars - 1)] + ".";
    }

    private void SetAudioEnabled(bool enabled)
    {
        _audioEnabled = enabled;
        if (!_audioEnabled)
        {
            _audio?.QueueFree();
            _audio = null;
            return;
        }

        if (_audio == null)
        {
            _audio = new SmwAudio { Name = "SmwAudio" };
            AddChild(_audio);
        }
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
        AddCommandButton(samples, "Coin", () => _audio?.PlayCoin());
        AddCommandButton(samples, "Stomp", () => _audio?.PlayStomp(0));
        AddCommandButton(samples, "1UP", () => _audio?.PlayOneUp());
        AddCommandButton(samples, "Fire", () => _audio?.PlayFireball());
        foreach (var sampleId in SmwAudio.ProbeSampleIds)
        {
            AddSampleButton(samples, sampleId, $"BRR{sampleId:X2}");
        }
        GD.Print($"smw-menu-audio: samples={(_audio?.LoadedProbeSampleCount ?? 0)} buttons={SmwAudio.ProbeSampleIds.Length} sfx_buttons=6 music_buttons=4");

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
        foreach (var name in new[] { "Level", "Overworld", "Credits", "Star" })
        {
            var bankPath = SmwAssetPaths.Path($"audio/spc_{name.ToLowerInvariant()}_music_bank.bin");
            var button = new Button
            {
                Text = name,
                Disabled = name != "Star" && !FileAccess.FileExists(bankPath),
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

    private void PrintMenuAudioProbeStatus()
    {
        GD.Print($"smw-menu-audio: samples={(_audio?.LoadedProbeSampleCount ?? 0)} buttons={SmwAudio.ProbeSampleIds.Length} sfx_buttons=6 music_buttons=4");
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
            ("Level", SmwAssetPaths.Path("audio/spc_level_music_bank.bin")),
            ("Overworld", SmwAssetPaths.Path("audio/spc_overworld_music_bank.bin")),
            ("Credits", SmwAssetPaths.Path("audio/spc_credits_music_bank.bin")),
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

    private void SetupWebAssetBridge(bool webDisplay)
    {
        if (!webDisplay)
        {
            return;
        }

        try
        {
            _webBridgeCallback = JavaScriptBridge.CreateCallback(Callable.From<Godot.Collections.Array>(OnWebAssetCommand));
            var window = JavaScriptBridge.GetInterface("window");
            window.Set("openPlatformerRuntimeGodotCommand", _webBridgeCallback);
            JavaScriptBridge.Eval(
                """
                window.openPlatformerRuntimeGodotReady = true;
                window.dispatchEvent(new CustomEvent("open-platformer-runtime-godot-ready"));
                if (window.parent && window.parent !== window) {
                  window.parent.postMessage({ type: "open-platformer-runtime-godot-ready" }, "*");
                }
                """,
                useGlobalExecutionContext: true);
            GD.Print("smw-web: bridge_ready=1");
        }
        catch (Exception exc)
        {
            GD.PushWarning($"smw-web: bridge unavailable: {exc.Message}");
        }
    }

    private static bool IsWebDisplay()
    {
        if (OS.HasFeature("web") ||
            DisplayServer.GetName().Contains("web", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            return JavaScriptBridge.GetInterface("window") != null;
        }
        catch
        {
            return false;
        }
    }

    private void OnWebAssetCommand(Godot.Collections.Array args)
    {
        if (args.Count == 0)
        {
            return;
        }

        var command = args[0].AsString();
        try
        {
            switch (command)
            {
                case "level_index":
                    ReceiveWebLevelIndex(args);
                    break;

                case "begin":
                    _webImportedFileCount = 0;
                    SmwAssetPaths.PreferUserAssetPack();
                    GD.Print("smw-web: import_begin=1");
                    break;

                case "file":
                    ReceiveWebAssetFile(args);
                    break;

                case "complete":
                    var levelId = args.Count > 1 ? NormalizeLevelId(args[1].AsString()) : DefaultLevelId;
                    var autoStart = args.Count > 2 && args[2].AsBool();
                    GD.Print($"smw-web: import_complete=1 files={_webImportedFileCount} level={levelId} autostart={(autoStart ? 1 : 0)}");
                    CallDeferred(nameof(RefreshAfterWebAssetImport), levelId, autoStart);
                    break;

                case "status":
                    GD.Print($"smw-web: status={FormatWebStatus(args)}");
                    break;
            }
        }
        catch (Exception exc)
        {
            GD.PrintErr($"smw-web: command_failed command={command} error={exc.Message}");
        }
    }

    private void ReceiveWebLevelIndex(Godot.Collections.Array args)
    {
        if (args.Count < 2)
        {
            return;
        }

        var parsed = Json.ParseString(args[1].AsString());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var payload = parsed.AsGodotDictionary();
        if (!payload.TryGetValue("levels", out var levelsVariant) ||
            levelsVariant.VariantType != Variant.Type.Array)
        {
            return;
        }

        var preferredLevel = _selectedLevelId;
        var count = AddWebIndexedLevels(levelsVariant.AsGodotArray(), clearExisting: true);

        LoadImportedLevelList();
        if (FindImportedLevel(preferredLevel) != null)
        {
            SelectMenuLevel(preferredLevel);
        }
        if (_assetStatusLabel != null)
        {
            _assetStatusLabel.Text = AssetStatusText();
        }
        if (_levelList != null)
        {
            FilterMenuLevels(_levelSearch?.Text ?? string.Empty);
        }

        GD.Print($"smw-web: level_index={count}");
    }

    private int AddWebIndexedLevels(Godot.Collections.Array levels, bool clearExisting)
    {
        if (clearExisting)
        {
            _webIndexedLevels.Clear();
        }

        foreach (var item in levels)
        {
            if (item.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }

            var level = item.AsGodotDictionary();
            var id = NormalizeLevelId(GetStringProperty(level, "id"));
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            _webIndexedLevels[id] = new ImportedLevelEntry(
                id,
                GetStringProperty(level, "name"),
                GetStringProperty(level, "display_name"),
                GetStringProperty(level, "title_source"),
                PreviewPath: null,
                ObjectCount: 0,
                SpriteCount: 0,
                ScreenExitCount: 0,
                IsGenerated: false);
        }

        return _webIndexedLevels.Count;
    }

    private void ReceiveWebAssetFile(Godot.Collections.Array args)
    {
        if (args.Count < 3)
        {
            throw new ArgumentException("file command requires path and buffer");
        }

        var relativePath = NormalizeWebAssetPath(args[1].AsString());
        if (args[2].VariantType != Variant.Type.Object ||
            args[2].AsGodotObject() is not JavaScriptObject jsBuffer ||
            !JavaScriptBridge.IsJsBuffer(jsBuffer))
        {
            throw new ArgumentException($"asset payload is not a JavaScript buffer: {relativePath}");
        }

        var bytes = JavaScriptBridge.JsBufferToPackedByteArray(jsBuffer);
        var userPath = SmwAssetPaths.UserPath(relativePath);
        var absolutePath = ProjectSettings.GlobalizePath(userPath);
        var directory = IoPath.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            DirAccess.MakeDirRecursiveAbsolute(directory);
        }

        using var file = FileAccess.Open(userPath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            throw new IOException($"failed to open {userPath} for writing");
        }

        file.StoreBuffer(bytes);
        _webImportedFileCount++;
    }

    private static string NormalizeWebAssetPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0 ||
            normalized.Contains("..", StringComparison.Ordinal) ||
            normalized.StartsWith("res://", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"invalid asset path: {relativePath}");
        }

        return normalized;
    }

    private static string FormatWebStatus(Godot.Collections.Array args)
    {
        return args.Count > 1 ? args[1].AsString() : string.Empty;
    }

    private bool RequestWebLevelImport(string levelId)
    {
        if (!IsWebDisplay())
        {
            return false;
        }

        var normalized = NormalizeLevelId(levelId);
        if (_startButton != null)
        {
            _startButton.Disabled = true;
            _startButton.Text = $"Importing {normalized}";
        }
        if (_levelSearchStatus != null)
        {
            _levelSearchStatus.Text = $"Importing level {normalized} from ROM...";
        }

        try
        {
            JavaScriptBridge.Eval(
                $$"""
                {
                  const message = { type: "open-platformer-runtime-import-level", levelId: "{{normalized}}" };
                  if (window.parent && window.parent !== window) {
                    window.parent.postMessage(message, "*");
                  } else {
                    window.postMessage(message, "*");
                  }
                }
                """,
                useGlobalExecutionContext: true);
            GD.Print($"smw-web: request_import level={normalized}");
            return true;
        }
        catch (Exception exc)
        {
            GD.PrintErr($"smw-web: request_import_failed level={normalized} error={exc.Message}");
            if (_startButton != null)
            {
                _startButton.Disabled = false;
                _startButton.Text = $"Import {normalized}";
            }
            return false;
        }
    }

    private void RefreshAfterWebAssetImport(string levelId, bool autoStart)
    {
        if (_game != null)
        {
            _game.CourseSelectRequested -= ReturnToCourseSelect;
            _game.QueueFree();
            _game = null;
        }

        _gameBackground?.QueueFree();
        _gameBackground = null;
        _menu?.QueueFree();
        ClearMenuReferences();
        LoadImportedLevelList();
        SelectMenuLevel(levelId, updateUi: false);
        ShowMenu();

        if (autoStart)
        {
            StartGame();
        }
    }

    private void StartGame()
    {
        if (_game != null)
        {
            return;
        }

        var selectedLevel = FindImportedLevel(_selectedLevelId);
        if (selectedLevel is { IsGenerated: false } && RequestWebLevelImport(selectedLevel.Id))
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
            InitialLevelId = _selectedLevelId,
            DebugOverlays = _debugOverlays,
            ActorsEnabled = _actorsEnabled,
            ActorVisualsEnabled = _actorVisualsEnabled,
            Audio = _audio,
            AudioEnabled = _audioEnabled,
        };
        _game.CourseSelectRequested += ReturnToCourseSelect;
        AddChild(_game);
    }

    private void ReturnToCourseSelect()
    {
        if (_game == null)
        {
            return;
        }

        GD.Print($"smw-menu: return level={_selectedLevelId}");
        _game.CourseSelectRequested -= ReturnToCourseSelect;
        _game.QueueFree();
        _game = null;
        _gameBackground?.QueueFree();
        _gameBackground = null;
        _audio?.StopMusicPreview();
        ClearMenuReferences();
        ShowMenu();
    }

    private void ClearMenuReferences()
    {
        _menu = null;
        _menuLevelPreview = null;
        _selectedLevelPreview = null;
        _selectedLevelLabel = null;
        _selectedLevelTitle = null;
        _levelSearchStatus = null;
        _assetStatusLabel = null;
        _startButton = null;
        _levelSelect = null;
        _levelSearch = null;
        _levelList = null;
        _audioToggle = null;
        _debugToggle = null;
        _actorsToggle = null;
        _actorVisualsToggle = null;
        _visibleLevels.Clear();
    }

    private void StartGameFromTitleStartProbe()
    {
        if (_game != null)
        {
            return;
        }

        GD.Print("smw-menu: title_start=1");
        StartGame();
    }

    private static bool ShouldEnableAudio()
    {
        var env = OS.GetEnvironment("SMW_AUDIO");
        if (env == "1" ||
            env.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            env.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (env == "0" ||
            env.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            env.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var enabled = false;
        foreach (var arg in OS.GetCmdlineArgs())
        {
            if (arg == "--smw-no-audio" ||
                arg.Equals("--smw-audio=off", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--smw-audio=0", StringComparison.OrdinalIgnoreCase))
            {
                enabled = false;
            }
            else if (arg == "--smw-audio" ||
                arg.Equals("--smw-audio=on", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--smw-audio=1", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("--smw-audio-preview=", StringComparison.Ordinal) ||
                arg.StartsWith("--smw-audio-sample=", StringComparison.Ordinal))
            {
                enabled = true;
            }
        }

        return enabled;
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
