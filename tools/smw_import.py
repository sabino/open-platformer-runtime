#!/usr/bin/env python3
"""Offline SMW ROM importer for the Godot port.

This intentionally contains the small extraction subset needed by the first
Godot milestone. It does not import from the moving native C++ repo at runtime.
"""

from __future__ import annotations

import argparse
import binascii
import hashlib
import json
import os
import struct
import sys
import wave
import zlib
from dataclasses import dataclass
from pathlib import Path
from typing import Any


SMW_SHA1_US = "6B47BB75D16514B6A476AA0C73A683A2A4C18765"
SCHEMA_VERSION = 1
GFX_FILE_COUNT = 50
NORMAL_GFX_3BPP_EXPAND = (
    True, True, True, True, True, True, True, True, True, True, True, True, True,
    True, True, True, True, True, True, True, True, True, True, True, True, True,
    True, True, True, True, True, True, True, True, True, True, True, True, True,
    False, False, False, False, False, True, True, True, False, True, True, False,
    True,
)
PLAYER_GFX_OAM_TABLES = {
    "player_xy_disp_index_index": {"addr": 0x00DCEC, "count": 70, "format": "u8"},
    "player_xy_disp_index": {"addr": 0x00DD32, "count": 28, "format": "u8"},
    "x_disp": {"addr": 0x00DD4E, "count": 114, "format": "s16"},
    "y_disp": {"addr": 0x00DE32, "count": 114, "format": "s16"},
    "powerup_tileset_index": {"addr": 0x00DF16, "count": 4, "format": "u8"},
    "tiles_index": {"addr": 0x00DF4C, "count": 192, "format": "u8"},
    "tiles": {"addr": 0x00DFDA, "count": 50, "format": "u8"},
    "head_tile_pointer_index": {"addr": 0x00E00C, "count": 192, "format": "u8"},
    "body_tile_pointer_index": {"addr": 0x00E0CC, "count": 192, "format": "u8"},
    "tile_x_flip": {"addr": 0x00E18C, "count": 2, "format": "u8"},
}
LM_CUSTOM_PALETTE_POINTER_TABLE = 0x0EF600
LM_CUSTOM_PALETTE_HIJACK_ADDR = 0x00A5C0
LM_CUSTOM_PALETTE_ROUTINE_ADDR = 0x0EF570
LM_SUPER_GFX_POINTER_ADDR = 0x0FF7FF
FG_AND_BG_GFX_LIST = [
    0x14, 0x17, 0x19, 0x15,
    0x14, 0x17, 0x1B, 0x18,
    0x14, 0x17, 0x1B, 0x16,
    0x14, 0x17, 0x0C, 0x1A,
    0x14, 0x17, 0x1B, 0x08,
    0x14, 0x17, 0x0C, 0x07,
    0x14, 0x17, 0x0C, 0x16,
    0x14, 0x17, 0x1B, 0x15,
    0x14, 0x17, 0x19, 0x16,
    0x14, 0x17, 0x0D, 0x1A,
    0x14, 0x17, 0x1B, 0x08,
    0x14, 0x17, 0x1B, 0x18,
    0x14, 0x17, 0x19, 0x1F,
    0x14, 0x17, 0x0D, 0x07,
    0x14, 0x17, 0x19, 0x1A,
    0x14, 0x17, 0x14, 0x14,
    0x0E, 0x0F, 0x17, 0x17,
    0x1C, 0x1D, 0x08, 0x1E,
    0x1C, 0x1D, 0x08, 0x1E,
    0x1C, 0x1D, 0x08, 0x1E,
    0x1C, 0x1D, 0x08, 0x1E,
    0x1C, 0x1D, 0x08, 0x1E,
    0x1C, 0x1D, 0x08, 0x1E,
    0x1C, 0x1D, 0x08, 0x1E,
    0x14, 0x17, 0x19, 0x2C,
    0x19, 0x17, 0x1B, 0x18,
]
SPRITE_GFX_LIST = [
    0x00, 0x01, 0x13, 0x02,
    0x00, 0x01, 0x12, 0x03,
    0x00, 0x01, 0x13, 0x05,
    0x00, 0x01, 0x13, 0x04,
    0x00, 0x01, 0x13, 0x06,
    0x00, 0x01, 0x13, 0x09,
    0x00, 0x01, 0x13, 0x04,
    0x00, 0x01, 0x06, 0x11,
    0x00, 0x01, 0x13, 0x20,
    0x00, 0x01, 0x13, 0x0F,
    0x00, 0x01, 0x13, 0x23,
    0x00, 0x01, 0x0D, 0x14,
    0x00, 0x01, 0x24, 0x0E,
    0x00, 0x01, 0x0A, 0x22,
    0x00, 0x01, 0x13, 0x0E,
    0x00, 0x01, 0x13, 0x14,
    0x00, 0x00, 0x00, 0x08,
    0x10, 0x0F, 0x1C, 0x1D,
    0x00, 0x01, 0x24, 0x22,
    0x00, 0x01, 0x25, 0x22,
    0x00, 0x22, 0x13, 0x2D,
    0x00, 0x01, 0x0F, 0x22,
    0x00, 0x26, 0x2E, 0x22,
    0x21, 0x0B, 0x25, 0x0A,
    0x00, 0x0D, 0x24, 0x22,
    0x2C, 0x30, 0x2D, 0x0E,
]
GENERIC_REPEATED_TILES = [0x02, 0x21, 0x23, 0x2A, 0x2B, 0x3F, 0x03, 0x13, 0x1E, 0x24, 0x2E, 0x2F, 0x30, 0x32, 0x65]
VERTICAL_PIPE_TOP_LEFT = [0x33, 0x37, 0x39, 0x00, 0x00]
VERTICAL_PIPE_TOP_RIGHT = [0x34, 0x38, 0x3A, 0x00, 0x00]
VERTICAL_PIPE_BOTTOM_LEFT = [0x00, 0x00, 0x39, 0x33, 0x37]
VERTICAL_PIPE_BOTTOM_RIGHT = [0x00, 0x00, 0x3A, 0x34, 0x38]
HORIZONTAL_PIPE_END = [0x3B, 0x3C, 0x3B, 0x3F, 0x3B, 0x3C, 0x3B, 0x3F]
HORIZONTAL_PIPE_SHAFT = [0x3D, 0x3E, 0x3D, 0x3E, 0x3D, 0x3E, 0x3D, 0x3E]
GROUND_EDGE_TOP = [0x40, 0x41, 0x06, 0x45, 0x4B, 0x48, 0x4C, 0x01, 0x03, 0xB6, 0xB7, 0x45, 0x4B, 0x48, 0x4C]
GROUND_EDGE_MIDDLE1 = [0x40, 0x41, 0x06, 0x4B, 0x4B, 0x4C, 0x4C, 0x40, 0x41, 0x4B, 0x4C, 0x4B, 0x4B, 0x4C, 0x4C]
GROUND_EDGE_MIDDLE2 = [0x40, 0x41, 0x06, 0x4B, 0x4B, 0x4C, 0x4C, 0x40, 0x41, 0x4B, 0x4C, 0x4B, 0x4B, 0x4C, 0x4C]
GROUND_EDGE_BOTTOM = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xE2, 0xE2, 0xE4, 0xE4]
MIDWAY_TOP = [0x2F, 0x25, 0x32]
MIDWAY_MIDDLE = [0x30, 0x25, 0x33]
MIDWAY_BOTTOM = [0x31, 0x25, 0x34]
GOAL_TOP = [0x39, 0x25, 0x3C]
GOAL_MIDDLE = [0x3A, 0x25, 0x3D]
GOAL_BOTTOM = [0x3B, 0x25, 0x3E]
ROPE_CLOUD_LINE = [0x05, 0x06]
SMALL_BUSH_LEFT = [0x73, 0x7A, 0x85, 0x88, 0xC3]
SMALL_BUSH_MIDDLE = [0x74, 0x7B, 0x86, 0x89, 0xC3]
SMALL_BUSH_RIGHT = [0x79, 0x80, 0x87, 0x8E, 0xC3]
DIAGONAL_PIPE_TILES = [0xC4, 0xC5, 0xC7, 0xEC, 0xED, 0xC6, 0xC7, 0xEE, 0x59, 0x5A, 0xEF, 0xC7, 0xEE, 0x59, 0x5B, 0x5C]
ROPE_TILESETS = {2, 6, 8}
LEVEL_VERTICAL_TABLE = [
    0x00, 0x00, 0x80, 0x01, 0x81, 0x02, 0x82, 0x03,
    0x83, 0x00, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80,
]
MAP16_POINTER_MASKS = [
    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xE0, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0xFE, 0x00, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
    0xFF, 0xFF, 0xE0, 0x00, 0x00, 0x03, 0xFF, 0xFF,
    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
]
TILESET_MAP16_POINTERS = [
    0x8B70, 0xBC00, 0xC800, 0xD400, 0xE300,
    0xE300, 0xC800, 0x8B70, 0xC800, 0xD400,
    0xD400, 0xD400, 0x8B70, 0xE300, 0xD400,
]
BG_PALETTE_ADDR = 0x00B0B0
FG_PALETTE_ADDR = 0x00B190
OBJECT_PALETTE_ADDR = 0x00B250
PLAYER_PALETTE_ADDR = 0x00B2C8
SPRITE_PALETTE_ADDR = 0x00B318
LAYER3_PALETTE_ADDR = 0x00B170
BERRY_PALETTE_ADDR = 0x00B674
BACK_AREA_COLOR_ADDR = 0x00B0A0
ANIMATED_COLOR_ADDR = 0x00B60C
PALETTE_BLACK = 0x0000
PALETTE_WHITE = 0x7FDD


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

    def has_lorom_range(self, addr: int, count: int) -> bool:
        try:
            start = self.lorom_index(addr)
        except ImportErrorWithExit:
            return False
        return start + count <= len(self.data)

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


def decode_level_header(raw: bytes) -> dict[str, Any]:
    if len(raw) < 5:
        raise ImportErrorWithExit("Level stream is too short for header")
    screens = (raw[0] & 0x1F) + 1
    mode = raw[1] & 0x1F
    layout_flags = LEVEL_VERTICAL_TABLE[mode]
    vertical = (layout_flags & 1) != 0
    return {
        "raw": list(raw[:5]),
        "screens": screens,
        "bg_palette": raw[0] >> 5,
        "level_mode": mode,
        "background_color": raw[1] >> 5,
        "sprite_graphics": raw[2] & 0x0F,
        "music_index": (raw[2] >> 4) & 0x07,
        "layer3_priority": (raw[2] >> 7) & 0x01,
        "fg_palette": raw[3] & 0x07,
        "sprite_palette": (raw[3] >> 3) & 0x07,
        "timer_index": raw[3] >> 6,
        "tileset": raw[4] & 0x0F,
        "layer1_scroll": (raw[4] >> 4) & 0x03,
        "item_memory": raw[4] >> 6,
        "layout_flags": layout_flags,
        "vertical": vertical,
        "width_tiles": 16 if vertical else screens * 16,
        "height_tiles": screens * 16 if vertical else 27,
    }


def decode_object_placement(
    b0: int,
    b1: int,
    b2: int,
    obj_id: int,
    screen_cursor: int,
    layout_flags: int,
    layer_index: int,
) -> dict[str, Any]:
    adjusted_b0 = b0
    adjusted_b1 = b1
    layer_flags = layout_flags if layer_index == 0 else layout_flags >> 1
    if (layer_flags & 1) != 0 and ((obj_id << 8) | b2) >= 2:
        low_nibble = b0 & 0x0F
        adjusted_b0 = (b1 & 0x0F) | (b0 & 0xF0)
        adjusted_b1 = low_nibble | (b1 & 0xF0)

    sub_x = adjusted_b0 & 0x0F
    sub_y = adjusted_b1 & 0x0F
    high_subscreen = (adjusted_b0 & 0x10) != 0
    y_tile = sub_y + (16 if high_subscreen else 0)
    return {
        "layer": layer_index + 1,
        "screen_cursor": screen_cursor,
        "screen_increment": (b0 & 0x80) != 0,
        "sub_x": sub_x,
        "sub_y": sub_y,
        "high_subscreen": high_subscreen,
        "map16_offset": (0x100 if high_subscreen else 0) + sub_y * 16 + sub_x,
        "x_tile": screen_cursor * 16 + sub_x,
        "y_tile": y_tile,
        "x_px": (screen_cursor * 16 + sub_x) * 16,
        "y_px": y_tile * 16,
        "adjusted_raw": [adjusted_b0, adjusted_b1, b2],
    }


def parse_level_objects(raw: bytes, header: dict[str, Any], layer_index: int = 0) -> dict[str, Any]:
    objects: list[dict[str, Any]] = []
    exits: list[dict[str, Any]] = []
    index = 5
    sequence = 0
    screen_cursor = 0
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
        if b0 & 0x80:
            screen_cursor += 1
        placement = decode_object_placement(
            b0,
            b1,
            b2,
            obj_id,
            screen_cursor,
            int(header["layout_flags"]),
            layer_index,
        )
        extra: list[int] = []
        if obj_id == 0 and b2 == 0:
            extra.append(raw[index])
            index += 1
            vanilla_properties = b1 & 0x03
            exit_low = extra[0]
            exits.append(
                {
                    "screen": b0 & 0x1F,
                    "screen_cursor": screen_cursor,
                    "exit_low": exit_low,
                    "raw_r11": b1,
                    "vanilla_properties": vanilla_properties,
                    "vanilla_destination_property_bits": ((vanilla_properties & 1) << 8) | exit_low,
                    "vanilla_secondary_property_bits": (vanilla_properties >> 1) & 1,
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
                "raw": [b0, b1, b2],
                "object_id": obj_id,
                "size_or_type": b2,
                "extra": extra,
                "placement": placement,
            }
        )
        sequence += 1
    return {"objects": objects, "screen_exits": exits}


def annotate_vanilla_screen_exits(screen_exits: list[dict[str, Any]], source_level_id: int) -> None:
    # Vanilla SMW does not use the screen-exit property byte as the direct target
    # high bit. The low byte comes from the screen exit, while the high bit comes
    # from the current overworld map. For the extracted vanilla level IDs, the
    # source level's 0x100 bit is the useful static proxy for that map bit.
    source_map_high = source_level_id & 0x100
    for screen_exit in screen_exits:
        raw_r11 = int(screen_exit["raw_r11"])
        exit_low = int(screen_exit["exit_low"])
        screen_exit["vanilla_destination_low"] = exit_low
        screen_exit["vanilla_source_map_high"] = source_map_high >> 8
        screen_exit["vanilla_destination"] = source_map_high | exit_low
        screen_exit["vanilla_secondary"] = raw_r11 >> 1


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


def signed16(word: int) -> int:
    return word - 0x10000 if word & 0x8000 else word


def extract_player_oam_tables(rom: Rom) -> dict[str, Any]:
    tables: dict[str, Any] = {}
    for name, spec in PLAYER_GFX_OAM_TABLES.items():
        addr = int(spec["addr"])
        count = int(spec["count"])
        fmt = str(spec["format"])
        if fmt == "s16":
            values = [signed16(value) for value in rom.get_words(addr, count)]
        elif fmt == "u8":
            values = list(rom.get_bytes(addr, count))
        else:
            raise ImportErrorWithExit(f"Unsupported player OAM table format {fmt!r} for {name}")

        tables[name] = {
            "source_addr": f"0x{addr:06X}",
            "count": count,
            "format": fmt,
            "values": values,
        }
    return tables


def build_player_sprite_palette_words(rom: Rom, player: int = 0) -> list[int]:
    palette = [PALETTE_BLACK, PALETTE_WHITE] + [PALETTE_BLACK] * 14
    copy_palette_words(palette, 0, 2, rom.get_words(OBJECT_PALETTE_ADDR + 4 * 0x0C, 6))
    player_index = max(0, min(player, 3))
    copy_palette_words(palette, 0, 6, rom.get_words(PLAYER_PALETTE_ADDR + player_index * 0x14, 10))
    return palette


def decode_4bpp_tile(tile: bytes) -> list[list[int]]:
    if len(tile) != 32:
        raise ImportErrorWithExit(f"SNES 4bpp tile must be 32 bytes, got {len(tile)}")
    pixels: list[list[int]] = []
    for y in range(8):
        p0 = tile[y * 2]
        p1 = tile[y * 2 + 1]
        p2 = tile[16 + y * 2]
        p3 = tile[16 + y * 2 + 1]
        row: list[int] = []
        for x in range(8):
            bit = 7 - x
            row.append(
                ((p0 >> bit) & 1)
                | (((p1 >> bit) & 1) << 1)
                | (((p2 >> bit) & 1) << 2)
                | (((p3 >> bit) & 1) << 3)
            )
        pixels.append(row)
    return pixels


def expand_3bpp_to_4bpp(data: bytes) -> bytes:
    if len(data) % 24 != 0:
        raise ImportErrorWithExit(f"3bpp graphics length must be divisible by 24: {len(data)}")
    expanded = bytearray()
    for offset in range(0, len(data), 24):
        expanded.extend(data[offset : offset + 16])
        for row in range(8):
            expanded.append(data[offset + 16 + row])
            expanded.append(0)
    return bytes(expanded)


def patch_mask_rows(data: bytearray, begin: int, end: int) -> None:
    offset = begin
    while offset < end:
        if (offset & 0x10) == 0:
            offset += 0x10
            continue
        if offset + 1 < len(data) and offset >= 16:
            data[offset + 1] = data[offset - 16] | data[offset - 15] | data[offset]
        offset += 2


def patch_sparse_mask_rows(data: bytearray, ranges: list[tuple[int, int]]) -> None:
    existing = 0
    for begin, end in ranges:
        offset = begin
        while offset < end:
            if (offset & 0x10) == 0:
                offset += 0x10
                continue
            if offset + 1 < len(data):
                existing |= data[offset + 1]
            offset += 2
    if existing != 0:
        return
    for begin, end in ranges:
        patch_mask_rows(data, begin, end)


def patch_gfx08_mask_tiles(data: bytearray) -> None:
    for tile in (
        0x37, 0x38, 0x39, 0x3A, 0x3B, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x56, 0x57,
        0x58, 0x59, 0x5A, 0x5B, 0x7A, 0x7B, 0x60, 0x70, 0x6E, 0x6F, 0x7E, 0x7F,
    ):
        begin = tile * 32 + 0x10
        patch_mask_rows(data, begin, begin + 0x10)


def apply_gfx_mask_fixes(data: bytes, gfx_id: int) -> bytes:
    patched = bytearray(data)
    if gfx_id in (0x01, 0x17, 0x31):
        patch_sparse_mask_rows(patched, [(0x10, 0x40), (0x210, 0x240)])
    elif gfx_id == 0x1E:
        patch_mask_rows(patched, 0x10, 0x1000)
    elif gfx_id == 0x08:
        patch_gfx08_mask_tiles(patched)
    return bytes(patched)


def png_chunk(kind: bytes, payload: bytes) -> bytes:
    crc = binascii.crc32(kind)
    crc = binascii.crc32(payload, crc) & 0xFFFFFFFF
    return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", crc)


def write_rgba_png(path: Path, width: int, height: int, rgba: bytes) -> str:
    if len(rgba) != width * height * 4:
        raise ImportErrorWithExit("RGBA buffer size does not match PNG dimensions")
    rows = []
    stride = width * 4
    for y in range(height):
        rows.append(b"\x00" + rgba[y * stride : (y + 1) * stride])
    payload = b"".join(
        [
            b"\x89PNG\r\n\x1a\n",
            png_chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)),
            png_chunk(b"IDAT", zlib.compress(b"".join(rows), level=9)),
            png_chunk(b"IEND", b""),
        ]
    )
    return write_bin(path, payload)


def write_4bpp_atlas_png(path: Path, gfx_data: bytes, palette_rgb: list[list[int]], columns: int = 16) -> dict[str, Any]:
    if len(gfx_data) % 32 != 0:
        raise ImportErrorWithExit(f"4bpp graphics length must be divisible by 32: {len(gfx_data)}")
    tile_count = len(gfx_data) // 32
    rows = (tile_count + columns - 1) // columns
    width = columns * 8
    height = rows * 8
    rgba = bytearray([0, 0, 0, 0] * width * height)
    palette = palette_rgb[:16]
    if len(palette) < 16:
        raise ImportErrorWithExit("A 4bpp atlas requires at least 16 palette colors")

    for tile_index in range(tile_count):
        tile = gfx_data[tile_index * 32 : (tile_index + 1) * 32]
        tile_pixels = decode_4bpp_tile(tile)
        tile_x = (tile_index % columns) * 8
        tile_y = (tile_index // columns) * 8
        for y, row in enumerate(tile_pixels):
            for x, color_index in enumerate(row):
                out = ((tile_y + y) * width + tile_x + x) * 4
                rgb = palette[color_index]
                rgba[out : out + 4] = bytes([rgb[0], rgb[1], rgb[2], 0 if color_index == 0 else 255])

    return {
        "file": str(path),
        "sha1": write_rgba_png(path, width, height, bytes(rgba)),
        "width": width,
        "height": height,
        "tile_count": tile_count,
        "columns": columns,
        "tile_width": 8,
        "tile_height": 8,
        "format": "rgba_png_from_snes_4bpp",
    }


def copy_palette_words(target: list[int], row: int, color: int, words: list[int]) -> None:
    start = row * 16 + color
    for index, word in enumerate(words):
        if 0 <= start + index < len(target):
            target[start + index] = word


def has_lunar_magic_custom_palette_hijack(rom: Rom) -> bool:
    if not rom.has_lorom_range(LM_CUSTOM_PALETTE_HIJACK_ADDR, 4):
        return False
    return (
        rom.get_byte(LM_CUSTOM_PALETTE_HIJACK_ADDR) == 0x22
        and rom.get_24(LM_CUSTOM_PALETTE_HIJACK_ADDR + 1) == LM_CUSTOM_PALETTE_ROUTINE_ADDR
    )


def read_level_custom_palette(rom: Rom, level_id: int) -> tuple[int, list[int]] | None:
    if not has_lunar_magic_custom_palette_hijack(rom):
        return None
    pointer_addr = LM_CUSTOM_PALETTE_POINTER_TABLE + level_id * 3
    if not rom.has_lorom_range(pointer_addr, 3):
        return None
    pointer = rom.get_24(pointer_addr)
    if pointer in (0x000000, 0xFFFFFF):
        return None
    if not rom.has_lorom_range(pointer, 0x202):
        return None
    words = rom.get_words(pointer, 0x101)
    return words[0], words[1:]


def build_vanilla_level_palette_words(rom: Rom, header: dict[str, Any], player: int = 0) -> tuple[int, list[int]]:
    palette = [0] * 256
    for row in range(16):
        palette[row * 16] = PALETTE_BLACK
        palette[row * 16 + 1] = PALETTE_WHITE

    back_area_color = rom.get_word(BACK_AREA_COLOR_ADDR + int(header["background_color"]) * 2)

    bg_index = int(header["bg_palette"])
    bg_words = rom.get_words(BG_PALETTE_ADDR + bg_index * 0x18, 12)
    copy_palette_words(palette, 0, 2, bg_words[:6])
    copy_palette_words(palette, 1, 2, bg_words[6:])

    fg_index = int(header["fg_palette"])
    fg_words = rom.get_words(FG_PALETTE_ADDR + fg_index * 0x18, 12)
    copy_palette_words(palette, 2, 2, fg_words[:6])
    copy_palette_words(palette, 3, 2, fg_words[6:])

    for row in range(4, 14):
        copy_palette_words(palette, row, 2, rom.get_words(OBJECT_PALETTE_ADDR + (row - 4) * 0x0C, 6))

    player_index = max(0, min(player, 3))
    copy_palette_words(palette, 8, 6, rom.get_words(PLAYER_PALETTE_ADDR + player_index * 0x14, 10))

    sprite_index = int(header["sprite_palette"])
    sprite_words = rom.get_words(SPRITE_PALETTE_ADDR + sprite_index * 0x18, 12)
    copy_palette_words(palette, 14, 2, sprite_words[:6])
    copy_palette_words(palette, 15, 2, sprite_words[6:])

    for row in (0, 1):
        copy_palette_words(palette, row, 8, rom.get_words(LAYER3_PALETTE_ADDR + row * 0x10, 8))

    for offset, row in enumerate((2, 3, 4)):
        berry_words = rom.get_words(BERRY_PALETTE_ADDR + offset * 0x0E, 7)
        copy_palette_words(palette, row, 9, berry_words)
        copy_palette_words(palette, row + 7, 9, berry_words)

    palette[6 * 16 + 4] = rom.get_word(ANIMATED_COLOR_ADDR)
    return back_area_color, palette


def level_palette_payload(rom: Rom, level_id: int, header: dict[str, Any]) -> dict[str, Any]:
    custom = read_level_custom_palette(rom, level_id)
    if custom is None:
        source = "vanilla_header_tables"
        back_area_color, palette_words = build_vanilla_level_palette_words(rom, header)
        custom_palette_addr = None
    else:
        source = "lunar_magic_custom_palette"
        back_area_color, palette_words = custom
        custom_palette_addr = LM_CUSTOM_PALETTE_POINTER_TABLE + level_id * 3

    payload: dict[str, Any] = {
        "status": "preview",
        "source": source,
        "level_id": f"{level_id:03X}",
        "back_area_color": {
            "snes_bgr555": back_area_color,
            "rgb888": snes_words_to_rgb([back_area_color])[0],
        },
        "snes_bgr555": palette_words,
        "rgb888": snes_words_to_rgb(palette_words),
        "layout": {
            "rows": 16,
            "colors_per_row": 16,
            "tilemap_palette_bits": "bits 10-12 select CGRAM rows 0-7 for BG/FG Map16 rendering",
            "sprite_rows": "rows 8-15 are used by OAM sprite palettes",
        },
        "header_palette_indexes": {
            "background_color": int(header["background_color"]),
            "bg_palette": int(header["bg_palette"]),
            "fg_palette": int(header["fg_palette"]),
            "sprite_palette": int(header["sprite_palette"]),
        },
        "notes": [
            "Vanilla palette assembly follows the header-selected tables documented by SMW Central and the speedrunning level-data notes.",
            "Lunar Magic custom palette pointers at $0EF600 are recognized when a supported ROM exposes them.",
        ],
    }
    if custom_palette_addr is not None:
        payload["custom_palette_pointer_table_addr"] = f"0x{custom_palette_addr:06X}"
    return payload


def extract_level_palette_assets(rom: Rom, out_dir: Path, level_id: int, header: dict[str, Any]) -> dict[str, Any]:
    level_key = f"{level_id:03X}"
    palette_path = out_dir / "palettes" / f"level_{level_key}_palette.json"
    payload = level_palette_payload(rom, level_id, header)
    payload["file"] = rel(palette_path, out_dir)
    payload["sha1"] = write_json(palette_path, payload)
    return payload


def graphics_file_address(rom: Rom, gfx_id: int) -> int:
    if gfx_id < 0:
        raise ImportErrorWithExit(f"GFX id out of range: {gfx_id}")
    if gfx_id >= 0x100:
        table_ptr = rom.get_24(0x0FF937)
        if table_ptr in (0x000000, 0xFFFFFF):
            raise ImportErrorWithExit(f"ExGFX pointer table is not installed for GFX{gfx_id:03X}")
        addr = rom.get_24(table_ptr + (gfx_id - 0x100) * 3)
        if addr == 0:
            raise ImportErrorWithExit(f"ExGFX{gfx_id:03X} is not inserted")
        return addr
    if gfx_id >= 0x80:
        addr = rom.get_24(0x0FF600 + (gfx_id - 0x80) * 3)
        if addr == 0:
            raise ImportErrorWithExit(f"ExGFX{gfx_id:03X} is not inserted")
        return addr
    if gfx_id >= GFX_FILE_COUNT:
        raise ImportErrorWithExit(f"GFX id out of range for vanilla table: {gfx_id}")
    lo = rom.get_byte(0x00B992 + gfx_id)
    hi = rom.get_byte(0x00B9C4 + gfx_id)
    bank = rom.get_byte(0x00B9F6 + gfx_id)
    return (bank << 16) | (hi << 8) | lo


def decompress_graphics_file(rom: Rom, gfx_id: int) -> tuple[bytes, int, int]:
    addr = graphics_file_address(rom, gfx_id)
    data, compressed_len = smw_decomp(rom, addr)
    if 0 <= gfx_id < len(NORMAL_GFX_3BPP_EXPAND) and NORMAL_GFX_3BPP_EXPAND[gfx_id]:
        data = expand_3bpp_to_4bpp(data)
        data = apply_gfx_mask_fixes(data, gfx_id)
    if len(data) % 32 != 0:
        raise ImportErrorWithExit(f"GFX{gfx_id:02X} decompressed to non-4bpp length {len(data)}")
    return data, addr, compressed_len


def level_fg_bg_gfx_ids(tileset: int) -> list[int]:
    if tileset < 0 or (tileset + 1) * 4 > len(FG_AND_BG_GFX_LIST):
        raise ImportErrorWithExit(f"Unsupported foreground/background tileset index: {tileset}")
    source = FG_AND_BG_GFX_LIST[tileset * 4 : tileset * 4 + 4]
    arr = [0, 0, 0, 0]
    for i, gfx_id in enumerate(source):
        arr[3 - i] = gfx_id
    return arr


def level_sprite_gfx_ids(sprite_graphics: int) -> list[int]:
    if sprite_graphics < 0 or (sprite_graphics + 1) * 4 > len(SPRITE_GFX_LIST):
        raise ImportErrorWithExit(f"Unsupported sprite graphics setting: {sprite_graphics}")
    source = SPRITE_GFX_LIST[sprite_graphics * 4 : sprite_graphics * 4 + 4]
    arr = [0, 0, 0, 0]
    for i, gfx_id in enumerate(source):
        arr[3 - i] = gfx_id
    return arr


def lm_super_gfx_entry_words(rom: Rom, level_id: int) -> list[int] | None:
    if not rom.has_lorom_range(LM_SUPER_GFX_POINTER_ADDR, 3):
        return None
    table_addr = rom.get_24(LM_SUPER_GFX_POINTER_ADDR)
    if table_addr in (0x000000, 0xFFFFFF) or not rom.has_lorom_range(table_addr + level_id * 0x20, 0x20):
        return None
    return rom.get_words(table_addr + level_id * 0x20, 16)


def lm_super_gfx_enabled(entry_words: list[int] | None) -> bool:
    return entry_words is not None and (entry_words[0] & 0x8000) != 0


def gfx_slot_number(word: int) -> int:
    return word & 0x0FFF


def resolve_fg_bg_gfx_ids(rom: Rom, level_id: int, header: dict[str, Any]) -> tuple[list[int], dict[str, Any]]:
    entry_words = lm_super_gfx_entry_words(rom, level_id)
    if lm_super_gfx_enabled(entry_words):
        assert entry_words is not None
        gfx_ids = [
            gfx_slot_number(entry_words[4]),  # FG3
            gfx_slot_number(entry_words[5]),  # BG1
            gfx_slot_number(entry_words[6]),  # FG2
            gfx_slot_number(entry_words[7]),  # FG1
        ]
        return gfx_ids, {
            "source": "lunar_magic_super_gfx_bypass",
            "entry_words": [f"0x{word:04X}" for word in entry_words],
            "slot_order": ["FG3", "BG1", "FG2", "FG1"],
        }

    tileset = int(header["tileset"])
    return level_fg_bg_gfx_ids(tileset), {
        "source": "vanilla_fg_bg_gfx_list",
        "tileset": tileset,
        "slot_order": ["FG3", "BG1", "FG2", "FG1"],
    }


def resolve_sprite_gfx_ids(rom: Rom, level_id: int, header: dict[str, Any]) -> tuple[list[int], dict[str, Any]]:
    entry_words = lm_super_gfx_entry_words(rom, level_id)
    if lm_super_gfx_enabled(entry_words):
        assert entry_words is not None
        gfx_ids = [
            gfx_slot_number(entry_words[8]),   # SP4
            gfx_slot_number(entry_words[9]),   # SP3
            gfx_slot_number(entry_words[10]),  # SP2
            gfx_slot_number(entry_words[11]),  # SP1
        ]
        return gfx_ids, {
            "source": "lunar_magic_super_gfx_bypass",
            "entry_words": [f"0x{word:04X}" for word in entry_words],
            "slot_order": ["SP4", "SP3", "SP2", "SP1"],
        }

    sprite_graphics = int(header["sprite_graphics"])
    return level_sprite_gfx_ids(sprite_graphics), {
        "source": "vanilla_sprite_gfx_list",
        "sprite_graphics": sprite_graphics,
        "slot_order": ["SP4", "SP3", "SP2", "SP1"],
    }


def level_fg_bg_vram(rom: Rom, level_id: int, header: dict[str, Any]) -> tuple[bytes, list[dict[str, Any]], dict[str, Any]]:
    vram = bytearray(0x4000)
    uploads: list[dict[str, Any]] = []
    gfx_ids, source = resolve_fg_bg_gfx_ids(rom, level_id, header)
    for slot, gfx_id in enumerate(gfx_ids):
        data, addr, compressed_len = decompress_graphics_file(rom, gfx_id)
        dst = [0x3000, 0x2000, 0x1000, 0x0000][slot]
        copy_len = min(0x1000, len(data))
        vram[dst : dst + copy_len] = data[:copy_len]
        uploads.append(
            {
                "slot": slot,
                "gfx_id": f"{gfx_id:02X}",
                "source_addr": f"0x{addr:06X}",
                "compressed_length": compressed_len,
                "decompressed_length": len(data),
                "vram_offset": f"0x{dst:04X}",
                "tile_start": dst // 32,
                "tile_count": copy_len // 32,
            }
        )
    return bytes(vram), uploads, source


def level_sprite_vram(rom: Rom, level_id: int, header: dict[str, Any]) -> tuple[bytes, list[dict[str, Any]], dict[str, Any]]:
    vram = bytearray(0x4000)
    uploads: list[dict[str, Any]] = []
    gfx_ids, source = resolve_sprite_gfx_ids(rom, level_id, header)
    for slot, gfx_id in enumerate(gfx_ids):
        data, addr, compressed_len = decompress_graphics_file(rom, gfx_id)
        dst = [0x3000, 0x2000, 0x1000, 0x0000][slot]
        copy_len = min(0x1000, len(data))
        vram[dst : dst + copy_len] = data[:copy_len]
        uploads.append(
            {
                "slot": slot,
                "gfx_id": f"{gfx_id:02X}",
                "source_addr": f"0x{addr:06X}",
                "compressed_length": compressed_len,
                "decompressed_length": len(data),
                "vram_base": "0x6000",
                "vram_addr": f"0x{0x6000 + dst // 2:04X}",
                "vram_offset": f"0x{dst:04X}",
                "tile_start": dst // 32,
                "tile_count": copy_len // 32,
            }
        )
    return bytes(vram), uploads, source


def palette_from_tile_word(word: int, level_palette_rgb: list[list[int]]) -> list[list[int]]:
    palette_id = (word >> 10) & 0x07
    start = palette_id * 16
    palette = level_palette_rgb[start : start + 16]
    if len(palette) < 16:
        palette = level_palette_rgb[:16]
    return palette


def blit_8x8_tile(
    rgba: bytearray,
    canvas_width: int,
    x0: int,
    y0: int,
    tile_pixels: list[list[int]],
    palette_rgb: list[list[int]],
    x_flip: bool = False,
    y_flip: bool = False,
) -> None:
    for y in range(8):
        src_y = 7 - y if y_flip else y
        for x in range(8):
            src_x = 7 - x if x_flip else x
            color_index = tile_pixels[src_y][src_x]
            if color_index == 0:
                continue
            out = ((y0 + y) * canvas_width + x0 + x) * 4
            rgb = palette_rgb[color_index]
            rgba[out : out + 4] = bytes([rgb[0], rgb[1], rgb[2], 255])


def write_vram_tile_atlas_png(path: Path, vram_4bpp: bytes, palette_rgb: list[list[int]], columns: int = 16) -> dict[str, Any]:
    return write_4bpp_atlas_png(path, vram_4bpp, palette_rgb, columns=columns)


def write_map16_preview_png(
    path: Path,
    map16_words: list[int],
    vram_4bpp: bytes,
    level_palette_rgb: list[list[int]],
    first_tile: int = 0,
    tile_count: int = 512,
    columns: int = 16,
) -> dict[str, Any]:
    vram_tile_count = len(vram_4bpp) // 32
    rows = (tile_count + columns - 1) // columns
    width = columns * 16
    height = rows * 16
    rgba = bytearray([0, 0, 0, 0] * width * height)

    for local_index in range(tile_count):
        map16_id = first_tile + local_index
        word_offset = map16_id * 4
        if word_offset + 4 > len(map16_words):
            break
        tile_x = (local_index % columns) * 16
        tile_y = (local_index // columns) * 16
        for sub in range(4):
            word = map16_words[word_offset + sub]
            tile_id = word & 0x03FF
            if tile_id >= vram_tile_count:
                continue
            tile_bytes = vram_4bpp[tile_id * 32 : (tile_id + 1) * 32]
            tile_pixels = decode_4bpp_tile(tile_bytes)
            sub_x = tile_x + (8 if sub & 1 else 0)
            sub_y = tile_y + (8 if sub & 2 else 0)
            blit_8x8_tile(
                rgba,
                width,
                sub_x,
                sub_y,
                tile_pixels,
                palette_from_tile_word(word, level_palette_rgb),
                x_flip=(word & 0x4000) != 0,
                y_flip=(word & 0x8000) != 0,
            )

    return {
        "file": str(path),
        "sha1": write_rgba_png(path, width, height, bytes(rgba)),
        "width": width,
        "height": height,
        "first_map16_tile": first_tile,
        "map16_tile_count": tile_count,
        "columns": columns,
        "format": "png_preview_from_map16_tile_words_and_level_vram",
    }


def map16_tile_words(map16_words: list[int], map16_id: int) -> list[int] | None:
    word_offset = map16_id * 4
    if word_offset < 0 or word_offset + 4 > len(map16_words):
        return None
    return map16_words[word_offset : word_offset + 4]


def level_map16_words(rom: Rom, tileset: int) -> list[int]:
    if tileset < 0 or tileset >= len(TILESET_MAP16_POINTERS):
        raise ImportErrorWithExit(f"Unsupported Map16 tileset index: {tileset}")

    pointers: list[int] = []
    global_ptr = 0x8000
    tileset_ptr = TILESET_MAP16_POINTERS[tileset]
    for mask in MAP16_POINTER_MASKS:
        bits = mask
        for _ in range(8):
            if (bits & 0x80) != 0:
                pointers.append(global_ptr)
                global_ptr += 8
            else:
                pointers.append(tileset_ptr)
                tileset_ptr += 8
            bits = (bits << 1) & 0xFF

    if tileset in (0, 7):
        override_ptr = 0x8A70
        for index in range(452, 456):
            pointers[index] = override_ptr
            override_ptr += 8
        for index in range(492, 496):
            pointers[index] = override_ptr
            override_ptr += 8

    words: list[int] = []
    for pointer in pointers:
        words.extend(rom.get_words(0x0D0000 | pointer, 4))
    return words


def blit_map16_tile(
    rgba: bytearray,
    canvas_width: int,
    x0: int,
    y0: int,
    map16_id: int,
    map16_words: list[int],
    vram_4bpp: bytes,
    level_palette_rgb: list[list[int]],
) -> bool:
    words = map16_tile_words(map16_words, map16_id)
    if words is None:
        return False
    vram_tile_count = len(vram_4bpp) // 32
    for sub, word in enumerate(words):
        tile_id = word & 0x03FF
        if tile_id >= vram_tile_count:
            continue
        tile_bytes = vram_4bpp[tile_id * 32 : (tile_id + 1) * 32]
        tile_pixels = decode_4bpp_tile(tile_bytes)
        sub_x = x0 + (8 if sub & 1 else 0)
        sub_y = y0 + (8 if sub & 2 else 0)
        blit_8x8_tile(
            rgba,
            canvas_width,
            sub_x,
            sub_y,
            tile_pixels,
            palette_from_tile_word(word, level_palette_rgb),
            x_flip=(word & 0x4000) != 0,
            y_flip=(word & 0x8000) != 0,
        )
    return True


def write_level_layout_preview_png(
    path: Path,
    width_tiles: int,
    height_tiles: int,
    placed_tiles: list[dict[str, Any]],
    map16_words: list[int],
    vram_4bpp: bytes,
    level_palette_rgb: list[list[int]],
) -> dict[str, Any]:
    width = width_tiles * 16
    height = height_tiles * 16
    rgba = bytearray([0, 0, 0, 0] * width * height)
    rendered = 0
    for placed in placed_tiles:
        x = int(placed["x"])
        y = int(placed["y"])
        if x < 0 or y < 0 or x >= width_tiles or y >= height_tiles:
            continue
        if blit_map16_tile(
            rgba,
            width,
            x * 16,
            y * 16,
            int(placed["map16"]),
            map16_words,
            vram_4bpp,
            level_palette_rgb,
        ):
            rendered += 1
    return {
        "file": str(path),
        "sha1": write_rgba_png(path, width, height, bytes(rgba)),
        "width": width,
        "height": height,
        "width_tiles": width_tiles,
        "height_tiles": height_tiles,
        "placed_tile_count": len(placed_tiles),
        "rendered_tile_count": rendered,
        "format": "partial_level_preview_from_object_map16_ids",
    }


def build_partial_level_tilemap(header: dict[str, Any], objects: list[dict[str, Any]]) -> dict[str, Any]:
    width_tiles = int(header["width_tiles"])
    height_tiles = max(int(header["height_tiles"]), 32)
    placed: list[dict[str, Any]] = []
    unsupported: dict[str, int] = {}

    def place(x: int, y: int, page: int, low: int, source: str) -> None:
        if low == 0xFF:
            return
        if 0 <= x < width_tiles and 0 <= y < height_tiles:
            placed.append({"x": x, "y": y, "map16": page * 0x100 + low, "source": source})

    def fill_rect(x: int, y: int, width: int, height: int, page: int, low: int, source: str) -> None:
        for yy in range(height):
            for xx in range(width):
                place(x + xx, y + yy, page, low, source)

    def render_generic(obj: dict[str, Any], x: int, y: int, width: int, height: int) -> None:
        k = int(obj["object_id"]) - 1
        if 0 <= k < len(GENERIC_REPEATED_TILES):
            page = 1 if k >= 7 else 0
            fill_rect(x, y, width, height, page, GENERIC_REPEATED_TILES[k], f"std_generic_{k:02X}")

    def render_standard_ledge(obj: dict[str, Any], x: int, y: int, size: int) -> None:
        width = (size & 0x0F) + 1
        lower_rows = size >> 4
        for xx in range(width):
            place(x + xx, y, 1, 0x00, "standard_ledge_top")
        for yy in range(1, lower_rows + 1):
            for xx in range(width):
                place(x + xx, y + yy, 0, 0x3F, "standard_ledge_fill")

    def render_ground_edge(obj: dict[str, Any], x: int, y: int, size: int) -> None:
        kind = size & 0x0F
        rows = (size >> 4) + 1
        if kind >= len(GROUND_EDGE_TOP):
            return
        top_page = 1 if kind >= 3 else 0
        place(x, y, top_page, GROUND_EDGE_TOP[kind], "ground_edge_top")
        if rows <= 1:
            return
        mid_page = 1 if kind >= 3 and kind < 9 or kind >= 11 else 0
        place(x, y + 1, mid_page, GROUND_EDGE_MIDDLE1[kind], "ground_edge_middle")
        for yy in range(2, rows):
            place(x, y + yy, mid_page, GROUND_EDGE_MIDDLE2[kind], "ground_edge_middle")
        if kind >= 11:
            place(x, y + rows, 1, GROUND_EDGE_BOTTOM[kind], "ground_edge_bottom")

    def render_vertical_pipe(obj: dict[str, Any], x: int, y: int, size: int) -> None:
        rows = (size >> 4) + 1
        pipe_type = size & 0x0F
        if pipe_type >= len(VERTICAL_PIPE_TOP_LEFT):
            pipe_type = 0
        place(x, y, 1, VERTICAL_PIPE_TOP_LEFT[pipe_type], "vertical_pipe_top_left")
        place(x + 1, y, 1, VERTICAL_PIPE_TOP_RIGHT[pipe_type], "vertical_pipe_top_right")
        for yy in range(1, rows):
            place(x, y + yy, 1, 0x35, "vertical_pipe_shaft_left")
            place(x + 1, y + yy, 1, 0x36, "vertical_pipe_shaft_right")
        if pipe_type >= 2:
            place(x, y + rows, 1, VERTICAL_PIPE_BOTTOM_LEFT[pipe_type], "vertical_pipe_bottom_left")
            place(x + 1, y + rows, 1, VERTICAL_PIPE_BOTTOM_RIGHT[pipe_type], "vertical_pipe_bottom_right")

    def render_horizontal_pipe(obj: dict[str, Any], x: int, y: int, size: int) -> None:
        width = (size & 0x0F) + 2
        pipe_type = (size & 0xF0) >> 3
        for row in range(2):
            tile_kind = min(pipe_type + row, len(HORIZONTAL_PIPE_END) - 1)
            place(x, y + row, 1, HORIZONTAL_PIPE_END[tile_kind], "horizontal_pipe_end")
            for xx in range(1, width - 1):
                place(x + xx, y + row, 1, HORIZONTAL_PIPE_SHAFT[tile_kind], "horizontal_pipe_shaft")
            place(x + width - 1, y + row, 1, HORIZONTAL_PIPE_END[tile_kind], "horizontal_pipe_end")

    def render_midway_goal(obj: dict[str, Any], x: int, y: int, size: int) -> None:
        rows = max(1, size >> 4)
        goal = (size & 0x0F) != 0
        top = GOAL_TOP if goal else MIDWAY_TOP
        middle = GOAL_MIDDLE if goal else MIDWAY_MIDDLE
        bottom = GOAL_BOTTOM if goal else MIDWAY_BOTTOM
        for xx in range(3):
            place(x + xx, y, 0, top[xx], "goal_top" if goal else "midway_top")
            for yy in range(1, rows):
                place(x + xx, y + yy, 0, middle[xx], "goal_middle" if goal else "midway_middle")
            place(x + xx, y + rows, 0, bottom[xx], "goal_bottom" if goal else "midway_bottom")

    def render_small_bush(obj: dict[str, Any], x: int, y: int, size: int) -> None:
        width = (size & 0x0F) + 1
        kind = min(size >> 4, len(SMALL_BUSH_LEFT) - 1)
        place(x, y, 0, SMALL_BUSH_LEFT[kind], "small_bush_left")
        for xx in range(1, max(1, width - 1)):
            place(x + xx, y, 0, SMALL_BUSH_MIDDLE[kind], "small_bush_middle")
        if width > 1:
            place(x + width - 1, y, 0, SMALL_BUSH_RIGHT[kind], "small_bush_right")

    def render_diagonal_pipe(obj: dict[str, Any], x: int, y: int) -> None:
        for yy in range(4):
            for xx in range(4):
                place(x + xx, y + yy, 1, DIAGONAL_PIPE_TILES[yy * 4 + xx], "diagonal_pipe")

    def render_slope_block(obj: dict[str, Any], x: int, y: int, size: int, left: bool) -> None:
        rows = (size >> 4) + 1
        width = max(2, (size & 0x0F) + 1)
        for yy in range(rows):
            solid = min(width, yy + 1)
            start = 0 if left else width - solid
            for xx in range(start, start + solid):
                place(x + xx, y + yy, 0, 0x3F, "slope_fill")
            edge_x = start + solid - 1 if left else start
            place(x + edge_x, y + yy, 1, 0xAA if left else 0xAF, "slope_edge")

    def render_rope_mushroom_top(obj: dict[str, Any], x: int, y: int, size: int) -> None:
        width = (size & 0x0F) + 1
        place(x, y, 1, 0x07, "rope_mushroom_top_left")
        for xx in range(1, width - 1):
            place(x + xx, y, 1, 0x08, "rope_mushroom_top_middle")
        if width > 1:
            place(x + width - 1, y, 1, 0x09, "rope_mushroom_top_right")

    def render_rope_mushroom_column(obj: dict[str, Any], x: int, y: int, size: int) -> None:
        width = (size & 0x0F) + 1
        rows = (size >> 4) + 1
        for yy in range(rows):
            place(x, y + yy, 0, 0x73, "rope_mushroom_column_left")
            for xx in range(1, width - 1):
                place(x + xx, y + yy, 0, 0x74, "rope_mushroom_column_middle")
            if width > 1:
                place(x + width - 1, y + yy, 0, 0x75, "rope_mushroom_column_right")

    def render_underground_ceiling_ledge(obj: dict[str, Any], x: int, y: int, size: int) -> None:
        width = (size & 0x0F) + 1
        filler_rows = size >> 4
        for yy in range(filler_rows):
            fill_rect(x, y + yy, width, 1, 1, 0x65, "underground_ceiling_ledge_fill")
        fill_rect(x, y + filler_rows, width, 1, 1, 0x4E, "underground_ceiling_ledge_bottom")

    def render_underground_ceiling_edge(obj: dict[str, Any], x: int, y: int, size: int) -> None:
        edge_kind = size & 0x0F
        rows = size >> 4
        top_tiles = [0x50, 0x50, 0x51, 0x51]
        bottom_tiles = [0x4D, 0x50, 0x4F, 0x51]
        if edge_kind >= len(top_tiles):
            return
        for yy in range(rows):
            place(x, y + yy, 1, top_tiles[edge_kind], "underground_ceiling_edge")
        place(x, y + rows, 1, bottom_tiles[edge_kind], "underground_ceiling_edge_bottom")

    for obj in objects:
        placement = obj["placement"]
        x = int(placement["x_tile"])
        y = int(placement["y_tile"])
        obj_id = int(obj["object_id"])
        size = int(obj["size_or_type"])
        tileset = int(header["tileset"])
        width = (size & 0x0F) + 1
        height = (size >> 4) + 1

        if 1 <= obj_id <= 0x0E:
            render_generic(obj, x, y, width, height)
        elif obj_id == 0x0F:
            render_vertical_pipe(obj, x, y, size)
        elif obj_id == 0x10:
            render_horizontal_pipe(obj, x, y, size)
        elif obj_id == 0x12:
            render_slope_block(obj, x, y, size, left=((size & 0x0F) <= 2))
        elif obj_id == 0x13:
            render_ground_edge(obj, x, y, size)
        elif obj_id == 0x14:
            render_standard_ledge(obj, x, y, size)
        elif obj_id == 0x15:
            render_midway_goal(obj, x, y, size)
        elif obj_id == 0x17:
            tile_kind = min(size >> 4, len(ROPE_CLOUD_LINE) - 1)
            fill_rect(x, y, width, 1, 1, ROPE_CLOUD_LINE[tile_kind], "rope_cloud_line")
        elif obj_id == 0x1F:
            rows = max(1, size >> 4)
            place(x, y, 1, 0x53, "skinny_vertical_top")
            for yy in range(1, rows):
                place(x, y + yy, 1, 0x54, "skinny_vertical_middle")
            place(x, y + rows, 1, 0x55, "skinny_vertical_bottom")
        elif obj_id == 0x21:
            fill_width = size + 1
            for xx in range(fill_width):
                place(x + xx, y, 1, 0x00, "wide_scale_ledge_top")
            for yy in range(1, 3):
                for xx in range(fill_width):
                    place(x + xx, y + yy, 0, 0x3F, "wide_scale_ledge_fill")
        elif obj_id == 0x39:
            render_diagonal_pipe(obj, x, y)
        elif obj_id == 0x3A:
            render_slope_block(obj, x, y, size, left=True)
        elif obj_id == 0x3B:
            render_slope_block(obj, x, y, size, left=False)
        elif obj_id == 0x3C and tileset in ROPE_TILESETS:
            render_rope_mushroom_top(obj, x, y, size)
        elif obj_id == 0x3D and tileset in ROPE_TILESETS:
            render_rope_mushroom_column(obj, x, y, size)
        elif obj_id == 0x3D and tileset == 3:
            render_underground_ceiling_ledge(obj, x, y, size)
        elif obj_id == 0x3E and tileset == 3:
            render_underground_ceiling_edge(obj, x, y, size)
        elif obj_id == 0x3F:
            render_small_bush(obj, x, y, size)
        elif obj_id == 0x00 and size == 0x41:
            place(x, y, 0, 0x2D, "yoshi_coin_top")
            place(x, y + 1, 0, 0x2E, "yoshi_coin_bottom")
        elif obj_id == 0x00 and size in (0x86, 0x8E):
            place(x, y, 0, 0x6A, "extended_switch_or_goal_marker")
        elif obj_id == 0x00 and size == 0x00:
            continue
        else:
            unsupported[f"{obj_id:02X}"] = unsupported.get(f"{obj_id:02X}", 0) + 1

    return {
        "status": "partial",
        "width_tiles": width_tiles,
        "height_tiles": height_tiles,
        "placed_tiles": placed,
        "placed_tile_count": len(placed),
        "unsupported_object_counts": unsupported,
        "notes": [
            "This is a partial visual tilemap generated from a focused port of common vanilla object placement routines.",
            "It preserves Map16 ids so final rendering uses each Map16 tile word's palette, priority, and flip bits.",
            "It is not yet a complete 1:1 port of ProcessStandardAndTilesetSpecificObjects.",
        ],
    }


def extract_level_layout_preview(
    rom: Rom,
    out_dir: Path,
    level_id: int,
    level_key: str,
    header: dict[str, Any],
    objects: list[dict[str, Any]],
    palette_assets: dict[str, Any],
) -> dict[str, Any]:
    tilemap = build_partial_level_tilemap(header, objects)
    level_palette_rgb = palette_assets["rgb888"]
    vram_4bpp, _uploads, gfx_source = level_fg_bg_vram(rom, level_id, header)
    map16_words = level_map16_words(rom, int(header["tileset"]))
    key = f"level_{level_key}"
    preview_path = out_dir / "levels" / f"{key}_partial_layout.png"
    tilemap_path = out_dir / "levels" / f"{key}_partial_tilemap.json"
    preview = write_level_layout_preview_png(
        preview_path,
        int(tilemap["width_tiles"]),
        int(tilemap["height_tiles"]),
        tilemap["placed_tiles"],
        map16_words,
        vram_4bpp,
        level_palette_rgb,
    )
    preview["file"] = rel(preview_path, out_dir)
    tilemap["preview_png"] = preview
    tilemap["palette_assets"] = {
        "file": palette_assets["file"],
        "source": palette_assets["source"],
    }
    tilemap["map16_pointer_source"] = {
        "source": "native_initialize_map16_pointers",
        "tileset": int(header["tileset"]),
    }
    tilemap["gfx_source"] = gfx_source
    tilemap["file"] = rel(tilemap_path, out_dir)
    tilemap["sha1"] = write_json(tilemap_path, tilemap)
    return tilemap


def extract_level_tileset_assets(
    rom: Rom,
    out_dir: Path,
    level_id: int,
    level_key: str,
    header: dict[str, Any],
    palette_assets: dict[str, Any],
) -> dict[str, Any]:
    tileset = int(header["tileset"])
    fg_palette_index = int(header["fg_palette"])
    level_palette_rgb = palette_assets["rgb888"]
    preview_palette = level_palette_rgb[2 * 16 : 3 * 16]
    if len(preview_palette) < 16:
        preview_palette = level_palette_rgb[:16]

    vram_4bpp, uploads, gfx_source = level_fg_bg_vram(rom, level_id, header)
    map16_words = level_map16_words(rom, tileset)

    tileset_dir = out_dir / "tilesets"
    key = f"level_{level_key}_tileset{tileset}"
    vram_path = tileset_dir / f"{key}_vram.bin"
    atlas_path = tileset_dir / f"{key}_8x8.png"
    map16_path = tileset_dir / f"{key}_map16_preview.png"
    metadata_path = tileset_dir / f"{key}.json"

    atlas = write_vram_tile_atlas_png(atlas_path, vram_4bpp, preview_palette)
    atlas["file"] = rel(atlas_path, out_dir)
    map16_preview = write_map16_preview_png(map16_path, map16_words, vram_4bpp, level_palette_rgb)
    map16_preview["file"] = rel(map16_path, out_dir)

    metadata = {
        "status": "preview",
        "notes": [
            "Uses the foreground/background GFX upload list resolved for this level.",
            "Map16 preview renders raw Map16 tile words; full 1:1 object expansion is still pending.",
            "SNES 4bpp BG graphics do not carry a final palette by themselves; Map16/tilemap words select CGRAM rows through palette bits.",
        ],
        "level_id": level_key,
        "tileset": tileset,
        "fg_palette": fg_palette_index,
        "palette_assets": {
            "file": palette_assets["file"],
            "source": palette_assets["source"],
        },
        "palette_mapping": {
            "tile_word_palette_bits": "bits 10-12",
            "source": "per-level full CGRAM palette",
            "cgram_row_indexing": True,
            "preview_row": 2,
        },
        "gfx_source": gfx_source,
        "map16_pointer_source": {
            "source": "native_initialize_map16_pointers",
            "tileset": tileset,
        },
        "uploads": uploads,
        "vram": {
            "file": rel(vram_path, out_dir),
            "sha1": write_bin(vram_path, vram_4bpp),
            "format": "snes_4bpp_tiles_in_level_vram_order",
            "tile_count": len(vram_4bpp) // 32,
        },
        "atlas_png": atlas,
        "map16_preview_png": map16_preview,
    }
    metadata["file"] = rel(metadata_path, out_dir)
    metadata["sha1"] = write_json(metadata_path, metadata)
    return metadata


def extract_level_sprite_tileset_assets(
    rom: Rom,
    out_dir: Path,
    level_id: int,
    level_key: str,
    header: dict[str, Any],
    palette_assets: dict[str, Any],
) -> dict[str, Any]:
    sprite_graphics = int(header["sprite_graphics"])
    sprite_palette_index = int(header["sprite_palette"])
    level_palette_rgb = palette_assets["rgb888"]
    preview_row = 14
    preview_palette = level_palette_rgb[preview_row * 16 : preview_row * 16 + 16]
    if len(preview_palette) < 16:
        preview_palette = level_palette_rgb[8 * 16 : 9 * 16]

    vram_4bpp, uploads, gfx_source = level_sprite_vram(rom, level_id, header)

    spriteset_dir = out_dir / "spritesets"
    key = f"level_{level_key}_spritegfx{sprite_graphics}"
    vram_path = spriteset_dir / f"{key}_vram.bin"
    atlas_path = spriteset_dir / f"{key}_8x8.png"
    metadata_path = spriteset_dir / f"{key}.json"

    atlas = write_vram_tile_atlas_png(atlas_path, vram_4bpp, preview_palette)
    atlas["file"] = rel(atlas_path, out_dir)

    metadata = {
        "status": "preview",
        "notes": [
            "Uses the sprite GFX upload list resolved for this level.",
            "Tiles are placed in the same $6000-$7FFF sprite VRAM window used by UploadGraphicsFiles.",
            "This atlas is a raw VRAM preview. Exact enemy frames still require each sprite's OAM/tile assembly and palette selection code.",
        ],
        "level_id": level_key,
        "sprite_graphics": sprite_graphics,
        "sprite_palette": sprite_palette_index,
        "palette_assets": {
            "file": palette_assets["file"],
            "source": palette_assets["source"],
        },
        "palette_mapping": {
            "source": "per-level full CGRAM palette rows 8-15",
            "preview_row": preview_row,
            "final_oam_palette_selection_pending": True,
        },
        "gfx_source": gfx_source,
        "uploads": uploads,
        "vram": {
            "file": rel(vram_path, out_dir),
            "sha1": write_bin(vram_path, vram_4bpp),
            "format": "snes_4bpp_tiles_in_sprite_vram_order_0x6000_to_0x7fff",
            "tile_count": len(vram_4bpp) // 32,
        },
        "atlas_png": atlas,
    }
    metadata["file"] = rel(metadata_path, out_dir)
    metadata["sha1"] = write_json(metadata_path, metadata)
    return metadata


def write_json(path: Path, payload: Any) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(payload, indent=2, sort_keys=True)
    path.write_text(text + "\n", encoding="utf-8")
    return sha1_bytes(path.read_bytes())


def write_bin(path: Path, payload: bytes) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)
    return sha1_bytes(payload)


def write_wav_mono16(path: Path, samples: list[int], sample_rate: int = 32000) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(sample_rate)
        payload = bytearray()
        for sample in samples:
            payload.extend(struct.pack("<h", max(-32768, min(32767, sample))))
        wav.writeframes(bytes(payload))
    return sha1_bytes(path.read_bytes())


def parse_spc_upload(payload: bytes) -> tuple[bytearray, list[dict[str, Any]]]:
    ram = bytearray(0x10000)
    blocks: list[dict[str, Any]] = []
    offset = 0
    while offset + 2 <= len(payload):
        count = payload[offset] | (payload[offset + 1] << 8)
        offset += 2
        if count == 0:
            return ram, blocks
        if offset + 2 + count > len(payload):
            raise ImportErrorWithExit("Truncated SPC upload block")
        target = payload[offset] | (payload[offset + 1] << 8)
        offset += 2
        ram[target : target + count] = payload[offset : offset + count]
        blocks.append({"target": f"0x{target:04X}", "length": count})
        offset += count
    raise ImportErrorWithExit("SPC upload stream missing zero terminator")


def decode_brr_sample(data: bytes, start: int) -> tuple[list[int], int]:
    samples: list[int] = []
    old = 0
    older = 0
    offset = start
    while offset + 9 <= len(data):
        command = data[offset]
        shift = command >> 4
        filter_id = (command >> 2) & 0x03
        for i in range(16):
            packed = data[offset + 1 + i // 2]
            nibble = (packed >> (0 if i & 1 else 4)) & 0x0F
            sample = (nibble & 7) - (nibble & 8)
            if shift <= 12:
                sample = (sample << shift) >> 1
            else:
                sample = (sample >> 3) << 12

            if filter_id == 1:
                sample += old + ((-old) >> 4)
            elif filter_id == 2:
                sample += old * 2 + ((-old * 3) >> 5) - older + (older >> 4)
            elif filter_id == 3:
                sample += old * 2 + ((-old * 13) >> 6) - older + ((older * 3) >> 4)

            sample = max(-0x8000, min(0x7FFF, sample))
            sample = (sample & 0x3FFF) - (sample & 0x4000)
            older, old = old, sample
            samples.append(sample * 2)

        offset += 9
        if command & 1:
            break
    return samples, offset - start


def extract_audio_assets(rom: Rom, out_dir: Path) -> dict[str, Any]:
    audio_dir = out_dir / "audio"
    banks = {
        "spc_engine": (0x0E8000, 6321, b"\x00\x00"),
        "spc_samples": (0x0F8000, 28538, b""),
        "spc_level_music_bank": (0x0EAED6, 16899, b""),
        "spc_overworld_music_bank": (0x0E98B1, 5667, b""),
        "spc_credits_music_bank": (0x03E400, 6624, b""),
    }
    payload: dict[str, Any] = {
        "status": "partial",
        "sample_rate": 32000,
        "notes": [
            "Raw SPC upload banks are preserved from the original ROM.",
            "Preview WAVs decode selected BRR samples directly from the vanilla sample directory.",
            "Full SPC/DSP music and SFX sequencing is not ported yet.",
        ],
        "banks": {},
        "decoded_samples": [],
    }
    sample_bank = b""
    for name, (addr, length, suffix) in banks.items():
        data = rom.get_bytes(addr, length) + suffix
        path = audio_dir / f"{name}.bin"
        payload["banks"][name] = {
            "file": rel(path, out_dir),
            "source_addr": f"0x{addr:06X}",
            "length": len(data),
            "sha1": write_bin(path, data),
            "format": "spc_upload_stream",
        }
        if name == "spc_samples":
            sample_bank = data

    ram, upload_blocks = parse_spc_upload(sample_bank)
    payload["sample_upload_blocks"] = upload_blocks
    directory_base = 0x8000
    for sample_id in (9, 14, 16):
        entry = directory_base + sample_id * 4
        start = ram[entry] | (ram[entry + 1] << 8)
        loop = ram[entry + 2] | (ram[entry + 3] << 8)
        decoded, brr_bytes = decode_brr_sample(bytes(ram), start)
        sample_name = f"sample_{sample_id:02d}"
        wav_path = audio_dir / f"{sample_name}.wav"
        payload["decoded_samples"].append(
            {
                "id": sample_id,
                "file": rel(wav_path, out_dir),
                "source": "spc_samples directory",
                "spc_start": f"0x{start:04X}",
                "spc_loop": f"0x{loop:04X}",
                "brr_bytes": brr_bytes,
                "sample_count": len(decoded),
                "sample_rate": 32000,
                "format": "pcm_s16le_wav_from_brr",
                "sha1": write_wav_mono16(wav_path, decoded),
            }
        )

    audio_manifest_path = audio_dir / "audio_manifest.json"
    payload["file"] = rel(audio_manifest_path, out_dir)
    payload["sha1"] = write_json(audio_manifest_path, payload)
    return payload


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
    header = decode_level_header(layer1_raw)
    parsed_layer1 = parse_level_objects(layer1_raw, header, layer_index=0)
    annotate_vanilla_screen_exits(parsed_layer1["screen_exits"], level_id)

    layer2_addr = rom.get_24(0x05E600 + level_id * 3)
    layer2_kind = "object_stream"
    if (layer2_addr & 0xFF0000) == 0xFF0000:
        layer2_addr = (layer2_addr & 0xFFFF) | 0x0C0000
        layer2_raw, layer2_len = unpack_rle(rom, layer2_addr)
        layer2_kind = "rle_background"
    else:
        layer2_len = calc_level_len(rom, layer2_addr)
        layer2_raw = rom.get_bytes(layer2_addr, layer2_len)
    parsed_layer2: dict[str, Any] = {"objects": [], "screen_exits": []}
    if layer2_kind == "object_stream":
        parsed_layer2 = parse_level_objects(layer2_raw, header, layer_index=1)
        annotate_vanilla_screen_exits(parsed_layer2["screen_exits"], level_id)

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
    palette_assets = extract_level_palette_assets(rom, out_dir, level_id, header)
    tileset_assets = extract_level_tileset_assets(rom, out_dir, level_id, level_key, header, palette_assets)
    sprite_tileset_assets = extract_level_sprite_tileset_assets(rom, out_dir, level_id, level_key, header, palette_assets)
    layout_preview = extract_level_layout_preview(
        rom,
        out_dir,
        level_id,
        level_key,
        header,
        parsed_layer1["objects"],
        palette_assets,
    )
    level_path = out_dir / "levels" / f"level_{level_key}.json"
    payload = {
        "level_id": level_key,
        "header": header,
        "layout": {
            "vertical": header["vertical"],
            "screens": header["screens"],
            "width_tiles": header["width_tiles"],
            "height_tiles": header["height_tiles"],
            "tile_size_px": 16,
            "width_px": header["width_tiles"] * 16,
            "height_px": header["height_tiles"] * 16,
        },
        "layer1": {
            "source_addr": f"0x{layer1_addr:06X}",
            "length": layer1_len,
            "raw": list(layer1_raw),
            "objects": parsed_layer1["objects"],
        },
        "layer2": {
            "source_addr": f"0x{layer2_addr:06X}",
            "length": layer2_len,
            "kind": layer2_kind,
            "raw": list(layer2_raw),
            "objects": parsed_layer2["objects"],
        },
        "sprite_layer": {
            "source_addr": f"0x{sprite_addr:06X}",
            "length": sprite_len,
            "raw": list(sprite_raw),
            "header": parsed_sprites["header"],
            "sprites": parsed_sprites["sprites"],
        },
        "screen_exits": parsed_layer1["screen_exits"],
        "palette_assets": {
            "file": palette_assets["file"],
            "source": palette_assets["source"],
            "back_area_color": palette_assets["back_area_color"],
        },
        "tileset_assets": tileset_assets,
        "sprite_tileset_assets": sprite_tileset_assets,
        "layout_preview": layout_preview,
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
        "tileset_assets": {
            "file": tileset_assets["file"],
            "atlas_png": tileset_assets["atlas_png"]["file"],
            "map16_preview_png": tileset_assets["map16_preview_png"]["file"],
        },
        "sprite_tileset_assets": {
            "file": sprite_tileset_assets["file"],
            "atlas_png": sprite_tileset_assets["atlas_png"]["file"],
            "status": sprite_tileset_assets["status"],
            "sprite_graphics": sprite_tileset_assets["sprite_graphics"],
        },
        "palette_assets": {
            "file": palette_assets["file"],
            "source": palette_assets["source"],
        },
        "layout_preview": {
            "file": layout_preview["file"],
            "preview_png": layout_preview["preview_png"]["file"],
            "status": layout_preview["status"],
            "placed_tile_count": layout_preview["placed_tile_count"],
        },
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
        "secondary_entrance_type_05fe00": list(rom.get_bytes(0x05FE00, 0x200)),
    }
    secondary_path = out_dir / "levels" / "secondary_tables.json"
    assets["secondary_tables"] = {
        "file": rel(secondary_path, out_dir),
        "sha1": write_json(secondary_path, secondary_payload),
    }

    player_sprite_palette_words = build_player_sprite_palette_words(rom, player=0)
    player_sprite_palette_rgb = snes_words_to_rgb(player_sprite_palette_words)

    for name, pointer_addr in {"gfx32": 0x00B8D8, "gfx33": 0x00B88B}.items():
        gfx_addr = 0x080000 | rom.get_word(pointer_addr)
        gfx_data, compressed_len = smw_decomp(rom, gfx_addr)
        gfx_path = out_dir / "gfx" / f"{name}.bin"
        atlas_path = out_dir / "player" / f"{name}_player_palette0.png"
        atlas = write_4bpp_atlas_png(atlas_path, gfx_data, player_sprite_palette_rgb)
        atlas["file"] = rel(atlas_path, out_dir)
        assets[name] = {
            "file": rel(gfx_path, out_dir),
            "source_addr": f"0x{gfx_addr:06X}",
            "compressed_length": compressed_len,
            "decompressed_length": len(gfx_data),
            "format": "snes_4bpp_planar",
            "sha1": write_bin(gfx_path, gfx_data),
            "atlas_png": atlas,
        }

    player_graphics_path = out_dir / "player" / "player_graphics.json"
    player_graphics_payload = {
        "status": "partial",
        "source_gfx": {
            "gfx32": assets["gfx32"]["atlas_png"]["file"],
            "gfx33": assets["gfx33"]["atlas_png"]["file"],
        },
        "palette": {
            "source": "palettes/global_palettes.json",
            "set": "player",
            "variant": 0,
            "snes_bgr555": player_sprite_palette_words,
            "colors": player_sprite_palette_rgb,
            "layout": "full OBJ palette row 8: colors 0-1 fixed, 2-5 object row, 6-15 dynamic Mario palette from $00B2C8",
        },
        "tile_pointer_tables": {
            "head": list(rom.get_bytes(0x00E00C, 192)),
            "body": list(rom.get_bytes(0x00E0CC, 192)),
            "walking_pose_count": list(rom.get_bytes(0x00DC78, 4)),
        },
        "oam_tables": extract_player_oam_tables(rom),
        "categories": {
            "player": {
                "small": [],
                "big": [],
                "cape": [],
                "fire": [],
                "yoshi": [],
            },
            "states_pending_direct_oam_port": [
                "idle",
                "walk",
                "run",
                "jump",
                "spin_jump",
                "fall",
                "duck",
                "climb",
                "swim",
                "cape_flight",
                "powerup_transition",
                "damage_transition",
            ],
        },
        "notes": [
            "PNG atlases are usable in Godot now.",
            "The runtime uses native PlayerGFXRt OAM placement tables for the first big-Mario pose set.",
            "Full frame/state categorization, cape, Yoshi, powerup transition, and damage transition rendering are still pending.",
        ],
    }
    assets["player_graphics"] = {
        "file": rel(player_graphics_path, out_dir),
        "sha1": write_json(player_graphics_path, player_graphics_payload),
    }
    assets["audio"] = extract_audio_assets(rom, out_dir)

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
