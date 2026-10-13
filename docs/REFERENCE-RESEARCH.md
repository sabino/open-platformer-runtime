# Reference research and pinned baseline

Original-ROM observations determine the target. Reference programs explain
behavior and provide external tooling; none is a shipped game dependency.

| Reference | Revision | Role and limits |
| --- | --- | --- |
| [snesrev/smw](https://github.com/snesrev/smw) | `eae20c65c58930c8b62c76188d259579ad4130f1` | Named C routines, extraction, globals, update order, and external native comparison. Separate vanilla logic from Lunar Magic and its PPU/DSP/emulator components. |
| [galaxyhaxz/smw-src](https://github.com/galaxyhaxz/smw-src) | `30e66e1ae3e8e0e518885dbcd5f85f2909a4969e` | Historical disassembly, RAM maps, control flow, arithmetic, and labels. Archived, with historical Windows build tooling; use for cross-checking. |
| [mstan/SuperMarioWorldRecomp](https://github.com/mstan/SuperMarioWorldRecomp) | `8dedb2869414f20d1d86d34081be26594560cc15` | Additional translated control flow, Lua tooling, rendering investigations, and recorded issues. Its hardware backend remains emulated; documented hand verification does not cover every world. |
| [SourMesen/Mesen2](https://github.com/SourMesen/Mesen2/tree/2.1.1) | `137ae7ce3bf3f539d007e2c4ef3cb3b6c97672a1`, release `2.1.1` | Original-ROM headless Lua input, RAM/framebuffer inspection, and later audio/checkpoint comparisons. Record initial RAM/SRAM, region, tool build, and capture phase. |
| [IsoFrieze/SMWDisX](https://github.com/IsoFrieze/SMWDisX) | Not yet locally pinned | Additional labeled disassembly/RAM map. Pin before using it in reproducible fixtures. |

Read-only source checkouts live under ignored `.godot_local/references/`.
Reference builds/tools also stay in `.godot_local/`; ROM data, captures, movies,
and snapshots belong under ignored `generated/`. Preserve applicable source
attribution; read ROM-derived tables from the ROM rather than copying them into
committed source.

## Confirmed findings

- Pinned snesrev uses `.c` bank files and `src/variables.h`. Historical `.cpp`
  notes and custom `tools/run-wayland.sh` adapters describe another checkout.
- Its game-mode dispatch includes named boot, title/file/erase/player selection,
  overworld, level/message flow, cutscene, ending, and roll-call routines. Use
  that inventory when replacing the current Godot development shell.
- Mesen has a native Apple Silicon release and `--testRunner`, which runs Lua
  with no normal GUI. Its APIs include `inputPolled`, `endFrame`, `setInput`,
  `read`, and `takeScreenshot`. Capture phase is part of every fixture.
- Mesen defaults to randomized RAM. The local canonical diagnostic profile
  uses zero-filled RAM and records that choice.
- The macOS release core links Homebrew SDL2. The Malt installation requires
  local linkage relocation and re-signing; record the adapted tool hashes.
  Its extraction logic compares dependency timestamps and sizes, so those
  packaging attributes also require care.
- The actor-contact fixture has identical state on Godot 4.6.3 and 4.7.2:
  after three diagnostic frames, the expected Banzai Bill hurt event is absent.
  Capture original clipping/spawn/slot state before changing its expectation.

## Browser engine

[sabino/godot](https://github.com/sabino/godot) carries experimental Web .NET
support and project marshalling fixes. Release
`web-dotnet-experimental-open-platform-runtime` publishes a Linux editor and
matching Web template; it currently has no macOS editor asset.

The web export script currently checks for a 9.x SDK specifically, despite its
`9.0+` wording, and requires `wasm-tools` plus `wasm-tools-net9`. Verify the
pinned combination before updating it. Desktop Godot is a separate workflow.

## Additional research

Prioritize a source or tool for a concrete unresolved question: ROM extraction,
music-driver commands, SRAM, object dispatch, clipping, or frame timing. Record
disagreements and resolve them against the supplied vanilla ROM. Controller
projects tuned to resemble Mario are architectural examples, not numeric truth.
