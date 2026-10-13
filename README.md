# Open Platformer Runtime

Open Platformer Runtime is a Godot 4 .NET runtime engine recreation of **Super Mario World**. It is a native engine/runtime clone, not an emulator, and it does not ship Nintendo ROMs, extracted art, extracted audio, level data, saves, screenshots, or generated asset packs.

The repository contains source code, ROM decoding/verification tools, and documentation. The shared C# decoder loads graphics, palettes, Map16, backgrounds, object/sprite streams, entrances, player animation/OAM data, and audio banks directly from a compatible local ROM. Levels `0C7`, `105`, `106`, and `1CB` also expand their terrain directly from the ROM; their complete Map16 buffers match the original loader, and they boot without a generated pack or manifest. The selector indexes all 512 ROM pointer slots, while native play is gated to verified layouts. Unported terrain no longer silently uses an approximate preview. Optional development packs remain under ignored `generated/smw/`.

This project is still early. The current runtime has a focused playable slice, a course selector, partial imported level support, and deterministic debugging tools, but it is not a complete game and should not be presented as a faithful replacement for the original runtime yet.

## Table of Contents

- [Current status and roadmap](docs/STATUS.md)
- [Current runtime features](docs/FEATURES.md)
- [Getting started](docs/GETTING-STARTED.md)
- [Asset pipeline](docs/ASSET-PIPELINE.md)
- [Native audio and listening player](docs/NATIVE-AUDIO.md)
- [Web runtime](docs/WEB-RUNTIME.md)
- [Debugging and verification](docs/DEBUGGING.md)
- [Reference notes](docs/REFERENCE.md)
- [Physics notes](docs/PHYSICS.md)
- [Native-unit physics reference](docs/PHYSICS-REFERENCE.md)
- [First complete course gate](docs/FIRST-COURSE.md)
- [High-level plan](docs/PLAN.md)
- [Release hygiene](docs/RELEASE-HYGIENE.md)
- [Contributing](CONTRIBUTING.md)
- [License](LICENSE)

## Quick Start

Run the project with Godot 4 .NET and select your ROM in the startup menu:

```bash
tools/run.sh
```

Or provide it directly; no Python import or generated pack is required:

```bash
tools/run.sh --rom "/path/to/compatible-rom.sfc"
```

Listen to ROM-derived music and test sound effects:

```bash
tools/run-audio-player.sh
```

The same player opens from **Music / SFX Player** in Course Select. See
[native audio verification](docs/NATIVE-AUDIO.md) for the precise fidelity scope.

Run source checks:

```bash
tools/check-dotnet.sh
```

More setup, import, and run commands live in [docs/GETTING-STARTED.md](docs/GETTING-STARTED.md).

The [complete implementation plan](docs/IMPLEMENTATION-PLAN.md) defines the
vanilla-game and TAS parity target. [Reference research](docs/REFERENCE-RESEARCH.md)
records source revisions, comparison tools, and the current assumption audit.

The [local parity loop](docs/PARITY-LOOP.md) documents frame control, original-ROM
capture, TAS diagnostics, and exact state/pixel comparisons.

Try the browser loader on GitHub Pages:

```text
https://sabino.pro/open-platformer-runtime/
```

The web page validates a local ROM selection in browser memory, strips a copier header in memory when needed, indexes ROM-derived level names for the in-game selector, and can launch the experimental Godot .NET Web runtime without uploading the ROM.

The playable browser path is experimental and requires a custom Godot build from `godotengine/godot#106125` plus this project's local web-runtime fixes; see [docs/WEB-DOTNET-PROTOTYPE.md](docs/WEB-DOTNET-PROTOTYPE.md).

## Current Scope

The current runtime can boot a SNES-sized `256x224` Godot viewport, generate local assets from a user-provided ROM, show a searchable imported-level selector, launch selected imported levels, and return from a level to the selector with `Esc`, `Backspace`, gamepad Back/Guide, or the current course-clear path.

The browser loader under `web/` validates the file locally and hands ROM bytes directly to the shared C# decoder. Replacing or clearing the selection destroys the previous runtime and its audio. Neither ROM bytes nor extracted assets are uploaded.

The strongest verified gameplay slice is still the first-level route and its direct pipe target. Other imported levels may boot or render, but broad level compatibility is not guaranteed. Many systems are partial or missing, including exact collision, complete Map16 semantics, most blocks, most enemies, exact sprite loading/despawn, overworld progression, save data, complete audio, and many player states.

For the detailed state of implemented and missing systems, read [docs/STATUS.md](docs/STATUS.md).

## Asset Boundary

This repository is source-only. Do not commit:

- ROMs or ROM patches containing copyrighted data
- generated assets under `generated/`
- extracted PNG/WAV/BIN/JSON asset packs
- saves, SRAM, emulator states, TAS downloads, screenshots, or videos derived from proprietary assets
- machine-local paths or private configuration

Before publishing changes, read [docs/RELEASE-HYGIENE.md](docs/RELEASE-HYGIENE.md).

## License

Source code is licensed under MIT. See [LICENSE](LICENSE).
