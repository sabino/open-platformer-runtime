using Godot;
using System;
using System.Collections.Generic;

public static class PhysicsSmoke
{
    public static int Main()
    {
        var physics = new SmwPhysics();
        if (!CheckReferenceTables())
        {
            return 1;
        }
        if (!CheckFlatGroundHorizontalPhysics(physics))
        {
            return 1;
        }
        if (!CheckLevelHorizontalBounds(physics))
        {
            return 1;
        }

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

        state = physics.MakeState(32, 100);
        state.OnGround = true;
        physics.Step(ref state, new SmwPhysics.FrameInput
        {
            Spin = true,
            SpinPressed = true,
        }, []);

        if (!state.SpinJump || state.YSpeed >= 0)
        {
            Console.Error.WriteLine("expected spin button to start a spin jump");
            return 1;
        }

        Console.WriteLine("smw-godot C# physics smoke: ok");
        return 0;
    }

    private static bool CheckReferenceTables()
    {
        sbyte[] expected =
        [
            unchecked((sbyte)0xec), 0x14,
            unchecked((sbyte)0xdc), 0x24,
            unchecked((sbyte)0xdc), 0x24,
            unchecked((sbyte)0xd0), 0x30,
        ];

        for (var i = 0; i < expected.Length; i++)
        {
            if (SmwPhysics.HorizontalMaxSpeedTable[i] != expected[i])
            {
                Console.Error.WriteLine($"horizontal max speed table mismatch at {i}");
                return false;
            }
        }

        return true;
    }

    private static bool CheckFlatGroundHorizontalPhysics(SmwPhysics physics)
    {
        var state = physics.MakeState(0, 0);
        state.OnGround = true;
        for (var i = 0; i < 90; i++)
        {
            physics.Step(ref state, new SmwPhysics.FrameInput { Right = true }, []);
        }
        if (state.XSpeed != 0x14)
        {
            Console.Error.WriteLine($"expected flat walk speed cap 0x14, got 0x{state.XSpeed:X2}");
            return false;
        }

        state = physics.MakeState(0, 0);
        state.OnGround = true;
        for (var i = 0; i < 90; i++)
        {
            physics.Step(ref state, new SmwPhysics.FrameInput { Right = true, Run = true }, []);
        }
        if (state.XSpeed != 0x24)
        {
            Console.Error.WriteLine($"expected flat run speed cap 0x24 before P-meter, got 0x{state.XSpeed:X2}");
            return false;
        }

        state = physics.MakeState(0, 0);
        state.OnGround = true;
        state.XSpeed = 0x23;
        state.PMeter = 0x6E;
        physics.Step(ref state, new SmwPhysics.FrameInput { Right = true, Run = true }, []);
        if (state.PMeter != 0x70 || state.XSpeed <= 0x23 || state.XSpeed > 0x30)
        {
            Console.Error.WriteLine($"expected P-meter sprint acceleration, got xs=0x{state.XSpeed:X2} p=0x{state.PMeter:X2}");
            return false;
        }

        state = physics.MakeState(0, 0);
        state.OnGround = true;
        state.XSpeed = 0x14;
        physics.Step(ref state, new SmwPhysics.FrameInput(), []);
        if (state.XSpeed != 0x13 || state.SubXSpeed != 0xE0)
        {
            Console.Error.WriteLine($"expected ground friction 0x0020, got xs=0x{state.XSpeed:X2} sub=0x{state.SubXSpeed:X2}");
            return false;
        }

        return true;
    }

    private static bool CheckLevelHorizontalBounds(SmwPhysics physics)
    {
        var state = physics.MakeState(1, 96);
        state.OnGround = true;
        for (var i = 0; i < 60; i++)
        {
            physics.Step(
                ref state,
                new SmwPhysics.FrameInput { Left = true, Run = true },
                [],
                [],
                levelLeft: 0,
                levelRight: 256);
        }
        if (state.X != 0 || state.XSpeed < 0 || state.SubX != 0)
        {
            Console.Error.WriteLine($"expected left level bound clamp, got x={state.X} sub={state.SubX} xs={state.XSpeed}");
            return false;
        }

        state = physics.MakeState(250, 96);
        state.OnGround = true;
        for (var i = 0; i < 60; i++)
        {
            physics.Step(
                ref state,
                new SmwPhysics.FrameInput { Right = true, Run = true },
                [],
                [],
                levelLeft: 0,
                levelRight: 256);
        }
        var expectedRight = 256 - SmwPhysics.PlayerWidth;
        if (state.X != expectedRight || state.XSpeed > 0 || state.SubX != 0)
        {
            Console.Error.WriteLine($"expected right level bound clamp, got x={state.X} sub={state.SubX} xs={state.XSpeed}");
            return false;
        }

        return true;
    }
}
