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
        if (!CheckDebugTraceState(physics))
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
        if (!CheckGroundedVerticalPhysics(physics))
        {
            return 1;
        }
        if (!CheckCapeFloatFallCap(physics))
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
        if (!CheckReusableSlopeProbe())
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
        if (!CheckHorizontalOnlySolid(physics))
        {
            return 1;
        }
        if (!CheckWideFloorIsNotSideWall(physics))
        {
            return 1;
        }
        if (!CheckFarSideCorrectionIsIgnored(physics))
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

        int[] expectedGravity = [0x06, 0x03, 0x04, 0x10, -0x0C, 0x01, 0x03, 0x04, 0x05, 0x06];
        for (var i = 0; i < expectedGravity.Length; i++)
        {
            if (SmwPhysics.VerticalGravityTable[i] != expectedGravity[i])
            {
                Console.Error.WriteLine($"vertical gravity table mismatch at {i}");
                return false;
            }
        }

        int[] expectedMaxFall = [0x40, 0x40, 0x20, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40];
        for (var i = 0; i < expectedMaxFall.Length; i++)
        {
            if (SmwPhysics.VerticalMaxFallTable[i] != expectedMaxFall[i])
            {
                Console.Error.WriteLine($"vertical max fall table mismatch at {i}");
                return false;
            }
        }

        int[] expectedCapeFloat = [0x10, -0x38, -0x20, 0x02];
        for (var i = 0; i < expectedCapeFloat.Length; i++)
        {
            if (SmwPhysics.CapeFloatYSpeedTable[i] != expectedCapeFloat[i])
            {
                Console.Error.WriteLine($"cape float y-speed table mismatch at {i}");
                return false;
            }
        }

        return true;
    }

    private static bool CheckDebugTraceState(SmwPhysics physics)
    {
        if (SmwPhysics.JumpSpeedIndexFor(0x00, spin: false) != 0 ||
            SmwPhysics.JumpYSpeedFor(0x00, spin: false) != -80 ||
            SmwPhysics.JumpSpeedIndexFor(0x30, spin: false) != 12 ||
            SmwPhysics.JumpYSpeedFor(0x30, spin: true) != -87)
        {
            Console.Error.WriteLine("expected public jump lookup helpers to mirror native jump table");
            return false;
        }

        var state = physics.MakeState(12, 34, SmwPhysics.CapePowerup);
        state.PMeter = 0x70;
        state.SpinJump = true;
        state.RunningTakeoff = true;
        state.Ducking = true;
        state.JumpHeldFrames = 5;
        state.CapeFloatFrames = 3;
        var trace = physics.CaptureTrace(state);
        if (trace.PMeter != 0x70 ||
            trace.Powerup != SmwPhysics.CapePowerup ||
            !trace.SpinJump ||
            !trace.RunningTakeoff ||
            !trace.Ducking ||
            trace.JumpHeldFrames != 5 ||
            trace.CapeFloatFrames != 3)
        {
            Console.Error.WriteLine("expected trace state to preserve transient physics debug fields");
            return false;
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

    private static bool CheckGroundedVerticalPhysics(SmwPhysics physics)
    {
        var floor = new List<Rect2> { new(0, 100, 256, 16) };
        var state = physics.MakeState(16, 68, SmwPhysics.BigPowerup);
        state.OnGround = true;
        state.YSpeed = 24;
        physics.Step(ref state, new SmwPhysics.FrameInput(), floor);
        if (!state.OnGround || state.Y != 68 || state.YSpeed != 0)
        {
            Console.Error.WriteLine($"expected grounded vertical speed to stay pinned, got g={state.OnGround} y={state.Y} ys={state.YSpeed}");
            return false;
        }

        state = physics.MakeState(280, 68, SmwPhysics.BigPowerup);
        state.OnGround = true;
        physics.Step(ref state, new SmwPhysics.FrameInput(), floor);
        if (state.OnGround || state.Y != 68 || state.YSpeed != 0)
        {
            Console.Error.WriteLine($"expected first ledge frame to clear grounded state without gravity, got g={state.OnGround} y={state.Y} ys={state.YSpeed}");
            return false;
        }

        physics.Step(ref state, new SmwPhysics.FrameInput(), floor);
        if (state.YSpeed != 6)
        {
            Console.Error.WriteLine($"expected gravity one frame after ledge clear, got ys={state.YSpeed}");
            return false;
        }

        return true;
    }

    private static bool CheckCapeFloatFallCap(SmwPhysics physics)
    {
        var state = physics.MakeState(0, 0, SmwPhysics.CapePowerup);
        state.YSpeed = 0x30;
        physics.Step(ref state, new SmwPhysics.FrameInput { Jump = true }, []);
        if (state.YSpeed != 0x10 || state.CapeFloatFrames != 0x0F)
        {
            Console.Error.WriteLine($"expected cape jump-hold fall cap, got ys=0x{state.YSpeed:X2} cape_float={state.CapeFloatFrames}");
            return false;
        }

        state = physics.MakeState(0, 0, SmwPhysics.CapePowerup);
        state.YSpeed = 0x30;
        physics.Step(ref state, new SmwPhysics.FrameInput(), []);
        if (state.YSpeed == 0x10 || state.CapeFloatFrames != 0)
        {
            Console.Error.WriteLine($"expected cape fall without jump hold to skip float cap, got ys=0x{state.YSpeed:X2} cape_float={state.CapeFloatFrames}");
            return false;
        }

        state = physics.MakeState(0, 0, SmwPhysics.BigPowerup);
        state.YSpeed = 0x30;
        physics.Step(ref state, new SmwPhysics.FrameInput { Jump = true }, []);
        if (state.YSpeed == 0x10 || state.CapeFloatFrames != 0)
        {
            Console.Error.WriteLine($"expected non-cape jump hold to skip cape cap, got ys=0x{state.YSpeed:X2} cape_float={state.CapeFloatFrames}");
            return false;
        }

        state = physics.MakeState(0, 0, SmwPhysics.CapePowerup);
        state.YSpeed = -0x20;
        physics.Step(ref state, new SmwPhysics.FrameInput { Jump = true }, []);
        if (state.YSpeed >= 0 || state.CapeFloatFrames != 0)
        {
            Console.Error.WriteLine($"expected rising cape jump to use normal jump-hold gravity, got ys=0x{state.YSpeed:X2} cape_float={state.CapeFloatFrames}");
            return false;
        }

        state = physics.MakeState(0, 0, SmwPhysics.CapePowerup);
        state.CapeFloatFrames = 7;
        physics.SetPowerup(ref state, SmwPhysics.BigPowerup);
        if (state.CapeFloatFrames != 0)
        {
            Console.Error.WriteLine($"expected power-down away from cape to clear float timer, got cape_float={state.CapeFloatFrames}");
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

    private static bool CheckReusableSlopeProbe()
    {
        var slopes = new List<SmwPhysics.SlopeSurface>
        {
            new(0, 128, 128, 96),
            new(0, 96, 64, 64, Ceiling: true),
        };

        if (!SmwPhysics.TryResolveFloorSlope(
            probeX: 64.0f,
            bottom: 112.0f,
            ySpeed: 2.0f,
            slopes,
            aboveTolerance: 8.0f,
            belowTolerance: 16.0f,
            out var surfaceY) ||
            Math.Abs(surfaceY - 112.0f) > 0.01f)
        {
            Console.Error.WriteLine($"expected reusable slope probe to resolve floor at 112, got {surfaceY}");
            return false;
        }

        if (SmwPhysics.TryResolveFloorSlope(
            probeX: 64.0f,
            bottom: 112.0f,
            ySpeed: -1.0f,
            slopes,
            aboveTolerance: 8.0f,
            belowTolerance: 16.0f,
            out _))
        {
            Console.Error.WriteLine("expected reusable slope probe to ignore upward motion");
            return false;
        }

        if (SmwPhysics.TryResolveFloorSlope(
            probeX: 64.0f,
            bottom: 80.0f,
            ySpeed: 2.0f,
            slopes,
            aboveTolerance: 8.0f,
            belowTolerance: 16.0f,
            out _))
        {
            Console.Error.WriteLine("expected reusable slope probe to ignore distant floor");
            return false;
        }

        if (SmwPhysics.TryResolveFloorSlopeFromAbove(
            probeX: 64.0f,
            top: 112.0f,
            bottom: 128.0f,
            previousBottom: 128.0f,
            ySpeed: 2.0f,
            slopes,
            aboveTolerance: 8.0f,
            belowTolerance: 16.0f,
            out _))
        {
            Console.Error.WriteLine("expected from-above slope probe to reject player already below slope surface");
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

        state = physics.MakeState(24, 84);
        physics.Step(ref state, new SmwPhysics.FrameInput { Left = true }, [], slopes);
        if (state.Y == 80 || state.YSpeed == 0)
        {
            Console.Error.WriteLine($"expected walking motion to ignore slope ceiling, got y={state.Y} ys={state.YSpeed}");
            return false;
        }

        return true;
    }

    private static bool CheckHorizontalOnlySolid(SmwPhysics physics)
    {
        var solids = new List<Rect2> { new(24, 64, 16, 32) };
        var noStep = new List<bool> { false };
        var noVertical = new List<bool> { false };
        var state = physics.MakeState(12, 64);
        state.XSpeed = 0x40;

        physics.Step(ref state, new SmwPhysics.FrameInput { Right = true }, solids, noStep, noVertical, []);
        if (state.X + SmwPhysics.PlayerWidth > 24 || state.Y != 64)
        {
            Console.Error.WriteLine($"expected horizontal-only solid to block side motion without step-up, got x={state.X} y={state.Y}");
            return false;
        }

        state = physics.MakeState(24, 36);
        state.YSpeed = 0x40;
        physics.Step(ref state, new SmwPhysics.FrameInput(), [new Rect2(24, 56, 16, 16)], noStep, noVertical, []);
        if (state.OnGround || state.Y <= 36 || state.YSpeed <= 0)
        {
            Console.Error.WriteLine($"expected horizontal-only solid to ignore vertical floor resolution, got y={state.Y} ys={state.YSpeed} ground={state.OnGround}");
            return false;
        }

        return true;
    }

    private static bool CheckWideFloorIsNotSideWall(SmwPhysics physics)
    {
        var state = physics.MakeState(64, 128);
        state.XSpeed = -0x20;
        physics.Step(
            ref state,
            new SmwPhysics.FrameInput { Left = true },
            [new Rect2(0, 128, 512, 32)],
            [],
            []);
        if (state.X > 64 || state.XSpeed == 0)
        {
            Console.Error.WriteLine($"expected wide floor overlap to be ignored by horizontal wall resolver, got x={state.X} xs={state.XSpeed}");
            return false;
        }

        return true;
    }

    private static bool CheckFarSideCorrectionIsIgnored(SmwPhysics physics)
    {
        var state = physics.MakeState(80, 64);
        state.XSpeed = -0x20;
        physics.Step(
            ref state,
            new SmwPhysics.FrameInput { Left = true },
            [new Rect2(0, 64, 512, 32)],
            [false],
            [false],
            []);
        if (state.X > 256)
        {
            Console.Error.WriteLine($"expected far horizontal correction to be ignored, got x={state.X}");
            return false;
        }

        return true;
    }
}
