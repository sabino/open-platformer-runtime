# Physics Notes

The first playable slice uses SMW-style 60 Hz fixed-step integer movement. The target for the finished port is a 100% physics match, so every approximation in this file is temporary until it is replaced by a reference-backed port and test.

Current implementation details:

- Position is stored in sixteenth-pixel units.
- Horizontal and vertical speeds use the same practical scale as SMW player speeds: one speed unit is `1/16 px/frame`.
- Normal flat-ground horizontal caps use the first entries from `kHandlePlayerPhysics_DATA_00D535`: walking `0x14`, running `0x24`, and full P-meter sprinting `0x30`.
- Ground friction now uses the native sub-speed step `0x0020`; airborne no-input friction uses `0x0100`.
- Normal jump speeds use the 16-entry `kHandlePlayerPhysics_JumpHeightTable`.
- Gravity uses the native non-cape constants visible around `HandlePlayerPhysics_D930`: `0x06` when jump is not held and `0x03` while jump is held during rising motion.
- Maximum normal fall speed is currently `0x40`, matching the common non-cape clamp in the same table.
- Horizontal movement is clamped to the generated level bounds so Mario cannot leave the level to the left or right during the playable slice.
- Runtime slope collision is a temporary geometric bridge. Only imported diagonal pipe/ledge/slope edge cells become slope surfaces; ledge/slope fill, assist, and support cells stay rectangular solids so Mario does not fall through the interior of a slope object. These surfaces are emitted per Map16 tile rather than as long connected-component averages, which avoids false collision lines when adjacent or overlapping slope objects touch. Standard slope Map16 tiles now use per-tile segment heights: gradual slopes change by 4 px per tile, normal slopes by 8 px per tile, and steep slopes by 16 px per tile.
- `tools/check-dotnet.sh` runs a C# physics smoke executable that verifies the flat-ground caps, P-meter sprint threshold behavior, friction step, and imported horizontal max-speed table.

This is not a complete reimplementation of every SMW physics branch yet. Missing pieces include the full native Map16 act-as/slope interaction table, ice, swimming, cape, carrying, Yoshi, rope/net climbing, solid-sprite interaction, and exact takeoff behavior. The purpose of this slice is to establish the deterministic Godot core, asset feed, and regression surface before broadening the compatibility matrix.

## Exact-Port Rule

When porting a physics branch, prefer a direct translation of the original state variables and tables over an idiomatic approximation. Rename only at the boundary where it improves Godot integration; keep internal units and timer semantics close enough that trace comparisons against the native reference can be automated.

## Reference Sources

Hamaluik's "Super Mario World Physics" article is useful as an external feel check, not as the 1:1 source of truth. It measured motion from video at 60 Hz with short Mario treated as 1 meter tall, then fit linear/quadratic regressions:

- Walking: about `3.7 m/s`, from the displayed fit `x = 3.698t - 0.229`.
- Running: about `9.1 m/s`, from the displayed fit `x = 9.091t + 0.039`.
- Small jump: displayed fit `y = -33.910t^2 + 17.361t - 0.018`, which implies about `67.82 m/s^2` downward acceleration and `17.36 m/s` initial upward velocity in that normalized scale.
- High jump: displayed fit `y = -17.396t^2 + 15.209t + 0.007`, implying about `34.79 m/s^2` downward acceleration and `15.21 m/s` initial upward velocity.
- Falling: displayed fit `y = -27.940t^2 - 1.057t + 2.477`, implying about `55.88 m/s^2` downward acceleration.

The direct implementation targets are the native SMW routines and tables:

- Horizontal movement: `HandlePlayerPhysics()`, `HandlePlayerPhysics_00D742()`, `HandlePlayerPhysics_00D772()`, `HandlePlayerPhysics_UpdatePMeterEx()`, and tables `kHandlePlayerPhysics_DATA_00D535`, `kHandlePlayerPhysics_MarioAccel`, `kHandlePlayerPhysics_DATA_00D2CD`, `kHandlePlayerPhysics_DATA_00D309`, `kHandlePlayerPhysics_DATA_00D5C9`, and `kHandlePlayerPhysics_DATA_00D5EB` in `smw/src/smw_00.cpp`.
- Jump and gravity: `kHandlePlayerPhysics_JumpHeightTable`, `HandlePlayerPhysics_InAir()`, `HandlePlayerPhysics_D930()`, `kHandlePlayerPhysics_DATA_00D7A5`, `kHandlePlayerPhysics_DATA_00D7AF`, and cape-specific `00D7B9/00D7C8/00D7D4/00D7D9` tables.
- Slope collision: `RunPlayerBlockCode_EB77()`, `RunPlayerBlockCode_00EEE1()`, `kSlopeSteepness_e55e`, `kSlopeSteepness_e5c8`, `kSlopeDataTables_ShapeOfSlope`, `kSlopeDataTables_Player`, `kSlopeDataTables_Player_StationaryYSpeed`, `kSlopeDataTables_Player_TowardsPeakYSpeed`, `kSlopeDataTables_Player_SnapToSlopeDistance`, and `kSlopeDataTables_SlopeType`.

Useful URLs:

- `https://blog.hamaluik.ca/posts/super-mario-world-physics/`
- `https://tasvideos.org/GameResources/SNES/SuperMarioWorld`
