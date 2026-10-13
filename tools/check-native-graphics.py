#!/usr/bin/env python3
"""Compare all native graphics against ROM decoding and explicit upload rules."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys

import smw_import as smw

ROOT = Path(__file__).resolve().parents[1]


def planar_pixels(data: bytes, bpp: int) -> bytes:
    result = bytearray()
    for start in range(0, len(data), bpp * 8):
        tile = data[start:start + bpp * 8]
        planes = [tile[p // 2 * 16 + p % 2::2][:8] for p in range(min(bpp, 2))]
        if bpp == 3:
            planes.append(tile[16:24])
        elif bpp == 4:
            planes.extend([tile[16::2], tile[17::2]])
        for y in range(8):
            for x in range(8):
                result.append(sum(((plane[y] >> (7 - x)) & 1) << p for p, plane in enumerate(planes)))
    return bytes(result)


def upload(raw: bytes, gfx_id: int, tileset: int) -> bytes:
    result = bytearray(smw.expand_3bpp_to_4bpp(raw))
    for tile in range(128):
        if gfx_id == 0x1E or (gfx_id == 8 and tileset >= 0x11) or (gfx_id in (1, 0x17) and tile in (0, 1, 16, 17)):
            smw.patch_mask_rows(result, tile * 32 + 16, tile * 32 + 32)
    return bytes(result)


def same(path: Path, expected: bytes) -> None:
    actual = path.read_bytes()
    if actual != expected:
        first = next((i for i, (a, b) in enumerate(zip(actual, expected)) if a != b), min(len(actual), len(expected)))
        raise ValueError(f"{path.name}: first byte difference at {first:#x}; lengths {len(actual)}/{len(expected)}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rom", type=Path, default=Path(os.environ.get("SMW_ROM_PATH", "")))
    parser.add_argument("--out", type=Path, default=ROOT / "generated/native-graphics-check")
    parser.add_argument("--oracle-boot", action="store_true", help="Also verify boot uploads against original-ROM Mesen execution")
    args = parser.parse_args()
    output = args.out.resolve()
    if output.is_relative_to(ROOT) and subprocess.run(["git", "check-ignore", "-q", "--", str(output)], cwd=ROOT).returncode:
        raise ValueError("Graphics check output must be ignored by git")
    rom = smw.Rom.load(args.rom)
    subprocess.run(["dotnet", "run", "--project", str(ROOT / "tools/SmwAssetTool/SmwAssetTool.csproj"),
                    "--", "extract-graphics", str(args.rom.resolve()), str(output)], cwd=ROOT, check=True)
    manifest = json.loads((output / "native_graphics_manifest.json").read_text())
    assert manifest["source_rom_sha1"] == smw.SMW_SHA1_US
    assert len(manifest["files"]) == 52
    source_files = {}
    for gfx_id, entry in enumerate(manifest["files"]):
        addr = smw.graphics_file_address(rom, gfx_id) if gfx_id < 50 else 0x080000 | rom.get_word(0x00B8D8 if gfx_id == 50 else 0x00B88B)
        raw, compressed_length = smw.smw_decomp(rom, addr)
        source_files[gfx_id] = raw
        same(output / entry["raw_file"], raw)
        assert entry["id"] == f"{gfx_id:02X}" and entry["source_addr"] == f"0x{addr:06X}"
        assert entry["compressed_length"] == compressed_length and entry["decompressed_length"] == len(raw)
        if gfx_id == 0x27:
            expected_format = "smw_mode7_packed_3bpp"
            pixels = bytes((int.from_bytes(raw[i:i + 3], "big") >> shift) & 7 for i in range(0, len(raw), 3) for shift in range(21, -1, -3))
            assert entry.get("planar4_file") is None
        else:
            bpp = 2 if gfx_id in (0x28, 0x29, 0x2A, 0x2B, 0x2F) else 4 if gfx_id == 0x32 else 3
            expected_format = f"snes_{bpp}bpp_planar"
            pixels = planar_pixels(raw, bpp)
            expanded = raw if bpp == 4 else smw.expand_3bpp_to_4bpp(raw) if bpp == 3 else b"".join(raw[i:i + 16] + bytes(16) for i in range(0, len(raw), 16))
            same(output / entry["planar4_file"], expanded)
            if bpp == 3 and len(raw) == 0xC00:
                same(output / f"gfx/full/gfx{gfx_id:02X}.upload-00.bin", upload(raw, gfx_id, 0))
        assert entry["format"] == expected_format and entry["tile_count"] == len(pixels) // 64
        same(output / entry["pixels_file"], pixels)
    same(output / "gfx/full/gfx08.upload-11.bin", upload(source_files[8], 8, 0x11))
    same(output / "gfx/full/gfx01.special-world.bin", upload(source_files[0x31] + source_files[1][0x600:], 1, 0))
    print("smw native graphics: all 52 raw files, indexed pixels, planar expansion and contextual uploads match")
    if args.oracle_boot:
        oracle = output / "oracle-boot"
        subprocess.run([str(ROOT / "tools/parity.sh"), "oracle", "--rom", str(args.rom.resolve()),
                        "--frames", "120", "--snapshot-at-exec", "0x00AB01", "--out", str(oracle)], cwd=ROOT, check=True)
        vram = (oracle / "exec-00AB01/vram.bin").read_bytes()
        # Normal upload return ($00AB01), before later dynamic player DMA
        # overwrites tiles 0/1/16/17/127. All bytes must match at this phase.
        checks = [(0, 0xC000, "upload-00")]
        checks.extend((gfx_id, 0x8000 + (gfx_id - 0x28) * 0x800, "raw") for gfx_id in range(0x28, 0x2C))
        for gfx_id, offset, kind in checks:
            expected = (output / f"gfx/full/gfx{gfx_id:02X}.{kind}.bin").read_bytes()
            actual = vram[offset:offset + len(expected)]
            if actual != expected:
                raise ValueError(f"Original boot VRAM differs from native GFX{gfx_id:02X}")
        print("smw native graphics: boot GFX00 and all four 2bpp font files match every original VRAM byte")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, AssertionError, subprocess.CalledProcessError) as error:
        print(f"smw native graphics: failed: {error}", file=sys.stderr)
        raise SystemExit(1)
