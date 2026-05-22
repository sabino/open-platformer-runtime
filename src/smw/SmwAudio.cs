using Godot;
using System.Collections.Generic;

public partial class SmwAudio : Node
{
    private readonly Dictionary<string, AudioStreamPlayer> _players = [];

    public override void _Ready()
    {
        Register("menu_start", "res://generated/smw/audio/sample_09.wav", -7.0f);
        Register("jump", "res://generated/smw/audio/sample_14.wav", -8.0f);
        Register("spin_jump", "res://generated/smw/audio/sample_16.wav", -8.0f);
    }

    public void PlayMenuStart()
    {
        Play("menu_start");
    }

    public void PlayJump()
    {
        Play("jump");
    }

    public void PlaySpinJump()
    {
        Play("spin_jump");
    }

    private void Register(string key, string resourcePath, float volumeDb)
    {
        if (!FileAccess.FileExists(resourcePath))
        {
            return;
        }

        var stream = AudioStreamWav.LoadFromFile(ProjectSettings.GlobalizePath(resourcePath), []);
        if (stream == null)
        {
            return;
        }

        var player = new AudioStreamPlayer
        {
            Name = $"SmwAudio_{key}",
            Stream = stream,
            VolumeDb = volumeDb,
            Bus = "Master",
        };
        AddChild(player);
        _players[key] = player;
    }

    private void Play(string key)
    {
        if (!_players.TryGetValue(key, out var player))
        {
            return;
        }

        player.Stop();
        player.Play();
    }
}
