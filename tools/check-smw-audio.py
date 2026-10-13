#!/usr/bin/env python3
"""Compare managed SMW driver writes with pinned native C, then PCM with Blargg."""
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


def driver_library():
    source = ROOT / ".godot_local/references/snesrev-smw"
    pin = "eae20c65c58930c8b62c76188d259579ad4130f1"
    if subprocess.check_output(["git", "-C", str(source), "rev-parse", "HEAD"], text=True).strip() != pin:
        raise ValueError("SMW driver source pin differs")
    output = ROOT / ".godot_local/oracles/snes-spc" / ("libsmw-driver-oracle.dylib" if sys.platform == "darwin" else "libsmw-driver-oracle.so")
    subprocess.run(["clang", "-std=gnu11", "-O2", "-fPIC", "-dynamiclib" if sys.platform == "darwin" else "-shared",
                    "-I", str(source / "src"), str(ROOT / "tools/audio-oracles/smw-driver.c"), "-o", str(output)], check=True)
    lib = ctypes.CDLL(str(output))
    lib.smw_oracle_new.argtypes = [ctypes.c_void_p]; lib.smw_oracle_new.restype = ctypes.c_void_p
    lib.smw_oracle_delete.argtypes = [ctypes.c_void_p]
    lib.smw_oracle_command.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_uint8]
    for name in ["smw_oracle_tick", "smw_oracle_writes"]:
        getattr(lib, name).argtypes = [ctypes.c_void_p, ctypes.c_void_p]
        getattr(lib, name).restype = ctypes.c_int
    return lib


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rom", type=Path, required=True)
    parser.add_argument("--out", type=Path, default=ROOT / "generated/native-audio-driver-check")
    parser.add_argument("--seconds", type=int, default=5)
    parser.add_argument("--reuse", action="store_true")
    args = parser.parse_args()
    out = safe_output(args.out); out.mkdir(parents=True, exist_ok=True)
    dsp = audio.oracle_library(); driver = driver_library()
    if not args.reuse:
        subprocess.run(["dotnet", "run", "--project", str(audio.TOOL), "-c", "Release", "--", "driver-cases",
                        str(args.rom.resolve()), str(args.seconds * 32000), str(out)], cwd=ROOT, check=True)
    cases = json.loads((out / "cases.json").read_text())
    results = []
    for case in cases:
        directory = out / case["name"]
        image = (directory / "initial.memory").read_bytes()
        managed = json.loads((directory / "writes.json").read_text())
        native = []
        ram = (ctypes.c_uint8 * 65536).from_buffer_copy(image)
        pairs = (ctypes.c_uint8 * 512)()
        instance = driver.smw_oracle_new(ram)
        if not instance: raise MemoryError("Native driver oracle allocation failed")
        def take(clock, count):
            for i in range(count): native.append(dict(clock=clock, register=pairs[i*2], value=pairs[i*2+1]))
        try:
            take(0, driver.smw_oracle_writes(instance, pairs))
            commands = case["commands"]; index = 0
            for tick in range(1, case["ticks"] + 1):
                clock = tick * 2048
                while index < len(commands) and commands[index]["Clock"] <= clock:
                    command = commands[index]; index += 1
                    driver.smw_oracle_command(instance, command["Port"], command["Value"])
                take(clock, driver.smw_oracle_tick(instance, pairs))
        finally: driver.smw_oracle_delete(instance)
        first = next((i for i,(a,b) in enumerate(zip(native,managed)) if a != b), None)
        if first is not None or len(native) != len(managed):
            result = dict(name=case["name"], register_events_match=False, first_event=first,
                          native_count=len(native), managed_count=len(managed),
                          native=native[first] if first is not None else None,
                          managed=managed[first] if first is not None else None)
        else:
            expected, _, _ = audio.reference(dsp, image, (directory / "initial.registers").read_bytes(), native, case["frames"])
            actual = (directory / "native.pcm").read_bytes()
            mismatches = [i for i in range(len(expected)//2) if expected[i*2:i*2+2] != actual[i*2:i*2+2]]
            result = dict(name=case["name"], register_events_match=True, register_events=len(native),
                          frames=case["frames"], mismatched_samples=len(mismatches), pcm_sha256=hashlib.sha256(actual).hexdigest())
            if mismatches:
                i=mismatches[0]; result.update(first_frame=i//2, channel=i%2,
                    expected=struct.unpack_from("<h",expected,i*2)[0], actual=struct.unpack_from("<h",actual,i*2)[0])
            (directory / "oracle.pcm").write_bytes(expected)
        results.append(result)
    failures = [item for item in results if not item["register_events_match"] or item.get("mismatched_samples",0)]
    write_json(out / "report.json", dict(scope="Translated driver at 500 Hz control ticks; NOT SPC700 firmware write timing or whole-ROM audio parity",
                                          cases=results, failures=failures))
    print(f"SMW driver comparison: {len(cases)} song cases, {len(failures)} failures")
    for failure in failures[:10]: print(json.dumps(failure))
    raise SystemExit(1 if failures else 0)


if __name__ == "__main__": main()
