using Godot;
using System;
using System.Collections.Generic;

public sealed class SmwPhysics
{
    public const int PlayerWidth = 14;
    public const int SmallPowerup = 0;
    public const int BigPowerup = 1;
    public const int CapePowerup = 2;
    public const int FirePowerup = 3;
    public const int SmallPlayerHeight = 16;
    public const int DuckingPlayerHeight = 16;
    public const int BigPlayerHeight = 32;
    public const int PlayerHeight = BigPlayerHeight;

    private const int WalkMax = 0x14;
    private const int RunMax = 0x24;
    private const int SprintMax = 0x30;
    private const int WalkAccel = 0x0180;
    private const int RunAccel = 0x0180;
    private const int TurnAccel = 0x0600;
    private const int GroundFriction = 0x0020;
    private const int AirAccel = 0x0100;
    private const int PMeterMax = 0x70;
    private const int PMeterSprintThreshold = 0x23;
    private const int MaxFall = 0x40;
    private const float StepUpTolerance = 12.0f;
    private const float MaxHorizontalCollisionCorrection = 64.0f;

    public static readonly sbyte[] PMeterDeltaTable =
    [
        -1, -1, 2,
    ];

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
        public int Powerup;
        public bool SpinJump;
        public bool RunningTakeoff;
        public bool Ducking;

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
            Ducking = state.Ducking;
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
        public readonly bool Ducking;
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

    public readonly record struct SlopeSurface(float X0, float Y0, float X1, float Y1, bool Ceiling = false);

    public PlayerState MakeState(int xPx, int yPx, int powerup = BigPowerup)
    {
        return new PlayerState
        {
            X = xPx,
            Y = yPx,
            Facing = 1,
            Powerup = Math.Clamp(powerup, SmallPowerup, FirePowerup),
        };
    }

    public void SetPowerup(ref PlayerState state, int powerup)
    {
        powerup = Math.Clamp(powerup, SmallPowerup, FirePowerup);
        var oldHeight = PlayerHeightFor(state);
        state.Powerup = powerup;
        if (state.Powerup == SmallPowerup)
        {
            state.Ducking = false;
        }
        var newHeight = PlayerHeightFor(state);
        state.Y += oldHeight - newHeight;
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
        Step(ref state, input, solids, null, slopes);
    }

    public void Step(
        ref PlayerState state,
        FrameInput input,
        IReadOnlyList<Rect2> solids,
        IReadOnlyList<bool>? solidStepUpEnabled,
        IReadOnlyList<SlopeSurface> slopes)
    {
        Step(ref state, input, solids, solidStepUpEnabled, null, slopes);
    }

    public void Step(
        ref PlayerState state,
        FrameInput input,
        IReadOnlyList<Rect2> solids,
        IReadOnlyList<bool>? solidStepUpEnabled,
        IReadOnlyList<bool>? solidVerticalEnabled,
        IReadOnlyList<SlopeSurface> slopes)
    {
        ApplyDucking(ref state, input);
        ApplyJumpAndGravity(ref state, input);
        ApplyHorizontal(ref state, input);

        IntegrateX(ref state);
        var steppedOntoSolid = ResolveAxis(ref state, solids, solidStepUpEnabled, solidVerticalEnabled, horizontal: true);

        IntegrateY(ref state);
        state.OnGround = false;
        ResolveAxis(ref state, solids, solidStepUpEnabled, solidVerticalEnabled, horizontal: false);
        ResolveSlopes(ref state, slopes);
        if (steppedOntoSolid && state.YSpeed >= 0)
        {
            state.OnGround = true;
        }
    }

    public void Step(
        ref PlayerState state,
        FrameInput input,
        IReadOnlyList<Rect2> solids,
        IReadOnlyList<SlopeSurface> slopes,
        int levelLeft,
        int levelRight)
    {
        Step(ref state, input, solids, slopes);
        ClampHorizontalLevelBounds(ref state, levelLeft, levelRight);
    }

    public void Step(
        ref PlayerState state,
        FrameInput input,
        IReadOnlyList<Rect2> solids,
        IReadOnlyList<bool> solidStepUpEnabled,
        IReadOnlyList<SlopeSurface> slopes,
        int levelLeft,
        int levelRight)
    {
        Step(ref state, input, solids, solidStepUpEnabled, slopes);
        ClampHorizontalLevelBounds(ref state, levelLeft, levelRight);
    }

    public void Step(
        ref PlayerState state,
        FrameInput input,
        IReadOnlyList<Rect2> solids,
        IReadOnlyList<bool> solidStepUpEnabled,
        IReadOnlyList<bool> solidVerticalEnabled,
        IReadOnlyList<SlopeSurface> slopes,
        int levelLeft,
        int levelRight)
    {
        Step(ref state, input, solids, solidStepUpEnabled, solidVerticalEnabled, slopes);
        ClampHorizontalLevelBounds(ref state, levelLeft, levelRight);
    }

    public static void ClampHorizontalLevelBounds(ref PlayerState state, int levelLeft, int levelRight)
    {
        var maxX = Math.Max(levelLeft, levelRight - PlayerWidth);
        if (state.XFloat < levelLeft)
        {
            state.X = levelLeft;
            state.SubX = 0;
            if (state.XSpeed < 0)
            {
                state.XSpeed = 0;
                state.SubXSpeed = 0;
            }
        }
        else if (state.XFloat > maxX)
        {
            state.X = maxX;
            state.SubX = 0;
            if (state.XSpeed > 0)
            {
                state.XSpeed = 0;
                state.SubXSpeed = 0;
            }
        }
    }

    public TraceState CaptureTrace(PlayerState state)
    {
        return new TraceState(state);
    }

    public Rect2 PlayerRect(PlayerState state)
    {
        return new Rect2(
            new Vector2(state.XFloat, state.YFloat),
            new Vector2(PlayerWidth, PlayerHeightFor(state)));
    }

    public static int PlayerHeightFor(PlayerState state)
    {
        return state.Ducking ? DuckingPlayerHeight : PlayerHeightForPowerup(state.Powerup);
    }

    public static int PlayerHeightForPowerup(int powerup)
    {
        return powerup == SmallPowerup ? SmallPlayerHeight : BigPlayerHeight;
    }

    public static bool TryResolveFloorSlope(
        float probeX,
        float bottom,
        float ySpeed,
        IReadOnlyList<SlopeSurface> slopes,
        float aboveTolerance,
        float belowTolerance,
        out float surfaceY)
    {
        foreach (var slope in slopes)
        {
            if (slope.Ceiling)
            {
                continue;
            }

            var minX = MathF.Min(slope.X0, slope.X1);
            var maxX = MathF.Max(slope.X0, slope.X1);
            if (probeX < minX || probeX > maxX)
            {
                continue;
            }

            var t = maxX == minX ? 0.0f : (probeX - slope.X0) / (slope.X1 - slope.X0);
            if (t < 0.0f || t > 1.0f)
            {
                continue;
            }

            surfaceY = slope.Y0 + (slope.Y1 - slope.Y0) * t;
            if (ySpeed < 0 || bottom < surfaceY - aboveTolerance || bottom > surfaceY + belowTolerance)
            {
                continue;
            }

            return true;
        }

        surfaceY = 0.0f;
        return false;
    }

    public static bool TryResolveFloorSlopeFromAbove(
        float probeX,
        float top,
        float bottom,
        float ySpeed,
        IReadOnlyList<SlopeSurface> slopes,
        float aboveTolerance,
        float belowTolerance,
        out float surfaceY)
    {
        if (!TryResolveFloorSlope(
            probeX,
            bottom,
            ySpeed,
            slopes,
            aboveTolerance,
            belowTolerance,
            out surfaceY))
        {
            return false;
        }

        return top < surfaceY - 1.0f;
    }

    private static void ApplyDucking(ref PlayerState state, FrameInput input)
    {
        var shouldDuck = state.OnGround && input.Down && state.Powerup != SmallPowerup;
        if (state.Ducking == shouldDuck)
        {
            return;
        }

        var oldHeight = PlayerHeightFor(state);
        state.Ducking = shouldDuck;
        var newHeight = PlayerHeightFor(state);
        state.Y += oldHeight - newHeight;
    }

    private static void ApplyHorizontal(ref PlayerState state, FrameInput input)
    {
        var dir = 0;
        var duckingOnGround = state.Ducking && state.OnGround;
        if (!duckingOnGround && input.Left)
        {
            dir--;
        }
        if (!duckingOnGround && input.Right)
        {
            dir++;
        }

        if (dir != 0)
        {
            state.Facing = dir > 0 ? 1 : 0;
            var absSpeed = Math.Abs(state.XSpeed);
            var pMeterMode = UpdatePMeterEx(ref state, PMeterModeForHorizontal(state, input, absSpeed));
            var target = HorizontalTargetForPMeterMode(pMeterMode);
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
            UpdatePMeterEx(ref state, 0);
            if (!state.OnGround)
            {
                return;
            }

            if (state.XSpeed > 0)
            {
                AddXAccel(ref state, -GroundFriction);
                if (state.XSpeed < 0)
                {
                    state.XSpeed = 0;
                    state.SubXSpeed = 0;
                }
            }
            else if (state.XSpeed < 0)
            {
                AddXAccel(ref state, GroundFriction);
                if (state.XSpeed > 0)
                {
                    state.XSpeed = 0;
                    state.SubXSpeed = 0;
                }
            }
        }
    }

    private static int PMeterModeForHorizontal(PlayerState state, FrameInput input, int absSpeed)
    {
        if (!input.Run)
        {
            return 0;
        }

        if (absSpeed >= PMeterSprintThreshold && (state.OnGround || state.RunningTakeoff))
        {
            return 2;
        }

        return 1;
    }

    private static int UpdatePMeterEx(ref PlayerState state, int mode)
    {
        mode = Math.Clamp(mode, 0, PMeterDeltaTable.Length - 1);
        var pMeter = state.PMeter + PMeterDeltaTable[mode];
        if (pMeter < 0)
        {
            pMeter = 0;
        }

        if (pMeter >= PMeterMax)
        {
            mode++;
            pMeter = PMeterMax;
        }

        state.PMeter = pMeter;
        return mode;
    }

    private static int HorizontalTargetForPMeterMode(int mode)
    {
        if (mode >= 3)
        {
            return SprintMax;
        }

        return mode >= 1 ? RunMax : WalkMax;
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
            state.RunningTakeoff = state.PMeter >= PMeterMax;
            state.SpinJump = input.SpinPressed;
            state.JumpHeldFrames = 0;
        }
        else if (state.OnGround)
        {
            state.RunningTakeoff = false;
            state.SpinJump = false;
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

    private bool ResolveAxis(
        ref PlayerState state,
        IReadOnlyList<Rect2> solids,
        IReadOnlyList<bool>? solidStepUpEnabled,
        IReadOnlyList<bool>? solidVerticalEnabled,
        bool horizontal)
    {
        var rect = PlayerRect(state);
        var steppedOntoSolid = false;
        for (var solidIndex = 0; solidIndex < solids.Count; solidIndex++)
        {
            var solid = solids[solidIndex];
            if (!rect.Intersects(solid))
            {
                continue;
            }

            if (horizontal)
            {
                if (ShouldIgnoreWideFloorForHorizontalCollision(rect, solid))
                {
                    continue;
                }

                var allowStepUp = solidStepUpEnabled == null ||
                    solidIndex >= solidStepUpEnabled.Count ||
                    solidStepUpEnabled[solidIndex];
                if (allowStepUp && TryStepUp(ref state, solid, rect, PlayerHeightFor(state)))
                {
                    rect = PlayerRect(state);
                    steppedOntoSolid = true;
                    continue;
                }

                if (state.XSpeed > 0)
                {
                    var resolvedX = solid.Position.X - PlayerWidth;
                    if (MathF.Abs(resolvedX - state.XFloat) > MaxHorizontalCollisionCorrection)
                    {
                        continue;
                    }

                    state.X = (int)MathF.Round(resolvedX);
                }
                else if (state.XSpeed < 0)
                {
                    var resolvedX = solid.Position.X + solid.Size.X;
                    if (MathF.Abs(resolvedX - state.XFloat) > MaxHorizontalCollisionCorrection)
                    {
                        continue;
                    }

                    state.X = (int)MathF.Round(resolvedX);
                }
                state.SubX = 0;
                state.SubXSpeed = 0;
                state.XSpeed = 0;
            }
            else
            {
                var allowVertical = solidVerticalEnabled == null ||
                    solidIndex >= solidVerticalEnabled.Count ||
                    solidVerticalEnabled[solidIndex];
                if (!allowVertical)
                {
                    continue;
                }

                if (state.YSpeed > 0)
                {
                    state.Y = (int)MathF.Round(solid.Position.Y - PlayerHeightFor(state));
                    state.OnGround = true;
                    state.RunningTakeoff = false;
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

        return steppedOntoSolid;
    }

    private static bool ShouldIgnoreWideFloorForHorizontalCollision(Rect2 playerRect, Rect2 solid)
    {
        return solid.Size.X >= 128.0f &&
            solid.Size.X > solid.Size.Y * 4.0f &&
            playerRect.Position.Y >= solid.Position.Y &&
            playerRect.Position.Y < solid.Position.Y + solid.Size.Y;
    }

    private static bool TryStepUp(ref PlayerState state, Rect2 solid, Rect2 playerRect, int playerHeight)
    {
        if (state.YSpeed < 0)
        {
            return false;
        }

        var solidTop = solid.Position.Y;
        var playerBottom = playerRect.Position.Y + playerRect.Size.Y;
        if (playerBottom <= solidTop || playerBottom > solidTop + StepUpTolerance)
        {
            return false;
        }

        state.Y = (int)MathF.Round(solidTop - playerHeight);
        state.SubY = 0;
        state.SubYSpeed = 0;
        state.RunningTakeoff = false;
        return true;
    }

    private void ResolveSlopes(ref PlayerState state, IReadOnlyList<SlopeSurface> slopes)
    {
        if (slopes.Count == 0)
        {
            return;
        }

        var probeX = state.XFloat + PlayerWidth * 0.5f;
        var playerHeight = PlayerHeightFor(state);
        var bottom = state.YFloat + playerHeight;
        var top = state.YFloat;
        foreach (var slope in slopes)
        {
            var minX = MathF.Min(slope.X0, slope.X1);
            var maxX = MathF.Max(slope.X0, slope.X1);
            if (probeX < minX || probeX > maxX)
            {
                continue;
            }

            var t = maxX == minX ? 0.0f : (probeX - slope.X0) / (slope.X1 - slope.X0);
            if (t < 0.0f || t > 1.0f)
            {
                continue;
            }

            var surfaceY = slope.Y0 + (slope.Y1 - slope.Y0) * t;
            if (slope.Ceiling)
            {
                if (state.YSpeed >= 0 || top < surfaceY - 16.0f || top > surfaceY + 6.0f)
                {
                    continue;
                }

                state.Y = (int)MathF.Round(surfaceY);
                state.SubY = 0;
                state.SubYSpeed = 0;
                if (state.YSpeed < 0)
                {
                    state.YSpeed = 0;
                }
                return;
            }
        }

        if (!TryResolveFloorSlopeFromAbove(probeX, top, bottom, state.YSpeed, slopes, 6.0f, 16.0f, out var floorY))
        {
            return;
        }

        state.Y = (int)MathF.Round(floorY - playerHeight);
        state.SubY = 0;
        state.SubYSpeed = 0;
        if (state.YSpeed > 0)
        {
            state.YSpeed = 0;
        }
        state.OnGround = true;
        state.RunningTakeoff = false;
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
