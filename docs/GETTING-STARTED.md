# Getting Started

This project requires Godot 4 .NET. The repository does not contain game assets. To run imported levels, provide your own compatible local ROM dump and generate local assets into `generated/smw/`.

## Import Assets

Import the default focused asset pack:

```bash
tools/import-smw.sh "/path/to/compatible-rom.sfc"
```

Import a specific level or a small requested set:

```bash
SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/import-smw.sh --level 106 --clean
SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/import-smw.sh --levels 105,106,1CB --clean
```

Import the full `000..1FF` level-id range:

```bash
SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/import-smw.sh --all-levels --clean
```

The importer accepts repeated `--level` values, follows direct screen-exit targets by default, decodes available overworld level titles from the ROM's level-name tables, and writes a fresh `generated/smw/manifest.json`.

Useful import options:

- `--no-exit-targets` imports only the requested IDs.
- `--exit-depth N` follows more than direct exits.
- `--all-levels` attempts the full level-id range.

## Run the Game

Open the graphical Godot run:

```bash
tools/run-wayland.sh
```

Import and run one level in a single command:

```bash
SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/run-level.sh 106
tools/run-level.sh 1CB --no-import --headless -- --quit-after 2
```

Automated runs can start one generated level directly:

```bash
godot4-mono --path . --smw-test-level=106
```

## Controls

- Movement: arrows or `A`/`D`/`S`
- Jump: `Z` or Space
- Spin jump: `X`
- Run/fire: Shift or `C`
- Start/pause: Enter
- Return to selector from a level: `Esc` or `Backspace`

Standard gamepad mapping:

- D-pad/left stick: movement
- A: jump
- B: spin jump
- X or right shoulder: run/fire
- Start: pause/menu start
- Back/Guide: return to selector

The start menu exposes compact `Gizmos`, `Actors`, and `Sprites` toggles. The `Audio` toggle is visible but disabled while audio stays opt-in by command line.

## Audio

Audio is silent by default. Enable the diagnostic internal audio probe with:

```bash
godot4-mono --path . --smw-audio
SMW_AUDIO=1 godot4-mono --path .
```

Keep audio disabled with:

```bash
godot4-mono --path . --smw-no-audio
SMW_AUDIO=0 godot4-mono --path .
```

## Build and Validate

Run source checks:

```bash
tools/check-dotnet.sh
```

Run only the generated-asset contract checker:

```bash
dotnet run --project tests/SmwAssetCheck/SmwAssetCheck.csproj -- generated/smw
```

Run headless Godot smoke tests:

```bash
tools/check-headless.sh
```

Import and headlessly boot every generated manifest level:

```bash
SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/check-level-smoke.sh --all-levels
tools/check-level-smoke.sh --no-import
```
