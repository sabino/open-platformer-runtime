using Godot;
using System;

namespace OpenPlatformerRuntime;

public partial class Main
{
    // Explicit development probe, never part of ordinary startup. Exercise
    // actual Godot controls/layout after ROM selection, not a second menu model.
    private async void RunRomMenuSmoke()
    {
        try
        {
            for (var frame = 0; frame < 120 && (_romImportInProgress || frame < 3); frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CheckMenu(_romCatalog?.Entries.Count == 512, "native ROM directory is missing");
            CheckMenu(_selectedLevelId == "105" && _startButton?.Disabled == false, "first course is unavailable");
            CheckMenu(_selectedLevelPreview?.Texture != null, "native layout preview is missing");
            CheckMenuBounds();

            _levelSearch!.Text = "107";
            _levelSearch.EmitSignal(LineEdit.SignalName.TextChanged, "107");
            CheckMenu(_visibleLevels.Count == 1 && _visibleLevels[0].Id == "107", "slot search failed");
            _levelList!.EmitSignal(ItemList.SignalName.ItemActivated, 0);
            CheckMenu(_game == null && _startButton?.Disabled == true, "unverified layout entered gameplay");
            CheckMenu(_selectedLevelPreview?.Texture == null, "unverified slot retained another course's preview");

            _levelSearch.Text = "105";
            _levelSearch.EmitSignal(LineEdit.SignalName.TextChanged, "105");
            _levelList.EmitSignal(ItemList.SignalName.ItemSelected, 0);
            CheckMenu(_startButton?.Disabled == false && _selectedLevelPreview?.Texture != null, "first course selection did not recover");
            FindMenuButton("Music / SFX Player").EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CheckMenu(_audioPlayerOpen && _audio?.HasNativeData == true, "selected ROM did not reach the listening player");
            FindMenuButton("Course Select").EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CheckMenu(!_audioPlayerOpen && _startButton?.Disabled == false, "return to course selection failed");
            CheckMenuBounds();
            GD.Print("smw-rom-menu: smoke=passed slots=512 native_preview=1 unsupported_gate=1 native_audio=1 layout=contained");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PrintErr($"smw-rom-menu: smoke=failed {error.Message}");
            GetTree().Quit(1);
        }
    }

    private Button FindMenuButton(string text)
    {
        foreach (var node in _menu!.FindChildren("*", "Button", recursive: true, owned: false))
            if (node is Button button && button.Text == text) return button;
        throw new InvalidOperationException($"Missing menu button: {text}");
    }

    private void CheckMenuBounds()
    {
        var bounds = new Rect2(Vector2.Zero, LogicalViewportSize);
        foreach (var control in new Control?[] { _startButton, _levelSearch, _levelList, _romImportButton, _selectedLevelPreview })
        {
            CheckMenu(control != null, "menu control is missing");
            var rect = control!.GetGlobalRect();
            CheckMenu(rect.Size.X > 0 && rect.Size.Y > 0 && rect.Position.X >= 0 && rect.Position.Y >= 0 &&
                rect.End.X <= bounds.End.X && rect.End.Y <= bounds.End.Y,
                $"{control.GetType().Name} extends outside the logical viewport: {rect}");
        }
    }

    private static void CheckMenu(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
