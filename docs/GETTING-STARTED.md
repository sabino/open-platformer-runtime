# Getting Started

This project requires Godot 4 .NET. Run it and choose **Select ROM** in the startup menu, or pass `tools/run.sh --rom "/path/to/compatible-rom.sfc"`. The supplied USA ROM is validated and decoded in memory. A 512-byte copier header is stripped at this selection boundary; the canonical hash must still match. No generated asset pack or Python import is required for native courses or audio.

The project targets Godot 4.6.3 and .NET 8; native macOS startup is also checked
with Godot .NET 4.7.2. Project tools find `godot-mono` or `Godot_mono.app` and
allow a newer installed .NET runtime when .NET 8 is missing. An explicitly set
`DOTNET_ROLL_FORWARD` remains authoritative.

Keep a local ROM under an ignored directory such as `generated/roms/`. Set
`SMW_ROM_PATH` in the environment, or use ignored `local-rom.cfg`:

```bash
export SMW_ROM_PATH="${SMW_ROM_PATH:-$SMW_PROJECT_ROOT/generated/roms/smw-usa.sfc}"
```

This is local shell configuration loaded by project tools. Use an absolute
path or a path rooted at `SMW_PROJECT_ROOT`.

With `SMW_ROM_PATH` configured, Godot validates the ROM and constructs player,
sprite, foreground, and background textures through the shared native C#
decoder. Player pose/animation/OAM tables also come directly from the ROM.
Object/sprite streams and main/secondary entrances also load natively. The
native selector lists all 512 pointer slots but enables only verified terrain layouts. Other slots remain indexed for investigation, without approximate geometry fallback.
Native audio can read the ROM directly or use the importer's versioned local cache.
For direct engine invocation, `--smw-rom=/absolute/path/to/rom.sfc` selects the
same native decoder. An explicitly supplied unsupported ROM fails startup.

`SMW_ASSET_BASE_PATH` can select a separate metadata pack for local testing.
The default remains `generated/smw/`; no generated data belongs in git.

## Optional development asset packs

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
tools/run.sh
```

Run a verified layout directly from your ROM:

```bash
SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/run-level.sh 106
tools/run-level.sh 1CB --no-import --headless -- --quit-after 2
```

Automated runs can start a native level directly with `SMW_ROM_PATH` or `--smw-rom=...`:

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

The start menu exposes compact `Gizmos`, `Actors`, `Sprites`, and `Audio` toggles,
plus a **Music / SFX Player** button.

## Audio

The course selector is silent by default. Open the listening player with:

```bash
tools/run-audio-player.sh
```

It starts **Here We Go!**, offers all 50 music IDs across three banks, and
provides a searchable sound-command list, Yoshi percussion, pause/resume,
stop/restart, stereo/mono/mirrored routing, L/R gains, instrument/voice solo
and mute, volume and stereo meters.
Escape returns to Course Select. Enable native gameplay audio with:

```bash
godot4-mono --path . --smw-audio
SMW_AUDIO=1 godot4-mono --path .
```

The audio core is managed C#, with no SPC700 execution or native audio library
in gameplay. DSP comparisons and whole-song timing have different validation
scopes; see [NATIVE-AUDIO.md](NATIVE-AUDIO.md).

Keep audio disabled with:

```bash
godot4-mono --path . --smw-no-audio
SMW_AUDIO=0 godot4-mono --path .
```

## Build and Validate

Portable launch and interactive debugging are available with:

```bash
tools/run.sh --headless --quit-after 5
tools/run-debug.sh
tools/smw-rcon.sh pause
tools/smw-rcon.sh step 1
tools/smw-rcon.sh state
```

The legacy Wayland launcher delegates to the native launcher on macOS. Native
reference/Linux capture scripts have additional platform requirements.

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
