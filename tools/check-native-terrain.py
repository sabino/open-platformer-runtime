#!/usr/bin/env python3
"""Compare native terrain against the original loader's complete Map16 buffers."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

from parity import ROM_SHA1, ROOT, safe_output, write_json


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rom", type=Path, default=Path(os.environ.get("SMW_ROM_PATH", "")))
    parser.add_argument("--out", type=Path, default=ROOT / "generated/native-terrain")
    parser.add_argument("--levels", default="0C7,105,106,1CB")
    parser.add_argument("--godot", action="store_true", help="Build and verify native Godot boots/routes with an empty asset pack")
    args = parser.parse_args()
    output = safe_output(args.out)
    report_path = output / "verification.json"
    report_path.unlink(missing_ok=True)
    rom = args.rom.read_bytes()
    if len(rom) != 0x80000 or hashlib.sha1(rom).hexdigest().upper() != ROM_SHA1:
        raise ValueError("Validated SMW USA ROM required")
    levels = [int(text, 16) for text in args.levels.split(",")]
    if any(not 0 <= level < 512 for level in levels):
        raise ValueError("Level IDs must be between 000 and 1FF")
    subprocess.run(["dotnet", "run", "--project", str(ROOT / "tools/SmwAssetTool/SmwAssetTool.csproj"), "--",
                    "extract-terrain", str(args.rom.resolve()), str(output), args.levels], cwd=ROOT, check=True)
    manifest = json.loads((output / "native_terrain.json").read_text())
    if manifest["source_rom_sha1"] != ROM_SHA1:
        raise ValueError("Native terrain ROM identity mismatch")
    report = {"rom_sha1": ROM_SHA1, "levels": [], "tolerance": 0,
              "scope": "Original object expansion with declared fresh state; gameplay and full TAS are separate gates"}
    for level in levels:
        key = f"{level:03X}"
        oracle = output / ("oracle-" + key)
        command = [str(ROOT / "tools/parity.sh"), "oracle", "--rom", str(args.rom.resolve()),
                   "--frames", "360", "--snapshot-at-exec", "0x058416", "--out", str(oracle)]
        # The first original title load is available from unchanged power-on.
        # Other main entrances use explicitly declared diagnostic RAM writes.
        if level != 0x0C7:
            command += ["--diagnostic-level", key]
        subprocess.run(command, cwd=ROOT, check=True)
        capture = json.loads((oracle / "manifest.json").read_text())
        if (capture["capture_status"] != "complete" or capture["rom_sha1"] != ROM_SHA1 or
                capture.get("diagnostic_level") != (key if level != 0x0C7 else None) or
                capture["execution_snapshot"]["address"] != "0x058416"):
            raise ValueError("Oracle terrain capture does not match its declared fixture")
        ram_path = oracle / "exec-058416/wram.bin"
        ram = ram_path.read_bytes()
        if len(ram) != 0x20000 or hashlib.sha256(ram).hexdigest() != capture["memory_captures"]["exec-058416/wram.bin"]["sha256"]:
            raise ValueError("Oracle terrain capture has changed")
        entry = next(item for item in manifest["levels"] if item["id"] == key)
        pointer = int.from_bytes(rom[0x2E000 + level * 3:0x2E003 + level * 3], "little")
        source = (pointer >> 16) * 0x8000 + (pointer & 0x7FFF)
        # The original data pointer sits on its terminator at this phase.
        # Validate it against this exact stream, rather than trusting injection.
        expected_pointer = pointer + 5
        index = source + 5
        while rom[index] != 0xFF:
            size = 4 if (rom[index] & 0x60) == 0 and (rom[index + 1] & 0xF0) == 0 and rom[index + 2] == 0 else 3
            index += size
            expected_pointer += size
        if int.from_bytes(ram[0x65:0x68], "little") != expected_pointer:
            raise ValueError(f"Original loader did not process the expected {key} stream")
        if ram[0x1925] != entry["level_mode"] or ram[0x1931] != entry["tileset"]:
            raise ValueError(f"Original loader header mismatch for {key}")
        if entry["header"][4] >> 6 == 3:
            raise ValueError("Item-memory selection 3 cannot use the declared fresh three-bank state")
        # The profile is fresh: no item-memory bits or collection flags.
        # The unused fourth item-memory window includes entrance metadata,
        # so compare the original three game-maintained windows here.
        if any(ram[0x19F8:0x1B78]) or any(ram[0x1F2F:0x1F49]) or any(ram[0x1FEE:0x1FFB]):
            raise ValueError("Original fixture has nonzero terrain collection state")
        if ram[0x13CE] or ram[0x1EA2 + ram[0x13BF]] & 0x40 or any(ram[0x1F27:0x1F2B]):
            raise ValueError("Original fixture has a midpoint or activated palace switch")
        pages = {}
        for page, address in (("low", 0xC800), ("high", 0x1C800)):
            native = (output / key / f"map16-{page}.bin").read_bytes()
            if len(native) != 0x3800:
                raise ValueError("Native Map16 buffer size mismatch")
            original = ram[address:address + len(native)]
            differences = [(i, a, b) for i, (a, b) in enumerate(zip(original, native)) if a != b]
            if differences:
                first = differences[0]
                raise ValueError(f"{key} {page}: {len(differences)} differing bytes; first ${address + first[0]:05X}: original {first[1]:02X}, native {first[2]:02X}")
            digest = hashlib.sha256(native).hexdigest().upper()
            if digest != entry[f"{page}_sha256"]:
                raise ValueError("Native terrain manifest hash mismatch")
            pages[page] = {"bytes": len(native), "sha256": digest, "different_bytes": 0}
        report["levels"].append({"id": key, "pages": pages, "matched": True,
                                 "entry": "original power-on title" if level == 0x0C7 else "declared diagnostic main entrance"})
        print(f"smw native terrain: {key} all {0x7000} Map16 bytes match the original loader")
    if args.godot:
        subprocess.run(["dotnet", "build", str(ROOT / "SmwGodotNative.csproj"), "--nologo", "--verbosity", "minimal"],
                       cwd=ROOT, check=True)
        pack = Path(tempfile.mkdtemp(prefix="empty-pack-", dir=output))
        env = os.environ.copy()
        env.update(SMW_ROM_PATH=str(args.rom.resolve()), SMW_ASSET_BASE_PATH=str(pack))
        boots = []
        for level in levels:
            key = f"{level:03X}"
            result = subprocess.run([str(ROOT / "tools/run.sh"), "--headless", "--no-build", "--no-import", "--quit-after", "3",
                                     "--smw-test-level=" + key, "--smw-no-audio"], env=env, cwd=ROOT,
                                    stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=30)
            (output / (key + "-empty-pack.log")).write_text(result.stdout)
            if result.returncode or "ERROR:" in result.stdout or "SCRIPT ERROR:" in result.stdout:
                raise ValueError(f"Godot empty-pack boot failed for {key}; inspect its log")
            lines = [line for line in result.stdout.splitlines() if line.startswith("smw-native-terrain:")]
            if len(lines) != 1:
                raise ValueError(f"Godot did not construct native terrain once for {key}")
            fields = dict(re.findall(r"(\w+)=([^\s]+)", lines[0]))
            entry = next(item for item in manifest["levels"] if item["id"] == key)
            for field, expected in (("level", key), ("source", "rom"), ("state", "fresh-development-entry"),
                                    ("low_sha256", entry["low_sha256"]), ("high_sha256", entry["high_sha256"])):
                if fields.get(field) != expected:
                    raise ValueError(f"Godot terrain {field} differs for {key}")
            if any(pack.iterdir()):
                raise ValueError("Godot wrote unexpected files into the empty asset pack")
            boots.append({"id": key, "matched": True, "tiles": int(fields["tiles"])})
            print(f"smw native terrain: Godot {key} buffers match with no generated pack or manifest")
        report["godot"] = {"headless": True, "asset_pack": str(pack), "generated_files": 0, "boots": boots}
        if 0x105 in levels and 0x1CB in levels:
            commands = output / "empty-pack-route.commands"
            commands.write_text("pause\nscreen_exit 7\nstate after_pipe\nscreen_exit 1\nstate after_return\nquit\n")
            result = subprocess.run([str(ROOT / "tools/run.sh"), "--headless", "--no-build", "--no-import", "--smw-test-level=105",
                                     "--smw-no-audio", "--smw-debug-command-file=" + str(commands)], env=env, cwd=ROOT,
                                    stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=30)
            (output / "empty-pack-route.log").write_text(result.stdout)
            if result.returncode or "ERROR:" in result.stdout or "unresolved" in result.stdout:
                raise ValueError("Godot empty-pack pipe route failed; inspect its log")
            for expected in ("entrance level=1CB source=1CB secondary=0 settings=4", "entrance level=105 source=1CB secondary=1 settings=6"):
                if expected not in result.stdout:
                    raise ValueError("Godot empty-pack route did not resolve the original main/secondary entrances")
            if any(pack.iterdir()):
                raise ValueError("Godot pipe route wrote into the empty asset pack")
            report["godot"]["route"] = "105 -> 1CB -> 105"
            print("smw native terrain: Godot main/secondary route works without a generated asset pack")
    write_json(report_path, report)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, OSError, subprocess.SubprocessError) as error:
        print("smw native terrain: " + str(error), file=sys.stderr)
        raise SystemExit(1)
