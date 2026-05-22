using Godot;
using System;
using System.Collections.Generic;

public sealed class SmwPhysics
{
    public const int PlayerWidth = 14;
    public const int PlayerHeight = 28;

    private const int WalkMax = 0x14;
    private const int RunMax = 0x24;
    private const int SprintMax = 0x30;
    private const int WalkAccel = 0x0180;
    private const int RunAccel = 0x0180;
    private const int TurnAccel = 0x0600;
    private const int GroundFriction = 0x0020;
    private const int AirAccel = 0x0100;
    private const int AirFriction = 0x0100;
    private const int PMeterMax = 0x70;
    private const int PMeterSprintThreshold = 0x23;
    private const int MaxFall = 0x40;

    public static readonly sbyte[] HorizontalMaxSpeedTable =
    [
        unchecked((sbyte)0xec), 0x14, unchecked((sbyte)0xdc), 0x24,
        unchecked((sbyte)0xdc), 0x24, unchecked((sbyte)0xd0), 0x30,
    ];

    private static readonly int[] JumpHeightTable =
    [
        -80, -74, -82, -76,
        -85, -78, -87, -80,
        -90, -82, -92, -85,
        -95, -87, -97, -90,
    ];

    public struct PlayerState
    {
        public int X;
        public int Y;
        public int SubX;
        public int SubY;
        public int SubXSpeed;
        public int SubYSpeed;
        public int XSpeed;
        public int YSpeed;
        public bool OnGround;
        public int Facing;
        public int JumpHeldFrames;
        public int PMeter;
        public bool SpinJump;

        public float XFloat => X + SubX / 256.0f;
        public float YFloat => Y + SubY / 256.0f;
    }

    public readonly struct TraceState
    {
        public TraceState(PlayerState state)
        {
            X = state.X;
            Y = state.Y;
            SubX = state.SubX;
            SubY = state.SubY;
            XSpeed = state.XSpeed;
            YSpeed = state.YSpeed;
            SubXSpeed = state.SubXSpeed;
            SubYSpeed = state.SubYSpeed;
            OnGround = state.OnGround;
        }

        public readonly int X;
        public readonly int Y;
        public readonly int SubX;
        public readonly int SubY;
        public readonly int XSpeed;
        public readonly int YSpeed;
        public readonly int SubXSpeed;
        public readonly int SubYSpeed;
        public readonly bool OnGround;
    }

    public struct FrameInput
    {
        public bool Left;
        public bool Right;
        public bool Down;
        public bool Jump;
        public bool JumpPressed;
        public bool Spin;
        public bool SpinPressed;
        public bool Run;
    }

    public readonly record struct SlopeSurface(float X0, float Y0, float X1, float Y1);

    public PlayerState MakeState(int xPx, int yPx)
    {
        return new PlayerState
        {
            X = xPx,
            Y = yPx,
            Facing = 1,
        };
    }

    public void Step(ref PlayerState state, FrameInput input, IReadOnlyList<Rect2> solids)
    {
        Step(ref state, input, solids, Array.Empty<SlopeSurface>());
    }

    public void Step(
        ref PlayerState state,
        FrameInput input,
        IReadOnlyList<Rect2> solids,
        IReadOnlyList<SlopeSurface> slopes)
    {
        ApplyHorizontal(ref state, input);
        ApplyJumpAndGravity(ref state, input);

        IntegrateX(ref state);
        ResolveAxis(ref state, solids, horizontal: true);

        IntegrateY(ref state);
        state.OnGround = false;
        ResolveAxis(ref state, solids, horizontal: false);
        ResolveSlopes(ref state, slopes);
    }

    public TraceState CaptureTrace(PlayerState state)
    {
        return new TraceState(state);
    }

    public Rect2 PlayerRect(PlayerState state)
    {
        return new Rect2(
            new Vector2(state.XFloat, state.YFloat),
            new Vector2(PlayerWidth, PlayerHeight));
    }

    private static void ApplyHorizontal(ref PlayerState state, FrameInput input)
    {
        var dir = 0;
        if (input.Left)
        {
            dir--;
        }
        if (input.Right)
        {
            dir++;
        }

        if (dir != 0)
        {
            state.Facing = dir > 0 ? 1 : 0;
            UpdatePMeter(ref state, input, Math.Abs(state.XSpeed));
            var target = HorizontalTarget(input.Run, Math.Abs(state.XSpeed), state.PMeter);
            var accel = input.Run ? RunAccel : WalkAccel;
            if (!state.OnGround)
            {
                accel = AirAccel;
            }
            if (state.XSpeed != 0 && Math.Sign(state.XSpeed) != dir)
            {
                accel = TurnAccel;
            }
            AddXAccel(ref state, dir * accel);
            state.XSpeed = ClampSigned8(state.XSpeed);
            if ((dir > 0 && state.XSpeed > target) || (dir < 0 && state.XSpeed < -target))
            {
                state.XSpeed = dir * target;
                state.SubXSpeed = 0;
            }
        }
        else
        {
            UpdatePMeter(ref state, input, Math.Abs(state.XSpeed));
            var friction = state.OnGround ? GroundFriction : AirFriction;
            if (state.XSpeed > 0)
            {
                AddXAccel(ref state, -friction);
                if (state.XSpeed < 0)
                {
                    state.XSpeed = 0;
                    state.SubXSpeed = 0;
                }
            }
            else if (state.XSpeed < 0)
            {
                AddXAccel(ref state, friction);
                if (state.XSpeed > 0)
                {
                    state.XSpeed = 0;
                    state.SubXSpeed = 0;
                }
            }
        }
    }

    private static void UpdatePMeter(ref PlayerState state, FrameInput input, int absSpeed)
    {
        if (state.OnGround && input.Run && absSpeed >= PMeterSprintThreshold)
        {
            state.PMeter = Math.Min(PMeterMax, state.PMeter + 2);
        }
        else
        {
            state.PMeter = Math.Max(0, state.PMeter - 1);
        }
    }

    private static int HorizontalTarget(bool runHeld, int absSpeed, int pMeter)
    {
        if (!runHeld)
        {
            return WalkMax;
        }

        return pMeter >= PMeterMax && absSpeed >= PMeterSprintThreshold ? SprintMax : RunMax;
    }

    private static void ApplyJumpAndGravity(ref PlayerState state, FrameInput input)
    {
        var jumpStarted = input.JumpPressed || input.SpinPressed;
        if (state.OnGround && jumpStarted)
        {
            var speedIndex = Math.Clamp((Math.Abs(state.XSpeed) >> 2) & 0xFE, 0, JumpHeightTable.Length - 1);
            if (input.SpinPressed)
            {
                speedIndex = Math.Min(speedIndex + 1, JumpHeightTable.Length - 1);
            }

            state.YSpeed = JumpHeightTable[speedIndex];
            state.OnGround = false;
            state.SpinJump = input.SpinPressed;
            state.JumpHeldFrames = 0;
        }

        if (state.YSpeed < 0 && (input.Jump || input.Spin))
        {
            state.YSpeed += 3;
            state.JumpHeldFrames++;
        }
        else
        {
            state.YSpeed += 6;
        }

        if (state.YSpeed > MaxFall)
        {
            state.YSpeed = MaxFall;
        }
    }

    private void ResolveAxis(ref PlayerState state, IReadOnlyList<Rect2> solids, bool horizontal)
    {
        var rect = PlayerRect(state);
        foreach (var solid in solids)
        {
            if (!rect.Intersects(solid))
            {
                continue;
            }

            if (horizontal)
            {
                if (state.XSpeed > 0)
                {
                    state.X = (int)MathF.Round(solid.Position.X - PlayerWidth);
                }
                else if (state.XSpeed < 0)
                {
                    state.X = (int)MathF.Round(solid.Position.X + solid.Size.X);
                }
                state.SubX = 0;
                state.SubXSpeed = 0;
                state.XSpeed = 0;
            }
            else
            {
                if (state.YSpeed > 0)
                {
                    state.Y = (int)MathF.Round(solid.Position.Y - PlayerHeight);
                    state.OnGround = true;
                }
                else if (state.YSpeed < 0)
                {
                    state.Y = (int)MathF.Round(solid.Position.Y + solid.Size.Y);
                }
                state.SubY = 0;
                state.SubYSpeed = 0;
                state.YSpeed = 0;
            }

            rect = PlayerRect(state);
        }
    }

    private void ResolveSlopes(ref PlayerState state, IReadOnlyList<SlopeSurface> slopes)
    {
        if (slopes.Count == 0)
        {
            return;
        }

        var footX = state.XFloat + PlayerWidth * 0.5f;
        var bottom = state.YFloat + PlayerHeight;
        foreach (var slope in slopes)
        {
            var minX = MathF.Min(slope.X0, slope.X1);
            var maxX = MathF.Max(slope.X0, slope.X1);
            if (footX < minX || footX > maxX)
            {
                continue;
            }

            var t = maxX == minX ? 0.0f : (footX - slope.X0) / (slope.X1 - slope.X0);
            if (t < 0.0f || t > 1.0f)
            {
                continue;
            }

            var surfaceY = slope.Y0 + (slope.Y1 - slope.Y0) * t;
            if (bottom < surfaceY - 6.0f || bottom > surfaceY + 16.0f)
            {
                continue;
            }

            state.Y = (int)MathF.Round(surfaceY - PlayerHeight);
            state.SubY = 0;
            state.SubYSpeed = 0;
            if (state.YSpeed > 0)
            {
                state.YSpeed = 0;
            }
            state.OnGround = true;
            return;
        }
    }

    private static void AddXAccel(ref PlayerState state, int accel)
    {
        var combined = ((state.XSpeed & 0xFF) << 8) | (state.SubXSpeed & 0xFF);
        combined = (combined + accel) & 0xFFFF;
        state.SubXSpeed = combined & 0xFF;
        state.XSpeed = ToS8((combined >> 8) & 0xFF);
    }

    private static void IntegrateX(ref PlayerState state)
    {
        var lowDelta = (state.XSpeed * 16) & 0xFF;
        var sum = state.SubX + lowDelta;
        var carry = sum >> 8;
        state.SubX = sum & 0xFF;
        state.X += ArithmeticShiftRight8(state.XSpeed, 4) + carry;
    }

    private static void IntegrateY(ref PlayerState state)
    {
        var lowDelta = (state.YSpeed * 16) & 0xFF;
        var sum = state.SubY + lowDelta;
        var carry = sum >> 8;
        state.SubY = sum & 0xFF;
        state.Y += ArithmeticShiftRight8(state.YSpeed, 4) + carry;
    }

    private static int ArithmeticShiftRight8(int value, int shift)
    {
        return ToS8(value & 0xFF) >> shift;
    }

    private static int ToS8(int value)
    {
        value &= 0xFF;
        return value >= 0x80 ? value - 0x100 : value;
    }

    private static int ClampSigned8(int value)
    {
        return Math.Clamp(value, -128, 127);
    }
}
