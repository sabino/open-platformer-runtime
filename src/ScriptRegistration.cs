using System;
using Godot;

[assembly: AssemblyHasScripts(new Type[]
{
    typeof(OpenPlatformerRuntime.Main),
    typeof(OpenPlatformerRuntime.GameScene),
    typeof(OpenPlatformerRuntime.SmwTerrainLayer),
    typeof(OpenPlatformerRuntime.SmwAudioPlayer),
})]

namespace OpenPlatformerRuntime;

[ScriptPath("res://src/scenes/Main.cs")]
public partial class Main
{
}

[ScriptPath("res://src/scripts/GameScene.cs")]
public partial class GameScene
{
}

[ScriptPath("res://src/scripts/SmwTerrainLayer.cs")]
public partial class SmwTerrainLayer
{
}

[ScriptPath("res://src/scenes/SmwAudioPlayer.cs")]
public partial class SmwAudioPlayer
{
}
