# Godot Port Plan

The native C++ SMW repository is only a moving reference. This repo owns the Godot project and must not depend on files from the native repo after importing or translating specific behavior.

## Milestone 1

Make Yoshi Island 1 playable in Godot with a path toward exact frame-level behavior:

1. Extract the original ROM into Godot-readable generated data.
2. Render enough of the imported level to inspect object streams, screen numbers, exits, and sprite spawns.
3. Implement Mario movement, jump, gravity, and collision in a deterministic fixed-step core.
4. Implement pipe and screen-exit transitions early, preserving the original exit low byte, high bit, secondary flag, and raw property byte.
5. Add focused regression tests for transition bugs before expanding content.
6. Keep deterministic input paths available for every playability slice: imported scripts for fixed repros, RCON frame stepping for local debugging, and `--smw-autoplay=explore` for unattended first-level traversal probes.

## Compatibility Bar

The final goal is a 100% gameplay and physics match with the original game. The Godot implementation should be modern and native to PC, but simulation behavior must be anchored to the original state machines, tables, fixed-point units, collision semantics, transitions, and timers.

The first playable slice can be incomplete, but incomplete branches must be marked explicitly and converted into reference-backed tests as they are ported.

## Runtime Choice

The project uses Godot .NET and C# for the gameplay core. The local executable is `godot4-mono`. The importer remains Python because it is offline tooling, not runtime game logic.
