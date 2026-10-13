# First complete course: Yoshi's Island 1

The next release gate is the complete native `105` course and its `1CB` pipe
room, from normal course entry through completion and return to the world.
Keep the development selector and audio player available for investigation.
Expand the playable course list only after the current course's required
behavior has original-ROM evidence. Exact object layout alone is not a
complete playable course.

## Current evidence

| Area | Evidence | Remaining gate |
| --- | --- | --- |
| ROM input | Desktop selection and browser byte handoff use the shared validated reader. Copier headers strip at the selection boundary. | Actual custom Web runtime execution and desktop package smoke. |
| Directory | All 512 pointer slots parse; original name table is bounded to $5D words and text to 19 tiles. Primary-layout aliases are explicit. | Reachability and world/cave/boss/unused-room classification. |
| Layout | Complete original loader Map16 buffers match for `105` and `1CB`, plus `0C7` and `106`. | Collection/switch/midpoint state, animated tiles and runtime edits. |
| Art | Native palettes, foreground/background atlases, sprites and player art use ROM data. Verified assets match byte/pixel fixtures. | Full scene pixels, camera, priority, animation, HUD and transitions. |
| Entrances | Native main/secondary entrance data; diagnostic `105 -> 1CB -> 105` passes with no generated pack. | Player-driven pipe entry, release timing, item memory and checkpoint state. |
| Player/actors | Fixed-step gameplay, contact diagnostics, actor/sensor/OAM traces and checkpoints exist. | Exact original motion, clipping, damage, power-up sequences, spawn order and actor state machines. |
| Audio | Native sequencing and managed integer DSP; original music and SFX playable. | Original firmware register-write timing and whole-song PCM identity. |
| Finish | Development goal/walkout path exists. | Original tape scoring, timer conversion, bonus stars, circle transition, world event and save effects. |

The broad gameplay suite still has the previously documented actor-contact
failure (`y=288.25` vs `288.00`). Keep that failure visible while reducing it
against original clipping and update order. Do not change an expectation just
to make the suite green.

## Implementation order

1. **Establish a common first-course state.** Capture original course-entry
   state, controllers, frame phase, counters, RNG, camera, sprite slots and
   collection flags. Produce the equivalent typed native state in Godot.
   The existing development entrance has explicit differences and cannot
   establish a power-on/TAS parity pass.
2. **Player and Map16 contact.** Compare neutral, walking/running, jump/spin,
   slopes, corners, embedded tiles, damage and power-up transitions. Find the
   first raw-state divergence and port the corresponding routine. Remove each
   geometric or position-specific bridge only with a matching regression.
3. **Every first-course actor and object.** Inventory each ROM sprite record,
   its dispatch, initial state, slot allocation, load/despawn range, collision,
   animation and interactions. Cover Rex, Banzai Bill, Koopas/shells, Chucks,
   Piranha plants, blocks, pickups, midpoint and goal tape from actual records.
   An unsupported actor must remain an explicit gap.
4. **Room and persistence flow.** Enter the pipe through ordinary input,
   collect coins, return, retry, die, restart at midpoint and re-enter. Apply
   native item memory and collected flags when reconstructing terrain. Verify
   checkpoints restore the same future input, actor and audio results.
5. **Presentation and audio.** Match logical framebuffer pixels with original
   palette, camera, tile priority, player/actor OAM, animations and status bar.
   Match original music/SFX events and timing independently from DSP fixtures.
   Validate authentic PCM before Godot output effects.
6. **Complete the course.** Match tape contact/scoring, all finish sequences,
   world progress and save changes. Replay an ordinary full playthrough and
   reduced edge cases against the original. Add a TAS case only after its
   initial state, both controllers, console events and lag semantics are
   supported and recorded.

For each step, retain original and native case manifests, exact state/pixel
comparisons and first-divergence diagnostics under ignored output paths.
The complete-course gate requires no unexplained differences in the declared
corpus. Passing a debug warp or autoplay with invulnerability does not meet it.

## Reproduce the current connection checks

```bash
tools/check-dotnet.sh
SMW_ROM_PATH=/path/to/smw-usa.sfc tools/check-rom-startup.sh
SMW_ROM_PATH=/path/to/smw-usa.sfc tools/check-native-terrain.sh --godot
node --test tests/WebLoader/loader.test.mjs
```

On a host with Bun, the last command can use
`bun test tests/WebLoader/loader.test.mjs`. Providing `SMW_ROM_PATH` also runs
the local browser-validator case; CI skips that proprietary input.

The startup check exercises source-only startup, native file selection,
copier-header handling, invalid-ROM rejection, all four verified entries,
unsupported-layout rejection, actual menu search/activation and viewport
containment, the diagnostic pipe route and audio-player controls. It explicitly
records that full-course gameplay, whole-song audio
and execution of Godot Web have not been proven by those checks.

## Grow toward other courses and ROM hacks

Use shared ROM data contracts, object/actor behavior dispatch and reusable
Godot scenes rather than course-coordinate patches. Keep the vanilla profile
and its corpus fixed. After the first complete course, choose the next course
by the new behavior family it adds; require each room's data, state, visuals
and progression evidence before marking that family supported.

Data-format compatibility and executable modifications are separate:

- Add declared decoder profiles for known regional/editor formats, expanded
  graphics, Map16, palettes, object/sprite streams and entrance tables. Preserve
  identifiers and reject unsupported formats explicitly.
- Add native behavior implementations for supported custom blocks, sprites,
  music drivers and game rules. Map ROM metadata to these implementations.
- Detect unsupported engine patches, mapping changes, coprocessors and custom
  assembly. Arbitrary machine code cannot automatically become native Godot
  behavior through asset import. Never silently run it or claim compatibility.

The current ROM fingerprint intentionally admits only vanilla USA. ROM hacks
are the long-term engine goal, not current import support.
