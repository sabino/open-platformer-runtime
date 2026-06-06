#if GODOT_WEB && GODOT_WEB_MANUAL_BOOTSTRAP
using System;
using System.Runtime.InteropServices;
using Godot.Bridge;
using Godot.NativeInterop;

namespace GodotPlugins.Game;

internal static partial class Main
{
    [UnmanagedCallersOnly(EntryPoint = "godotsharp_game_main_init")]
    private static godot_bool InitializeFromGameProject(
        IntPtr godotDllHandle,
        IntPtr outManagedCallbacks,
        IntPtr unmanagedCallbacks,
        int unmanagedCallbacksSize)
    {
        try
        {
            DllImportResolver dllImportResolver = new GodotDllImportResolver(godotDllHandle).OnResolveDllImport;
            var coreApiAssembly = typeof(global::Godot.GodotObject).Assembly;
            NativeLibrary.SetDllImportResolver(coreApiAssembly, dllImportResolver);

            NativeFuncs.Initialize(unmanagedCallbacks, unmanagedCallbacksSize);
            ManagedCallbacks.Create(outManagedCallbacks);
            ScriptManagerBridge.LookupScriptsInAssembly(typeof(Main).Assembly);

            return godot_bool.True;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return false.ToGodotBool();
        }
    }
}
#endif
