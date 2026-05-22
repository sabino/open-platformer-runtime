# Physics Notes

The first playable slice uses SMW-style 60 Hz fixed-step integer movement. The target for the finished port is a 100% physics match, so every approximation in this file is temporary until it is replaced by a reference-backed port and test.

Current implementation details:

- Position is stored in sixteenth-pixel units.
- Horizontal and vertical speeds use the same practical scale as SMW player speeds: one speed unit is `1/16 px/frame`.
- Normal horizontal caps use `0x24` walking and `0x30` running from the native reference table.
- Normal jump speeds use the 16-entry `kHandlePlayerPhysics_JumpHeightTable`.
- Gravity uses the native non-cape constants visible around `HandlePlayerPhysics_D930`: `0x06` when jump is not held and `0x03` while jump is held during rising motion.
- Maximum normal fall speed is currently `0x40`, matching the common non-cape clamp in the same table.

This is not a complete reimplementation of every SMW physics branch yet. Missing pieces include slopes, ice, swimming, cape, carrying, Yoshi, rope/net climbing, solid-sprite interaction, and exact P-meter/takeoff behavior. The purpose of this slice is to establish the deterministic Godot core, asset feed, and regression surface before broadening the compatibility matrix.

## Exact-Port Rule

When porting a physics branch, prefer a direct translation of the original state variables and tables over an idiomatic approximation. Rename only at the boundary where it improves Godot integration; keep internal units and timer semantics close enough that trace comparisons against the native reference can be automated.
