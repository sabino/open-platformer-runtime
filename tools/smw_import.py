#!/usr/bin/env python3
"""Offline SMW ROM importer for the Godot port.

This intentionally contains the small extraction subset needed by the first
Godot milestone. It does not import from the moving native C++ repo at runtime.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import struct
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any


SMW_SHA1_US = "6B47BB75D16514B6A476AA0C73A683A2A4C18765"
SCHEMA_VERSION = 1


class ImportErrorWithExit(Exception):
    pass


@dataclass(frozen=True)
class Rom:
    path: Path
    data: bytes

    @classmethod
    def load(cls, path: Path) -> "Rom":
        if not path.exists():
            raise ImportErrorWithExit(f"ROM does not exist: {path}")
        data = path.read_bytes()
        if (len(data) & 0xFFFFF) == 0x200:
            sha1 = hashlib.sha1(data).hexdigest().upper()
            raise ImportErrorWithExit(
                "Headered ROMs are not supported by this importer. "
                f"Got size={len(data)} sha1={sha1}. Use an unheadered SMW USA ROM."
            )
        sha1 = hashlib.sha1(data).hexdigest().upper()
        if sha1 != SMW_SHA1_US:
            raise ImportErrorWithExit(
                f"Unsupported ROM sha1={sha1}. Expected unheadered SMW USA {SMW_SHA1_US}."
            )
        if len(data) != 0x80000:
            raise ImportErrorWithExit(
                f"Unsupported ROM size={len(data)}. Expected 524288 bytes for SMW USA."
            )
        return cls(path=path, data=data)

    def lorom_index(self, addr: int) -> int:
        if (addr & 0x8000) == 0:
            raise ImportErrorWithExit(f"LoROM address must have bit 0x8000 set: 0x{addr:06X}")
        index = ((addr >> 16) & 0x7F) * 0x8000 + (addr & 0x7FFF)
        if index >= len(self.data):
            raise ImportErrorWithExit(f"LoROM address out of ROM range: 0x{addr:06X}")
        return index

    def get_byte(self, addr: int) -> int:
        return self.data[self.lorom_index(addr)]

    def get_word(self, addr: int) -> int:
        return self.get_byte(addr) | (self.get_byte(addr + 1) << 8)

    def get_24(self, addr: int) -> int:
        return self.get_word(addr) | (self.get_byte(addr + 2) << 16)

    def get_bytes(self, addr: int, count: int) -> bytes:
        out = bytearray()
        for _ in range(count):
            out.append(self.get_byte(addr))
            addr += 1
            if (addr & 0x8000) == 0:
                addr += 0x8000
        return bytes(out)

    def get_words(self, addr: int, count: int) -> list[int]:
        return [self.get_word(addr + i * 2) for i in range(count)]


class Reader:
    def __init__(self, rom: Rom, addr: int) -> None:
        self.rom = rom
        self.addr = addr

    def next(self) -> int:
        value = self.rom.get_byte(self.addr)
        self.addr += 1
        if (self.addr & 0xFFFF) == 0:
            self.addr += 0x8000
        return value


def smw_decomp(rom: Rom, addr: int) -> tuple[bytes, int]:
    result = bytearray()
    reader = Reader(rom, addr)
    while True:
        command = reader.next()
        if command == 0xFF:
            return bytes(result), (reader.addr - addr) & 0x7FFF
        if (command & 0xE0) != 0xE0:
            length = command & 0x1F
            op = command & 0xE0
        else:
            op = (command << 3) & 0xE0
            length = ((command & 3) << 8) | reader.next()
        length += 1
        if op == 0x00:
            for _ in range(length):
                result.append(reader.next())
        elif op & 0x80:
            offset = (reader.next() << 8) | reader.next()
            for _ in range(length):
                result.append(result[offset])
                offset += 1
        elif (op & 0x40) == 0:
            value = reader.next()
            result.extend([value] * length)
        elif (op & 0x20) == 0:
            first = reader.next()
            second = reader.next()
            while length > 0:
                result.append(first)
                length -= 1
                if length <= 0:
                    break
                result.append(second)
                length -= 1
        else:
            value = reader.next()
            for _ in range(length):
                result.append(value)
                value = (value + 1) & 0xFF


def unpack_rle(rom: Rom, addr: int) -> tuple[bytes, int]:
    start = addr
    out = bytearray()
    while rom.get_word(addr) != 0xFFFF:
        control = rom.get_byte(addr)
        addr += 1
        count = (control & 0x7F) + 1
        if control & 0x80:
            value = rom.get_byte(addr)
            addr += 1
            out.extend([value] * count)
        else:
            out.extend(rom.get_byte(addr + i) for i in range(count))
            addr += count
    return bytes(out), addr + 2 - start


def calc_level_len(rom: Rom, addr: int) -> int:
    start = addr
    addr += 5
    while True:
        b0 = rom.get_byte(addr)
        addr += 1
        if b0 == 0xFF:
            break
        b1 = rom.get_byte(addr)
        b2 = rom.get_byte(addr + 1)
        addr += 2
        obj_id = (b1 >> 4) | ((b0 & 0x60) >> 1)
        if obj_id == 0 and b2 == 0:
            addr += 1
        elif obj_id in (0x22, 0x23):
            addr += 1
        elif obj_id == 0x27:
            addr += 2
    return addr - start


def sprite_data_len(rom: Rom, addr: int) -> int:
    start = addr
    addr += 1
    while rom.get_byte(addr) != 0xFF:
        addr += 3
    return addr + 1 - start


def parse_level_objects(raw: bytes) -> dict[str, Any]:
    objects: list[dict[str, Any]] = []
    exits: list[dict[str, Any]] = []
    index = 5
    sequence = 0
    while index < len(raw):
        offset = index
        b0 = raw[index]
        index += 1
        if b0 == 0xFF:
            break
        if index + 2 > len(raw):
            raise ImportErrorWithExit("Truncated level object stream")
        b1 = raw[index]
        b2 = raw[index + 1]
        index += 2
        obj_id = (b1 >> 4) | ((b0 & 0x60) >> 1)
        extra: list[int] = []
        if obj_id == 0 and b2 == 0:
            extra.append(raw[index])
            index += 1
            vanilla_properties = b1 & 0x03
            exit_low = extra[0]
            exits.append(
                {
                    "screen": b0 & 0x1F,
                    "exit_low": exit_low,
                    "raw_r11": b1,
                    "vanilla_properties": vanilla_properties,
                    "vanilla_destination": ((vanilla_properties & 1) << 8) | exit_low,
                    "vanilla_secondary": (vanilla_properties >> 1) & 1,
                    "lunar_magic_properties": b1 & 0x0F,
                    "lunar_magic_secondary": (b1 >> 1) & 1,
                }
            )
        elif obj_id in (0x22, 0x23):
            extra.append(raw[index])
            index += 1
        elif obj_id == 0x27:
            extra.extend([raw[index], raw[index + 1]])
            index += 2
        objects.append(
            {
                "sequence": sequence,
                "offset": offset,
                "screen": b0 & 0x1F,
                "raw": [b0, b1, b2],
                "object_id": obj_id,
                "size_or_type": b2,
                "extra": extra,
            }
        )
        sequence += 1
    return {"objects": objects, "screen_exits": exits}


def parse_sprite_data(raw: bytes) -> dict[str, Any]:
    records: list[dict[str, Any]] = []
    index = 1
    while index < len(raw) and raw[index] != 0xFF:
        if index + 3 > len(raw):
            raise ImportErrorWithExit("Truncated sprite stream")
        records.append(
            {
                "offset": index,
                "screen_y": raw[index],
                "x_id": raw[index + 1],
                "extra_bits": (raw[index + 2] >> 6) & 0x03,
                "sprite_id": raw[index + 2] & 0x3F,
                "raw": [raw[index], raw[index + 1], raw[index + 2]],
            }
        )
        index += 3
    return {"header": raw[0], "sprites": records}


def snes_words_to_rgb(words: list[int]) -> list[list[int]]:
    colors: list[list[int]] = []
    for word in words:
        r5 = word & 0x1F
        g5 = (word >> 5) & 0x1F
        b5 = (word >> 10) & 0x1F
        colors.append([(r5 << 3) | (r5 >> 2), (g5 << 3) | (g5 >> 2), (b5 << 3) | (b5 >> 2)])
    return colors


def write_json(path: Path, payload: Any) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(payload, indent=2, sort_keys=True)
    path.write_text(text + "\n", encoding="utf-8")
    return sha1_bytes(path.read_bytes())


def write_bin(path: Path, payload: bytes) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)
    return sha1_bytes(payload)


def sha1_bytes(data: bytes) -> str:
    return hashlib.sha1(data).hexdigest().upper()


def rel(path: Path, root: Path) -> str:
    return path.relative_to(root).as_posix()


def parse_level_id(text: str) -> int:
    text = text.strip()
    if text.lower().startswith("0x"):
        return int(text, 16)
    return int(text, 16)


def extract_level(rom: Rom, out_dir: Path, level_id: int) -> dict[str, Any]:
    layer1_addr = rom.get_24(0x05E000 + level_id * 3)
    layer1_len = calc_level_len(rom, layer1_addr)
    layer1_raw = rom.get_bytes(layer1_addr, layer1_len)
    parsed_layer1 = parse_level_objects(layer1_raw)

    layer2_addr = rom.get_24(0x05E600 + level_id * 3)
    layer2_kind = "object_stream"
    if (layer2_addr & 0xFF0000) == 0xFF0000:
        layer2_addr = (layer2_addr & 0xFFFF) | 0x0C0000
        layer2_raw, layer2_len = unpack_rle(rom, layer2_addr)
        layer2_kind = "rle_background"
    else:
        layer2_len = calc_level_len(rom, layer2_addr)
        layer2_raw = rom.get_bytes(layer2_addr, layer2_len)

    banks = (
        list(rom.get_bytes(0x0EF100, 512))
        if rom.get_byte(0x05D8F5) == 0x22
        else [7] * 512
    )
    sprite_addr = rom.get_word(0x05EC00 + level_id * 2) | (banks[level_id] << 16)
    sprite_len = sprite_data_len(rom, sprite_addr)
    sprite_raw = rom.get_bytes(sprite_addr, sprite_len)
    parsed_sprites = parse_sprite_data(sprite_raw)

    level_key = f"{level_id:03X}"
    level_path = out_dir / "levels" / f"level_{level_key}.json"
    payload = {
        "level_id": level_key,
        "layer1": {
            "source_addr": f"0x{layer1_addr:06X}",
            "length": layer1_len,
            "header": list(layer1_raw[:5]),
            "raw": list(layer1_raw),
            "objects": parsed_layer1["objects"],
        },
        "layer2": {
            "source_addr": f"0x{layer2_addr:06X}",
            "length": layer2_len,
            "kind": layer2_kind,
            "raw": list(layer2_raw),
        },
        "sprite_layer": {
            "source_addr": f"0x{sprite_addr:06X}",
            "length": sprite_len,
            "raw": list(sprite_raw),
            "header": parsed_sprites["header"],
            "sprites": parsed_sprites["sprites"],
        },
        "screen_exits": parsed_layer1["screen_exits"],
    }
    level_sha = write_json(level_path, payload)
    return {
        "file": rel(level_path, out_dir),
        "sha1": level_sha,
        "layer1_addr": f"0x{layer1_addr:06X}",
        "layer2_addr": f"0x{layer2_addr:06X}",
        "sprite_addr": f"0x{sprite_addr:06X}",
        "object_count": len(parsed_layer1["objects"]),
        "sprite_count": len(parsed_sprites["sprites"]),
        "screen_exits": parsed_layer1["screen_exits"],
    }


def extract_global_assets(rom: Rom, out_dir: Path) -> dict[str, Any]:
    assets: dict[str, Any] = {}

    map16_words = rom.get_words(0x0D8000, (0xA100 - 0x8000) // 2)
    map16_payload = b"".join(struct.pack("<H", word) for word in map16_words)
    map16_path = out_dir / "map16" / "global_map16.bin"
    assets["map16_global"] = {
        "file": rel(map16_path, out_dir),
        "format": "little_endian_uint16",
        "word_count": len(map16_words),
        "sha1": write_bin(map16_path, map16_payload),
    }

    palette_sets = {
        "sky": rom.get_words(0x00B0A0, 16),
        "background": rom.get_words(0x00B0B0, 96),
        "layer3": rom.get_words(0x00B170, 16),
        "foreground": rom.get_words(0x00B190, 96),
        "objects": rom.get_words(0x00B250, 60),
        "player": rom.get_words(0x00B2C8, 40),
        "sprites": rom.get_words(0x00B318, 84),
        "flashing": rom.get_words(0x00B60C, 16),
        "yoshi_berry": rom.get_words(0x00B674, 21),
    }
    palettes_path = out_dir / "palettes" / "global_palettes.json"
    palettes_payload = {
        name: {
            "snes_bgr555": values,
            "rgb888": snes_words_to_rgb(values),
        }
        for name, values in palette_sets.items()
    }
    assets["palettes_global"] = {
        "file": rel(palettes_path, out_dir),
        "sha1": write_json(palettes_path, palettes_payload),
    }

    secondary_payload = {
        "level_info_05f000": list(rom.get_bytes(0x05F000, 0x200)),
        "level_info_05f200": list(rom.get_bytes(0x05F200, 0x200)),
        "level_info_05f400": list(rom.get_bytes(0x05F400, 0x200)),
        "level_info_05f600": list(rom.get_bytes(0x05F600, 0x200)),
        "secondary_level_low_05f800": list(rom.get_bytes(0x05F800, 0x200)),
        "secondary_y_05fa00": list(rom.get_bytes(0x05FA00, 0x200)),
        "secondary_x_05fc00": list(rom.get_bytes(0x05FC00, 0x200)),
    }
    secondary_path = out_dir / "levels" / "secondary_tables.json"
    assets["secondary_tables"] = {
        "file": rel(secondary_path, out_dir),
        "sha1": write_json(secondary_path, secondary_payload),
    }

    for name, pointer_addr in {"gfx32": 0x00B8D8, "gfx33": 0x00B88B}.items():
        gfx_addr = 0x080000 | rom.get_word(pointer_addr)
        gfx_data, compressed_len = smw_decomp(rom, gfx_addr)
        gfx_path = out_dir / "gfx" / f"{name}.bin"
        assets[name] = {
            "file": rel(gfx_path, out_dir),
            "source_addr": f"0x{gfx_addr:06X}",
            "compressed_length": compressed_len,
            "decompressed_length": len(gfx_data),
            "format": "snes_4bpp_planar",
            "sha1": write_bin(gfx_path, gfx_data),
        }

    return assets


def import_rom(args: argparse.Namespace) -> dict[str, Any]:
    rom = Rom.load(Path(args.rom).expanduser().resolve())
    out_dir = Path(args.out).expanduser().resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    level_queue = [parse_level_id(level) for level in args.level]
    if len(level_queue) != len(set(level_queue)):
        level_queue = list(dict.fromkeys(level_queue))
    assets = extract_global_assets(rom, out_dir)
    levels: dict[str, Any] = {}
    depth_by_level = {level_id: 0 for level_id in level_queue}
    index = 0
    while index < len(level_queue):
        level_id = level_queue[index]
        index += 1
        if level_id < 0 or level_id >= 0x200:
            raise ImportErrorWithExit(f"Level id out of range: 0x{level_id:X}")
        level_info = extract_level(rom, out_dir, level_id)
        levels[f"{level_id:03X}"] = level_info

        if not args.include_exit_targets:
            continue
        depth = depth_by_level[level_id]
        if depth >= args.exit_depth:
            continue
        for screen_exit in level_info["screen_exits"]:
            destination = int(screen_exit["vanilla_destination"])
            if 0 <= destination < 0x200 and destination not in depth_by_level:
                depth_by_level[destination] = depth + 1
                level_queue.append(destination)

    manifest = {
        "schema_version": SCHEMA_VERSION,
        "importer": {
            "name": "tools/smw_import.py",
            "asset_boundary": "generated data is local-only and must not be committed",
        },
        "rom": {
            "path": str(rom.path),
            "sha1": sha1_bytes(rom.data),
            "size": len(rom.data),
            "title": "compatible USA ROM",
            "headered": False,
        },
        "assets": assets,
        "levels": levels,
    }
    manifest_path = out_dir / "manifest.json"
    manifest_sha = write_json(manifest_path, manifest)
    manifest["manifest_sha1"] = manifest_sha
    write_json(manifest_path, manifest)
    return manifest


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Extract SMW USA data into a Godot-readable asset pack.")
    parser.add_argument("--rom", required=True, help="Path to an unheadered compatible USA ROM")
    parser.add_argument("--out", required=True, help="Output asset pack directory")
    parser.add_argument(
        "--level",
        action="append",
        default=[],
        help="SMW level id in hex notation without requiring 0x, e.g. 105 for Yoshi Island 1",
    )
    parser.add_argument(
        "--include-exit-targets",
        action="store_true",
        help="Also import vanilla screen-exit destination levels up to --exit-depth.",
    )
    parser.add_argument(
        "--exit-depth",
        type=int,
        default=1,
        help="Depth for --include-exit-targets; 1 imports direct pipe/door targets.",
    )
    args = parser.parse_args(argv)
    if not args.level:
        args.level = ["105"]
    try:
        manifest = import_rom(args)
    except ImportErrorWithExit as exc:
        print(f"smw-import: {exc}", file=sys.stderr)
        return 2
    print(
        "smw-import: wrote {levels} level(s), {assets} global asset groups to {out}".format(
            levels=len(manifest["levels"]),
            assets=len(manifest["assets"]),
            out=os.path.abspath(args.out),
        )
    )
    for level_id, level in manifest["levels"].items():
        print(
            "smw-import: level {level} objects={objects} sprites={sprites} screen_exits={exits}".format(
                level=level_id,
                objects=level["object_count"],
                sprites=level["sprite_count"],
                exits=len(level["screen_exits"]),
            )
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
