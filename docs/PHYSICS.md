# Physics Notes

The first playable slice uses SMW-style 60 Hz fixed-step integer movement. The target for the finished port is a 100% physics match, so every approximation in this file is temporary until it is replaced by a reference-backed port and test.

Current implementation details:

- Position is stored in sixteenth-pixel units.
- Horizontal and vertical speeds use the same practical scale as SMW player speeds: one speed unit is `1/16 px/frame`.
- The full native horizontal max-speed, acceleration, friction/deceleration, and target sub-speed tables are exposed as `SmwPhysics.HorizontalMaxSpeedTable`, `HorizontalAccelerationTable`, `HorizontalGroundFrictionTable`, `HorizontalDecelerationTable`, and `HorizontalTargetSubSpeedTable`.
- Normal flat-ground horizontal caps now read the corresponding `kHandlePlayerPhysics_DATA_00D535` direction/P-meter entries directly: walking `0x14`, running `0x24`, and full P-meter sprinting `0x30`.
- P-meter updates use the native `kHandlePlayerPhysics_DATA_00D5EB` mode deltas `[-1, -1, +2]`; mode `2` returns mode `3` when it reaches `0x70`, which selects the sprint cap. The current C# bridge tracks full-P-meter running jumps as `RunningTakeoff` until landing so the airborne sprint mode is only carried from a native-style takeoff, not from every airborne full-meter state.
- Flat-ground acceleration now reads `kHandlePlayerPhysics_MarioAccel` for grounded left/right and run/walk movement. Turn-around acceleration uses the native walk/run entries `0x0280` and `0x0500` instead of the earlier temporary `0x0600` shortcut.
- Held horizontal movement now follows the native `HandlePlayerPhysics_00D742()` branch: reaching or overshooting a cap applies table drag/deceleration back toward the target sub-speed instead of hard-clamping `XSpeed` and clearing subspeed.
- Flat no-input ground friction now reads the native `kHandlePlayerPhysics_DATA_00D309` table entries `-0x0020`/`+0x0020`; no-input friction is skipped while airborne, matching `HandlePlayerPhysics_00D764()` only calling the friction branch when `player_in_air_flag` is clear.
- Slope contact now feeds `NativeSlopePlayerTable[kind]` back into the horizontal branch as the current `player_slope_player_is_on1` value. That value selects native slope rows in `kHandlePlayerPhysics_DATA_00D535`, `kHandlePlayerPhysics_MarioAccel`, `kHandlePlayerPhysics_DATA_00D309`, and `kHandlePlayerPhysics_DATA_00D5C9`, including the first no-input downslope sliding behavior. This still depends on the temporary geometric slope resolver to identify the contact, so exact block-edge behavior remains pending.
- Slope contact also ports the `RunPlayerBlockCode_00EEE1()` fast-toward-peak branch: if Mario is moving at least `0x28` toward a slope peak, vertical speed comes from `kSlopeDataTables_Player_TowardsPeakYSpeed`; slower toward-peak movement falls back to native slope row `32` before the stationary cap is applied.
- Normal jump speeds use the 16-entry `kHandlePlayerPhysics_JumpHeightTable`.
- The jump-table lookup is now public as `SmwPhysics.JumpSpeedIndexFor()` / `JumpYSpeedFor()` and exposed through RCON `physics`, so live playtests can verify exactly which native jump row a given X speed and spin state would use.
- The fixed-step order now starts jump/gravity before horizontal acceleration/P-meter selection, matching the native `HandlePlayerPhysics()` branch where jump can set the running-takeoff air flag before horizontal speed mode is resolved.
- Gravity uses the native `HandlePlayerPhysics_D930` table entries exposed as `VerticalGravityTable`/`VerticalMaxFallTable`: `0x06` when jump is not held and `0x03` while jump/spin is held in the current simplified in-air branch.
- Grounded frames now pin vertical speed to zero and skip `HandlePlayerPhysics_D930`-style gravity. A one-pixel top-of-solid contact probe preserves grounded state when Mario is exactly standing on a floor, while walking beyond that support clears `OnGround` before gravity starts on the next frame.
- Maximum normal fall speed is currently `0x40`, matching the common non-cape clamp in the same table.
- Cape form now ports the first `HandlePlayerPhysics_InAir()` float branch: when cape Mario is falling and holding jump, the runtime starts the native `0x10`-frame float timer and clamps downward `YSpeed` to the first `00D7B9` table value, `0x10`. This is only the fall-cap slice; full cape takeoff, flight phase, dive, glide, and `00D7C8/00D7D4/00D7D9` behavior are still pending.
- Big/cape/fire forms now track a `Ducking` state when grounded and holding down. The temporary duck hitbox uses the 16px small-height footprint and preserves Mario's feet when entering or leaving the state; grounded ducking suppresses horizontal acceleration like the native early ducking branch.
- Fire form now has the first `CheckPowerUpSpecificPlayerAttacks`/`SpawnPlayerFireball` bridge: a run/Y pressed edge creates a capped player fireball using native spawn offsets, native `0x03/0xFD` horizontal speed direction, initial downward speed `0x30`, and the native 10-frame shoot-pose timer. Its motion and terrain bounce are still a temporary extended-sprite approximation until the complete `ExtSpr05_MarioFireball`, extended sprite collision, smoke, and fire-kill normal-sprite branches are ported.
- Horizontal movement is clamped to the generated level bounds so Mario cannot leave the level to the left or right during the playable slice.
- Runtime slope collision is a temporary geometric bridge. Only imported diagonal pipe/ledge/slope edge cells become slope surfaces; slope-family support/fill/assist cells are not emitted as rectangular solids because those temporary AABBs made Mario stand on hidden stair steps inside visual slopes. These surfaces are emitted per Map16 tile rather than as long connected-component averages, which avoids false collision lines when adjacent or overlapping slope objects touch. Standard slope Map16 tiles now use per-tile segment heights: gradual slopes change by 4 px per tile, normal slopes by 8 px per tile, and steep slopes by 16 px per tile. Diagonal pipe floor, body, and underside cells are tracked separately so floor/ceiling slope segments can remain traversable while embedded player positions still get rejected from body cells inside the pipe volume; floor cells are not reused for horizontal body intrusion. The player floor solver probes center plus both feet, biased toward horizontal movement direction, to reduce missed slope contact at tile boundaries. The final fix is still the native Map16 act-as/slope handling path, not this geometric shortcut.
- Diagonal-pipe body recovery now only resolves frames with actual horizontal intrusion. Pure vertical motion inside the temporary pipe-volume cells is left to the slope/ceiling branch so adjacent body cells do not alternately push Mario left and right during a jump trace.
- Ceiling-slope contact now requires crossing from below/inside on the previous frame before zeroing upward motion. This keeps controlled RCON jump probes near the Yoshi Island 1 diagonal-pipe underside from having their native jump speed canceled by a slope segment that Mario was already embedded past because of the temporary geometric resolver.
- The floor-slope probe is shared by the player and the first-pass runtime sprite actors, so imported enemies can stand on the same temporary slope surfaces instead of only interacting with rectangular solids. Sprite actors still use a simpler center probe than the player for now.
- RCON `slope_probe` and `trace_sensors` expose the left/center/right floor probes, per-probe Map16/pipe-cell roles, from-above hit decision, and nearby native slope `kind`/`snap` values so pipe and slope collision regressions can be captured without manual screenshots.
- `SMW_DEBUG_COLLISION_TRACE=1` and `SMW_DEBUG_SLOPE_TRACE=1` add low-level physics logs for rect intersections and ceiling-slope candidates during headless/RCON repros. They are intentionally environment-gated because normal traces are already verbose.
- `tools/check-dotnet.sh` runs a C# physics smoke executable that verifies the flat-ground caps, native cap-drag step, P-meter sprint threshold behavior, friction step, imported horizontal max-speed table, native slope table decode, slope-player horizontal slide rows, and native-unit golden trajectories for sustained walk/run, small-jump hold/release, and ledge-fall probes.

## Recording Sync Baseline

The current trace workflow compares a clean native boot recording against Godot without native save states:

- `tools/run-native-input-recording-wayland.sh` records the native `smw/` build from boot, stores timestamped full and level-start `.input` files under `generated/smw/recordings/`, and refreshes stable `latest-*` aliases for the harness.
- `K` is the manual level-start marker when recording. The trace wrapper can also use an automatic active-level marker, but manual `K` is preferred when the user enters the level and wants the Godot slice to begin at that exact point.
- `tools/run-recording-trace-compare.sh` now runs native long enough to reach the marked absolute frame, drops native records before that frame, and then compares against Godot frame 0 from the sliced input.

Fresh trace status from the latest small-Mario Yoshi Island 1 recording:

- Native and Godot now agree on initial `x=16.00` and `powerup=0` at the selected level-start slice, so the earlier Yoshi-house/wrong-level input problem is no longer the active blocker.
- The first remaining divergence is vertical anchoring. Native reports raw `player_y=352`, which the current comparator normalizes to `288` with `--native-y-offset=-64`, and native stays grounded. Godot starts at `y=288`, but the Godot collision box treats that as a top-left coordinate; the foot probe sees no solid support until the top-left reaches about `304`. Normal entrances now seed `OnGround`, `InAirState=0`, and zero vertical speed, but the first physics step still loses grounded state because the anchor/collision interpretation disagrees with native.
- The next physics task is to identify the native `player_ypos` anchor and small-Mario interaction box from the SMW routines, then update either the comparator offset or Godot player anchor so standing height, feet, and trace Y all describe the same point.

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

Current slope bridge status: Godot still resolves slopes through generated per-Map16 line segments, but each segment now carries decoded native slope metadata from the SMW tables: slope kind, player slope value, slope type, stationary Y-speed cap, and snap distance. `state`, `trace`, and collision RCON probes expose these fields so slope bugs can be compared against `RunPlayerBlockCode_00EEE1()` table behavior. Map16 rows whose native snap distance is zero are kept as kind diagnostics but continue using the geometric fallback tolerance until the full native block-interaction path replaces the temporary line solver.

Tortellini (`https://github.com/ToadsworthLP/Tortellini`) was reviewed as a Godot/C# Mario-style controller reference. It is MIT-licensed and useful for structure: explicit stand/walk/run/long-run/jump/spin-jump/fall/crouch/slide states, floor-normal driven slope behavior, and Godot snapping flow. It is not a source of truth for this port's constants because it targets a Godot 3-era 3D controller with exported feel parameters such as `WalkSpeed`, `RunSpeed`, and jump forces rather than SMW ROM units and native tables.

Useful URLs:

- `https://blog.hamaluik.ca/posts/super-mario-world-physics/`
- `https://tasvideos.org/GameResources/SNES/SuperMarioWorld`
