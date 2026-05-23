# SMW Physics Reference Notes

This file captures external and native-reference physics facts used by the Godot port. The target remains native SMW table behavior, not a hand-tuned platformer feel.

## Runtime Units

The current C# runtime stores horizontal and vertical velocity in SMW-style signed byte units where one whole velocity unit contributes `1/16 px` per frame. For example:

- `XSpeed = 0x14` is `1.25 px/frame`, the current walk cap.
- `XSpeed = 0x24` is `2.25 px/frame`, the current run cap before full P-meter sprint.
- `XSpeed = 0x30` is `3.00 px/frame`, the current sprint cap.
- `YSpeed < 0` moves Mario upward; `YSpeed > 0` falls downward.
- `MaxFall = 0x40` is `4.00 px/frame`.

The runtime also carries separate subpixel accumulators for position and acceleration. This is important because many native constants are fractional in practice, even when the visible pixel position changes only by whole pixels.

## Native Slope Contact

SMW does not treat slopes as continuous physics lines. Once a Map16 tile resolves to a native slope kind, the player/block collision path samples `kSlopeDataTables_ShapeOfSlope[kind * 16 + x_in_block]` and compares that 16-entry per-tile height against Mario's position in the block. The Godot runtime now keeps the generated slope segment as placement metadata, but floor contact height is computed from that native shape table. Ceiling/underside pipe helpers still use the geometric segment path until the rest of the block act-as pipeline is ported.

## Current Native-Style Constants

These are the values currently implemented in `SmwPhysics.cs`:

| Concept | Value |
| --- | ---: |
| Walk cap | `0x14` |
| Run cap | `0x24` |
| Sprint cap | `0x30` |
| Walk acceleration | `0x0180` |
| Run acceleration | `0x0180` |
| Turn acceleration | `0x0280` walk, `0x0500` run |
| Ground friction | `0x0020`, table-selected by direction |
| Air acceleration | native `MarioAccel` table branch |
| P-meter max | `0x70` |
| Normal gravity | `0x06` |
| Jump-held gravity | `0x03` |
| Normal max fall speed | `0x40` |
| Cape float/fall cap while holding jump | `0x10` |

The current jump table is a temporary native-shaped bridge indexed from horizontal speed and spin state. Vertical gravity/max-fall tables and the first cape hold-jump falling cap are now exposed in `SmwPhysics.cs`, but the full cape flight/diving state machine, native jump velocity selection, and player-state-specific branch conditions still need to be ported directly.

## Hamaluik Regression Sanity Checks

Hamaluik's video-analysis article is not native code and should not override the tables above, but it provides useful high-level checks for how movement should feel. The measurements assume small Mario is one meter tall:

| Motion | Regression | Derived value |
| --- | --- | --- |
| Walking | `x = 3.698t - 0.229` | about `3.7 m/s` |
| Running | `x = 9.091t + 0.039` | about `9.1 m/s` |
| Small jump | `y = -33.910t^2 + 17.361t - 0.018` | gravity about `67.82 m/s^2`, push-off about `17.36 m/s` |
| High jump | `y = -17.396t^2 + 15.209t + 0.007` | gravity about `34.79 m/s^2`, push-off about `15.21 m/s` |
| Falling | `y = -27.940t^2 - 1.057t + 2.477` | gravity about `55.88 m/s^2` |

Those curves explain why a plain "realistic" platformer controller feels wrong here: SMW uses very high acceleration/gravity, strong horizontal caps, and variable jump gravity rather than real-world motion.

## Porting Priorities

1. Replace temporary jump velocities and the remaining simplified branch conditions with the native vertical-speed path for normal jump, spin jump, running jump, cape flight/diving, underwater, climbing, riding Yoshi, and damage/knockback states.
2. Keep all player-motion tests in native units (`XSpeed`, `YSpeed`, subpixel position), then add pixel-trajectory golden tests for short scripted input sequences.
3. Treat the Hamaluik curves as visual sanity checks only after native-unit tests pass.
4. Move slope handling toward native Map16 act-as and block interaction code instead of treating modern collision lines as authoritative.
