#!/usr/bin/env python3
"""Compare native sequencing with original ROM firmware in an external test oracle.

This intentionally executes SPC700 firmware only in the local validation tool.
It never changes the Godot runtime's no-CPU implementation.
"""
from __future__ import annotations
import argparse
import ctypes
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import subprocess
import sys
from parity import ROOT, safe_output, write_json

spec = importlib.util.spec_from_file_location("audio_oracle", ROOT / "tools/check-native-audio.py")
audio = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audio)


class Event(ctypes.Structure):
    _fields_ = [("clock", ctypes.c_int64), ("register", ctypes.c_int32), ("value", ctypes.c_int32)]


def firmware_library():
    audio.oracle_library()  # Enforces the source revision and prepares the ignored directory.
    source = audio.SOURCE / "snes_spc"
    suffix = ".dylib" if sys.platform == "darwin" else ".so"
    output = ROOT / ".godot_local/oracles/snes-spc" / ("libspc-firmware-oracle" + suffix)
    units = [source / name for name in ["SNES_SPC.cpp", "SNES_SPC_misc.cpp", "SNES_SPC_state.cpp", "SPC_DSP.cpp"]]
    units += [ROOT / "tools/audio-oracles/spc-firmware.cpp"]
    header = ROOT / "tools/audio-oracles/spc-trace.h"
    if not output.exists() or any(p.stat().st_mtime > output.stat().st_mtime for p in units + [header]):
        subprocess.run(["clang++", "-std=c++11", "-O2", "-fPIC", "-Wno-shift-negative-value",
                        "-dynamiclib" if sys.platform == "darwin" else "-shared", "-include", str(header),
                        "-I", str(source), *map(str, units), "-o", str(output)], check=True)
    lib = ctypes.CDLL(str(output))
    lib.smw_firmware_new.argtypes = [ctypes.c_void_p, ctypes.c_int]
    lib.smw_firmware_new.restype = ctypes.c_void_p
    lib.smw_firmware_delete.argtypes = [ctypes.c_void_p]
    lib.smw_firmware_command.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_int]
    lib.smw_firmware_render.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p]
    lib.smw_firmware_render.restype = ctypes.c_char_p
    lib.smw_firmware_events.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_int]
    lib.smw_firmware_events.restype = ctypes.c_int
    lib.smw_firmware_snapshot.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
    return lib


def snapshot_from_rom(rom, bank):
    image = bytearray(65536)
    def upload(address, size):
        offset = ((address >> 16) & 127) * 32768 + (address & 32767)
        data = rom[offset:offset + size] + b"\0\0"
        p = 0
        while True:
            count = struct.unpack_from("<H", data, p)[0]; p += 2
            if count == 0: break
            target = struct.unpack_from("<H", data, p)[0]; p += 2
            if target + count > 65536 or p + count > len(data): raise ValueError("Invalid sound upload")
            image[target:target + count] = data[p:p + count]; p += count
    upload(0x0E8000, 6321); upload(0x0F8000, 28538)
    address, size = dict(Level=(0x0EAED6, 16899), Overworld=(0x0E98B1, 5667), Credits=(0x03E400, 6624))[bank]
    upload(address, size)
    snapshot = bytearray(0x10200)
    snapshot[:35] = b"SNES-SPC700 Sound File Data v0.30\x1a\x1a\x1a\x1e"
    struct.pack_into("<H", snapshot, 0x25, 0x0500)
    snapshot[0x2B] = 0xCF
    snapshot[0x100:0x10100] = image
    snapshot[0x10100 + 0x6C] = 0xE0
    return snapshot


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rom", type=Path, required=True)
    parser.add_argument("--bank", choices=["Level", "Overworld", "Credits"], default="Overworld")
    parser.add_argument("--song", type=int, default=1)
    parser.add_argument("--seconds", type=int, default=60)
    parser.add_argument("--port", type=int, choices=[0, 1, 3])
    parser.add_argument("--command", type=int)
    parser.add_argument("--require-values-match", action="store_true")
    parser.add_argument("--out", type=Path, default=ROOT / "generated/firmware-audio-check")
    args = parser.parse_args()
    rom = args.rom.read_bytes()
    if len(rom) == 524800: rom = rom[512:]
    sha1 = hashlib.sha1(rom).hexdigest().upper()
    if sha1 != "6B47BB75D16514B6A476AA0C73A683A2A4C18765": raise ValueError("Unsupported ROM fingerprint")
    if args.seconds <= 0 or args.seconds > 300: raise ValueError("Duration must be 1..300 seconds")
    limit = dict(Level=29, Overworld=9, Credits=12)[args.bank]
    if not 1 <= args.song <= limit: raise ValueError("Song ID outside bank")
    if args.port is not None and (args.command is None or not 1 <= args.command <= 128): raise ValueError("Specify a sound command")
    out = safe_output(args.out) / (f"{args.bank}-{args.song:02}" if args.port is None else f"Sfx-{args.port}-{args.command:02X}")
    out.mkdir(parents=True, exist_ok=True)
    frames = args.seconds * 32000
    if args.port is None:
        command = ["render", str(args.rom.resolve()), args.bank, str(args.song), str(frames), str(out), "--music-only"]
    else:
        args.bank = "Level"
        command = ["sfx-case", str(args.rom.resolve()), str(args.port), str(args.command), str(frames), str(out), "--isolated"]
    subprocess.run(["dotnet", "run", "--project", str(audio.TOOL), "-c", "Release", "--", *command], cwd=ROOT, check=True)
    snapshot = snapshot_from_rom(rom, args.bank)
    (out / "initial-firmware.spc").write_bytes(snapshot)
    lib = firmware_library()
    storage = (ctypes.c_uint8 * len(snapshot)).from_buffer_copy(snapshot)
    instance = lib.smw_firmware_new(storage, len(snapshot))
    if not instance: raise ValueError("Original firmware oracle boot failed")
    try:
        # Let the original reset routine finish clearing ports and enabling its timer.
        boot = (ctypes.c_int16 * 1024)()
        error = lib.smw_firmware_render(instance, len(boot), boot)
        if error: raise ValueError(error.decode())
        lib.smw_firmware_command(instance, 2 if args.port is None else args.port, args.song if args.port is None else args.command)
        pcm = (ctypes.c_int16 * (frames * 2))()
        error = lib.smw_firmware_render(instance, len(pcm), pcm)
        if error: raise ValueError(error.decode())
        count = lib.smw_firmware_events(instance, None, 0)
        events = (Event * count)(); lib.smw_firmware_events(instance, events, count)
        firmware = [dict(clock=e.clock, register=e.register, value=e.value) for e in events]
        (out / "firmware.pcm").write_bytes(bytes(pcm))
        final = (ctypes.c_uint8 * 0x10200)(); lib.smw_firmware_snapshot(instance, final)
        (out / "final-firmware.spc").write_bytes(bytes(final))
    finally: lib.smw_firmware_delete(instance)
    write_json(out / "firmware-writes.json", firmware)
    native = json.loads((out / "writes.json").read_text())
    pairs = lambda writes: [(e["register"], e["value"]) for e in writes]
    first = next((i for i, (a, b) in enumerate(zip(pairs(firmware), pairs(native))) if a != b), None)
    result = dict(scope="Original ROM firmware from controlled audio boot; no whole-game startup or PCM identity claim",
                  rom_sha1=sha1, oracle_revision=audio.ORACLE_REVISION, bank=args.bank, song=args.song,
                  seconds=args.seconds, port=args.port, command=args.command, firmware_events=len(firmware), native_events=len(native),
                  first_value_divergence=first, exact_timing=False,
                  register_values_match=first is None and len(firmware) == len(native),
                  native=native[first] if first is not None else None,
                  firmware=firmware[first] if first is not None else None,
                  firmware_pcm_sha256=hashlib.sha256(bytes(pcm)).hexdigest())
    write_json(out / "firmware-report.json", result)
    print(json.dumps(result))
    if args.require_values_match and not result["register_values_match"]: raise SystemExit(1)


if __name__ == "__main__": main()
