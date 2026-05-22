using Godot;
using System;

public partial class Main : Node2D
{
    private Control? _menu;
    private GameScene? _game;

    public override void _Ready()
    {
        SetupInputMap();
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
            CustomMinimumSize = new Vector2(520, 180),
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
    }

    private static string AssetStatusText()
    {
        return FileAccess.FileExists("res://generated/smw/manifest.json")
            ? "Generated SMW asset pack found."
            : "No generated asset pack found. The playable slice will use a placeholder level.";
    }

    private void StartGame()
    {
        if (_game != null)
        {
            return;
        }

        _menu?.QueueFree();
        _menu = null;

        _game = new GameScene { Name = "GameScene" };
        AddChild(_game);
    }
}
