using Godot;
using System;
using System.Collections.Generic;

public partial class GameScene : Node2D
{
    private readonly SmwPhysics _physics = new();
    private readonly List<Rect2> _solids = [];
    private readonly List<Godot.Collections.Dictionary> _screenExits = [];

    private SmwPhysics.PlayerState _state;
    private ColorRect? _player;
    private Label? _hud;
    private float _cameraX;

    public override void _Ready()
    {
        _state = _physics.MakeState(64, 64);
        LoadAssetPack();
        BuildWorld();
        BuildPlayer();
        BuildHud();
    }

    public override void _PhysicsProcess(double delta)
    {
        var frameInput = new SmwPhysics.FrameInput
        {
            Left = Input.IsActionPressed("smw_left"),
            Right = Input.IsActionPressed("smw_right"),
            Down = Input.IsActionPressed("smw_down"),
            Jump = Input.IsActionPressed("smw_jump"),
            JumpPressed = Input.IsActionJustPressed("smw_jump"),
            SpinPressed = Input.IsActionJustPressed("smw_spin"),
            Run = Input.IsActionPressed("smw_run"),
        };

        _physics.Step(ref _state, frameInput, _solids);
        _cameraX = MathF.Max(0.0f, _state.X / 16.0f - 160.0f);
        Position = new Vector2(-MathF.Round(_cameraX), 0);

        if (_player != null)
        {
            _player.Position = new Vector2(_state.X / 16.0f, _state.Y / 16.0f);
        }

        UpdateHud();
        CheckPipeDebug();
    }

    private void LoadAssetPack()
    {
        if (!FileAccess.FileExists("res://generated/smw/manifest.json"))
        {
            return;
        }

        using var file = FileAccess.Open("res://generated/smw/manifest.json", FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return;
        }

        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var manifest = parsed.AsGodotDictionary();
        if (!manifest.TryGetValue("levels", out var levelsVariant) || levelsVariant.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var levels = levelsVariant.AsGodotDictionary();
        if (!levels.TryGetValue("105", out var levelVariant) || levelVariant.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var level = levelVariant.AsGodotDictionary();
        if (!level.TryGetValue("screen_exits", out var exitsVariant) || exitsVariant.VariantType != Variant.Type.Array)
        {
            return;
        }

        foreach (var exitVariant in exitsVariant.AsGodotArray())
        {
            if (exitVariant.VariantType == Variant.Type.Dictionary)
            {
                _screenExits.Add(exitVariant.AsGodotDictionary());
            }
        }
    }

    private void BuildWorld()
    {
        AddSolid(new Rect2(0, 192, 3584, 64), new Color(0.20f, 0.55f, 0.25f, 1.0f));
        AddSolid(new Rect2(240, 160, 48, 32), new Color(0.55f, 0.42f, 0.20f, 1.0f));
        AddSolid(new Rect2(368, 144, 64, 48), new Color(0.20f, 0.48f, 0.22f, 1.0f));
        AddPipeMarker(new Rect2(416, 112, 32, 80));

        for (var i = 0; i < 15; i++)
        {
            AddScreenLine(i);
        }
    }

    private void AddSolid(Rect2 rect, Color color)
    {
        _solids.Add(rect);
        var node = new ColorRect
        {
            Color = color,
            Position = rect.Position,
            Size = rect.Size,
        };
        AddChild(node);
    }

    private void AddPipeMarker(Rect2 rect)
    {
        var node = new ColorRect
        {
            Name = "PipeDebug",
            Color = new Color(0.10f, 0.75f, 0.22f, 0.85f),
            Position = rect.Position,
            Size = rect.Size,
        };
        AddChild(node);
    }

    private void AddScreenLine(int index)
    {
        var line = new ColorRect
        {
            Color = new Color(1, 1, 1, 0.14f),
            Position = new Vector2(index * 256, 0),
            Size = new Vector2(1, 224),
        };
        AddChild(line);

        var label = new Label
        {
            Text = $"{index:X2}",
            Position = new Vector2(index * 256 + 4, 4),
        };
        label.AddThemeFontSizeOverride("font_size", 10);
        AddChild(label);
    }

    private void BuildPlayer()
    {
        _player = new ColorRect
        {
            Color = new Color(0.88f, 0.12f, 0.10f, 1.0f),
            Size = new Vector2(SmwPhysics.PlayerWidth, SmwPhysics.PlayerHeight),
        };
        AddChild(_player);
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        _hud = new Label { Position = new Vector2(12, 10) };
        _hud.AddThemeFontSizeOverride("font_size", 13);
        layer.AddChild(_hud);
        UpdateHud();
    }

    private void UpdateHud()
    {
        if (_hud == null)
        {
            return;
        }

        _hud.Text = $"x={_state.X / 16.0f:000000.00} y={_state.Y / 16.0f:000000.00} " +
            $"xs={_state.XSpeed} ys={_state.YSpeed} exits={_screenExits.Count}";
    }

    private void CheckPipeDebug()
    {
        if (!Input.IsActionPressed("smw_down"))
        {
            return;
        }

        var pipeRect = new Rect2(416, 112, 32, 80);
        if (!_physics.PlayerRect(_state).Intersects(pipeRect))
        {
            return;
        }

        var screen = (int)((_state.X / 16.0f) / 256.0f);
        Godot.Collections.Dictionary? exitData = null;
        foreach (var entry in _screenExits)
        {
            if (entry.TryGetValue("screen", out var screenVariant) && screenVariant.AsInt32() == screen)
            {
                exitData = entry;
                break;
            }
        }

        GD.Print($"pipe-debug screen={screen:X2} exit={Json.Stringify(exitData ?? new Godot.Collections.Dictionary())}");
    }
}
