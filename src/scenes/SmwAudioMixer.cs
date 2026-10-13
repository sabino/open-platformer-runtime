using Godot;
using System;

namespace OpenPlatformerRuntime;

/// <summary>Optional monitor controls; original DSP output is untouched.</summary>
public partial class SmwAudioMixer : VBoxContainer
{
    public SmwAudio Audio { get; set; } = null!;
    private CheckBox _enabled = null!;
    private VBoxContainer _rows = null!;
    private readonly Label[] _voiceLabels = new Label[8];
    private double _refresh;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 8);
        var switches = new HBoxContainer(); AddChild(switches);
        _enabled = new CheckBox { Text = "Enable monitor mix" };
        _enabled.Toggled += value => Audio.Monitor.Enabled = value; switches.AddChild(_enabled);
        var echo = new CheckBox { Text = "Include original echo", TooltipText = "Echo contains every original voice, including muted voices. Leave off for clean solos." };
        echo.Toggled += value => { Enable(); Audio.Monitor.IncludeOriginalEcho = value; }; switches.AddChild(echo);
        var reset = new Button { Text = "Original mix" };
        reset.Pressed += () => { Audio.Monitor.Reset(); _enabled.SetPressedNoSignal(false); echo.SetPressedNoSignal(false); RebuildRows(); };
        switches.AddChild(reset);
        AddChild(new Label { Text = "Monitor controls affect listening only. Solos are dry unless original echo is included.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(scroll);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 5); scroll.AddChild(_rows);
        RebuildRows();
    }

    private void Enable() { Audio.Monitor.Enabled = true; _enabled.SetPressedNoSignal(true); }
    private void RebuildRows()
    {
        foreach (var child in _rows.GetChildren()) { _rows.RemoveChild(child); child.QueueFree(); }
        _rows.AddChild(new Label { Text = "Eight voices — each voice can change instruments during a song" });
        for (var voice = 0; voice < 8; voice++) _voiceLabels[voice] = AddRow(sample: false, voice);
        _rows.AddChild(new HSeparator());
        _rows.AddChild(new Label { Text = "Instrument samples — applies to all voices using that BRR sample" });
        for (var sample = 0; sample < 20; sample++) AddRow(sample: true, sample);
    }

    private Label AddRow(bool sample, int id)
    {
        var row = new HBoxContainer(); _rows.AddChild(row);
        var label = new Label { Text = sample ? $"BRR {id:X2}" : $"Voice {id + 1}", CustomMinimumSize = new Vector2(142, 28) };
        label.AddThemeFontSizeOverride("font_size", 11); row.AddChild(label);
        Slider(row, "Gain", -48, 12, 0, value => { Enable(); Audio.Monitor.SetGain(sample, id, (float)value); }, "dB");
        Slider(row, "L", 0, 200, 100, value => { Enable(); Audio.Monitor.SetSideGain(sample, id, false, (float)value / 100); }, "%");
        Slider(row, "R", 0, 200, 100, value => { Enable(); Audio.Monitor.SetSideGain(sample, id, true, (float)value / 100); }, "%");
        var mute = new CheckBox { Text = "Mute" }; row.AddChild(mute);
        mute.Toggled += value => { Enable(); Audio.Monitor.SetMuted(sample, id, value); };
        var solo = new CheckBox { Text = "Solo" }; row.AddChild(solo);
        solo.Toggled += value => { Enable(); Audio.Monitor.SetSolo(sample, id, value); };
        return label;
    }

    private static void Slider(Node parent, string title, double min, double max, double value, Action<double> changed, string unit)
    {
        var group = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(98, 0) };
        parent.AddChild(group);
        var label = new Label { Text = $"{title} {value:0}{unit}" }; label.AddThemeFontSizeOverride("font_size", 11); group.AddChild(label);
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = 1, Value = value };
        slider.ValueChanged += v => { label.Text = $"{title} {v:0}{unit}"; changed(v); }; group.AddChild(slider);
    }

    public override void _Process(double delta)
    {
        _refresh += delta; if (_refresh < 0.15) return; _refresh = 0;
        for (var voice = 0; voice < 8; voice++)
        {
            var state = Audio.GetVoiceStatus(voice);
            _voiceLabels[voice].Text = $"Voice {voice + 1} · BRR {state.Sample:X2}{(state.Sfx ? " *" : "")}\nL/R {state.Left}/{state.Right} · ENV {state.Envelope}";
        }
    }
}
