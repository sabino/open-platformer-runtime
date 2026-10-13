#!/usr/bin/env python3
"""Local original-ROM capture and exact comparison; never a game dependency."""
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

ROOT = Path(__file__).resolve().parents[1]
ROM_SHA1 = "6B47BB75D16514B6A476AA0C73A683A2A4C18765"
BUTTONS = {"B": 0x8000, "Y": 0x4000, "Select": 0x2000, "Start": 0x1000,
           "Up": 0x0800, "Down": 0x0400, "Left": 0x0200, "Right": 0x0100,
           "A": 0x0080, "X": 0x0040, "L": 0x0020, "R": 0x0010}
ALIASES = {name.lower(): name for name in BUTTONS}
ALIASES.update(jump="B", spin="A", spinjump="A", run="Y", fire="Y")
FIELDS = ("game_mode", "x", "y", "subx", "suby", "xs", "ys", "subxs",
          "subys", "powerup", "p_meter", "hurt", "global_counter", "local_counter")


def write_json(path: Path, value: object) -> None:
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n")


def safe_output(path: Path) -> Path:
    path = path.resolve()
    if path.is_relative_to(ROOT):
        check = subprocess.run(["git", "check-ignore", "-q", "--", str(path)], cwd=ROOT)
        if check.returncode:
            raise ValueError("ROM-derived output must be ignored by git: " + str(path))
    path.mkdir(parents=True, exist_ok=True)
    return path


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def clear_capture_files(output: Path, names: tuple[str, ...]) -> None:
    # A failed or unvisited probe must never inherit a prior successful capture.
    for name in names:
        (output / name).unlink(missing_ok=True)


def prepare_input(args: argparse.Namespace, output: Path) -> list[int]:
    path = args.input
    if args.tas:
        if path:
            raise ValueError("Choose --input or --tas")
        path = output / "converted.input"
        subprocess.run([sys.executable, str(ROOT / "tools/convert-tas-input.py"),
                        str(args.tas.resolve()), "-o", str(path),
                        "--max-frames", str(args.frames)], check=True)
        # The legacy converter is explicitly a P1 diagnostic projection.
        # Complete movie metadata/controllers/console events are a separate gate.
        write_json(output / "movie.json", {
            "source_sha256": sha256(args.tas), "projection": "legacy-P1-diagnostic",
            "full_movie_parity_eligible": False,
            "reason": "Full initial-state, console-event and controller contracts need verification",
        })
    frames: list[int] = []
    if path:
        for number, raw in enumerate(path.read_text().splitlines(), 1):
            line = raw.split("#", 1)[0].strip()
            if not line:
                continue
            if line in ("@allow-opposing-directions", "@allow-opposing-directions 1", "@allow-opposing-directions on"):
                continue
            if line.startswith("@"):
                raise ValueError(f"Unsupported input directive at {path}:{number}: {line}")
            parts = line.replace("+", " ").split()
            count = int(parts[0])
            if count <= 0:
                raise ValueError(f"Non-positive input duration at {path}:{number}")
            mask = 0
            for token in parts[1:]:
                if token.lower() in ("none", "neutral", "-", "--------"):
                    continue
                name = ALIASES.get(token.lower())
                if name is None:
                    raise ValueError(f"Unsupported input token at {path}:{number}: {token}")
                mask |= BUTTONS[name]
            frames.extend([mask] * min(count, args.frames - len(frames)))
            if len(frames) == args.frames:
                break
        if len(frames) != args.frames:
            raise ValueError(f"Input provides {len(frames)} frames; requested {args.frames}")
    else:
        frames = [0] * args.frames
    lines = ["@allow-opposing-directions"]
    for mask in frames:
        names = [name for name, bit in BUTTONS.items() if mask & bit]
        lines.append("1 " + " ".join(names or ["none"]))
    (output / "input.input").write_text("\n".join(lines) + "\n")
    return frames


def manifest(args: argparse.Namespace, output: Path, frames: list[int], engine: str) -> dict:
    value = {"schema": 1, "engine": engine, "frames": len(frames),
             "capture_status": "running",
             "input_sha256": sha256(output / "input.input"),
             "input_projection": "P1 diagnostic; full movie parity is not claimed",
             "sampling_phase": "mesen.endFrame" if engine == "original" else "Godot post-simulation trace",
             "initial_state": "power-on, zero RAM, declared SRAM" if engine == "original" else "development-level entry",
             "coordinate_transform": None, "frame_offset": 0, "tolerance": 0}
    write_json(output / "manifest.json", value)
    return value


def capture_oracle(args: argparse.Namespace) -> int:
    output = safe_output(args.out)
    rom = args.rom or Path(os.environ.get("SMW_ROM_PATH", ""))
    if not rom.is_file() or len(rom.read_bytes()) != 0x80000 or hashlib.sha1(rom.read_bytes()).hexdigest().upper() != ROM_SHA1:
        raise ValueError("A validated unheadered SMW USA ROM is required")
    binary = args.mesen or Path(os.environ.get("SMW_MESEN_BIN", str(
        ROOT / ".godot_local/oracles/mesen-2.1.1/Mesen.app/Contents/MacOS/Mesen")))
    if not binary.is_file():
        raise ValueError("Mesen not installed; set SMW_MESEN_BIN")
    clear_capture_files(output, ("manifest.json", "state.jsonl", "frame.png", "wram.bin", "vram.bin", "oam.bin", "cgram.bin", "initial-sram.bin", "level-initialization.json"))
    frames = prepare_input(args, output)
    save_folder = Path(tempfile.mkdtemp(prefix="sram-", dir=output))
    initial_sram = args.sram.read_bytes() if args.sram else bytes(0x800)
    if len(initial_sram) != 0x800:
        raise ValueError("Original-ROM SRAM must be exactly 2 KiB; convert emulator movie SRAM explicitly")
    (save_folder / (rom.stem + ".srm")).write_bytes(initial_sram)
    (output / "declared-sram.bin").write_bytes(initial_sram)
    value = manifest(args, output, frames, "original")
    value.update(rom_sha1=ROM_SHA1, mesen_binary_sha256=sha256(binary),
                 core_sha256=sha256(binary.with_name("MesenCore.dylib")),
                 ram_power_on_state="AllZeros", initial_sram_sha256=hashlib.sha256(initial_sram).hexdigest(),
                 sram_folder=str(save_folder), sram_initialization="Lua before CPU resume, read-back verified")
    initialization = []
    if args.diagnostic_level is not None:
        level = args.diagnostic_level
        if not 0 <= level < 512:
            raise ValueError("Diagnostic level ID must be between 000 and 1FF")
        # Select a main entrance through the original sublevel entry branch.
        # This is a declared diagnostic fixture, never a power-on/TAS result.
        overrides = {0x141A: 1, 0x1B93: 0, 0x1B95: 0, 0x1425: 0,
                     0x0DD6: 0, 0x0DD7: 0, 0x005B: 0, 0x0095: 0, 0x0097: 0,
                     0x1F11: level >> 8, 0x19B8: level & 255}
        value.update(diagnostic_level=f"{level:03X}",
                     initial_state="power-on, zero RAM, declared SRAM; diagnostic level-entry RAM override at $05D796",
                     full_movie_parity_eligible=False,
                     diagnostic_ram_overrides={f"0x{address:04X}": byte for address, byte in overrides.items()})
        initialization = [
            "local initialized = false",
            "emu.addMemoryCallback(function()",
            "  if initialized then return end; initialized=true",
            "  local log=assert(io.open(root .. '/level-initialization.json','w'))",
            "  log:write('{\"frame\":' .. frame .. ',\"writes\":[')",
        ]
        for index, (address, byte_value) in enumerate(overrides.items()):
            initialization.extend([
                f"  local previous=byte({address}); emu.write({address},{byte_value},emu.memType.snesWorkRam)",
                "  log:write(string.format(" + json.dumps((',' if index else '') +
                    '{"address":%d,"before":%d,"after":%d}') +
                    f",{address},previous,byte({address})))",
            ])
        initialization += ["  log:write(']}'); log:close()", "end,emu.callbackType.exec,0x05D796)"]
    write_json(output / "manifest.json", value)
    state_fields = {
        "game_mode": "byte(0x100)", "x": "word(0x94)", "y": "word(0x96)",
        "subx": "byte(0x13DA)", "suby": "byte(0x13DC)",
        "xs": "signed(0x7B)", "ys": "signed(0x7D)",
        "subxs": "byte(0x7A)", "subys": "byte(0x7C)",
        "powerup": "byte(0x19)", "p_meter": "byte(0x13E4)",
        "hurt": "byte(0x1497)", "global_counter": "byte(0x13)",
        "local_counter": "byte(0x14)", "held_high": "byte(0x15)",
        "pressed_high": "byte(0x16)", "held_low": "byte(0x17)",
        "pressed_low": "byte(0x18)"}
    expressions = ", ".join(state_fields.values())
    template = '{"frame":%d,"input_expected":%d,' + ",".join(
        '"' + name + '":%d' for name in state_fields) + "}\n"
    probe = []
    snapshot_prefix = None
    if args.snapshot_at_exec is not None:
        address = args.snapshot_at_exec
        if not 0 <= address <= 0xFFFFFF:
            raise ValueError("Execution snapshot address must fit in 24 bits")
        snapshot_prefix = f"exec-{address:06X}"
        (output / snapshot_prefix).mkdir(exist_ok=True)
        clear_capture_files(output / snapshot_prefix, ("state.jsonl", "wram.bin", "vram.bin", "oam.bin", "cgram.bin"))
        probe = [
            "local captured = false",
            "emu.addMemoryCallback(function()",
            "  if captured then return end; captured=true",
            f"  dump('{snapshot_prefix}/wram.bin',emu.memType.snesWorkRam)",
            f"  dump('{snapshot_prefix}/vram.bin',emu.memType.snesVideoRam)",
            f"  dump('{snapshot_prefix}/oam.bin',emu.memType.snesSpriteRam)",
            f"  dump('{snapshot_prefix}/cgram.bin',emu.memType.snesCgRam)",
            f"  local state=assert(io.open(root .. '/{snapshot_prefix}/state.jsonl','w'))",
            "  state:write(string.format(" + json.dumps(template) + ", frame, frames[frame+1] or 0, " + expressions + ")); state:close()",
            f"end,emu.callbackType.exec,{address})",
        ]
    script = "\n".join([
        "local frames = {" + ",".join(map(str, frames)) + "}",
        "local root = " + json.dumps(str(output), ensure_ascii=False),
        "local trace = assert(io.open(root .. '/state.jsonl', 'w'))",
        "local frame = 0",
        "local function byte(a) return emu.read(a, emu.memType.snesWorkRam) end",
        "local function word(a) return byte(a) + byte(a + 1) * 256 end",
        "local function signed(a) local v=byte(a); if v >= 128 then return v-256 else return v end end",
        "local function dump(name, kind)",
        "  local out=assert(io.open(root .. '/' .. name,'wb'))",
        "  local size=emu.getMemorySize(kind)",
        "  for base=0,size-1,1024 do local chunk={}; for i=base,math.min(base+1023,size-1) do chunk[#chunk+1]=string.char(emu.read(i,kind)) end; out:write(table.concat(chunk)) end",
        "  out:close()",
        "end", *initialization, *probe,
        "local declared = assert(io.open(root .. '/declared-sram.bin','rb')):read('*a')",
        "assert(#declared == emu.getMemorySize(emu.memType.snesSaveRam))",
        "for i=1,#declared do emu.write(i-1,string.byte(declared,i),emu.memType.snesSaveRam) end",
        "local initial = assert(io.open(root .. '/initial-sram.bin','wb'))",
        "for i=0,emu.getMemorySize(emu.memType.snesSaveRam)-1 do initial:write(string.char(emu.read(i,emu.memType.snesSaveRam))) end",
        "initial:close()",
        "emu.addEventCallback(function()",
        "  local mask = frames[frame + 1] or 0",
        "  emu.setInput({" + ",".join(name.lower() + "=(mask & " + str(bit) + ") ~= 0" for name, bit in BUTTONS.items()) + "},0)",
        "end,emu.eventType.inputPolled)",
        "emu.addEventCallback(function()",
        "  frame = frame + 1",
        "  trace:write(string.format(" + json.dumps(template) + ", frame, frames[frame] or 0, " + expressions + "))",
        "  if frame == #frames then",
        "    trace:close()",
        "    local image = assert(io.open(root .. '/frame.png','wb'))",
        "    image:write(emu.takeScreenshot()); image:close()",
        "    dump('wram.bin',emu.memType.snesWorkRam)",
        "    dump('vram.bin',emu.memType.snesVideoRam)",
        "    dump('oam.bin',emu.memType.snesSpriteRam)",
        "    dump('cgram.bin',emu.memType.snesCgRam)",
        "    emu.stop(0)",
        "  end",
        "end,emu.eventType.endFrame)", ""])
    (output / "capture.lua").write_text(script)
    command = [str(binary.resolve()), "--testRunner", "--doNotSaveSettings",
               "--snes.ramPowerOnState=AllZeros", "--enableStdout", "--timeout=" + str(args.timeout),
               "--preferences.overrideSaveDataFolder=true", "--preferences.saveDataFolder=" + str(save_folder),
               str(output / "capture.lua"), str(rom.resolve())]
    with (output / "engine.log").open("w") as log:
        subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=args.timeout + 5)
    rows = read_rows(output / "state.jsonl")
    if (output / "initial-sram.bin").read_bytes() != initial_sram:
        raise ValueError("Emulator did not load the declared initial SRAM")
    if len(rows) != args.frames:
        raise ValueError(f"Oracle captured {len(rows)} of {args.frames} frames; inspect engine.log")
    if args.diagnostic_level is not None:
        applied = json.loads((output / "level-initialization.json").read_text())
        if {entry["address"]: entry["after"] for entry in applied["writes"]} != overrides:
            raise ValueError("Diagnostic level RAM initialization did not match its declaration")
        value["diagnostic_initialization_frame"] = applied["frame"]
    value["memory_captures"] = {}
    for name, size in (("wram.bin", 0x20000), ("vram.bin", 0x10000), ("oam.bin", 0x220), ("cgram.bin", 0x200)):
        path = output / name
        if path.stat().st_size != size:
            raise ValueError(f"Oracle memory capture {name} has unexpected size")
        value["memory_captures"][name] = {"size": size, "sha256": sha256(path)}
        if snapshot_prefix:
            snapshot = output / snapshot_prefix / name
            if not snapshot.is_file() or snapshot.stat().st_size != size:
                raise ValueError("Execution snapshot was not captured; address may not have executed")
            value["memory_captures"][f"{snapshot_prefix}/{name}"] = {"size": size, "sha256": sha256(snapshot)}
    if snapshot_prefix:
        value["execution_snapshot"] = {"address": f"0x{args.snapshot_at_exec:06X}", "occurrence": 1,
                                       "sampling_phase": "before instruction execution", "directory": snapshot_prefix,
                                       "frame_semantics": "number of endFrame callbacks completed before this instruction"}
    value["capture_status"] = "complete"
    write_json(output / "manifest.json", value)
    print(json.dumps({"engine": "original", "frames": len(rows), "output": str(output),
                      "last_state": rows[-1]}, sort_keys=True))
    return 0


def capture_godot(args: argparse.Namespace) -> int:
    output = safe_output(args.out)
    clear_capture_files(output, ("manifest.json", "state.jsonl"))
    frames = prepare_input(args, output)
    unsupported = BUTTONS["Start"] | BUTTONS["Select"] | BUTTONS["L"] | BUTTONS["R"]
    for frame, mask in enumerate(frames, 1):
        if mask & unsupported:
            names = [name for name, bit in BUTTONS.items() if mask & bit & unsupported]
            raise ValueError(f"Godot frame {frame} requires unimplemented controller buttons: {', '.join(names)}")
    value = manifest(args, output, frames, "godot")
    asset_manifest = json.loads((ROOT / "generated/smw/manifest.json").read_text())
    value["rom_sha1"] = asset_manifest["rom"]["sha1"].upper()
    value.update(level=args.level, unsupported_fields=["game_mode", "global_counter", "local_counter"],
                 original_boot_implemented=False, full_movie_parity_eligible=False,
                 controller_projection="P1 gameplay booleans; X/Y edges are merged; original latches and P2 remain unimplemented")
    write_json(output / "manifest.json", value)
    commands = output / "capture.commands"
    commands.write_text(f"pause\nlive_trace {args.frames} tag=parity quit=1\n")
    command = [str(ROOT / "tools/run.sh"), "--headless", "--smw-test-level=" + args.level,
               "--smw-input-script=" + str(output / "input.input"),
               "--smw-debug-command-file=" + str(commands)]
    with (output / "engine.log").open("w") as log:
        subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=args.timeout)
    rows = []
    for line in (output / "engine.log").read_text().splitlines():
        if not line.startswith("smw-debug-trace: tag=parity "):
            continue
        pairs = dict(re.findall(r"(\w+)=([^\s]+)", line))
        row = {"frame": int(pairs["frame"]), "game_mode": None,
               "global_counter": None, "local_counter": None,
               "input_actual": pairs["input"]}
        for source, target in {"xi": "x", "yi": "y", "subx": "subx", "suby": "suby",
                               "xs": "xs", "ys": "ys", "subxs": "subxs", "subys": "subys",
                               "pow": "powerup", "hurt": "hurt"}.items():
            if source not in pairs:
                raise ValueError("Godot trace lacks required field: " + source)
            row[target] = int(pairs[source])
        row["p_meter"] = int(pairs["p"], 16)
        rows.append(row)
    if len(rows) != args.frames:
        raise ValueError(f"Godot captured {len(rows)} of {args.frames} frames; inspect engine.log")
    (output / "state.jsonl").write_text("".join(json.dumps(row, sort_keys=True) + "\n" for row in rows))
    value["capture_status"] = "complete"
    write_json(output / "manifest.json", value)
    print(json.dumps({"engine": "godot", "frames": len(rows), "output": str(output),
                      "unsupported_fields": value["unsupported_fields"]}, sort_keys=True))
    return 0


def read_rows(path: Path) -> list[dict]:
    return [json.loads(line) for line in path.read_text().splitlines() if line.strip()]


def compare(args: argparse.Namespace) -> int:
    expected, actual = read_rows(args.expected), read_rows(args.actual)
    fields = args.fields.split(",") if args.fields else FIELDS
    result = {"schema": 1, "equal": True, "tolerance": 0,
              "expected_frames": len(expected), "actual_frames": len(actual), "fields": list(fields)}
    result["diagnostic_only"] = bool(args.diagnostic or args.fields)
    result["comparison_scope"] = "declared state fields; full-game completion is a separate corpus gate"
    expected_manifest = args.expected.parent / "manifest.json"
    actual_manifest = args.actual.parent / "manifest.json"
    incompatible_start = False
    movie_eligible = True
    capture_complete = True
    if expected_manifest.exists() and actual_manifest.exists():
        left_setup, right_setup = json.loads(expected_manifest.read_text()), json.loads(actual_manifest.read_text())
        movie_eligible = all(setup.get("full_movie_parity_eligible") is not False for setup in (left_setup, right_setup))
        for side, setup in (("expected", left_setup), ("actual", right_setup)):
            if "capture_status" in setup and setup["capture_status"] != "complete":
                result.update(equal=False, reason="incomplete_capture", side=side)
                capture_complete = False
                break
        for key in ("initial_state", "input_sha256", "rom_sha1", "initial_sram_sha256", "diagnostic_level"):
            if not capture_complete:
                break
            if key not in left_setup and key not in right_setup:
                continue
            if left_setup.get(key) == right_setup.get(key):
                continue
            incompatible_start = True
            if not args.diagnostic:
                result.update(equal=False, reason="incompatible_setup", setup_field=key,
                              expected_setup=left_setup.get(key), actual_setup=right_setup.get(key))
            break
    result["parity_eligible"] = not result["diagnostic_only"] and not incompatible_start and movie_eligible and capture_complete
    if not result["equal"]:
        pass
    elif not expected or not actual:
        result.update(equal=False, reason="empty_capture", first_frame=1)
    elif len(expected) != len(actual):
        result.update(equal=False, reason="frame_count", first_frame=min(len(expected), len(actual)) + 1)
    else:
        for index, (left, right) in enumerate(zip(expected, actual), 1):
            for field in ("frame", *fields):
                if field not in left or field not in right or left[field] is None or right[field] is None:
                    result.update(equal=False, reason="unsupported_or_missing_field", first_frame=index,
                                  field=field, expected=left.get(field), actual=right.get(field))
                    break
                if left[field] != right[field]:
                    result.update(equal=False, reason="state", first_frame=index, field=field,
                                  expected=left[field], actual=right[field])
                    break
            if not result["equal"]:
                break
    if args.report:
        report = safe_output(args.report.parent) / args.report.name
        write_json(report, result)
    print(json.dumps(result, sort_keys=True))
    return 0 if result["equal"] else 1


def pixels(args: argparse.Namespace) -> int:
    from PIL import Image
    with Image.open(args.expected) as left_source, Image.open(args.actual) as right_source:
        left, right = left_source.convert("RGBA"), right_source.convert("RGBA")
    result = {"schema": 1, "equal": False, "expected_size": left.size, "actual_size": right.size,
              "resampling": None, "tolerance": 0}
    if left.size != right.size:
        result["reason"] = "dimensions"
    else:
        expected_bytes, actual_bytes = left.tobytes(), right.tobytes()
        count = 0
        for offset in range(0, len(expected_bytes), 4):
            a, b = expected_bytes[offset:offset+4], actual_bytes[offset:offset+4]
            if a == b:
                continue
            count += 1
            if count == 1:
                index = offset // 4
                result.update(first_pixel=[index % left.width, index // left.width], expected=list(a), actual=list(b))
        result.update(equal=count == 0, differing_pixels=count)
    if args.report:
        write_json(safe_output(args.report.parent) / args.report.name, result)
    print(json.dumps(result, sort_keys=True))
    return 0 if result["equal"] else 1


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subcommands = parser.add_subparsers(dest="command", required=True)
    for name in ("oracle", "godot"):
        capture = subcommands.add_parser(name)
        capture.add_argument("--frames", type=int, default=120)
        capture.add_argument("--input", type=Path)
        capture.add_argument("--tas", type=Path)
        capture.add_argument("--out", type=Path, default=ROOT / "generated/parity" / name)
        capture.add_argument("--timeout", type=int, default=60)
        if name == "oracle":
            capture.add_argument("--rom", type=Path)
            capture.add_argument("--mesen", type=Path)
            capture.add_argument("--sram", type=Path)
            capture.add_argument("--diagnostic-level", type=lambda value: int(value, 16),
                                 help="Original main entrance with declared RAM overrides; ineligible for TAS parity")
            capture.add_argument("--snapshot-at-exec", type=lambda value: int(value, 0),
                                 help="Capture the first execution of a SNES CPU address, e.g. 0x00AB41")
            capture.set_defaults(function=capture_oracle)
        else:
            capture.add_argument("--level", default="105")
            capture.set_defaults(function=capture_godot)
    for name, function in (("compare", compare), ("pixels", pixels)):
        comparison = subcommands.add_parser(name)
        comparison.add_argument("expected", type=Path)
        comparison.add_argument("actual", type=Path)
        comparison.add_argument("--report", type=Path)
        if name == "compare":
            comparison.add_argument("--fields")
            comparison.add_argument("--diagnostic", action="store_true")
        comparison.set_defaults(function=function)
    args = parser.parse_args()
    if hasattr(args, "frames") and args.frames <= 0:
        parser.error("--frames must be positive")
    try:
        return args.function(args)
    except (ValueError, OSError, subprocess.SubprocessError) as error:
        print("parity: " + str(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
