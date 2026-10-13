#!/usr/bin/env python3
"""Exercise ROM selection, native course entry and audio with no generated asset pack."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

from parity import ROOT, ROM_SHA1, safe_output, write_json


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rom", type=Path, default=Path(os.environ.get("SMW_ROM_PATH", "")))
    parser.add_argument("--out", type=Path, default=ROOT / "generated/native-startup")
    parser.add_argument("--no-build", action="store_true")
    args = parser.parse_args()
    rom_path = args.rom.resolve()
    rom = rom_path.read_bytes()
    if len(rom) != 0x80000 or hashlib.sha1(rom).hexdigest().upper() != ROM_SHA1:
        raise ValueError("This local startup fixture requires the validated unheadered SMW USA ROM")
    output = safe_output(args.out)
    (output / "verification.json").unlink(missing_ok=True)
    pack = Path(tempfile.mkdtemp(prefix="empty-pack-", dir=output))
    env = os.environ.copy()
    env.update(SMW_ROM_PATH="", SMW_ASSET_BASE_PATH=str(pack))
    godot = os.environ["GODOT_BIN"]
    cases = []

    def run(name: str, arguments: list[str], required: tuple[str, ...], expected_code: int = 0) -> str:
        result = subprocess.run([godot, "--headless", "--audio-driver", "Dummy", "--path", str(ROOT),
                                 "--log-file", str(output / (name + "-engine.log")), *arguments],
                                env=env, cwd=ROOT, text=True, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, timeout=60)
        (output / (name + ".log")).write_text(result.stdout)
        if result.returncode != expected_code:
            raise ValueError(f"{name}: exit {result.returncode}; inspect {name}.log")
        if "SCRIPT ERROR:" in result.stdout or (expected_code == 0 and "ERROR:" in result.stdout):
            raise ValueError(f"{name}: Godot reported an error; inspect {name}.log")
        for text in required:
            if text not in result.stdout:
                raise ValueError(f"{name}: missing {text!r}; inspect {name}.log")
        if any(pack.iterdir()):
            raise ValueError(f"{name}: unexpectedly generated files in the asset directory")
        cases.append({"name": name, "passed": True, "generated_files": 0})
        print(f"native ROM startup: {name} passed", flush=True)
        return result.stdout

    project = ROOT / "SmwGodotNative.csproj"
    project_bytes = project.read_bytes()
    try:
        run("import-resources", ["--editor", "--import", "--quit"], ())
    finally:
        # A newer editor migrates the SDK and inserts an unconditional target,
        # which would override the project's conditional Web net9.0 selection.
        # Keep this bounded test from silently changing the project's targets.
        if project.read_bytes() != project_bytes:
            project.write_bytes(project_bytes)
    if not args.no_build:
        subprocess.run(["dotnet", "build", str(project), "--nologo", "-v:minimal"],
                       cwd=ROOT, env=env, check=True)
    log = run("source-only", ["--quit-after", "3", "--smw-test-autostart", "--smw-no-audio"],
              ("reason=rom-required", "assets=0"))
    if "smw-runtime: level=" in log:
        raise ValueError("Source-only startup entered a placeholder course without a ROM")
    run("select-rom", ["--quit-after", "240", "--smw-test-import-rom=" + str(rom_path),
                       "--smw-test-import-quit", "--smw-audio"],
        ("native_complete=1 indexed=512 verified_terrain=4", "source=rom-memory generated_files=0", "native_dsp=1 source=rom"))
    headered = output / "headered-local.smc"
    headered.write_bytes(bytes(512) + rom)
    run("select-headered-rom", ["--quit-after", "240", "--smw-test-import-rom=" + str(headered),
                                "--smw-test-import-quit", "--smw-no-audio"],
        ("native_complete=1 indexed=512 verified_terrain=4", "sha1=" + ROM_SHA1))
    run("native-rom-menu", ["--quit-after", "240", "--smw-test-import-rom=" + str(rom_path),
                            "--smw-rom-menu-smoke", "--smw-no-audio"],
        ("smw-rom-menu: smoke=passed", "native_audio=1 layout=contained"))
    invalid = output / "invalid-synthetic.sfc"
    invalid.write_bytes(bytes(0x80000))
    run("reject-invalid-rom", ["--quit-after", "240", "--smw-test-import-rom=" + str(invalid),
                               "--smw-test-import-quit", "--smw-no-audio"],
        ("smw-menu-import: failed", "wrong-sha1"), expected_code=1)
    for level in ("0C7", "105", "106", "1CB"):
        run("course-" + level, ["--quit-after", "3", "--smw-rom=" + str(rom_path),
                                "--smw-test-level=" + level, "--smw-no-audio"],
            ("smw-native-terrain: level=" + level, "smw-runtime: level=" + level, "source=rom state=fresh-development-entry"))
    log = run("reject-unverified-layout", ["--quit-after", "3", "--smw-rom=" + str(rom_path),
                                           "--smw-test-level=107", "--smw-no-audio"],
              ("unsupported=107 reason=layout-not-verified",))
    if "smw-runtime: level=107" in log or "source=generated-preview" in log:
        raise ValueError("Unverified native level silently entered preview geometry")
    commands = output / "first-course-route.commands"
    commands.write_text("pause\nscreen_exit 7\nstate after_pipe\nscreen_exit 1\nstate after_return\nquit\n")
    run("first-course-route", ["--smw-rom=" + str(rom_path), "--smw-test-level=105", "--smw-audio",
                               "--smw-debug-command-file=" + str(commands)],
        ("entrance level=1CB source=1CB secondary=0 settings=4", "entrance level=105 source=1CB secondary=1 settings=6",
         "native_dsp=1 source=rom"))
    run("native-audio-player", ["--quit-after", "420", "--smw-rom=" + str(rom_path), "--smw-audio-player-smoke"],
        ("smw-audio-player: smoke=passed", "loaded=1"))
    write_json(output / "verification.json", {
        "rom_sha1": ROM_SHA1, "cases": cases, "generated_asset_files": 0,
        "scope": "Headless native ROM selection, verified terrain entries, diagnostic pipe route and listening player",
        "full_course_gameplay_parity": False, "full_song_pcm_parity": False,
        "browser_runtime_execution": "Separate custom Web export gate; browser loader unit tests do not execute Godot Web",
    })
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, OSError, subprocess.SubprocessError) as error:
        print("native ROM startup: " + str(error), file=sys.stderr)
        raise SystemExit(1)
