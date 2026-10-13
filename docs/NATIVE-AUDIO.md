# Native SMW audio

## Listening

Configure the compatible local ROM as described in [GETTING-STARTED.md](GETTING-STARTED.md), then run:

```bash
tools/run-audio-player.sh
```

Course Select also has a **Music / SFX Player** button. The player starts
Here We Go!, lists 29 level, nine overworld and 12 credits song IDs (some
are aliases), and supports double-click playback, restart, pause/resume,
stop, gameplay effects and playback volume. SMW's pause chime completes
before music mutes. Escape returns to Course Select.

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
- Eight active voices with echo: 11.54 ms to generate one audio second,
  about 86.6 times real time, zero allocated bytes in the measured hot path
  (20 seconds after five warmup seconds). Run the `SmwAudioTool benchmark`
  command to measure the current checkout on another host.
- The player control smoke exercises three banks, jump/coin/spin jump,
  pause/resume, stop/restart, nonzero PCM and complete viewport containment.
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
Listening reports about layers, echo, coin tone and instrument levels need
an original-firmware trace comparison, not arbitrary gain changes.

Next gates are original-firmware event/timing validation, complete song loops,
all SFX and priority combinations, bank transitions and pause/fade restoration,
measured device/SFX latency, and actual desktop/Web playback checks. Timing
diagnostics currently report command-to-KON delay, queues, dropped frames,
generation cost and cumulative device underruns; they do not measure the
physical audio device's end-to-end latency. Windows/Linux execution has not
been tested on this host. The full-game parity goal remains incomplete.
