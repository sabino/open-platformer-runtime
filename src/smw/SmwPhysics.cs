using Godot;
using System;
using System.Collections.Generic;

public sealed class SmwPhysics
{
    public const int FixedPoint = 16;
    public const int PlayerWidth = 14;
    public const int PlayerHeight = 28;

    private const int WalkMax = 0x24;
    private const int RunMax = 0x30;
    private const int TurnAccel = 6;
    private const int WalkAccel = 2;
    private const int RunAccel = 3;
    private const int GroundFriction = 4;
    private const int AirAccel = 1;
    private const int AirFriction = 1;
    private const int MaxFall = 0x40;

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
        public int XSpeed;
        public int YSpeed;
        public bool OnGround;
        public int Facing;
        public int JumpHeldFrames;
        public int PMeter;
        public bool SpinJump;
    }

    public struct FrameInput
    {
        public bool Left;
        public bool Right;
        public bool Down;
        public bool Jump;
        public bool JumpPressed;
        public bool SpinPressed;
        public bool Run;
    }

    public PlayerState MakeState(int xPx, int yPx)
    {
        return new PlayerState
        {
            X = xPx * FixedPoint,
            Y = yPx * FixedPoint,
            Facing = 1,
        };
    }

    public void Step(ref PlayerState state, FrameInput input, IReadOnlyList<Rect2> solids)
    {
        ApplyHorizontal(ref state, input);
        ApplyJumpAndGravity(ref state, input);

        state.X += state.XSpeed;
        ResolveAxis(ref state, solids, horizontal: true);

        state.Y += state.YSpeed;
        state.OnGround = false;
        ResolveAxis(ref state, solids, horizontal: false);
    }

    public Rect2 PlayerRect(PlayerState state)
    {
        return new Rect2(
            new Vector2(state.X / 16.0f, state.Y / 16.0f),
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
            var target = input.Run ? RunMax : WalkMax;
            var accel = input.Run ? RunAccel : WalkAccel;
            if (!state.OnGround)
            {
                accel = AirAccel;
            }
            if (state.XSpeed != 0 && Math.Sign(state.XSpeed) != dir)
            {
                accel = TurnAccel;
            }
            state.XSpeed = MoveToward(state.XSpeed, dir * target, accel);
        }
        else
        {
            var friction = state.OnGround ? GroundFriction : AirFriction;
            state.XSpeed = MoveToward(state.XSpeed, 0, friction);
        }

        var absSpeed = Math.Abs(state.XSpeed);
        if (state.OnGround && input.Run && absSpeed >= WalkMax)
        {
            state.PMeter = Math.Min(0x70, state.PMeter + 2);
        }
        else
        {
            state.PMeter = Math.Max(0, state.PMeter - 1);
        }
    }

    private static void ApplyJumpAndGravity(ref PlayerState state, FrameInput input)
    {
        if (state.OnGround && input.JumpPressed)
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

        if (state.YSpeed < 0 && input.Jump)
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
                    state.X = (int)MathF.Round((solid.Position.X - PlayerWidth) * FixedPoint);
                }
                else if (state.XSpeed < 0)
                {
                    state.X = (int)MathF.Round((solid.Position.X + solid.Size.X) * FixedPoint);
                }
                state.XSpeed = 0;
            }
            else
            {
                if (state.YSpeed > 0)
                {
                    state.Y = (int)MathF.Round((solid.Position.Y - PlayerHeight) * FixedPoint);
                    state.OnGround = true;
                }
                else if (state.YSpeed < 0)
                {
                    state.Y = (int)MathF.Round((solid.Position.Y + solid.Size.Y) * FixedPoint);
                }
                state.YSpeed = 0;
            }

            rect = PlayerRect(state);
        }
    }

    private static int MoveToward(int value, int target, int delta)
    {
        if (value < target)
        {
            return Math.Min(value + delta, target);
        }
        if (value > target)
        {
            return Math.Max(value - delta, target);
        }
        return value;
    }
}
