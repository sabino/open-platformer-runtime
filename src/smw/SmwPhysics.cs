using Godot;
using System;
using System.Collections.Generic;

public sealed class SmwPhysics
{
    private static readonly bool DebugCollisionTrace =
        System.Environment.GetEnvironmentVariable("SMW_DEBUG_COLLISION_TRACE") == "1";
    private static readonly bool DebugSlopeTrace =
        System.Environment.GetEnvironmentVariable("SMW_DEBUG_SLOPE_TRACE") == "1";
    private const float SteepPipeAirborneGroundFlagDepth = 8.0f;
    private const int SteepPipeLooseGroundFrameCount = 3;
    private const int SteepPipeLooseGroundFirstCarryYSpeed = 0x3D;
    private const int SteepPipeLooseGroundPreJumpYSpeed = -0x1A;
    private const int NativeSlopeOverrunLandingSubpixelCorrection = -0x110;

    public const int PlayerWidth = 14;
    public const int SolidSupportLegacy = 0;
    public const int SolidSupportLeadingFoot = 1;
    public const int SmallPowerup = 0;
    public const int BigPowerup = 1;
    public const int CapePowerup = 2;
    public const int FirePowerup = 3;
    public const int SmallPlayerHeight = 16;
    public const int DuckingPlayerHeight = 16;
    public const int BigPlayerHeight = 32;
    public const int PlayerHeight = BigPlayerHeight;
    public const int PlayerCollisionYOffset = 16;
    public const int BigPlayerCollisionYOffset = 0;

    public const int NativeFlatWalkTurnAcceleration = 0x0280;
    public const int NativeFlatRunTurnAcceleration = 0x0500;
    private const int PMeterMax = 0x70;
    private const int PMeterSprintThreshold = 0x23;
    public const int NativeNormalJumpInAirState = 0x0B;
    public const int NativeRunningJumpInAirState = 0x0C;
    public const int NativeFallingInAirState = 0x24;
    private const int CapeFloatFrameCount = 0x10;
    private const int PostLandingAirDragFrameCount = 48;
    private const float StepUpTolerance = 12.0f;
    private const float MaxHorizontalCollisionCorrection = 64.0f;
    private const float NativeHorizontalWallSlack = 1.0f;
    private const int NativeFastAirborneWallSnapSpeed = 0x10;
    private const int NativeRightWallPostCorrectionNibble = 0x02;
    private const int NativeLeftWallPostCorrectionNibble = 0x0D;

    public static readonly sbyte[] PMeterDeltaTable =
    [
        -1, -1, 2,
    ];

    public static readonly sbyte[] HorizontalMaxSpeedTable =
    [
        unchecked((sbyte)0xEC), 0x14, unchecked((sbyte)0xDC), 0x24, unchecked((sbyte)0xDC), 0x24, unchecked((sbyte)0xD0), 0x30, unchecked((sbyte)0xEC), 0x14, unchecked((sbyte)0xDC), 0x24,
        unchecked((sbyte)0xDC), 0x24, unchecked((sbyte)0xD0), 0x30, unchecked((sbyte)0xEC), 0x14, unchecked((sbyte)0xDC), 0x24, unchecked((sbyte)0xDC), 0x24, unchecked((sbyte)0xD0), 0x30,
        unchecked((sbyte)0xE8), 0x12, unchecked((sbyte)0xDC), 0x20, unchecked((sbyte)0xDC), 0x20, unchecked((sbyte)0xD0), 0x2C, unchecked((sbyte)0xEE), 0x18, unchecked((sbyte)0xE0), 0x24,
        unchecked((sbyte)0xE0), 0x24, unchecked((sbyte)0xD4), 0x30, unchecked((sbyte)0xDC), 0x10, unchecked((sbyte)0xDC), 0x1C, unchecked((sbyte)0xDC), 0x1C, unchecked((sbyte)0xD0), 0x28,
        unchecked((sbyte)0xF0), 0x24, unchecked((sbyte)0xE4), 0x24, unchecked((sbyte)0xE4), 0x24, unchecked((sbyte)0xD8), 0x30, unchecked((sbyte)0xDC), 0x10, unchecked((sbyte)0xDC), 0x1C,
        unchecked((sbyte)0xDC), 0x1C, unchecked((sbyte)0xD0), 0x28, unchecked((sbyte)0xDC), 0x10, unchecked((sbyte)0xDC), 0x1C, unchecked((sbyte)0xDC), 0x1C, unchecked((sbyte)0xD0), 0x28,
        unchecked((sbyte)0xF0), 0x24, unchecked((sbyte)0xE4), 0x24, unchecked((sbyte)0xE4), 0x24, unchecked((sbyte)0xD8), 0x30, unchecked((sbyte)0xF0), 0x24, unchecked((sbyte)0xE4), 0x24,
        unchecked((sbyte)0xE4), 0x24, unchecked((sbyte)0xD8), 0x30, unchecked((sbyte)0xDC), unchecked((sbyte)0xF0), unchecked((sbyte)0xDC), unchecked((sbyte)0xF8), unchecked((sbyte)0xDC), unchecked((sbyte)0xF8), unchecked((sbyte)0xD0), unchecked((sbyte)0xFC),
        0x10, 0x24, 0x08, 0x24, 0x08, 0x24, 0x04, 0x30, unchecked((sbyte)0xD0), 0x08, unchecked((sbyte)0xD0), 0x08,
        unchecked((sbyte)0xD0), 0x08, unchecked((sbyte)0xD0), 0x08, unchecked((sbyte)0xF8), 0x30, unchecked((sbyte)0xF8), 0x30, unchecked((sbyte)0xF8), 0x30, unchecked((sbyte)0xF8), 0x30,
        unchecked((sbyte)0xF8), 0x08, unchecked((sbyte)0xF0), 0x10, unchecked((sbyte)0xF4), 0x04, unchecked((sbyte)0xE8), 0x08, unchecked((sbyte)0xF0), 0x10, unchecked((sbyte)0xE0), 0x20,
        unchecked((sbyte)0xEC), 0x0C, unchecked((sbyte)0xD8), 0x18, unchecked((sbyte)0xD8), 0x28, unchecked((sbyte)0xD4), 0x2C, unchecked((sbyte)0xD0), 0x30, unchecked((sbyte)0xD0), unchecked((sbyte)0xD0),
        0x30, 0x30, unchecked((sbyte)0xE0), 0x20,
    ];

    public static readonly int[] HorizontalAccelerationTable =
    [
        -0x0180, -0x0180, 0x0180, 0x0180, -0x0180, -0x0180, 0x0180, 0x0180,
        -0x0180, -0x0180, 0x0180, 0x0180, -0x0180, -0x0180, 0x0140, 0x0140,
        -0x0140, -0x0140, 0x0180, 0x0180, -0x0180, -0x0180, 0x0100, 0x0100,
        -0x0100, -0x0100, 0x0180, 0x0180, -0x0180, -0x0180, 0x0100, 0x0100,
        -0x0180, -0x0180, 0x0100, 0x0100, -0x0100, -0x0100, 0x0180, 0x0180,
        -0x0100, -0x0100, 0x0180, 0x0180, -0x0400, -0x0400, -0x0300, -0x0300,
        0x0300, 0x0300, 0x0400, 0x0400, -0x0400, -0x0400, 0x0600, 0x0600,
        -0x0600, -0x0600, 0x0400, 0x0400, -0x0080, 0x0080, -0x0100, 0x0100,
        -0x0180, 0x0180, -0x0180, -0x0180, 0x0180, 0x0180, -0x0180, 0x0280,
        -0x0280, -0x0500, 0x0280, 0x0500, -0x0280, -0x0500, 0x0280, 0x0500,
        -0x0280, -0x0500, 0x0280, 0x0500, -0x02C0, -0x0580, 0x0240, 0x0480,
        -0x0240, -0x0480, 0x02C0, 0x0580, -0x0300, -0x0600, 0x0200, 0x0400,
        -0x0200, -0x0400, 0x0300, 0x0600, -0x0300, -0x0600, 0x0200, 0x0400,
        -0x0300, -0x0600, 0x0200, 0x0400, -0x0200, -0x0400, 0x0300, 0x0600,
        -0x0200, -0x0400, 0x0300, 0x0600, -0x0300, -0x0600, -0x0300, -0x0600,
        0x0300, 0x0600, 0x0300, 0x0600,
    ];

    public static readonly int[] HorizontalDecelerationTable =
    [
        -0x0100, 0x0100, -0x0100, 0x0100, -0x0100, 0x0100, -0x0180, 0x00C0,
        -0x00C0, 0x0180, -0x0200, 0x0040, -0x0040, 0x0200, -0x0200, 0x0040,
        -0x0200, 0x0040, -0x0040, 0x0200, -0x0040, 0x0200, -0x0400, -0x0100,
        0x0100, 0x0400, -0x0100, 0x0100, -0x0100, 0x0100,
    ];

    public static readonly int[] HorizontalGroundFrictionTable =
    [
        -0x0020, 0x0020, -0x0020, 0x0020, -0x0020, 0x0020, -0x0040, 0x0020,
        -0x0020, 0x0040, -0x0080, 0x0020, -0x0020, 0x0080, -0x0080, 0x0020,
        -0x0080, 0x0020, -0x0020, 0x0080, -0x0020, 0x0080, -0x0200, -0x0080,
        0x0080, 0x0200, -0x0100, 0x0100, -0x0100, 0x0100,
    ];

    public static readonly int[] HorizontalTargetSubSpeedTable =
    [
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, -0x1000, 0x1000, 0x0000,
        0x0000, 0x0000, 0x0000, -0x2000, 0x2000, 0x0000, 0x0000, -0x1000,
        -0x0800,
    ];

    public static readonly int[] VerticalGravityTable =
    [
        0x06, 0x03, 0x04, 0x10, -0x0C,
        0x01, 0x03, 0x04, 0x05, 0x06,
    ];

    public static readonly int[] VerticalMaxFallTable =
    [
        0x40, 0x40, 0x20, 0x40, 0x40,
        0x40, 0x40, 0x40, 0x40, 0x40,
    ];

    public static readonly int[] CapeFloatYSpeedTable =
    [
        0x10, -0x38, -0x20, 0x02, 0x03,
        0x03, 0x04, 0x03, 0x02, 0x00,
        0x01, 0x00, 0x00, 0x00, 0x00,
    ];

    public static readonly byte[] NativeSlopePlayerTable =
    [
        0x08, 0x08, 0x08, 0x08, 0x10, 0x10, 0x10, 0x10,
        0x18, 0x18, 0x20, 0x20, 0x28, 0x30, 0x08, 0x10,
        0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x38, 0x50, 0x48, 0x40, 0x58, 0x58, 0x60, 0x60,
        0x00,
    ];

    public static readonly byte[] NativeSlopePlayerStationaryYSpeedTable =
    [
        0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10,
        0x20, 0x20, 0x20, 0x20, 0x30, 0x30, 0x40, 0x30,
        0x30, 0x30, 0x30, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x30, 0x30, 0x30, 0x30, 0x40, 0x40, 0x40, 0x40,
        0x00,
    ];

    public static readonly sbyte[] NativeSlopePlayerTowardsPeakYSpeedTable =
    [
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        unchecked((sbyte)0xEC), unchecked((sbyte)0xEC), unchecked((sbyte)0xEE), unchecked((sbyte)0xEE),
        unchecked((sbyte)0xDA), unchecked((sbyte)0xDA), 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        unchecked((sbyte)0xDA), unchecked((sbyte)0xDA), unchecked((sbyte)0xDA), unchecked((sbyte)0xDA),
        0x00, 0x00, 0x00, 0x00,
        0x00,
    ];

    public static readonly byte[] NativeSlopePlayerSnapDistanceTable =
    [
        0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08,
        0x09, 0x09, 0x09, 0x09, 0x0B, 0x0B, 0x0B, 0x0B,
        0x0B, 0x0B, 0x0B, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x0B, 0x0B, 0x0B, 0x0B, 0x14, 0x14, 0x14, 0x14,
        0x06,
    ];

    public static readonly sbyte[] NativeSlopeTypeTable =
    [
        unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),
        0x01, 0x01, 0x01, 0x01,
        unchecked((sbyte)0xFE), unchecked((sbyte)0xFE), 0x02, 0x02,
        unchecked((sbyte)0xFD), 0x03, unchecked((sbyte)0xFD), 0x03,
        unchecked((sbyte)0xFD), 0x03, unchecked((sbyte)0xFD), 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x08, 0x08, unchecked((sbyte)0xF8), unchecked((sbyte)0xF8),
        unchecked((sbyte)0xFC), unchecked((sbyte)0xFC), 0x04, 0x04,
        0x00,
    ];

    public static readonly byte[] NativeSlopeSteepnessTable =
    [
        0, 0, 0, 0, 0, 1, 1, 1, 1, 1,
        2, 2, 2, 2, 2, 3, 3, 3, 3, 3,
        4, 4, 4, 4, 4, 5, 5, 5, 5, 5,
        6, 6, 6, 6, 6, 7, 7, 7, 7, 7,
        8, 8, 8, 8, 8, 9, 9, 9, 9, 9,
        10, 10, 10, 10, 10, 11, 11, 11, 11, 11,
        12, 12, 12, 12, 12, 13, 13, 13, 13, 13,
        14, 15, 16, 17, 3, 3, 4, 4, 9, 9,
        10, 10, 12, 12, 13, 13, 18, 19, 20, 21,
        22, 23, 28, 29, 30, 31, 24, 25, 26, 27,
        8, 9, 10, 11, 12, 13,
    ];

    private static readonly byte[] NativeSlopeShapeTable = Convert.FromHexString(
        "0F0F0F0F0E0E0E0E0D0D0D0D0C0C0C0C0B0B0B0B0A0A0A0A09090909080808080707070706060606050505050404040403030303020202020101010100000000" +
        "000000000101010102020202030303030404040405050505060606060707070708080808090909090A0A0A0A0B0B0B0B0C0C0C0C0D0D0D0D0E0E0E0E0F0F0F0F" +
        "0F0F0E0E0D0D0C0C0B0B0A0A090908080707060605050404030302020101000000000101020203030404050506060707080809090A0A0B0B0C0C0D0D0E0E0F0F" +
        "0F0E0D0C0B0A09080706050403020100000102030405060708090A0B0C0D0E0F0F0E0D0C0B0A09080706050403020100000102030405060708090A0B0C0D0E0F" +
        "0806040302020101000000000000000000000000000000000101020203040608FFFEFDFCFBFAF9F8F7F6F5F4F3F2F1F0F0F1F2F3F4F5F6F7F8F9FAFBFCFDFEFF" +
        "FFFFFEFEFDFDFCFCFBFBFAFAF9F9F8F8F7F7F6F6F5F5F4F4F3F3F2F2F1F1F0F0F0F0F1F1F2F2F3F3F4F4F5F5F6F6F7F7F8F8F9F9FAFAFBFBFCFCFDFDFEFEFFFF" +
        "0F0E0D0C0B0A09080706050403020100000102030405060708090A0B0C0D0E0F000102030405060708090A0B0C0D0E0F0F0E0D0C0B0A09080706050403020100" +
        "10101010101010100E0C0A08060402000E0C0A0806040200FEFCFAF8F6F4F2F000020406080A0C0E1010101010101010F0F2F4F6F8FAFCFE00020406080A0C0E");

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
        public int CapeFloatFrames;
        public int InAirState;
        public int SlopeKind;
        public int SlopePlayer;
        public int SlopeType;
        public int PostLandingAirDragFrames;
        public int LooseSteepSlopeGroundFrames;
        public int LooseSteepSlopeKind;
        public int LeadingFootCarryFrames;
        public bool NativeSlopeOverrunGround;
        public int PreserveGroundYSpeedFrames;

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
            Facing = state.Facing;
            PMeter = state.PMeter;
            Powerup = state.Powerup;
            SpinJump = state.SpinJump;
            RunningTakeoff = state.RunningTakeoff;
            Ducking = state.Ducking;
            JumpHeldFrames = state.JumpHeldFrames;
            CapeFloatFrames = state.CapeFloatFrames;
            InAirState = state.InAirState;
            SlopeKind = state.SlopeKind;
            SlopePlayer = state.SlopePlayer;
            SlopeType = state.SlopeType;
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
        public readonly int Facing;
        public readonly int PMeter;
        public readonly int Powerup;
        public readonly bool SpinJump;
        public readonly bool RunningTakeoff;
        public readonly bool Ducking;
        public readonly int JumpHeldFrames;
        public readonly int CapeFloatFrames;
        public readonly int InAirState;
        public readonly int SlopeKind;
        public readonly int SlopePlayer;
        public readonly int SlopeType;
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
        public bool RunPressed;
    }

    public readonly record struct SlopeSurface(
        float X0,
        float Y0,
        float X1,
        float Y1,
        bool Ceiling = false,
        int NativeSlopeKind = 32,
        float SnapDistance = -1.0f);

    public PlayerState MakeState(int xPx, int yPx, int powerup = BigPowerup)
    {
        return new PlayerState
        {
            X = xPx,
            Y = yPx,
            Facing = 1,
            Powerup = Math.Clamp(powerup, SmallPowerup, FirePowerup),
            SlopeKind = -1,
        };
    }

    public void SetPowerup(ref PlayerState state, int powerup)
    {
        powerup = Math.Clamp(powerup, SmallPowerup, FirePowerup);
        state.Powerup = powerup;
        if (state.Powerup == SmallPowerup)
        {
            state.Ducking = false;
        }
        if (state.Powerup != CapePowerup)
        {
            state.CapeFloatFrames = 0;
        }
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
        Step(ref state, input, solids, solidStepUpEnabled, solidVerticalEnabled, null, slopes);
    }

    public void Step(
        ref PlayerState state,
        FrameInput input,
        IReadOnlyList<Rect2> solids,
        IReadOnlyList<bool>? solidStepUpEnabled,
        IReadOnlyList<bool>? solidVerticalEnabled,
        IReadOnlyList<int>? solidSupportModes,
        IReadOnlyList<SlopeSurface> slopes)
    {
        var preserveTerrainlessGround = state.OnGround && solids.Count == 0 && slopes.Count == 0;
        var wasOnGround = state.OnGround;
        var usePostLandingAirDrag = state.PostLandingAirDragFrames > 0;
        var preservedSlopeKind = state.SlopeKind;
        var preservedSlopePlayer = state.SlopePlayer;
        var preservedSlopeType = state.SlopeType;
        var wasNativeSlopeOverrunGround = state.NativeSlopeOverrunGround;
        state.NativeSlopeOverrunGround = false;
        var previousBottom = PlayerCollisionBottom(state);
        var previousYSpeed = state.YSpeed;
        IntegrateX(ref state);
        var steppedOntoSolid = ResolveAxis(ref state, solids, solidStepUpEnabled, solidVerticalEnabled, solidSupportModes, previousBottom, horizontal: true);

        IntegrateY(ref state);
        state.OnGround = false;
        state.SlopeKind = -1;
        state.SlopePlayer = 0;
        state.SlopeType = 0;
        ResolveAxis(ref state, solids, solidStepUpEnabled, solidVerticalEnabled, solidSupportModes, previousBottom, horizontal: false);
        ResolveSlopes(ref state, slopes, previousBottom, wasOnGround, wasNativeSlopeOverrunGround);
        if (!state.OnGround &&
            state.YSpeed >= 0 &&
            IsStandingOnSolid(state, solids, solidVerticalEnabled, solidSupportModes))
        {
            state.OnGround = true;
            state.RunningTakeoff = false;
        }
        if (steppedOntoSolid && state.YSpeed >= 0)
        {
            state.OnGround = true;
        }
        if (preserveTerrainlessGround)
        {
            state.OnGround = true;
            state.SlopeKind = preservedSlopeKind;
            state.SlopePlayer = preservedSlopePlayer;
            state.SlopeType = preservedSlopeType;
        }
        if (!state.OnGround || state.SlopeKind >= 0)
        {
            state.LeadingFootCarryFrames = 0;
        }
        var landedThisFrame = !wasOnGround && state.OnGround;
        if (landedThisFrame)
        {
            state.PostLandingAirDragFrames = Math.Max(state.PostLandingAirDragFrames, PostLandingAirDragFrameCount);
        }
        if (state.OnGround && !state.NativeSlopeOverrunGround)
        {
            state.InAirState = 0;
        }
        else if (state.InAirState == 0)
        {
            state.InAirState = NativeFallingInAirState;
        }

        if (!state.OnGround && previousYSpeed < 0 && state.YSpeed == 0)
        {
            return;
        }

        ApplyDucking(ref state, input);
        var slopePlayerBeforeJump = state.SlopePlayer;
        var groundedBeforeJump = state.OnGround;
        var jumpedThisFrame = ApplyJumpAndGravity(ref state, input);
        var horizontalSlopePlayerOverride = jumpedThisFrame && groundedBeforeJump
            ? slopePlayerBeforeJump
            : (int?)null;
        ApplyHorizontal(ref state, input, usePostLandingAirDrag, horizontalSlopePlayerOverride);
        if (jumpedThisFrame &&
            groundedBeforeJump &&
            state.Powerup != SmallPowerup &&
            slopePlayerBeforeJump == 0x28 &&
            state.XSpeed > 0 &&
            state.XSpeed <= 8)
        {
            AddXAccel(ref state, 0x0080);
        }
        if (usePostLandingAirDrag && state.PostLandingAirDragFrames > 0)
        {
            state.PostLandingAirDragFrames--;
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

    public void Step(
        ref PlayerState state,
        FrameInput input,
        IReadOnlyList<Rect2> solids,
        IReadOnlyList<bool> solidStepUpEnabled,
        IReadOnlyList<bool> solidVerticalEnabled,
        IReadOnlyList<int> solidSupportModes,
        IReadOnlyList<SlopeSurface> slopes,
        int levelLeft,
        int levelRight)
    {
        Step(ref state, input, solids, solidStepUpEnabled, solidVerticalEnabled, solidSupportModes, slopes);
        ClampHorizontalLevelBounds(ref state, levelLeft, levelRight);
    }

    public static void ClampHorizontalLevelBounds(ref PlayerState state, int levelLeft, int levelRight)
    {
        var maxX = Math.Max(levelLeft, levelRight - PlayerWidth);
        if (state.XFloat <= levelLeft)
        {
            state.X = levelLeft;
            state.SubX = 0;
            if (state.XSpeed < 0)
            {
                state.XSpeed = 0;
                state.SubXSpeed = 0;
            }
        }
        else if (state.XFloat >= maxX)
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
            new Vector2(state.XFloat, PlayerCollisionTop(state)),
            new Vector2(PlayerWidth, PlayerCollisionHeightFor(state)));
    }

    public static int PlayerHeightFor(PlayerState state)
    {
        return state.Ducking ? DuckingPlayerHeight : PlayerHeightForPowerup(state.Powerup);
    }

    public static int PlayerCollisionHeightFor(PlayerState state)
    {
        return PlayerHeightFor(state);
    }

    public static int PlayerCollisionYOffsetFor(PlayerState state)
    {
        return state.Powerup == SmallPowerup || state.Ducking
            ? PlayerCollisionYOffset
            : BigPlayerCollisionYOffset;
    }

    public static float PlayerCollisionTop(PlayerState state)
    {
        return state.YFloat + PlayerCollisionYOffsetFor(state);
    }

    public static float PlayerCollisionBottom(PlayerState state)
    {
        return PlayerCollisionTop(state) + PlayerCollisionHeightFor(state);
    }

    public static int AnchorYForCollisionTop(float collisionTop, PlayerState state)
    {
        return (int)MathF.Round(collisionTop - PlayerCollisionYOffsetFor(state));
    }

    public static int AnchorYForCollisionBottom(float collisionBottom, PlayerState state)
    {
        return (int)MathF.Round(collisionBottom - PlayerCollisionYOffsetFor(state) - PlayerCollisionHeightFor(state));
    }

    public static int PlayerHeightForPowerup(int powerup)
    {
        return powerup == SmallPowerup ? SmallPlayerHeight : BigPlayerHeight;
    }

    public static int JumpSpeedIndexFor(int xSpeed, bool spin)
    {
        var speedIndex = Math.Clamp((Math.Abs(xSpeed) >> 2) & 0xFE, 0, JumpHeightTable.Length - 1);
        return spin
            ? Math.Min(speedIndex + 1, JumpHeightTable.Length - 1)
            : speedIndex;
    }

    public static int JumpYSpeedFor(int xSpeed, bool spin)
    {
        return JumpHeightTable[JumpSpeedIndexFor(xSpeed, spin)];
    }

    public static bool TryNativeSlopeKindForMap16(int map16, out int kind)
    {
        var low = map16 & 0xFF;
        var index = low - 0x6E;
        if (index < 0 || index >= NativeSlopeSteepnessTable.Length)
        {
            kind = 32;
            return false;
        }

        kind = NativeSlopeSteepnessTable[index];
        return kind >= 0 && kind < NativeSlopePlayerSnapDistanceTable.Length;
    }

    public static float NativeSlopeSnapDistanceForKind(int kind)
    {
        return kind >= 0 && kind < NativeSlopePlayerSnapDistanceTable.Length
            ? NativeSlopePlayerSnapDistanceTable[kind]
            : 16.0f;
    }

    public static int NativeSlopeTypeForKind(int kind)
    {
        return kind >= 0 && kind < NativeSlopeTypeTable.Length
            ? NativeSlopeTypeTable[kind]
            : 0;
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

            surfaceY = SurfaceYAt(slope, probeX);
            var effectiveBelowTolerance = EffectiveSlopeBelowTolerance(slope, belowTolerance);
            if (ySpeed < 0 || bottom < surfaceY - aboveTolerance || bottom > surfaceY + effectiveBelowTolerance)
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
        float previousBottom,
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

        var effectiveBelowTolerance = EffectiveSlopeBelowTolerance(slopes, probeX, surfaceY, belowTolerance);
        return top < surfaceY - 1.0f && previousBottom <= surfaceY + effectiveBelowTolerance;
    }

    private static float EffectiveSlopeBelowTolerance(SlopeSurface slope, float fallback)
    {
        return slope.SnapDistance >= 0.0f ? slope.SnapDistance : fallback;
    }

    private static float EffectiveSlopeBelowTolerance(
        IReadOnlyList<SlopeSurface> slopes,
        float probeX,
        float surfaceY,
        float fallback)
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

            var candidateY = SurfaceYAt(slope, probeX);
            if (MathF.Abs(candidateY - surfaceY) <= 0.01f)
            {
                return EffectiveSlopeBelowTolerance(slope, fallback);
            }
        }

        return fallback;
    }

    private static void ApplyDucking(ref PlayerState state, FrameInput input)
    {
        var shouldDuck = state.OnGround && input.Down && state.Powerup != SmallPowerup;
        if (state.Ducking == shouldDuck)
        {
            return;
        }

        state.Ducking = shouldDuck;
    }

    private static void ApplyHorizontal(
        ref PlayerState state,
        FrameInput input,
        bool usePostLandingAirDrag,
        int? slopePlayerOverride = null)
    {
        var dir = 0;
        var horizontalSuppressed = state.OnGround &&
            input.Down &&
            state.LeadingFootCarryFrames <= 1;
        var suppressedDirectionalInput = horizontalSuppressed && (input.Left || input.Right);
        if (!horizontalSuppressed && input.Left)
        {
            dir--;
        }
        if (!horizontalSuppressed && input.Right)
        {
            dir++;
        }

        if (dir != 0)
        {
            state.Facing = dir > 0 ? 1 : 0;
            var absSpeed = Math.Abs(state.XSpeed);
            var pMeterMode = UpdatePMeterEx(ref state, PMeterModeForHorizontal(state, input, absSpeed));
            var slopePlayer = slopePlayerOverride ?? HorizontalSlopePlayerForInput(state, input, suppressedDirectionalInput);
            ApplyNativeHorizontal(
                ref state,
                dir,
                input.Run,
                pMeterMode,
                slopePlayer,
                usePostLandingAirDrag);
        }
        else
        {
            UpdatePMeterEx(ref state, 0);
            if (!state.OnGround || state.NativeSlopeOverrunGround)
            {
                return;
            }

            if (state.XSpeed > 0)
            {
                if (ShouldApplyDuckingSteepRightSuppressedTurn(state, suppressedDirectionalInput))
                {
                    AddXAccel(ref state, -0x0300);
                    return;
                }

                if (ShouldApplyDuckingSteepRightDownhillCarry(state, input, suppressedDirectionalInput))
                {
                    AddXAccel(ref state, 0x0180);
                    return;
                }

                ApplyNativeHorizontalDrag(ref state, slopePlayerOverride ?? HorizontalSlopePlayerForInput(state, input, suppressedDirectionalInput), useIceFriction: false);
                if (state.XSpeed < 0)
                {
                    state.XSpeed = 0;
                    state.SubXSpeed = 0;
                }
            }
            else if (state.XSpeed < 0)
            {
                ApplyNativeHorizontalDrag(ref state, slopePlayerOverride ?? HorizontalSlopePlayerForInput(state, input, suppressedDirectionalInput), useIceFriction: false);
                if (state.XSpeed > 0)
                {
                    state.XSpeed = 0;
                    state.SubXSpeed = 0;
                }
            }
            else if (state.OnGround && HorizontalSlopePlayerForState(state) != 0)
            {
                ApplyNativeHorizontalDrag(ref state, slopePlayerOverride ?? HorizontalSlopePlayerForState(state), useIceFriction: false);
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

    private static void ApplyNativeHorizontal(
        ref PlayerState state,
        int dir,
        bool runHeld,
        int pMeterMode,
        int slopePlayer,
        bool usePostLandingAirDrag)
    {
        var directionBit = NativeDirectionBit(dir);
        var targetIndex = directionBit | (slopePlayer & 0xFF) | (2 * Math.Clamp(pMeterMode, 0, 3));
        var target = HorizontalMaxSpeedTable[Math.Clamp(targetIndex, 0, HorizontalMaxSpeedTable.Length - 1)];
        if (ShouldApplyNativeFlatDrag(state.XSpeed, target))
        {
            ApplyNativeHorizontalDrag(ref state, slopePlayer, useIceFriction: false);
            return;
        }

        var accelIndexByte = (slopePlayer & 0xFF) | (4 * directionBit);
        if (IsNativeHorizontalTurningAround(state.XSpeed, accelIndexByte))
        {
            accelIndexByte = (accelIndexByte - 112) & 0xFF;
        }
        if (runHeld)
        {
            accelIndexByte += 2;
        }

        AddXAccel(ref state, HorizontalAccelerationTable[Math.Clamp(accelIndexByte >> 1, 0, HorizontalAccelerationTable.Length - 1)]);
    }

    private static bool ShouldApplyNativeFlatDrag(int xSpeed, int target)
    {
        var x = xSpeed & 0xFF;
        var t = target & 0xFF;
        return x == t || ((t ^ ((x - t) & 0xFF)) & 0x80) == 0;
    }

    private static void ApplyNativeHorizontalDrag(ref PlayerState state, int slopePlayer, bool useIceFriction)
    {
        var k = (slopePlayer & 0xFF) >> 2;
        var j = (slopePlayer & 0xFF) >> 1;
        if (ToS8(state.XSpeed - NativeHorizontalSubspeedTableByte(k + 1)) < 0)
        {
            j += 2;
        }

        var tableIndex = Math.Clamp(j >> 1, 0, HorizontalDecelerationTable.Length - 1);
        AddXAccel(ref state, useIceFriction ? HorizontalGroundFrictionTable[tableIndex] : HorizontalDecelerationTable[tableIndex]);
        ClampNativeHorizontalDragToTarget(ref state, tableIndex, k >> 1);
    }

    private static void ClampNativeHorizontalDragToTarget(ref PlayerState state, int tableIndex, int targetIndex)
    {
        var combined = CombinedXSpeed16(state);
        var target = HorizontalTargetSubSpeedTable[Math.Clamp(targetIndex, 0, HorizontalTargetSubSpeedTable.Length - 1)];
        if (((ToU16(HorizontalDecelerationTable[tableIndex]) ^ ToU16(combined - target)) & 0x8000) == 0)
        {
            SetCombinedXSpeed16(ref state, target);
        }
    }

    private static bool IsNativeHorizontalTurningAround(int xSpeed, int accelIndexByte)
    {
        return xSpeed != 0 && ((NativeHorizontalAccelerationByte(accelIndexByte + 1) ^ (xSpeed & 0xFF)) & 0x80) != 0;
    }

    private static int NativeHorizontalAccelerationByte(int byteIndex)
    {
        var word = HorizontalAccelerationTable[Math.Clamp(byteIndex >> 1, 0, HorizontalAccelerationTable.Length - 1)];
        return byteIndex % 2 == 0
            ? word & 0xFF
            : (word >> 8) & 0xFF;
    }

    private static int NativeHorizontalSubspeedTableByte(int byteIndex)
    {
        var word = HorizontalTargetSubSpeedTable[Math.Clamp(byteIndex >> 1, 0, HorizontalTargetSubSpeedTable.Length - 1)];
        return byteIndex % 2 == 0
            ? ToS8(word & 0xFF)
            : ToS8((word >> 8) & 0xFF);
    }

    private static int HorizontalSlopePlayerForState(PlayerState state)
    {
        return state.OnGround ? state.SlopePlayer : 0;
    }

    private static int HorizontalSlopePlayerForInput(PlayerState state, FrameInput input, bool suppressedDirectionalInput)
    {
        if ((input.Left || input.Right || suppressedDirectionalInput) &&
            ShouldUseFlatHorizontalForDuckingSteepRightSlopeInput(state))
        {
            return 0;
        }

        return HorizontalSlopePlayerForState(state);
    }

    private static bool ShouldUseFlatHorizontalForDuckingSteepRightSlopeInput(PlayerState state)
    {
        return state.OnGround &&
            state.Ducking &&
            state.SlopeKind == 13 &&
            state.YSpeed > NativeSlopePlayerStationaryYSpeedTable[13];
    }

    private static bool ShouldApplyDuckingSteepRightDownhillCarry(
        PlayerState state,
        FrameInput input,
        bool suppressedDirectionalInput)
    {
        return input.Down &&
            !input.Left &&
            !input.Right &&
            !suppressedDirectionalInput &&
            ShouldUseFlatHorizontalForDuckingSteepRightSlopeInput(state);
    }

    private static bool ShouldApplyDuckingSteepRightSuppressedTurn(PlayerState state, bool suppressedDirectionalInput)
    {
        return suppressedDirectionalInput &&
            ShouldUseFlatHorizontalForDuckingSteepRightSlopeInput(state);
    }

    private static int NativeDirectionBit(int dir)
    {
        return dir > 0 ? 1 : 0;
    }

    private static bool ApplyJumpAndGravity(ref PlayerState state, FrameInput input)
    {
        var jumpStarted = input.JumpPressed || input.SpinPressed;
        var jumpedThisFrame = false;
        if (state.OnGround && !state.NativeSlopeOverrunGround && jumpStarted)
        {
            state.YSpeed = JumpYSpeedFor(state.XSpeed, input.SpinPressed);
            state.OnGround = false;
            state.RunningTakeoff = state.PMeter >= PMeterMax;
            state.SpinJump = input.SpinPressed;
            state.JumpHeldFrames = 0;
            state.CapeFloatFrames = 0;
            state.InAirState = state.RunningTakeoff
                ? NativeRunningJumpInAirState
                : NativeNormalJumpInAirState;
            jumpedThisFrame = true;
        }
        else if (state.OnGround && !state.NativeSlopeOverrunGround)
        {
            state.RunningTakeoff = false;
            state.SpinJump = false;
            state.JumpHeldFrames = 0;
            state.CapeFloatFrames = 0;
            state.InAirState = 0;
            if (state.SlopeKind >= 0)
            {
                return false;
            }
            if (state.LeadingFootCarryFrames <= 1)
            {
                state.YSpeed = 0;
            }
            state.SubYSpeed = 0;
            if (state.PreserveGroundYSpeedFrames > 0)
            {
                state.PreserveGroundYSpeedFrames--;
                return false;
            }
        }

        if (TryApplyCapeFloatFallCap(ref state, input))
        {
            return jumpedThisFrame;
        }

        if (input.Jump || input.Spin)
        {
            ApplyVerticalGravity(ref state, 1);
            state.JumpHeldFrames++;
        }
        else
        {
            ApplyVerticalGravity(ref state, 0);
        }

        return jumpedThisFrame;
    }

    private static bool TryApplyCapeFloatFallCap(ref PlayerState state, FrameInput input)
    {
        if (state.Powerup != CapePowerup || !input.Jump || state.YSpeed < 0)
        {
            state.CapeFloatFrames = 0;
            return false;
        }

        if (state.CapeFloatFrames <= 0)
        {
            state.CapeFloatFrames = CapeFloatFrameCount;
        }

        state.CapeFloatFrames--;
        var cap = CapeFloatYSpeedTable[0];
        if (state.YSpeed < cap)
        {
            return false;
        }

        state.YSpeed = cap;
        state.SubYSpeed = 0;
        return true;
    }

    private static void ApplyVerticalGravity(ref PlayerState state, int tableIndex)
    {
        tableIndex = Math.Clamp(tableIndex, 0, VerticalGravityTable.Length - 1);
        if (state.YSpeed >= 0 && state.YSpeed >= VerticalMaxFallTable[tableIndex])
        {
            state.YSpeed = VerticalMaxFallTable[tableIndex];
        }
        if (state.YSpeed >= 0 && state.InAirState == NativeNormalJumpInAirState)
        {
            state.InAirState = NativeFallingInAirState;
        }

        state.YSpeed = ToS8(state.YSpeed + VerticalGravityTable[tableIndex]);
    }

    private bool ResolveAxis(
        ref PlayerState state,
        IReadOnlyList<Rect2> solids,
        IReadOnlyList<bool>? solidStepUpEnabled,
        IReadOnlyList<bool>? solidVerticalEnabled,
        IReadOnlyList<int>? solidSupportModes,
        float previousBottom,
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

            if (DebugCollisionTrace)
            {
                GD.Print(
                    $"smw-physics-collision: axis={(horizontal ? "x" : "y")} solid={solidIndex} " +
                    $"rect={rect.Position.X:0.00},{rect.Position.Y:0.00},{rect.Size.X:0.00},{rect.Size.Y:0.00} " +
                    $"solid={solid.Position.X:0.00},{solid.Position.Y:0.00},{solid.Size.X:0.00},{solid.Size.Y:0.00} " +
                    $"xs={state.XSpeed} ys={state.YSpeed}");
            }

            if (horizontal)
            {
                if (ShouldIgnoreWideFloorForHorizontalCollision(rect, solid))
                {
                    continue;
                }

                var allowVerticalForWallSlack = solidVerticalEnabled == null ||
                    solidIndex >= solidVerticalEnabled.Count ||
                    solidVerticalEnabled[solidIndex];
                var allowStepUp = solidStepUpEnabled == null ||
                    solidIndex >= solidStepUpEnabled.Count ||
                    solidStepUpEnabled[solidIndex];
                var supportMode = SupportModeAt(solidSupportModes, solidIndex);
                if (supportMode == SolidSupportLeadingFoot && !state.OnGround && state.YSpeed < 0)
                {
                    continue;
                }
                if (allowStepUp && TryStepUp(ref state, solid, rect, supportMode))
                {
                    rect = PlayerRect(state);
                    steppedOntoSolid = true;
                    continue;
                }
                if (allowStepUp && ShouldPassUnsupportedTallStepEdge(state, solid, rect, supportMode))
                {
                    continue;
                }
                if (allowVerticalForWallSlack && !HasNativeHorizontalSideProbeYOverlap(state, rect, solid))
                {
                    continue;
                }

                var preserveSubXSpeed = !state.OnGround && allowVerticalForWallSlack;
                if (state.XSpeed > 0)
                {
                    if (preserveSubXSpeed &&
                        Math.Abs(state.XSpeed) < NativeFastAirborneWallSnapSpeed &&
                        (state.X & 0x0F) == NativeRightWallPostCorrectionNibble)
                    {
                        continue;
                    }

                    if (state.OnGround && solid.Size.X > 16.0f)
                    {
                        state.X -= 1;
                    }
                    else if (preserveSubXSpeed && Math.Abs(state.XSpeed) < NativeFastAirborneWallSnapSpeed)
                    {
                        state.X -= 1;
                    }
                    else
                    {
                        var wallSlack = !state.OnGround && allowVerticalForWallSlack ? NativeHorizontalWallSlack : 0.0f;
                        var resolvedX = solid.Position.X - PlayerWidth + wallSlack;
                        if (MathF.Abs(resolvedX - state.XFloat) > MaxHorizontalCollisionCorrection)
                        {
                            continue;
                        }

                        state.X = (int)MathF.Round(resolvedX);
                    }
                }
                else if (state.XSpeed < 0)
                {
                    if (preserveSubXSpeed &&
                        Math.Abs(state.XSpeed) < NativeFastAirborneWallSnapSpeed &&
                        (state.X & 0x0F) == NativeLeftWallPostCorrectionNibble)
                    {
                        continue;
                    }

                    if (state.OnGround && solid.Size.X > 16.0f)
                    {
                        state.X += 1;
                    }
                    else if (preserveSubXSpeed && Math.Abs(state.XSpeed) < NativeFastAirborneWallSnapSpeed)
                    {
                        state.X += 1;
                    }
                    else
                    {
                        var wallSlack = !state.OnGround && allowVerticalForWallSlack ? NativeHorizontalWallSlack : 0.0f;
                        var resolvedX = solid.Position.X + solid.Size.X - wallSlack;
                        if (MathF.Abs(resolvedX - state.XFloat) > MaxHorizontalCollisionCorrection)
                        {
                            continue;
                        }

                        state.X = (int)MathF.Round(resolvedX);
                    }
                }
                if (!preserveSubXSpeed || Math.Abs(state.XSpeed) >= NativeFastAirborneWallSnapSpeed)
                {
                    state.SubX = 0;
                }
                if (!preserveSubXSpeed)
                {
                    state.SubXSpeed = 0;
                }
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

                var preserveVerticalSubpixel = false;
                if (state.YSpeed > 0)
                {
                    var supportMode = SupportModeAt(solidSupportModes, solidIndex);
                    if (supportMode == SolidSupportLeadingFoot &&
                        previousBottom > solid.Position.Y + StepUpTolerance)
                    {
                        continue;
                    }
                    if (!HasNativeSolidSupport(state, solid, supportMode))
                    {
                        continue;
                    }

                    var groundedLeadingFootCarry = supportMode == SolidSupportLeadingFoot && state.InAirState == 0;
                    state.Y = AnchorYForCollisionBottom(solid.Position.Y, state);
                    state.OnGround = true;
                    state.InAirState = 0;
                    state.RunningTakeoff = false;
                    state.LeadingFootCarryFrames = groundedLeadingFootCarry
                        ? state.LeadingFootCarryFrames + 1
                        : 0;
                    preserveVerticalSubpixel = state.SpinJump;
                }
                else if (state.YSpeed < 0)
                {
                    if (SupportModeAt(solidSupportModes, solidIndex) == SolidSupportLeadingFoot)
                    {
                        continue;
                    }
                    if (!HasNativeVerticalCenterOverlap(state, solid))
                    {
                        continue;
                    }

                    state.Y = AnchorYForCollisionTop(solid.Position.Y + solid.Size.Y, state);
                }
                if (!preserveVerticalSubpixel)
                {
                    state.SubY = 0;
                }
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

    private static bool TryStepUp(ref PlayerState state, Rect2 solid, Rect2 playerRect, int supportMode)
    {
        if (!state.OnGround || state.YSpeed < 0)
        {
            return false;
        }

        var solidTop = solid.Position.Y;
        var playerBottom = playerRect.Position.Y + playerRect.Size.Y;
        if (playerBottom <= solidTop || playerBottom > solidTop + StepUpTolerance)
        {
            return false;
        }
        if (solid.Size.Y > 16.0f && !HasNativeSolidSupport(state, solid, supportMode))
        {
            return false;
        }

        state.Y = AnchorYForCollisionBottom(solidTop, state);
        state.SubY = 0;
        state.SubYSpeed = 0;
        state.RunningTakeoff = false;
        return true;
    }

    private static bool ShouldPassUnsupportedTallStepEdge(PlayerState state, Rect2 solid, Rect2 playerRect, int supportMode)
    {
        if (state.YSpeed < 0 || solid.Size.Y <= 16.0f || HasNativeSolidSupport(state, solid, supportMode))
        {
            return false;
        }

        var solidTop = solid.Position.Y;
        var playerBottom = playerRect.Position.Y + playerRect.Size.Y;
        return playerBottom > solidTop && playerBottom <= solidTop + StepUpTolerance;
    }

    private static bool IsStandingOnSolid(
        PlayerState state,
        IReadOnlyList<Rect2> solids,
        IReadOnlyList<bool>? solidVerticalEnabled,
        IReadOnlyList<int>? solidSupportModes)
    {
        var rect = new Rect2(
            new Vector2(state.XFloat, PlayerCollisionTop(state)),
            new Vector2(PlayerWidth, PlayerCollisionHeightFor(state)));
        var bottom = rect.Position.Y + rect.Size.Y;
        for (var solidIndex = 0; solidIndex < solids.Count; solidIndex++)
        {
            var allowVertical = solidVerticalEnabled == null ||
                solidIndex >= solidVerticalEnabled.Count ||
                solidVerticalEnabled[solidIndex];
            if (!allowVertical)
            {
                continue;
            }

            var solid = solids[solidIndex];
            if (bottom < solid.Position.Y || bottom > solid.Position.Y + 1.0f)
            {
                continue;
            }

            if (!HasNativeSolidSupport(state, solid, SupportModeAt(solidSupportModes, solidIndex)))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool HasNativeSolidSupport(PlayerState state, Rect2 solid, int supportMode = SolidSupportLegacy)
    {
        var useLeadingFootSupport = supportMode == SolidSupportLeadingFoot && state.InAirState != 0;
        var supportX = useLeadingFootSupport
            ? state.XSpeed switch
            {
                > 0 => state.XFloat + PlayerWidth - 3.0f,
                < 0 => state.XFloat + 2.0f,
                _ => state.XFloat + PlayerWidth * 0.5f,
            }
            : state.XSpeed switch
            {
                > 0 => state.XFloat + 5.0f,
                < 0 => state.XFloat + PlayerWidth - 5.0f,
                _ => state.XFloat + PlayerWidth * 0.5f,
            };
        if (!useLeadingFootSupport && supportMode == SolidSupportLeadingFoot && supportX >= solid.Position.X + solid.Size.X - 1.0f)
        {
            return false;
        }

        return supportX >= solid.Position.X && supportX < solid.Position.X + solid.Size.X;
    }

    private static int SupportModeAt(IReadOnlyList<int>? supportModes, int index)
    {
        return supportModes == null || index >= supportModes.Count
            ? SolidSupportLegacy
            : supportModes[index];
    }

    private static bool HasNativeVerticalCenterOverlap(PlayerState state, Rect2 solid)
    {
        var centerX = state.XFloat + PlayerWidth * 0.5f;
        return centerX >= solid.Position.X && centerX < solid.Position.X + solid.Size.X;
    }

    private static bool HasNativeHorizontalSideProbeYOverlap(PlayerState state, Rect2 playerRect, Rect2 solid)
    {
        var probeY = state.OnGround
            ? playerRect.Position.Y + playerRect.Size.Y
            : state.Powerup == SmallPowerup || state.Ducking
            ? playerRect.Position.Y + playerRect.Size.Y * 0.5f
            : playerRect.Position.Y + 26.0f;
        return probeY >= solid.Position.Y && probeY < solid.Position.Y + solid.Size.Y;
    }

    private void ResolveSlopes(
        ref PlayerState state,
        IReadOnlyList<SlopeSurface> slopes,
        float previousBottom,
        bool wasOnGround,
        bool wasNativeSlopeOverrunGround)
    {
        if (slopes.Count == 0)
        {
            return;
        }

        var probeX = state.XFloat + PlayerWidth * 0.5f;
        var playerHeight = PlayerCollisionHeightFor(state);
        var bottom = PlayerCollisionBottom(state);
        var top = PlayerCollisionTop(state);
        var previousTop = previousBottom - playerHeight;
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

            var surfaceY = SurfaceYAt(slope, probeX);
            if (slope.Ceiling)
            {
                if (DebugSlopeTrace)
                {
                    GD.Print(
                        $"smw-physics-slope: ceil x={probeX:0.00} top={top:0.00} prev_top={previousTop:0.00} " +
                        $"surface={surfaceY:0.00} ys={state.YSpeed} line={slope.X0:0.00},{slope.Y0:0.00}->{slope.X1:0.00},{slope.Y1:0.00}");
                }
                if (state.YSpeed >= 0 ||
                    previousTop < surfaceY - 1.0f ||
                    top < surfaceY - 16.0f ||
                    top > surfaceY + 6.0f)
                {
                    continue;
                }

                state.Y = AnchorYForCollisionTop(surfaceY, state);
                state.SubY = 0;
                state.SubYSpeed = 0;
                if (state.YSpeed < 0)
                {
                    state.YSpeed = 0;
                    state.InAirState = 0;
                }
                return;
            }
        }

        if (wasNativeSlopeOverrunGround &&
            TryApplyPersistentNativeSlopeOverrunGround(ref state, bottom, slopes))
        {
            return;
        }

        if (!TryResolvePlayerFloorSlopeFromAbove(state, top, bottom, previousBottom, wasOnGround, slopes, out var floorY, out var floorSlope))
        {
            if (TryApplyNativeSlopeOverrunGround(ref state, bottom, wasOnGround, slopes))
            {
                return;
            }

            if (state.LooseSteepSlopeGroundFrames > 0)
            {
                ApplyLooseSteepSlopeGround(ref state, state.LooseSteepSlopeKind);
                return;
            }

            state.LooseSteepSlopeGroundFrames = 0;
            state.LooseSteepSlopeKind = 32;
            return;
        }
        if (floorSlope.NativeSlopeKind is >= 20 and <= 23 && state.LooseSteepSlopeGroundFrames > 0)
        {
            ApplyLooseSteepSlopeGround(ref state, floorSlope.NativeSlopeKind);
            return;
        }

        if (!wasOnGround && floorSlope.NativeSlopeKind is >= 20 and <= 23)
        {
            if (bottom >= floorY + SteepPipeAirborneGroundFlagDepth)
            {
                ApplyNativeSlopeMetadata(ref state, floorSlope);
                state.OnGround = true;
                state.RunningTakeoff = false;
                state.LooseSteepSlopeGroundFrames = SteepPipeLooseGroundFrameCount;
                state.LooseSteepSlopeKind = floorSlope.NativeSlopeKind;
            }

            return;
        }

        floorY = AdjustPlayerFloorYForNativeSlope(floorY, floorSlope, state);
        var preserveSlopeSubY = ShouldPreserveDuckingSteepRightSlopeSubY(state, floorSlope);
        state.Y = AnchorYForCollisionBottom(floorY, state);
        if (!preserveSlopeSubY)
        {
            state.SubY = 0;
        }
        state.SubYSpeed = 0;
        ApplyNativeSlopeContact(ref state, floorSlope);
        state.OnGround = true;
        state.InAirState = 0;
        state.RunningTakeoff = false;
        state.LooseSteepSlopeGroundFrames = 0;
        state.LooseSteepSlopeKind = 32;
    }

    private static bool TryApplyNativeSlopeOverrunGround(
        ref PlayerState state,
        float bottom,
        bool wasOnGround,
        IReadOnlyList<SlopeSurface> slopes)
    {
        if (!wasOnGround || state.YSpeed < 0)
        {
            return false;
        }

        var centerProbe = state.X + 8.0f;
        var leftProbe = state.X + 2.0f;
        var rightProbe = state.X + PlayerWidth - 2.0f;
        Span<float> probes = stackalloc float[3];
        var probeCount = 3;
        if (state.XSpeed > 0)
        {
            probes[0] = centerProbe;
            probes[1] = rightProbe;
            probes[2] = leftProbe;
        }
        else if (state.XSpeed < 0)
        {
            probes[0] = centerProbe;
            probes[1] = leftProbe;
            probes[2] = rightProbe;
        }
        else
        {
            probes[0] = centerProbe;
            probeCount = 1;
        }

        for (var i = 0; i < probeCount; i++)
        {
            if (TryFindNativeSlopeOverrun(probes[i], bottom, slopes))
            {
                state.OnGround = true;
                state.NativeSlopeOverrunGround = true;
                state.InAirState = NativeFallingInAirState;
                state.SlopeKind = -1;
                state.SlopePlayer = 0;
                state.SlopeType = 0;
                return true;
            }
        }

        return false;
    }

    private static bool TryApplyPersistentNativeSlopeOverrunGround(
        ref PlayerState state,
        float bottom,
        IReadOnlyList<SlopeSurface> slopes)
    {
        var centerProbe = state.X + 8.0f;
        var leftProbe = state.X + 2.0f;
        var rightProbe = state.X + PlayerWidth - 2.0f;
        Span<float> probes = stackalloc float[3];
        probes[0] = centerProbe;
        if (state.XSpeed < 0)
        {
            probes[1] = leftProbe;
            probes[2] = rightProbe;
        }
        else
        {
            probes[1] = rightProbe;
            probes[2] = leftProbe;
        }

        for (var i = 0; i < probes.Length; i++)
        {
            if (TryFindPersistentNativeSlopeOverrun(probes[i], bottom, slopes, out var shouldLand))
            {
                if (shouldLand)
                {
                    ApplyNativeSlopeOverrunLanding(ref state);
                }
                else
                {
                    state.OnGround = true;
                    state.NativeSlopeOverrunGround = true;
                    state.InAirState = NativeFallingInAirState;
                    state.SlopeKind = -1;
                    state.SlopePlayer = 0;
                    state.SlopeType = 0;
                }
                return true;
            }
        }

        return false;
    }

    private static bool TryFindNativeSlopeOverrun(
        float probeX,
        float bottom,
        IReadOnlyList<SlopeSurface> slopes)
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

            var surfaceY = SurfaceYAt(slope, probeX);
            if (bottom < surfaceY && bottom >= surfaceY - 16.0f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindPersistentNativeSlopeOverrun(
        float probeX,
        float bottom,
        IReadOnlyList<SlopeSurface> slopes,
        out bool shouldLand)
    {
        shouldLand = false;
        foreach (var slope in slopes)
        {
            if (slope.Ceiling || slope.NativeSlopeKind != 13)
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

            var surfaceY = SurfaceYAt(slope, probeX);
            if (bottom > surfaceY + 1.0f)
            {
                shouldLand = true;
                return true;
            }
            if (bottom <= surfaceY + NativeSlopeSnapDistanceForKind(slope.NativeSlopeKind) + 16.0f)
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyNativeSlopeOverrunLanding(ref PlayerState state)
    {
        AddYSubpixelDelta(ref state, NativeSlopeOverrunLandingSubpixelCorrection);
        state.OnGround = true;
        state.NativeSlopeOverrunGround = false;
        state.InAirState = 0;
        state.SlopeKind = -1;
        state.SlopePlayer = 0;
        state.SlopeType = 0;
        state.LeadingFootCarryFrames = Math.Max(state.LeadingFootCarryFrames, 2);
        state.PreserveGroundYSpeedFrames = Math.Max(state.PreserveGroundYSpeedFrames, 1);
    }

    private static float AdjustPlayerFloorYForNativeSlope(float floorY, SlopeSurface slope, PlayerState state)
    {
        if (TryAdjustDuckingSteepRightSlopeFloorY(floorY, slope, state, out var adjustedFloorY))
        {
            return adjustedFloorY;
        }

        return slope.NativeSlopeKind == 12 && state.XSpeed > 8
            ? floorY + 1.0f
            : floorY;
    }

    private static bool TryAdjustDuckingSteepRightSlopeFloorY(
        float floorY,
        SlopeSurface slope,
        PlayerState state,
        out float adjustedFloorY)
    {
        adjustedFloorY = floorY;
        if (!state.Ducking ||
            state.LeadingFootCarryFrames > 1 ||
            slope.NativeSlopeKind != 13 ||
            slope.NativeSlopeKind * 16 + 15 >= NativeSlopeShapeTable.Length)
        {
            return false;
        }

        var localX = ((state.X & 0x0F) + 8) & 0x0F;
        var shape = unchecked((sbyte)NativeSlopeShapeTable[slope.NativeSlopeKind * 16 + localX]);
        var pushOut = (state.Y & 0x0F) - shape;
        if (pushOut < 0 || pushOut >= NativeSlopeSnapDistanceForKind(slope.NativeSlopeKind))
        {
            return false;
        }

        var nativeAnchorY = state.Y - pushOut;
        adjustedFloorY = nativeAnchorY + PlayerCollisionYOffsetFor(state) + PlayerCollisionHeightFor(state);
        return true;
    }

    private static bool TryResolvePlayerFloorSlopeFromAbove(
        PlayerState state,
        float top,
        float bottom,
        float previousBottom,
        bool wasOnGround,
        IReadOnlyList<SlopeSurface> slopes,
        out float floorY,
        out SlopeSurface floorSlope)
    {
        var leftProbe = state.X + 2.0f;
        var nativeCenterProbe = state.X + 8.0f;
        var rightProbe = state.X + PlayerWidth - 2.0f;
        var legacyRightSupportProbe = state.X + 5.0f;
        Span<float> probes = stackalloc float[4];
        var probeCount = 3;
        if (!wasOnGround)
        {
            probes[0] = nativeCenterProbe;
            probeCount = 1;
        }
        else if (state.XSpeed > 0)
        {
            probes[0] = nativeCenterProbe;
            probes[1] = rightProbe;
            probes[2] = leftProbe;
            probes[3] = legacyRightSupportProbe;
            probeCount = 4;
        }
        else if (state.XSpeed < 0)
        {
            probes[0] = nativeCenterProbe;
            probes[1] = leftProbe;
            probes[2] = rightProbe;
            probeCount = 3;
        }
        else
        {
            probes[0] = nativeCenterProbe;
            probes[1] = leftProbe;
            probes[2] = rightProbe;
        }

        var aboveTolerance = wasOnGround ? 6.0f : 0.0f;
        for (var probeIndex = 0; probeIndex < probeCount; probeIndex++)
        {
            var probeX = probes[probeIndex];
            if (TryResolveFloorSlopeFromAbove(
                probeX,
                top,
                bottom,
                previousBottom,
                state.YSpeed,
                slopes,
                aboveTolerance,
                16.0f,
                out floorY))
            {
                FindMatchingFloorSlope(probeX, floorY, slopes, out floorSlope);
                if (ShouldSkipNonNativeSlopeSupportProbe(
                    state,
                    probeX,
                    legacyRightSupportProbe,
                    floorSlope))
                {
                    continue;
                }
                return true;
            }
        }

        floorY = 0.0f;
        floorSlope = default;
        return false;
    }

    private static bool ShouldSkipNonNativeSlopeSupportProbe(
        PlayerState state,
        float probeX,
        float legacyRightSupportProbe,
        SlopeSurface floorSlope)
    {
        return (state.XSpeed > 0 &&
                state.LeadingFootCarryFrames <= 1 &&
                floorSlope.NativeSlopeKind == 13 &&
                MathF.Abs(probeX - legacyRightSupportProbe) > 0.01f);
    }

    private static bool TryResolveNativeKindRangeFloorSlopeFromAbove(
        float probeX,
        float top,
        float bottom,
        float previousBottom,
        float ySpeed,
        IReadOnlyList<SlopeSurface> slopes,
        int minNativeKind,
        int maxNativeKind,
        float aboveTolerance,
        float belowTolerance,
        out float floorY,
        out SlopeSurface floorSlope)
    {
        foreach (var slope in slopes)
        {
            if (slope.Ceiling ||
                slope.NativeSlopeKind < minNativeKind ||
                slope.NativeSlopeKind > maxNativeKind)
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

            var surfaceY = SurfaceYAt(slope, probeX);
            var effectiveBelowTolerance = EffectiveSlopeBelowTolerance(slope, belowTolerance);
            if (ySpeed < 0 ||
                bottom < surfaceY - aboveTolerance ||
                bottom > surfaceY + effectiveBelowTolerance ||
                top >= surfaceY - 1.0f ||
                previousBottom > surfaceY + effectiveBelowTolerance)
            {
                continue;
            }

            floorY = surfaceY;
            floorSlope = slope;
            return true;
        }

        floorY = 0.0f;
        floorSlope = default;
        return false;
    }

    private static void ApplyNativeSlopeContact(ref PlayerState state, SlopeSurface slope)
    {
        ApplyNativeSlopeMetadata(ref state, slope);

        var kind = state.SlopeKind;
        var stationaryKind = kind;
        if (kind < 0x1C && state.XSpeed != 0)
        {
            var slopeType = NativeSlopeTypeTable[kind];
            if (slopeType != 0 && (((state.XSpeed & 0xFF) ^ (slopeType & 0xFF)) & 0x80) != 0)
            {
                if (Math.Abs(state.XSpeed) >= 0x28)
                {
                    state.YSpeed = NativeSlopePlayerTowardsPeakYSpeedTable[kind];
                    return;
                }

                stationaryKind = 32;
            }
        }

        var stationaryYSpeed = NativeSlopePlayerStationaryYSpeedTable[stationaryKind];
        if (ShouldPreserveDuckingSteepRightSlopeFallSpeed(state, slope, stationaryKind, stationaryYSpeed))
        {
            return;
        }

        if (state.YSpeed > stationaryYSpeed)
        {
            state.YSpeed = stationaryYSpeed;
        }
    }

    private static bool ShouldPreserveDuckingSteepRightSlopeFallSpeed(
        PlayerState state,
        SlopeSurface slope,
        int stationaryKind,
        int stationaryYSpeed)
    {
        return state.Ducking &&
            slope.NativeSlopeKind == 13 &&
            stationaryKind == 13 &&
            state.YSpeed > stationaryYSpeed;
    }

    private static bool ShouldPreserveDuckingSteepRightSlopeSubY(PlayerState state, SlopeSurface slope)
    {
        return state.Ducking &&
            slope.NativeSlopeKind == 13 &&
            state.YSpeed > NativeSlopePlayerStationaryYSpeedTable[13];
    }

    private static void ApplyLooseSteepSlopeGround(ref PlayerState state, int kind)
    {
        ApplyNativeSlopeMetadataForKind(ref state, kind);
        state.OnGround = true;
        state.RunningTakeoff = false;
        state.LooseSteepSlopeKind = state.SlopeKind;
        state.LooseSteepSlopeGroundFrames = Math.Max(0, state.LooseSteepSlopeGroundFrames - 1);
        if (state.LooseSteepSlopeGroundFrames == 2 && state.YSpeed > SteepPipeLooseGroundFirstCarryYSpeed)
        {
            state.YSpeed = SteepPipeLooseGroundFirstCarryYSpeed;
        }
        else if (state.LooseSteepSlopeGroundFrames == 1 && state.YSpeed > SteepPipeLooseGroundPreJumpYSpeed)
        {
            state.YSpeed = SteepPipeLooseGroundPreJumpYSpeed;
        }
    }

    private static void ApplyNativeSlopeMetadata(ref PlayerState state, SlopeSurface slope)
    {
        ApplyNativeSlopeMetadataForKind(ref state, slope.NativeSlopeKind);
    }

    private static void ApplyNativeSlopeMetadataForKind(ref PlayerState state, int kind)
    {
        if (kind < 0 || kind >= NativeSlopePlayerTable.Length)
        {
            kind = 32;
        }

        state.SlopeKind = kind;
        state.SlopePlayer = NativeSlopePlayerTable[kind];
        state.SlopeType = NativeSlopeTypeTable[kind];
    }

    private static bool FindMatchingFloorSlope(
        float probeX,
        float surfaceY,
        IReadOnlyList<SlopeSurface> slopes,
        out SlopeSurface floorSlope)
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

            var candidateY = SurfaceYAt(slope, probeX);
            if (MathF.Abs(candidateY - surfaceY) <= 0.01f)
            {
                floorSlope = slope;
                return true;
            }
        }

        floorSlope = default;
        return false;
    }

    public static bool TrySurfaceYAt(SlopeSurface slope, float probeX, out float surfaceY)
    {
        var minX = MathF.Min(slope.X0, slope.X1);
        var maxX = MathF.Max(slope.X0, slope.X1);
        if (probeX < minX || probeX > maxX)
        {
            surfaceY = 0.0f;
            return false;
        }

        var t = maxX == minX ? 0.0f : (probeX - slope.X0) / (slope.X1 - slope.X0);
        if (t < 0.0f || t > 1.0f)
        {
            surfaceY = 0.0f;
            return false;
        }

        surfaceY = SurfaceYAt(slope, probeX);
        return true;
    }

    private static float SurfaceYAt(SlopeSurface slope, float probeX)
    {
        if (!slope.Ceiling &&
            slope.NativeSlopeKind >= 0 &&
            slope.NativeSlopeKind * 16 + 15 < NativeSlopeShapeTable.Length)
        {
            var localX = Math.Clamp((int)MathF.Floor(probeX - MathF.Min(slope.X0, slope.X1)), 0, 15);
            var tileTop = MathF.Min(slope.Y0, slope.Y1);
            return tileTop + unchecked((sbyte)NativeSlopeShapeTable[slope.NativeSlopeKind * 16 + localX]);
        }

        var minX = MathF.Min(slope.X0, slope.X1);
        var maxX = MathF.Max(slope.X0, slope.X1);
        var t = maxX == minX ? 0.0f : (probeX - slope.X0) / (slope.X1 - slope.X0);
        return slope.Y0 + (slope.Y1 - slope.Y0) * t;
    }

    private static void AddXAccel(ref PlayerState state, int accel)
    {
        var combined = (CombinedXSpeed16(state) + accel) & 0xFFFF;
        SetCombinedXSpeed16(ref state, combined);
    }

    private static int CombinedXSpeed16(PlayerState state)
    {
        return ((state.XSpeed & 0xFF) << 8) | (state.SubXSpeed & 0xFF);
    }

    private static void SetCombinedXSpeed16(ref PlayerState state, int combined)
    {
        combined &= 0xFFFF;
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

    private static void AddYSubpixelDelta(ref PlayerState state, int delta)
    {
        var combined = state.Y * 0x100 + state.SubY + delta;
        state.Y = Math.DivRem(combined, 0x100, out var subY);
        if (subY < 0)
        {
            state.Y--;
            subY += 0x100;
        }
        state.SubY = subY;
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

    private static int ToU16(int value)
    {
        return value & 0xFFFF;
    }
}
