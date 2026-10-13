#!/usr/bin/env python3
"""Audit all vanilla level slots, raw streams, coordinates and entrance tables."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import struct
import subprocess
import sys

import smw_import as smw

ROOT = Path(__file__).resolve().parents[1]


def check(actual, expected, context: str) -> None:
    if actual != expected:
        raise ValueError(f"{context}: expected {expected!r}, got {actual!r}")


def entrance(rom: smw.Rom, index: int, secondary: bool) -> dict:
    level = (index & 0x100) | rom.get_byte(0x05F800 + index) if secondary else index
    f000, f200, f400, f600 = [rom.get_byte(address + level) for address in (0x05F000, 0x05F200, 0x05F400, 0x05F600)]
    if secondary:
        y_byte, x_byte = rom.get_byte(0x05FA00 + index), rom.get_byte(0x05FC00 + index)
        yi, xi, screen, action = y_byte & 15, x_byte >> 5, x_byte & 31, rom.get_byte(0x05FE00 + index) & 7
        l1, l2 = rom.get_byte(0x05D708 + (y_byte >> 4 & 3)), rom.get_byte(0x05D70C + (y_byte >> 6))
    else:
        yi, xi, screen, action = f000 & 15, f200 & 7, f600 & 31, f200 >> 3 & 7
        l1, l2 = rom.get_byte(0x05D708 + (f400 >> 2 & 3)), rom.get_byte(0x05D70C + (f400 & 3))
    x = rom.get_byte(0x05D750 + xi) + rom.get_byte(0x05D758 + xi) * 256
    y = rom.get_byte(0x05D730 + yi) + rom.get_byte(0x05D740 + yi) * 256
    flags = f600 >> 5 & 3
    if flags & 1:
        y = screen * 256 + y % 256
        l1 += screen * 256
        if rom.get_byte(0x05D710 + (f000 >> 4)) != 3:
            l2 += screen * 256
    else:
        x = screen * 256 + x % 256
    return dict(Level=level, SourceId=index, Secondary=secondary, X=x, Y=y, Action=action, Layer1Y=l1, Layer2Y=l2,
                LayoutFlags=flags, Layer3Settings=f200 >> 6, DisableIntro=bool(f600 & 128))


def objects(rom: smw.Rom, source: int, layer: int, flags: int, actual: dict, raw: bytes, key: str) -> int:
    expected_raw = rom.get_bytes(source, smw.calc_level_len(rom, source))
    check(raw, expected_raw, f"{key} layer {layer + 1} ROM bytes")
    parsed = smw.parse_level_objects(raw, {"layout_flags": flags}, layer)
    check(len(actual["objects"]), len(parsed["objects"]), f"{key} layer {layer + 1} object count")
    for obj, native in zip(parsed["objects"], actual["objects"]):
        for field in ("sequence", "offset", "raw", "object_id", "size_or_type"):
            check(native[field], obj[field], f"{key} object {obj['sequence']} {field}")
        p = native["placement"]
        expected_p = obj["placement"]
        for field in ("layer", "screen_cursor", "sub_x", "sub_y", "high_subscreen", "map16_offset"):
            check(p[field], expected_p[field], f"{key} object {obj['sequence']} {field}")
        # Native vertical layout exchanges the axes and uses the second half
        # as additional columns; old preview coordinates always assumed X screens.
        vertical = bool(flags & (1 << layer))
        cross = expected_p["sub_y"] + (16 if expected_p["high_subscreen"] else 0)
        along = expected_p["screen_cursor"] * 16 + expected_p["sub_x"]
        check((p["x_tile"], p["y_tile"]), (cross, along) if vertical else (along, cross), f"{key} native placement axes")
    check(len(actual["exits"]), len(parsed["screen_exits"]), f"{key} exit count")
    for native, expected in zip(actual["exits"], parsed["screen_exits"]):
        for field in ("screen", "exit_low", "raw_r11"):
            check(native[field], expected[field], f"{key} exit {field}")
        check(native["destination_main_map"], expected["exit_low"], f"{key} main map bank")
        check(native["destination_submap"], expected["exit_low"] + 256, f"{key} submap bank")
    return len(parsed["objects"])


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rom", type=Path, default=Path(os.environ.get("SMW_ROM_PATH", "")))
    parser.add_argument("--out", type=Path, default=ROOT / "generated/native-level-data")
    parser.add_argument("--oracle-entry", action="store_true", help="Check the original title-level entrance at the LoadLevel return")
    args = parser.parse_args()
    output = args.out.resolve()
    if output.is_relative_to(ROOT) and subprocess.run(["git", "check-ignore", "-q", "--", str(output)], cwd=ROOT).returncode:
        raise ValueError("Level data output must be ignored by git")
    rom = smw.Rom.load(args.rom)
    subprocess.run(["dotnet", "run", "--project", str(ROOT / "tools/SmwAssetTool/SmwAssetTool.csproj"), "--", "extract-level-data",
                    str(args.rom.resolve()), str(output)], cwd=ROOT, check=True)
    manifest = json.loads((output / "native_level_data.json").read_text())
    check(manifest["source_rom_sha1"], smw.SMW_SHA1_US, "ROM identity")
    check(len(manifest["levels"]), 512, "all vanilla pointer slots")
    count_objects = count_sprites = count_vertical = 0
    for index in range(512):
        key = f"{index:03X}"
        data = json.loads((output / key / "data.json").read_text())
        layer1, layer2 = rom.get_24(0x05E000 + index * 3), rom.get_24(0x05E600 + index * 3)
        raw1, raw2, raw_sprites = [(output / key / name).read_bytes() for name in ("layer1.bin", "layer2.bin", "sprites.bin")]
        check(data["header"], list(rom.get_bytes(layer1, 5)), key + " header")
        flags = rom.get_byte(0x058417 + (raw1[1] & 31))
        check(data["layout_flags"], flags, key + " ROM layout flags")
        count_objects += objects(rom, layer1, 0, flags, data["layer1"], raw1, key)
        if layer2 >> 16 == 0xFF:
            source = 0x0C0000 | (layer2 & 65535)
            expected, length = smw.unpack_rle(rom, source)
            check(raw2, expected, key + " RLE background bytes")
            check(data["layer2_compressed_length"], length, key + " RLE compressed size")
        else:
            count_objects += objects(rom, layer2, 1, flags, data["layer2"], raw2, key)
        sprite_address = 0x070000 | rom.get_word(0x05EC00 + index * 2)
        check(raw_sprites, rom.get_bytes(sprite_address, smw.sprite_data_len(rom, sprite_address)), key + " sprite ROM bytes")
        check(data["sprites"]["header"], raw_sprites[0], key + " sprite header")
        records = data["sprites"]["records"]
        check(len(records), (len(raw_sprites) - 2) // 3, key + " sprite record count")
        for ordinal, record in enumerate(records):
            position, screen_position, sprite_id = raw_sprites[1 + ordinal * 3:4 + ordinal * 3]
            screen = ((position << 3) & 16) | (screen_position & 15)
            along = screen * 256 + (screen_position & 240)
            across = (position & 240) + (position & 13) * 256
            expected_xy = (across, along) if flags & 1 else (along, across)
            check(record["raw"], [position, screen_position, sprite_id], key + " sprite record bytes")
            check((record["x"], record["y"]), expected_xy, key + " original sprite axes/high bits")
        count_sprites += len(records)
        count_vertical += bool(flags & 1)
        for name, secondary in (("main_entrance", False), ("secondary_entrance", True)):
            check(data[name], entrance(rom, index, secondary), key + " " + name)
    for name, address in (("level_info_05f000", 0x05F000), ("level_info_05f200", 0x05F200), ("level_info_05f400", 0x05F400),
                          ("level_info_05f600", 0x05F600), ("secondary_level_low_05f800", 0x05F800), ("secondary_y_05fa00", 0x05FA00),
                          ("secondary_x_05fc00", 0x05FC00), ("secondary_entrance_type_05fe00", 0x05FE00)):
        check((output / "entrances" / (name + ".bin")).read_bytes(), rom.get_bytes(address, 512), name)
    report = dict(rom_sha1=smw.SMW_SHA1_US, slots=512, objects=count_objects, sprites=count_sprites, vertical_slots=count_vertical,
                  entrance_records=1024, pointer_alias_groups=len(manifest["pointer_alias_groups"]),
                  scope="ROM data and parsing; original gameplay, object expansion and reachability are separate gates")
    if args.oracle_entry:
        oracle = output / "oracle-entry"
        subprocess.run([str(ROOT / "tools/parity.sh"), "oracle", "--rom", str(args.rom.resolve()), "--frames", "360",
                        "--snapshot-at-exec", "0x05DAEE", "--out", str(oracle)], cwd=ROOT, check=True)
        ram = (oracle / "exec-05DAEE/wram.bin").read_bytes()
        pointer = int.from_bytes(ram[0x65:0x68], "little")
        expected = json.loads((output / "0C7/data.json").read_text())
        check(pointer, int(expected["layer1"]["source_addr"], 16), "original title level pointer")
        entry = expected["main_entrance"]
        for address, name in ((0x94, "X"), (0x96, "Y"), (0x1C, "Layer1Y"), (0x20, "Layer2Y")):
            check(int.from_bytes(ram[address:address + 2], "little"), entry[name], "original entrance " + name)
        check(ram[0x192A], entry["Action"], "original entrance action")
        report["oracle_entry"] = {"level": "0C7", "snapshot": "before $05DAEE LoadLevel return", "matched": True}
        (oracle / "verification.json").write_text(json.dumps(report["oracle_entry"], indent=2) + "\n")
        print("smw native level data: original title entry position, camera offsets and action match")
    (output / "verification.json").write_text(json.dumps(report, indent=2) + "\n")
    print("smw native level data: " + json.dumps(report, sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, OSError, KeyError, subprocess.SubprocessError) as error:
        print("smw native level data: failed: " + str(error), file=sys.stderr)
        raise SystemExit(1)
