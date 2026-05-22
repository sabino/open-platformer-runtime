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
import zlib
from dataclasses import dataclass
from pathlib import Path
from typing import Any


SMW_SHA1_US = "6B47BB75D16514B6A476AA0C73A683A2A4C18765"
SCHEMA_VERSION = 1
GFX_FILE_COUNT = 50
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
LEVEL_VERTICAL_TABLE = [
    0x00, 0x00, 0x80, 0x01, 0x81, 0x02, 0x82, 0x03,
    0x83, 0x00, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80,
]


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
                "raw": [b0, b1, b2],
                "object_id": obj_id,
                "size_or_type": b2,
                "extra": extra,
                "placement": placement,
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


def graphics_file_address(rom: Rom, gfx_id: int) -> int:
    if gfx_id < 0 or gfx_id >= GFX_FILE_COUNT:
        raise ImportErrorWithExit(f"GFX id out of range: {gfx_id}")
    lo = rom.get_byte(0x00B992 + gfx_id)
    hi = rom.get_byte(0x00B9C4 + gfx_id)
    bank = rom.get_byte(0x00B9F6 + gfx_id)
    return (bank << 16) | (hi << 8) | lo


def decompress_graphics_file(rom: Rom, gfx_id: int) -> tuple[bytes, int, int]:
    addr = graphics_file_address(rom, gfx_id)
    data, compressed_len = smw_decomp(rom, addr)
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


def level_fg_bg_vram(rom: Rom, tileset: int) -> tuple[bytes, list[dict[str, Any]]]:
    vram = bytearray(0x2000)
    uploads: list[dict[str, Any]] = []
    gfx_ids = level_fg_bg_gfx_ids(tileset)
    for slot, gfx_id in enumerate(gfx_ids):
        data, addr, compressed_len = decompress_graphics_file(rom, gfx_id)
        dst = [0x1800, 0x1000, 0x0800, 0x0000][slot]
        copy_len = min(0x800, len(data))
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
    return bytes(vram), uploads


def palette_from_tile_word(word: int, fg_palettes_rgb: list[list[int]]) -> list[list[int]]:
    palette_id = (word >> 10) & 0x07
    palette_index = palette_id - 2 if palette_id >= 2 else palette_id
    start = max(0, min(palette_index, 5)) * 16
    palette = fg_palettes_rgb[start : start + 16]
    if len(palette) < 16:
        palette = fg_palettes_rgb[:16]
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
    fg_palettes_rgb: list[list[int]],
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
                palette_from_tile_word(word, fg_palettes_rgb),
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


def extract_level_tileset_assets(rom: Rom, out_dir: Path, level_key: str, header: dict[str, Any]) -> dict[str, Any]:
    tileset = int(header["tileset"])
    fg_palette_index = int(header["fg_palette"])
    fg_palettes_rgb = snes_words_to_rgb(rom.get_words(0x00B190, 96))
    player_safe_palette = fg_palettes_rgb[fg_palette_index * 16 : fg_palette_index * 16 + 16]
    if len(player_safe_palette) < 16:
        player_safe_palette = fg_palettes_rgb[:16]

    vram_4bpp, uploads = level_fg_bg_vram(rom, tileset)
    map16_words = rom.get_words(0x0D8000, (0xA100 - 0x8000) // 2)

    tileset_dir = out_dir / "tilesets"
    key = f"level_{level_key}_tileset{tileset}"
    vram_path = tileset_dir / f"{key}_vram.bin"
    atlas_path = tileset_dir / f"{key}_8x8.png"
    map16_path = tileset_dir / f"{key}_map16_preview.png"
    metadata_path = tileset_dir / f"{key}.json"

    atlas = write_vram_tile_atlas_png(atlas_path, vram_4bpp, player_safe_palette)
    atlas["file"] = rel(atlas_path, out_dir)
    map16_preview = write_map16_preview_png(map16_path, map16_words, vram_4bpp, fg_palettes_rgb)
    map16_preview["file"] = rel(map16_path, out_dir)

    metadata = {
        "status": "preview",
        "notes": [
            "Uses the vanilla foreground/background GFX upload list for this level tileset.",
            "Map16 preview renders raw Map16 tile words; object expansion into the level tilemap is still pending.",
        ],
        "level_id": level_key,
        "tileset": tileset,
        "fg_palette": fg_palette_index,
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
    header = decode_level_header(layer1_raw)
    parsed_layer1 = parse_level_objects(layer1_raw, header, layer_index=0)

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
    tileset_assets = extract_level_tileset_assets(rom, out_dir, level_key, header)
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
        "tileset_assets": tileset_assets,
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
        atlas_path = out_dir / "player" / f"{name}_player_palette0.png"
        atlas = write_4bpp_atlas_png(atlas_path, gfx_data, palettes_payload["player"]["rgb888"][:16])
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
            "colors": palettes_payload["player"]["rgb888"][:16],
        },
        "tile_pointer_tables": {
            "head": list(rom.get_bytes(0x00E00C, 192)),
            "body": list(rom.get_bytes(0x00E0CC, 192)),
            "walking_pose_count": list(rom.get_bytes(0x00DC78, 4)),
        },
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
            "Frame/state categorization is intentionally empty until PlayerGFXRt OAM tables are ported 1:1.",
        ],
    }
    assets["player_graphics"] = {
        "file": rel(player_graphics_path, out_dir),
        "sha1": write_json(player_graphics_path, player_graphics_payload),
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
