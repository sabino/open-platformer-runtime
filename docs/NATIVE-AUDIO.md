# Native SMW audio

## Listening

Configure the compatible local ROM as described in [GETTING-STARTED.md](GETTING-STARTED.md), then run:

```bash
tools/run-audio-player.sh
```

Course Select also has a **Music / SFX Player** button. The player starts
Here We Go!, lists 29 level, nine overworld and 12 credits song IDs (some
are aliases), and supports double-click playback, restart, pause/resume,
stop, a searchable list of 106 sound commands/cues, and playback volume. SMW's pause chime completes
before music mutes. Escape returns to Course Select.

The Sound effects tab includes all 94 original table IDs, jump/Yoshi controls,
the hurry-up command and music/transition cues. **Circle close / zoom in** is
level-bank music command `11`, sent by the original victory-pose routine.
**Course clear → circle close** previews both cues, switching when the first
sequence ends; it does not reproduce the game's visual transition timing.
Removed effect IDs are preserved and labeled silent.

The Yoshi toggle sends original drum-on/off commands. Mounting also plays
Yoshi's sound. Dismounting chooses jump or hurt with a seeded UI-only generator;
drum-off and jump use different audio frames to avoid overwriting their shared
port. Original percussion exists in specific level songs, including Piano,
Here We Go!, Water and Cave.

Output offers **Stereo**, **Mono (L + R)**, **Left → both**, **Right → both**,
and independent L/R gains. Mono averages both sides; original opposite-phase
surround can cancel in that sum, while mirroring a side avoids cancellation.
The Mixer tab has solo/mute, gain and separate L/R controls for eight voices
and 20 instrument samples. Sample controls follow a BRR sample across voices;
voice controls follow a track even when its instrument changes. The optional
monitor mix is dry by default. Including original echo also includes the echo
of muted voices, so leave it off for clean solos. **Original mix** resets the
monitor. These listening controls leave native PCM/registers/state unchanged;
their processed output has no bit-identity claim.

Application volume and Godot device resampling occur after the raw PCM
boundary. Stereo meters show source levels before application volume.

## Architecture and source data

`src/SmwAudioCore` is an independent managed C# library. `SmwAudioData`
reads original BRR samples/directory entries, instrument tables, raw music
and SFX commands, and the level music mapping from the validated ROM.
The importer can write schema-1 `audio/native_audio_data.bin`, which
preserves the original upload payloads and ROM SHA-1. It is a local,
ignored cache, not a redistributed asset. Legacy raw bank caches are
explicitly labeled unverified and lack the level header music mapping.

`SmwMusicDriver` translates the MIT `snesrev/smw` driver into typed native
state and routines. Its data image is sequence/sample storage; executable
SPC firmware is excluded from the runtime image and never interpreted.
Eight channels handle N-SPC scheduling, instruments, pitch, volume/panning,
control flow, fades, vibrato, echo setup and SFX interruption/restoration.
Unsupported commands stop playback with their offset rather than being
replaced with approximate notes. Tremolo command E5 is currently unsupported.

`SnesDsp` implements the 32-phase integer pipeline, exact BRR predictors,
Gaussian interpolation, envelopes/counters, modulation, noise, signed
mixing, echo storage/feedback and FIR. The 64 KiB DSP data buffer is needed
for sample addresses and echo; there is no console CPU, PPU, or SPC700
instruction interpreter. ISC ares routines and MIT driver/table references
are acknowledged in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).

`NativeSmwAudio` schedules timestamped events and generates signed 16-bit
stereo PCM at 32,000 Hz. Event order, state and output do not depend on
render block size or device consumption. Late events are rejected.
Checkpoints include all DSP/driver state and pending events plus the ROM
fingerprint. Godot's `SmwAudio` advances the core from logical gameplay
frames and only drains already generated PCM in `_Process`. Offline
generation can run faster than real time. RCON audio APIs remain available.

## Verification and measured results

Run the managed checks and external oracle comparisons:

```bash
tools/check-dotnet.sh
tools/check-native-audio.sh
tools/check-smw-audio.sh
tools/check-smw-firmware-audio.sh --port 3 --command 1 --seconds 2 --require-values-match
tools/run.sh --no-import --headless --smw-audio-player-smoke --quit-after 420
```

The oracle tools build pinned references only under ignored `.godot_local/`.
No LGPL code or native test library is shipped or loaded during gameplay.
ROM-derived fixtures, traces, hashes, PCM and reports remain under ignored
`generated/`. The DSP fixtures are synthetic and require no ROM.

Verified on macOS Apple Silicon with .NET 10.0.401 and Godot .NET 4.7.2:

- 190 DSP cases: 516,096 stereo frames, zero mismatched samples, equal final
  sample/echo memory and registers. Coverage includes all BRR shifts/filters,
  Gaussian/pitch, ADSR/GAIN, noise, modulation, signed mixing, echo/FIR and
  writes at all 32 phase offsets.
- 50 ROM song cases, five seconds each: 8,000,000 stereo frames, identical
  register-event streams against pinned native C routines and identical PCM
  against `snes_spc` given those events. Jump and coin commands are injected.
- Block-size independence, complete DSP and native sequencer checkpoints,
  queued-event restoration, ROM-cache roundtrip and zero DSP hot-path
  allocations pass executable managed tests.
- Eight active voices with echo: 11.59 ms to generate one audio second,
  about 86.3 times real time, zero allocated bytes in the measured hot path
  (20 seconds after five warmup seconds). Run the `SmwAudioTool benchmark`
  command to measure the current checkout on another host.
- The player control smoke exercises three banks, jump/coin/spin jump,
  pause/resume, stop/restart, Yoshi drums, circle-close lookup, monitor controls,
  mono routing, nonzero PCM and complete viewport containment. Independent
  managed tests prove that enabling/muting the monitor preserves raw PCM and
  native checkpoints, and that instrument R=0 silences the dry monitor's R.
  The graphical player was launched and the user confirmed audible playback.
- Conditional `GodotWebDotNetPrototype=true` net9 compilation passed. Actual
  WebAssembly export/playback was not tested; the custom editor/templates
  and .NET 9 WASM workloads are unavailable on this Mac.
- Shared .NET physics, ROM, generated asset, graphics and level data checks
  passed. The broad headless gameplay suite still has an existing actor-hit
  expectation discrepancy (`y=288.25` versus `288.00`), outside audio scope.

## Fidelity limits and remaining work

**DSP identity and song identity are separate claims.** The exact comparisons
above concern supplied DSP register events. The native driver currently groups
register writes at 500 Hz control ticks; original instruction-by-instruction
write offsets and startup phases have not been reproduced. Native C-driver
agreement does not independently validate that C translation against the
original firmware. Whole-song PCM identity is therefore not established.

`check-smw-firmware-audio.sh` now runs the original ROM sound driver in an
external `snes_spc` test oracle from a controlled audio boot. CPU execution
exists only in that local validation process, with no game dependency.
The 60-second Title Screen comparison confirms different pitch-sweep events
and timing; the first register-value divergence is event 199. This is a real
remaining sequencing/timing gap. This controlled boot is not an original
whole-game startup snapshot or a full-song PCM identity test.

The comparison identified a native C-reference bug: noise SFX selected fixed
rate `1F`, while original routine `CODE_094E` masks the ROM command's low five
bits. The managed driver now follows the original. Isolated Podoboo command
`$1DFC:27` matches all 32 register values, and coin `$1DFC:01` matches all 35.
Their precise write times still differ. These checks establish register-value
agreement, not full PCM identity.

Next gates are original-firmware event/timing validation, complete song loops,
all SFX and priority combinations, bank transitions and pause/fade restoration,
measured device/SFX latency, and actual desktop/Web playback checks. Timing
diagnostics currently report command-to-KON delay, queues, dropped frames,
generation cost and cumulative device underruns; they do not measure the
physical audio device's end-to-end latency. Windows/Linux execution has not
been tested on this host. ROM-free managed builds and exact DSP fixtures also
run in pull-request CI. The full-game parity goal remains incomplete.

## Sharing and startup

The intended shareable app contains managed game code and the engine runtime,
with each user's own ROM supplied locally. Desktop currently accepts
`--smw-rom=/path/to/compatible-rom.sfc` or `SMW_ROM_PATH` and validates the USA
ROM fingerprint before loading. The desktop startup menu also provides
**Select ROM**. Both hosts strip a recognized copier header and validate the
canonical image. The browser hands the selected bytes into the same native
ROM and audio nodes. Its experimental native audio playback still needs
validation. A Linux export preset is present; packaged desktop and Web runtime
execution are separate release checks. The checked-in Web
export excludes `generated/**`, ROMs, local configuration and caches. Native
audio reads the ROM directly without requiring preview WAVs or pre-rendered
music. Private development caches remain ignored and untracked.
