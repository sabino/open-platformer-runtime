#!/usr/bin/env python3
"""Exact, ROM-free PCM comparisons against an external LGPL snes_spc oracle."""
from __future__ import annotations

import argparse
import ctypes
import hashlib
import json
from pathlib import Path
import struct
import subprocess

from parity import ROOT, safe_output, write_json

ORACLE_REVISION = "ec8ee2bbe30451614c1d02a83f7af1c97d497d45"
SOURCE = ROOT / ".godot_local/references/snes-spc"
TOOL = ROOT / "tools/SmwAudioTool/SmwAudioTool.csproj"


def oracle_library() -> ctypes.CDLL:
    if not SOURCE.exists():
        subprocess.run(["git", "clone", "https://github.com/blarggs-audio-libraries/snes_spc.git", str(SOURCE)], check=True)
        subprocess.run(["git", "-C", str(SOURCE), "checkout", "--detach", ORACLE_REVISION], check=True)
    revision = subprocess.check_output(["git", "-C", str(SOURCE), "rev-parse", "HEAD"], text=True).strip()
    if revision != ORACLE_REVISION:
        raise ValueError("External snes_spc reference revision differs from the pinned baseline")
    directory = ROOT / ".godot_local/oracles/snes-spc"
    directory.mkdir(parents=True, exist_ok=True)
    suffix = ".dylib" if __import__("sys").platform == "darwin" else ".so"
    output = directory / ("libdsp-oracle" + suffix)
    sources = [SOURCE / "snes_spc/SPC_DSP.cpp", SOURCE / "snes_spc/dsp.cpp"]
    if not output.exists() or any(p.stat().st_mtime > output.stat().st_mtime for p in sources):
        flag = "-dynamiclib" if suffix == ".dylib" else "-shared"
        subprocess.run(["clang++", "-std=c++11", "-O2", "-fPIC", "-fno-rtti", "-fno-exceptions", flag,
                        *map(str, sources), "-o", str(output)], check=True)
    lib = ctypes.CDLL(str(output))
    lib.spc_dsp_new.restype = ctypes.c_void_p
    declarations = {
        "spc_dsp_init": [ctypes.c_void_p, ctypes.c_void_p],
        "spc_dsp_load": [ctypes.c_void_p, ctypes.c_void_p],
        "spc_dsp_set_output": [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_int],
        "spc_dsp_run": [ctypes.c_void_p, ctypes.c_int],
        "spc_dsp_write": [ctypes.c_void_p, ctypes.c_int, ctypes.c_int],
        "spc_dsp_read": [ctypes.c_void_p, ctypes.c_int],
        "spc_dsp_sample_count": [ctypes.c_void_p],
        "spc_dsp_delete": [ctypes.c_void_p],
    }
    for name, types in declarations.items():
        getattr(lib, name).argtypes = types
    lib.spc_dsp_sample_count.restype = lib.spc_dsp_read.restype = ctypes.c_int
    return lib


def cases():
    def baseline(voices=1):
        memory, registers = bytearray(65536), bytearray(128)
        registers[0x0C] = registers[0x1C] = 127
        registers[0x5D] = 2
        registers[0x6D] = 0x60
        registers[0x6C] = 0x20
        for voice in range(8):
            address = 0x2000 + voice * 0x100
            struct.pack_into("<HH", memory, 0x200 + voice * 4, address, address + 9)
            memory[address:address + 18] = bytes([0x80, 0x17, 0x83, 0xA6, 0xFE, 0x2D, 0x49, 0xC5, 0x70,
                                               0x83, 0x76, 0x54, 0x32, 0x10, 0xFE, 0xDC, 0xBA, 0x98])
            if voice < voices:
                base = voice * 16
                registers[base] = 0x50
                registers[base + 1] = 0x40
                registers[base + 3] = 0x10
                registers[base + 4] = voice
                registers[base + 7] = 0x7F
        return memory, registers, [{"clock": 0, "register": 0x4C, "value": (1 << voices) - 1}]

    for shift in range(16):
        for filter_id in range(4):
            memory, registers, events = baseline()
            memory[0x2000] = shift * 16 + filter_id * 4
            memory[0x2009] = shift * 16 + filter_id * 4 + 3
            yield f"brr-{shift}-{filter_id}", memory, registers, events, 2048
    for pitch in [1, 0x37, 0x234, 0x555, 0x800, 0xFFF, 0x1000, 0x1101, 0x2000, 0x3FFF]:
        memory, registers, events = baseline()
        registers[2], registers[3] = pitch & 255, pitch >> 8
        yield f"pitch-{pitch:04x}", memory, registers, events, 4096
    for attack in range(16):
        memory, registers, events = baseline()
        registers[5], registers[6] = 0xF0 | attack, 0x1F
        events.append({"clock": 3071, "register": 0x5C, "value": 1})
        yield f"adsr-{attack}", memory, registers, events, 4096
    for mode in range(4, 8):
        for rate in [0, 1, 15, 31]:
            memory, registers, events = baseline()
            registers[7] = mode * 32 + rate
            if mode < 6:
                events.append({"clock": 3200, "register": 7, "value": 127})
                events.append({"clock": 6435, "register": 7, "value": mode * 32 + rate})
            yield f"gain-{mode}-{rate}", memory, registers, events, 4096
    for rate in range(32):
        memory, registers, events = baseline()
        registers[0x3D] = 1; registers[0x6C] |= rate
        yield f"noise-{rate}", memory, registers, events, 2048
    for voices in [2, 8]:
        for modulation in [0, 0xFE]:
            memory, registers, events = baseline(voices)
            registers[0x2D] = modulation
            for voice in range(voices):
                registers[voice * 16 + 2] = voice * 17
                registers[voice * 16] = (60 + voice * 31) & 255
                registers[voice * 16 + 1] = (220 - voice * 17) & 255
            yield f"mix-{voices}-{modulation}", memory, registers, events, 4096
    for delay in [0, 1, 2, 15]:
        for feedback in [0, 96, 128, 255]:
            memory, registers, events = baseline(8)
            registers[0x6C] = 0
            registers[0x4D] = 255; registers[0x7D] = delay; registers[0x0D] = feedback
            registers[0x2C] = 77; registers[0x3C] = 201
            for tap, value in enumerate([127, -31, 22, 77, -99, 12, -8, 6]):
                registers[tap * 16 + 15] = value & 255
            for i in range(0x6000, 0xE000): memory[i] = (i * 13 + (i >> 4)) & 255
            yield f"echo-{delay}-{feedback}", memory, registers, events, 4096
    for phase in range(32):
        memory, registers, events = baseline(8)
        events += [{"clock": 1024 + phase, "register": 0x5C, "value": 0xA5},
                   {"clock": 2016 + phase, "register": 0x4C, "value": 0xFF},
                   {"clock": 3040 + phase, "register": 0x6C, "value": 0x3F},
                   {"clock": 4064 + phase, "register": 0x3D, "value": 0xAA},
                   {"clock": 5088 + phase, "register": 0x7C, "value": 0xFF}]
        yield f"timing-{phase}", memory, registers, events, 2048


def reference(lib, memory, registers, events, frames):
    ram = (ctypes.c_uint8 * 65536).from_buffer_copy(memory)
    regs = (ctypes.c_uint8 * 128).from_buffer_copy(registers)
    # snes_spc redirects its output pointer to a scratch buffer when exactly
    # full. Leave a spare frame so sample_count retains its documented meaning.
    pcm = (ctypes.c_int16 * (frames * 2 + 2))()
    dsp = lib.spc_dsp_new()
    if not dsp: raise MemoryError("Oracle DSP allocation failed")
    try:
        lib.spc_dsp_init(dsp, ram)
        lib.spc_dsp_load(dsp, regs)
        lib.spc_dsp_set_output(dsp, pcm, len(pcm))
        clock = 0
        for event in events:
            if event["clock"] > clock: lib.spc_dsp_run(dsp, event["clock"] - clock)
            clock = event["clock"]
            lib.spc_dsp_write(dsp, event["register"], event["value"])
        if frames * 32 > clock: lib.spc_dsp_run(dsp, frames * 32 - clock)
        if lib.spc_dsp_sample_count(dsp) != frames * 2: raise ValueError("Oracle output cadence mismatch")
        return bytes(pcm)[:frames * 4], bytes(ram), bytes(lib.spc_dsp_read(dsp, i) for i in range(128))
    finally:
        lib.spc_dsp_delete(dsp)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, default=ROOT / "generated/audio-dsp-check")
    parser.add_argument("--filter", default="")
    args = parser.parse_args()
    out = safe_output(args.out)
    out.mkdir(parents=True, exist_ok=True)
    lib = oracle_library()
    fixtures, references = [], {}
    for name, memory, registers, events, frames in cases():
        if args.filter and args.filter not in name: continue
        (out / (name + ".memory")).write_bytes(memory)
        (out / (name + ".registers")).write_bytes(registers)
        fixtures.append(dict(name=name, memory=str(out / (name + ".memory")), registers=str(out / (name + ".registers")),
                             frames=frames, events=events))
        references[name] = reference(lib, memory, registers, events, frames)
        (out / (name + ".oracle.pcm")).write_bytes(references[name][0])
    if not fixtures: raise ValueError("No fixtures selected")
    write_json(out / "cases.json", fixtures)
    subprocess.run(["dotnet", "run", "--project", str(TOOL), "-c", "Release", "--",
                    "dsp-batch", str(out / "cases.json"), str(out / "managed")], cwd=ROOT, check=True)
    failures, results = [], []
    for fixture in fixtures:
        name = fixture["name"]
        expected, memory, registers = references[name]
        actual = (out / "managed" / (name + ".pcm")).read_bytes()
        mismatches = [i for i in range(len(expected) // 2) if expected[i * 2:i * 2 + 2] != actual[i * 2:i * 2 + 2]]
        memory_equal = memory == (out / "managed" / (name + ".memory")).read_bytes()
        regs_equal = registers == (out / "managed" / (name + ".registers")).read_bytes()
        result = dict(name=name, frames=fixture["frames"], mismatched_samples=len(mismatches),
                      pcm_sha256=hashlib.sha256(actual).hexdigest(), echo_memory_equal=memory_equal, registers_equal=regs_equal)
        if mismatches:
            i = mismatches[0]
            result.update(first_frame=i // 2, channel=i % 2, expected=struct.unpack_from("<h", expected, i * 2)[0],
                          actual=struct.unpack_from("<h", actual, i * 2)[0],
                          final_state=(out / "managed" / (name + ".state")).read_text())
        if mismatches or not memory_equal or not regs_equal: failures.append(result)
        results.append(result)
    write_json(out / "report.json", dict(oracle_revision=ORACLE_REVISION, raw_format="s16le stereo 32000 Hz",
                                          scope="DSP-only synthetic register inputs; not whole-song parity", cases=results, failures=failures))
    print(f"DSP comparison: {len(results)} cases, {sum(r['frames'] for r in results)} stereo frames, {len(failures)} failures")
    for failure in failures[:10]: print(json.dumps(failure))
    raise SystemExit(1 if failures else 0)


if __name__ == "__main__": main()
