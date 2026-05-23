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
        if (!CheckDuckingState(physics))
        {
            return 1;
        }
        if (!CheckLevelHorizontalBounds(physics))
        {
            return 1;
        }
        if (!CheckSlopeJumpAndLanding(physics))
        {
            return 1;
        }
        if (!CheckSlopeCeiling(physics))
        {
            return 1;
        }
        if (!CheckStepUpCollision(physics))
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

        sbyte[] expectedPMeterDeltas = [-1, -1, 2];
        for (var i = 0; i < expectedPMeterDeltas.Length; i++)
        {
            if (SmwPhysics.PMeterDeltaTable[i] != expectedPMeterDeltas[i])
            {
                Console.Error.WriteLine($"P-meter delta table mismatch at {i}");
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
        state.XSpeed = 0x10;
        state.PMeter = 0x70;
        physics.Step(ref state, new SmwPhysics.FrameInput { Right = true, Run = true }, []);
        if (state.PMeter != 0x6F || state.XSpeed > 0x24)
        {
            Console.Error.WriteLine($"expected full P-meter to decay below sprint threshold, got xs=0x{state.XSpeed:X2} p=0x{state.PMeter:X2}");
            return false;
        }

        state = physics.MakeState(0, 0);
        state.OnGround = true;
        state.XSpeed = 0x30;
        state.PMeter = 0x70;
        physics.Step(ref state, new SmwPhysics.FrameInput { Right = true, Jump = true, JumpPressed = true, Run = true }, []);
        if (!state.RunningTakeoff || state.PMeter != 0x70)
        {
            Console.Error.WriteLine($"expected full P-meter jump to mark running takeoff, got takeoff={state.RunningTakeoff} p=0x{state.PMeter:X2}");
            return false;
        }
        physics.Step(ref state, new SmwPhysics.FrameInput { Right = true, Run = true }, []);
        if (state.PMeter != 0x70 || state.XSpeed <= 0x24 || state.XSpeed > 0x30)
        {
            Console.Error.WriteLine($"expected running-takeoff air sprint carry, got xs=0x{state.XSpeed:X2} p=0x{state.PMeter:X2}");
            return false;
        }

        state = physics.MakeState(0, 0);
        state.OnGround = true;
        state.PMeter = 1;
        physics.Step(ref state, new SmwPhysics.FrameInput(), []);
        if (state.PMeter != 0)
        {
            Console.Error.WriteLine($"expected P-meter decay to clamp at zero, got p=0x{state.PMeter:X2}");
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

        state = physics.MakeState(0, 0);
        state.XSpeed = 0x14;
        state.SubXSpeed = 0x80;
        state.PMeter = 1;
        physics.Step(ref state, new SmwPhysics.FrameInput(), []);
        if (state.XSpeed != 0x14 || state.SubXSpeed != 0x80 || state.PMeter != 0)
        {
            Console.Error.WriteLine($"expected no airborne no-input friction, got xs=0x{state.XSpeed:X2} sub=0x{state.SubXSpeed:X2} p=0x{state.PMeter:X2}");
            return false;
        }

        return true;
    }

    private static bool CheckDuckingState(SmwPhysics physics)
    {
        var floor = new List<Rect2> { new(0, 100, 256, 16) };
        var state = physics.MakeState(16, 68, SmwPhysics.BigPowerup);
        state.OnGround = true;
        physics.Step(ref state, new SmwPhysics.FrameInput { Down = true, Right = true }, floor);
        if (!state.Ducking || SmwPhysics.PlayerHeightFor(state) != SmwPhysics.DuckingPlayerHeight || state.Y != 84)
        {
            Console.Error.WriteLine($"expected big Mario to duck with feet preserved, got duck={state.Ducking} h={SmwPhysics.PlayerHeightFor(state)} y={state.Y}");
            return false;
        }
        if (state.XSpeed != 0)
        {
            Console.Error.WriteLine($"expected grounded ducking to suppress horizontal acceleration, got xs=0x{state.XSpeed:X2}");
            return false;
        }

        physics.Step(ref state, new SmwPhysics.FrameInput(), floor);
        if (state.Ducking || SmwPhysics.PlayerHeightFor(state) != SmwPhysics.BigPlayerHeight || state.Y != 68)
        {
            Console.Error.WriteLine($"expected releasing down to stand with feet preserved, got duck={state.Ducking} h={SmwPhysics.PlayerHeightFor(state)} y={state.Y}");
            return false;
        }

        state = physics.MakeState(16, 84, SmwPhysics.SmallPowerup);
        state.OnGround = true;
        physics.Step(ref state, new SmwPhysics.FrameInput { Down = true }, floor);
        if (state.Ducking || SmwPhysics.PlayerHeightFor(state) != SmwPhysics.SmallPlayerHeight)
        {
            Console.Error.WriteLine($"expected small Mario down input to keep normal small hitbox, got duck={state.Ducking} h={SmwPhysics.PlayerHeightFor(state)}");
            return false;
        }

        state = physics.MakeState(16, 68, SmwPhysics.BigPowerup);
        state.OnGround = true;
        physics.Step(ref state, new SmwPhysics.FrameInput { Down = true }, floor);
        physics.SetPowerup(ref state, SmwPhysics.SmallPowerup);
        if (state.Ducking || SmwPhysics.PlayerHeightFor(state) != SmwPhysics.SmallPlayerHeight || state.Y != 84)
        {
            Console.Error.WriteLine($"expected power-down to clear ducking while preserving feet, got duck={state.Ducking} h={SmwPhysics.PlayerHeightFor(state)} y={state.Y}");
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

    private static bool CheckSlopeJumpAndLanding(SmwPhysics physics)
    {
        var slopes = new List<SmwPhysics.SlopeSurface>
        {
            new(0, 128, 128, 96),
        };
        var state = physics.MakeState(56, 78);
        state.OnGround = true;

        physics.Step(ref state, new SmwPhysics.FrameInput
        {
            Jump = true,
            JumpPressed = true,
        }, [], slopes);

        if (state.OnGround || state.YSpeed >= 0)
        {
            Console.Error.WriteLine($"expected slope jump to leave ground, got on_ground={state.OnGround} ys={state.YSpeed}");
            return false;
        }

        state = physics.MakeState(56, 72);
        state.YSpeed = 12;
        for (var i = 0; i < 16 && !state.OnGround; i++)
        {
            physics.Step(ref state, new SmwPhysics.FrameInput(), [], slopes);
        }

        if (!state.OnGround)
        {
            Console.Error.WriteLine("expected falling player to land on slope");
            return false;
        }

        return true;
    }

    private static bool CheckStepUpCollision(SmwPhysics physics)
    {
        var state = physics.MakeState(49, 84);
        state.OnGround = true;
        state.XSpeed = 0x30;
        physics.Step(ref state, new SmwPhysics.FrameInput { Right = true }, [new Rect2(64, 108, 16, 16)]);
        if (state.Y > 80 || state.XSpeed == 0)
        {
            Console.Error.WriteLine($"expected low ledge side impact to step up, got x={state.X} y={state.Y}");
            return false;
        }

        state = physics.MakeState(49, 84);
        state.OnGround = true;
        state.XSpeed = 0x30;
        physics.Step(ref state, new SmwPhysics.FrameInput { Right = true }, [new Rect2(64, 96, 16, 32)]);
        if (state.X > 50 || state.Y <= 80)
        {
            Console.Error.WriteLine($"expected high wall side impact to block, got x={state.X} y={state.Y}");
            return false;
        }

        return true;
    }

    private static bool CheckSlopeCeiling(SmwPhysics physics)
    {
        var slopes = new List<SmwPhysics.SlopeSurface>
        {
            new(0, 96, 64, 64, Ceiling: true),
        };
        var state = physics.MakeState(24, 84);
        state.YSpeed = -16;

        physics.Step(ref state, new SmwPhysics.FrameInput(), [], slopes);
        if (state.Y != 80 || state.YSpeed != 0 || state.OnGround)
        {
            Console.Error.WriteLine($"expected upward motion to stop against slope ceiling, got y={state.Y} ys={state.YSpeed} ground={state.OnGround}");
            return false;
        }

        state = physics.MakeState(24, 84);
        state.YSpeed = 16;
        physics.Step(ref state, new SmwPhysics.FrameInput(), [], slopes);
        if (state.Y == 80 || state.YSpeed == 0)
        {
            Console.Error.WriteLine($"expected falling motion to ignore slope ceiling, got y={state.Y} ys={state.YSpeed}");
            return false;
        }

        return true;
    }
}
