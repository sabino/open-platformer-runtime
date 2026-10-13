# Local comparison and inspection loop

This is development tooling. Mesen executes the original ROM externally;
the Godot game remains its own implementation. All captures, movies, states,
ROMs, caches, and tool builds stay ignored.

## Setup and commands

Configure ignored `local-rom.cfg` or `SMW_ROM_PATH` as described in
[GETTING-STARTED.md](GETTING-STARTED.md). Prepare pinned sources and the
local macOS ARM64 Mesen tool with:

```bash
python3 tools/setup-reference-tools.py
tools/check-dotnet.sh
tools/run-debug.sh
```

`tools/run-debug.sh --headless` provides the same state/control interface
without a graphical window. Inspection commands include:

```bash
tools/smw-rcon.sh pause
tools/smw-rcon.sh state
tools/smw-rcon.sh step 1
tools/smw-rcon.sh input 60 right run
tools/smw-rcon.sh oam
tools/smw-rcon.sh actors_near 256
tools/smw-rcon.sh collision 880 304 48
tools/smw-rcon.sh save before_change
tools/smw-rcon.sh load before_change
tools/smw-rcon.sh capture_now generated/parity/frame.png
```

Graphical captures require a graphical run. The logical render target is
`256x224`, with nearest filtering and integer host scaling. Agent GUI use
must follow the required macOS focus workflow. Headless captures from Mesen
are the emulator's software framebuffer, not screenshots of a Mac window.

## Capture and compare

```bash
tools/parity.sh oracle --frames 120 --out generated/parity/original
tools/parity.sh godot --frames 120 --out generated/parity/godot
tools/parity.sh compare generated/parity/original/state.jsonl \
  generated/parity/godot/state.jsonl --report generated/parity/first-diff.json
tools/parity.sh pixels generated/parity/original/frame.png \
  generated/parity/frame.png --report generated/parity/pixel-diff.json
```

Oracle runs use zero-filled initial RAM and an isolated 2 KiB SRAM file.
Use `--sram FILE` for a declared original SRAM image. The tool records hashes,
controller input, sampling phase, engine/core hashes, and starting conditions.
Mesen exports integer state, exact subpixels/speeds, WRAM/VRAM/OAM/CGRAM dumps,
and PNG. Each memory dump has a recorded size and hash. To capture the first
execution of a particular original routine address, add
`--snapshot-at-exec 0x00AB01`. This captures memory before that instruction
executes, with the number of completed frame callbacks explicitly recorded.
It is a separate sampling phase from the ordinary end-of-frame capture.
`--diagnostic-level 1CB` enters an arbitrary main entrance through the original
loader's sublevel branch using declared RAM writes at `$05D796`. The manifest
and initialization log record each write and verify its result. These captures
are explicitly ineligible for full TAS parity; they isolate loading routines.
Godot uses its raw integer trace fields rather than rounded position text.
`pixels` compares RGBA exactly with no resize, crop, or tolerance.

Exit codes: `0` means the requested capture/comparison succeeded, `1` means a
comparison differs or its initial state is incompatible, and `2` means setup
or input/capture failed. A capture succeeding does not mean game parity passed.

The current Godot capture starts at a development level; the original starts
at power-on. The default comparator reports this incompatibility. The original
boot/mode graph and global/local counters remain unimplemented and are not
fabricated in the trace. Use `--diagnostic` only to inspect the missing fields;
any such result is explicitly ineligible as a full parity pass.

## Inputs and TAS

An input file contains frame durations and SNES button names, for example:

```text
@allow-opposing-directions
1 none
30 Right Y
1 Right Y B
20 Right Y
```

Use `--input FILE` with either capture command to replay the declared stream.
All twelve P1 button bits and opposing directions are preserved by the local
normalizer and original capture. Durations and unsupported tokens/directives
are checked. Godot's current gameplay projection merges X/Y and does not yet
implement original controller latches or P2. Godot capture rejects Start,
Select, L, and R inputs rather than silently discarding them.

`--tas FILE` accepts the existing BK2/LSMV/SMV converter as a **P1 diagnostic
projection**, retaining source hash and an explicit ineligibility marker.
It is not complete movie-parity support: both controllers, reset/power events,
region/lag semantics, source-frame anchors, and initial-state formats must pass
the implementation plan's TAS contract. Do not use the historical fixed-frame
slice or silently truncate an embedded emulator SRAM block to claim alignment.

## Iteration

Capture a declared case, inspect its first divergence, identify the original
routine and data, port one coherent behavior, rebuild, and replay the affected
cases. Keep exact starting-state/frame/coordinate definitions. The current
baseline actor-contact failure and unsupported original shell remain visible
until reference evidence resolves them.

The shared C# graphics decoder can be checked against all 52 original files
and the actual original boot uploads with:

```bash
tools/check-native-graphics.sh --oracle-boot
tools/check-native-visuals.sh --oracle-palette
tools/check-native-level-data.sh --oracle-entry
tools/check-native-terrain.sh --godot
```

This compares the complete GFX00 upload and four 2bpp font files at the original
upload return (`$00AB01`). A later frame contains dynamic player tile updates,
so it is not the same snapshot. See [ROM-DATA.md](ROM-DATA.md) for formats and
context-dependent conversion rules.

The visual verifier creates a separate metadata-only pack, checks all player,
sprite, Map16, and background RGBA bytes, and verifies the bytes Godot receives.
It preserves the normal generated pack. Its successful result covers texture
inputs, native stream/entrance loading, and the direct/secondary pipe route;
graphical framebuffer composition and full gameplay remain separate comparison
gates. The level-data verifier inventories every ROM pointer slot and uses the
original title entry as an independent entrance fixture.
The terrain verifier compares both entire Map16 buffers at `$058416`, before
gameplay edits, for `0C7`, `105`, `106`, and `1CB`. The title terrain uses original
power-on; the other captures declare main-entry overrides. `--godot` rebuilds
and boots all four with a completely empty asset pack, verifies their actual
runtime buffer hashes and checks the pipe route without even a manifest. Audio
is disabled for this gate. This does not verify collision physics or title flow.

See [IMPLEMENTATION-PLAN.md](IMPLEMENTATION-PLAN.md) for the complete target and
[REFERENCE-RESEARCH.md](REFERENCE-RESEARCH.md) for source pins and tool boundaries.
