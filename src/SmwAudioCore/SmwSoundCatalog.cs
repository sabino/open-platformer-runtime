using System.Collections.Generic;

namespace OpenPlatformerRuntime.Audio;

public sealed record SmwSoundCommand(string Name, int Port, byte Command, bool MusicCue = false, bool CourseSequence = false);

/// <summary>Names/identifiers only; sound instructions and samples are read from the ROM.</summary>
public static class SmwSoundCatalog
{
    public static IReadOnlyList<SmwSoundCommand> Commands { get; } = Build();
    private static IReadOnlyList<SmwSoundCommand> Build()
    {
        string[] port3 = ["Coin", "Prize block", "Vine block", "Spin jump", "1-UP", "Fireball", "Shatter", "Springboard",
            "Bullet Bill", "Egg hatch", "Item into reserve", "Item from reserve", "Item from reserve (alternate)", "Scroll screen",
            "Door", "Bullet Bill (alternate)", "Drum roll", "Drum roll end", "Lose Yoshi", "Unused cape", "Overworld tile appears",
            "Overworld castle collapse", "Fire spit", "Thunder", "Clap / pop", "Castle bomb", "Castle fuse", "Switch palace eject",
            "Hurry-up warning", "Whistle", "Yoshi mount", "Koopaling hits lava", "Yoshi tongue", "Message box", "Move on overworld",
            "P-switch time low", "Yoshi enemy stomp", "Swooper", "Podoboo", "Enemy stunned", "Correct", "Incorrect",
            "Firework whistle", "Firework bang", "Podoboo left 100", "Podoboo left 71", "Podoboo left 43", "Podoboo left 14",
            "Podoboo right 14", "Podoboo right 43", "Podoboo right 71", "Podoboo right 100"];
        string[] port0 = ["Hit head", "Contact", "Kick shell", "Pipe / hurt", "Midway point", "Yoshi gulp", "Dry Bones collapse",
            "Spin-jump kill", "Fly with cape", "Collect power-up", "ON/OFF switch", "Item past goal", "Get cape", "Swim",
            "Hurt while flying", "Magikoopa magic", "Pause music", "Resume music", "Stomp 1", "Stomp 2", "Stomp 3", "Stomp 4",
            "Stomp 5", "Stomp 6", "Stomp 7", "Grinder click 1", "Grinder click 2", "Dragon coin", "Hurry-up chime", "P-balloon",
            "Koopaling defeated", "Yoshi spit", "Valley of Bowser appears", "Removed effect 22 (silent)", "Lemmy / Wendy fall",
            "Removed effect 24 (silent)", "Blargg roar", "Firework whistle left", "Firework bang left", "Firework whistle right",
            "Firework bang right", "Peach pops up"];
        var entries = new List<SmwSoundCommand>();
        for (var i = 0; i < port3.Length; i++) entries.Add(new(port3[i], 3, (byte)(i + 1)));
        for (var i = 0; i < port0.Length; i++) entries.Add(new(port0[i], 0, (byte)(i + 1)));
        entries.Add(new("Jump", 1, 1)); entries.Add(new("Yoshi drums on", 1, 2));
        entries.Add(new("Yoshi drums off", 1, 3)); entries.Add(new("Two-note jump-channel effect", 1, 4));
        entries.Add(new("Hurry-up tempo + warning", 0, 0x80));
        entries.Add(new("Course clear", 2, 0x0C, MusicCue: true));
        entries.Add(new("Circle close / zoom in", 2, 0x11, MusicCue: true));
        entries.Add(new("Course clear → circle close", 2, 0x0C, MusicCue: true, CourseSequence: true));
        entries.Add(new("Boss clear", 2, 0x0B, MusicCue: true));
        entries.Add(new("Player died", 2, 9, MusicCue: true)); entries.Add(new("Game over", 2, 10, MusicCue: true));
        entries.Add(new("Keyhole close", 2, 15, MusicCue: true));
        return entries.AsReadOnly();
    }
}
