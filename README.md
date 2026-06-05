# Open Platformer Runtime

Open Platformer Runtime is a Godot 4 .NET runtime engine recreation of **Super Mario World**. It is a native engine/runtime clone, not an emulator, and it does not ship Nintendo ROMs, extracted art, extracted audio, level data, saves, screenshots, or generated asset packs.

The repository contains source code, import/verification tools, and documentation. A user-provided compatible local ROM dump is used only as input to an offline importer that writes local Godot-readable data under `generated/smw/`, which is ignored by git.

This project is still early. The current runtime has a focused playable slice, a course selector, partial imported level support, and deterministic debugging tools, but it is not a complete game and should not be presented as a faithful replacement for the original runtime yet.

## Table of Contents

- [Current status and roadmap](docs/STATUS.md)
- [Current runtime features](docs/FEATURES.md)
- [Getting started](docs/GETTING-STARTED.md)
- [Asset pipeline](docs/ASSET-PIPELINE.md)
- [Web runtime](docs/WEB-RUNTIME.md)
- [Debugging and verification](docs/DEBUGGING.md)
- [Reference notes](docs/REFERENCE.md)
- [Physics notes](docs/PHYSICS.md)
- [Native-unit physics reference](docs/PHYSICS-REFERENCE.md)
- [High-level plan](docs/PLAN.md)
- [Release hygiene](docs/RELEASE-HYGIENE.md)
- [Contributing](CONTRIBUTING.md)
- [License](LICENSE)

## Quick Start

Import assets from a compatible local ROM dump:

```bash
tools/import-smw.sh "/path/to/compatible-rom.sfc"
```

Run the project with Godot 4 .NET:

```bash
tools/run-wayland.sh
```

Run source checks:

```bash
tools/check-dotnet.sh
```

More setup, import, and run commands live in [docs/GETTING-STARTED.md](docs/GETTING-STARTED.md).

Try the browser loader on GitHub Pages:

```text
https://sabino.pro/open-platformer-runtime/
```

The web page currently validates a local ROM selection in browser memory and exports a small browser manifest. Gameplay is still local Godot-only until the web runtime bridge is implemented.

There is also an experimental custom-Godot path for the upstream Web .NET prototype work. It requires a Godot build from `godotengine/godot#106125`; see [docs/WEB-DOTNET-PROTOTYPE.md](docs/WEB-DOTNET-PROTOTYPE.md).

## Current Scope

The current runtime can boot a SNES-sized `256x224` Godot viewport, generate local assets from a user-provided ROM, show a searchable imported-level selector, launch selected imported levels, and return from a level to the selector with `Esc`, `Backspace`, gamepad Back/Guide, or the current course-clear path.

The repository also includes a GitHub Pages browser loader under `web/`. It uses the browser's native file picker and local Web Crypto validation, but it is not yet wired to the playable Godot runtime.

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
