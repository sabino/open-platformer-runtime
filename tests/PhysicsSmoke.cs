using Godot;
using System;
using System.Collections.Generic;

public static class PhysicsSmoke
{
    public static int Main()
    {
        var physics = new SmwPhysics();
        var solids = new List<Rect2> { new(0, 128, 512, 32) };
        var state = physics.MakeState(32, 64);

        for (var i = 0; i < 90; i++)
        {
            physics.Step(ref state, new SmwPhysics.FrameInput
            {
                Right = true,
                Run = true,
            }, solids);
        }

        if (state.X <= 32)
        {
            Console.Error.WriteLine("expected player to move right");
            return 1;
        }
        if (!state.OnGround)
        {
            Console.Error.WriteLine("expected player to land on ground");
            return 1;
        }

        physics.Step(ref state, new SmwPhysics.FrameInput
        {
            Jump = true,
            JumpPressed = true,
        }, solids);

        if (state.YSpeed >= 0)
        {
            Console.Error.WriteLine("expected jump to set negative y speed");
            return 1;
        }

        Console.WriteLine("smw-godot C# physics smoke: ok");
        return 0;
    }
}
