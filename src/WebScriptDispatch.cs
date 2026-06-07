#if GODOT_WEB && GODOT_WEB_MANUAL_DISPATCH
using Godot;
using Godot.NativeInterop;

namespace OpenPlatformerRuntime;

public partial class Main
{
    private static readonly StringName WebMethodReady = "_Ready";
    private static readonly StringName WebMethodUnhandledInput = "_UnhandledInput";
    private static readonly StringName WebMethodProcess = "_Process";
    private static readonly StringName WebMethodExitTree = "_ExitTree";
    private static readonly StringName WebMethodRefreshAfterImport = nameof(RefreshAfterWebAssetImport);
    private static readonly StringName WebMethodTitleStartProbe = nameof(StartGameFromTitleStartProbe);

    protected override bool InvokeGodotClassMethod(in godot_string_name method, NativeVariantPtrArgs args, out godot_variant ret)
    {
        if (method == WebMethodReady && args.Count == 0)
        {
            _Ready();
            ret = default;
            return true;
        }

        if (method == WebMethodUnhandledInput && args.Count == 1)
        {
            _UnhandledInput(VariantUtils.ConvertTo<InputEvent>(in args[0]));
            ret = default;
            return true;
        }

        if (method == WebMethodProcess && args.Count == 1)
        {
            _Process(VariantUtils.ConvertTo<double>(in args[0]));
            ret = default;
            return true;
        }

        if (method == WebMethodExitTree && args.Count == 0)
        {
            _ExitTree();
            ret = default;
            return true;
        }

        if (method == WebMethodRefreshAfterImport && args.Count == 2)
        {
            RefreshAfterWebAssetImport(
                VariantUtils.ConvertTo<string>(in args[0]),
                VariantUtils.ConvertTo<bool>(in args[1]));
            ret = default;
            return true;
        }

        if (method == WebMethodTitleStartProbe && args.Count == 0)
        {
            StartGameFromTitleStartProbe();
            ret = default;
            return true;
        }

        return base.InvokeGodotClassMethod(in method, args, out ret);
    }

    protected override bool HasGodotClassMethod(in godot_string_name method)
    {
        if (method == WebMethodReady ||
            method == WebMethodUnhandledInput ||
            method == WebMethodProcess ||
            method == WebMethodExitTree ||
            method == WebMethodRefreshAfterImport ||
            method == WebMethodTitleStartProbe)
        {
            return true;
        }

        return base.HasGodotClassMethod(in method);
    }
}

public partial class GameScene
{
    private static readonly StringName WebMethodReady = "_Ready";
    private static readonly StringName WebMethodPhysicsProcess = "_PhysicsProcess";
    private static readonly StringName WebMethodExitTree = "_ExitTree";

    protected override bool InvokeGodotClassMethod(in godot_string_name method, NativeVariantPtrArgs args, out godot_variant ret)
    {
        if (method == WebMethodReady && args.Count == 0)
        {
            _Ready();
            ret = default;
            return true;
        }

        if (method == WebMethodPhysicsProcess && args.Count == 1)
        {
            _PhysicsProcess(VariantUtils.ConvertTo<double>(in args[0]));
            ret = default;
            return true;
        }

        if (method == WebMethodExitTree && args.Count == 0)
        {
            _ExitTree();
            ret = default;
            return true;
        }

        return base.InvokeGodotClassMethod(in method, args, out ret);
    }

    protected override bool HasGodotClassMethod(in godot_string_name method)
    {
        if (method == WebMethodReady ||
            method == WebMethodPhysicsProcess ||
            method == WebMethodExitTree)
        {
            return true;
        }

        return base.HasGodotClassMethod(in method);
    }

    private sealed partial class Map16TileLayer
    {
        private static readonly StringName WebMethodDraw = "_Draw";

        protected override bool InvokeGodotClassMethod(in godot_string_name method, NativeVariantPtrArgs args, out godot_variant ret)
        {
            if (method == WebMethodDraw && args.Count == 0)
            {
                _Draw();
                ret = default;
                return true;
            }

            return base.InvokeGodotClassMethod(in method, args, out ret);
        }

        protected override bool HasGodotClassMethod(in godot_string_name method)
        {
            if (method == WebMethodDraw)
            {
                return true;
            }

            return base.HasGodotClassMethod(in method);
        }
    }
}

public partial class SmwAudio
{
    private static readonly StringName WebMethodReady = "_Ready";
    private static readonly StringName WebMethodProcess = "_Process";
    private static readonly StringName WebMethodExitTree = "_ExitTree";

    protected override bool InvokeGodotClassMethod(in godot_string_name method, NativeVariantPtrArgs args, out godot_variant ret)
    {
        if (method == WebMethodReady && args.Count == 0)
        {
            _Ready();
            ret = default;
            return true;
        }

        if (method == WebMethodProcess && args.Count == 1)
        {
            _Process(VariantUtils.ConvertTo<double>(in args[0]));
            ret = default;
            return true;
        }

        if (method == WebMethodExitTree && args.Count == 0)
        {
            _ExitTree();
            ret = default;
            return true;
        }

        return base.InvokeGodotClassMethod(in method, args, out ret);
    }

    protected override bool HasGodotClassMethod(in godot_string_name method)
    {
        if (method == WebMethodReady ||
            method == WebMethodProcess ||
            method == WebMethodExitTree)
        {
            return true;
        }

        return base.HasGodotClassMethod(in method);
    }
}
#endif
