# Physics Notes

The first playable slice uses SMW-style 60 Hz fixed-step integer movement. The target for the finished port is a 100% physics match, so every approximation in this file is temporary until it is replaced by a reference-backed port and test.

Current implementation details:

- Position is stored in sixteenth-pixel units.
- Horizontal and vertical speeds use the same practical scale as SMW player speeds: one speed unit is `1/16 px/frame`.
- Normal flat-ground horizontal caps use the first entries from `kHandlePlayerPhysics_DATA_00D535`: walking `0x14`, running `0x24`, and full P-meter sprinting `0x30`.
- P-meter updates use the native `kHandlePlayerPhysics_DATA_00D5EB` mode deltas `[-1, -1, +2]`; mode `2` returns mode `3` when it reaches `0x70`, which selects the sprint cap. The current C# bridge tracks full-P-meter running jumps as `RunningTakeoff` until landing so the airborne sprint mode is only carried from a native-style takeoff, not from every airborne full-meter state.
- Ground friction now uses the native sub-speed step `0x0020`; no-input friction is skipped while airborne, matching `HandlePlayerPhysics_00D764()` only calling the friction branch when `player_in_air_flag` is clear.
- Normal jump speeds use the 16-entry `kHandlePlayerPhysics_JumpHeightTable`.
- The fixed-step order now starts jump/gravity before horizontal acceleration/P-meter selection, matching the native `HandlePlayerPhysics()` branch where jump can set the running-takeoff air flag before horizontal speed mode is resolved.
- Gravity uses the native `HandlePlayerPhysics_D930` table entries exposed as `VerticalGravityTable`/`VerticalMaxFallTable`: `0x06` when jump is not held and `0x03` while jump/spin is held in the current simplified in-air branch.
- Grounded frames now pin vertical speed to zero and skip `HandlePlayerPhysics_D930`-style gravity. A one-pixel top-of-solid contact probe preserves grounded state when Mario is exactly standing on a floor, while walking beyond that support clears `OnGround` before gravity starts on the next frame.
- Maximum normal fall speed is currently `0x40`, matching the common non-cape clamp in the same table.
- Cape form now ports the first `HandlePlayerPhysics_InAir()` float branch: when cape Mario is falling and holding jump, the runtime starts the native `0x10`-frame float timer and clamps downward `YSpeed` to the first `00D7B9` table value, `0x10`. This is only the fall-cap slice; full cape takeoff, flight phase, dive, glide, and `00D7C8/00D7D4/00D7D9` behavior are still pending.
- Big/cape/fire forms now track a `Ducking` state when grounded and holding down. The temporary duck hitbox uses the 16px small-height footprint and preserves Mario's feet when entering or leaving the state; grounded ducking suppresses horizontal acceleration like the native early ducking branch.
- Horizontal movement is clamped to the generated level bounds so Mario cannot leave the level to the left or right during the playable slice.
- Runtime slope collision is a temporary geometric bridge. Only imported diagonal pipe/ledge/slope edge cells become slope surfaces; slope-family support/fill/assist cells are not emitted as rectangular solids because those temporary AABBs made Mario stand on hidden stair steps inside visual slopes. These surfaces are emitted per Map16 tile rather than as long connected-component averages, which avoids false collision lines when adjacent or overlapping slope objects touch. Standard slope Map16 tiles now use per-tile segment heights: gradual slopes change by 4 px per tile, normal slopes by 8 px per tile, and steep slopes by 16 px per tile. Diagonal pipe undersides can also emit ceiling slope surfaces so upward movement stops against the pipe body without reintroducing full support-tile blockers. The player floor solver probes center plus both feet, biased toward horizontal movement direction, to reduce missed slope contact at tile boundaries. The final fix is still the native Map16 act-as/slope handling path, not this geometric shortcut.
- The floor-slope probe is shared by the player and the first-pass runtime sprite actors, so imported enemies can stand on the same temporary slope surfaces instead of only interacting with rectangular solids. Sprite actors still use a simpler center probe than the player for now.
- `tools/check-dotnet.sh` runs a C# physics smoke executable that verifies the flat-ground caps, P-meter sprint threshold behavior, friction step, and imported horizontal max-speed table.

This is not a complete reimplementation of every SMW physics branch yet. Missing pieces include the full native Map16 act-as/slope interaction table, ice, swimming, full cape flight, carrying, Yoshi, rope/net climbing, solid-sprite interaction, and exact takeoff behavior. The purpose of this slice is to establish the deterministic Godot core, asset feed, and regression surface before broadening the compatibility matrix.

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

Tortellini (`https://github.com/ToadsworthLP/Tortellini`) was reviewed as a Godot/C# Mario-style controller reference. It is MIT-licensed and useful for structure: explicit stand/walk/run/long-run/jump/spin-jump/fall/crouch/slide states, floor-normal driven slope behavior, and Godot snapping flow. It is not a source of truth for this port's constants because it targets a Godot 3-era 3D controller with exported feel parameters such as `WalkSpeed`, `RunSpeed`, and jump forces rather than SMW ROM units and native tables.

Useful URLs:

- `https://blog.hamaluik.ca/posts/super-mario-world-physics/`
- `https://tasvideos.org/GameResources/SNES/SuperMarioWorld`
