// The experimental Godot 4 .NET web export prototype currently needs a managed
// Main entry point before .NET 10, even though Godot owns the real startup path.
#if !NET10_0_OR_GREATER
namespace OpenPlatformerRuntime;

internal static class MainJS
{
    public static void Main()
    {
        // Godot owns startup. This method only satisfies the browser-wasm SDK entrypoint.
    }
}
#endif
