#!/usr/bin/env python3
"""Verify native ROM visuals, Godot image transfer, and independence from generated art."""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys
import tempfile

from PIL import Image
import smw_import as smw

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("smw_native_graphics_check", ROOT / "tools/check-native-graphics.py")
checker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checker)
planar_pixels, same = checker.planar_pixels, checker.same


def words(values: list[int]) -> bytes:
    return struct.pack(f"<{len(values)}H", *values)


def compare_png(path: Path, rgba: Path, width: int, height: int) -> None:
    with Image.open(path) as png:
        assert png.size == (width, height), f"{path.name}: dimensions differ"
        same(rgba, png.convert("RGBA").tobytes())


def atlas_bytes(pixels: bytes, palette: list[list[int]], width: int, height: int) -> bytes:
    rgba = bytearray(width * height * 4)
    columns = width // 8
    for tile in range(len(pixels) // 64):
        for y in range(8):
            for x in range(8):
                index = pixels[tile * 64 + y * 8 + x]
                offset = ((tile // columns * 8 + y) * width + tile % columns * 8 + x) * 4
                rgba[offset:offset + 4] = bytes(palette[index] + [255 if index else 0])
    return bytes(rgba)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rom", type=Path, default=Path(os.environ.get("SMW_ROM_PATH", "")))
    parser.add_argument("--pack", type=Path, default=ROOT / "generated/smw")
    parser.add_argument("--out", type=Path, default=ROOT / "generated/native-visuals-check")
    parser.add_argument("--oracle-palette", action="store_true")
    args = parser.parse_args()
    output = args.out.resolve()
    if output.is_relative_to(ROOT) and subprocess.run(["git", "check-ignore", "-q", "--", str(output)], cwd=ROOT).returncode:
        raise ValueError("Native visual check output must be ignored by git")
    rom = smw.Rom.load(args.rom)
    subprocess.run(["dotnet", "run", "--project", str(ROOT / "tools/SmwAssetTool/SmwAssetTool.csproj"),
                    "--", "extract-level-graphics", str(args.rom.resolve()), str(output)], cwd=ROOT, check=True)
    manifest = json.loads((output / "native_level_graphics.json").read_text())
    assert manifest["source_rom_sha1"] == smw.SMW_SHA1_US
    metadata = Path(tempfile.mkdtemp(prefix="metadata-only-", dir=output))
    # Separate local pack: preserve the user's normal generated assets untouched.
    for path in args.pack.rglob("*.json"):
        if path.relative_to(args.pack).parts[0] in ("player", "palettes"):
            continue
        if (path.name == "secondary_tables.json" or
                re.fullmatch(r"level_[0-9A-Fa-f]{3}(?:_partial_tilemap)?\.json", path.name)):
            continue
        destination = metadata / path.relative_to(args.pack)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, destination)
    if any(path.suffix != ".json" for path in metadata.rglob("*") if path.is_file()):
        raise ValueError("Metadata-only pack contains non-JSON data")
    image_count = 0
    for entry in manifest["levels"]:
        level = entry["id"]
        directory = output / f"level_{level}"
        address = rom.get_24(0x05E000 + int(level, 16) * 3)
        header = smw.decode_level_header(rom.get_bytes(address, 5))
        same(directory / "header.bin", rom.get_bytes(address, 5))
        sky, palette = smw.build_vanilla_level_palette_words(rom, header)
        assert sky == entry["sky"]
        same(directory / "palette_entry.bin", words(palette))
        foreground, _, _ = smw.level_fg_bg_vram(rom, int(level, 16), header)
        sprites, _, _ = smw.level_sprite_vram(rom, int(level, 16), header)
        same(directory / "foreground.bin", foreground)
        same(directory / "sprites.bin", sprites)
        same(directory / "map16.bin", words(smw.level_map16_words(rom, header["tileset"])))
        background = entry.get("background")
        if background:
            raw, size = smw.unpack_rle(rom, int(background["source_addr"], 16))
            same(directory / "background.bin", raw)
            assert size == background["compressed_length"]
        images = {item["file"]: item for item in entry["images"]}
        for file, image in images.items():
            path = directory / file
            assert hashlib.sha256(path.read_bytes()).hexdigest().upper() == image["sha256"]
            if file.startswith("sprites-"):
                row = 8 + int(file.removeprefix("sprites-").removesuffix(".rgba"))
                colors = smw.snes_words_to_rgb(palette[row * 16:row * 16 + 16])
                same(path, atlas_bytes(planar_pixels(sprites, 4), colors, image["width"], image["height"]))
            else:
                png = args.pack / (f"tilesets/level_{level}_tileset{header['tileset']}_map16_preview.png" if file == "map16.rgba" else
                                   f"levels/level_{level}_layer2_background.png" if file == "background.rgba" else
                                   f"player/gfx32_player_palette{file.removeprefix('player-').removesuffix('.rgba')}.png")
                compare_png(png, path, image["width"], image["height"])
            image_count += 1
        env = os.environ.copy()
        env.update(SMW_ROM_PATH=str(args.rom.resolve()), SMW_ASSET_BASE_PATH=str(metadata))
        result = subprocess.run([str(ROOT / "tools/run.sh"), "--headless", "--no-import", "--no-build", "--quit-after", "3",
                                 "--smw-test-level=" + level, "--smw-no-audio"], cwd=ROOT, env=env,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=30)
        (directory / "godot.log").write_text(result.stdout)
        if result.returncode or "ERROR:" in result.stdout or "SCRIPT ERROR:" in result.stdout:
            raise ValueError(f"Godot metadata-only boot failed for {level}; inspect godot.log")
        assert "smw-rom: source=native sha1=" + smw.SMW_SHA1_US in result.stdout
        assert "smw-native-player: tables=12 source=rom" in result.stdout
        assert f"smw-native-streams: level={level} " in result.stdout
        tile_count = {"105": 1468, "1CB": 585}[level]
        assert f"smw-native-terrain: level={level} source=rom state=fresh-development-entry tiles={tile_count} " in result.stdout
        transferred = {}
        for line in result.stdout.splitlines():
            if line.startswith("smw-native-texture:"):
                fields = dict(re.findall(r"(\w+)=([^\s]+)", line))
                transferred[fields["name"]] = fields
        for file, image in images.items():
            name = file.removesuffix(".rgba")
            assert name in transferred, f"Godot did not build {name} from ROM"
            assert transferred[name]["sha256"] == image["sha256"], f"Godot image bytes differ: {name}"
            assert (int(transferred[name]["width"]), int(transferred[name]["height"])) == (image["width"], image["height"])
        print(f"smw native visuals: level {level} pixels and Godot image transfer match; JSON-only boot passed")
    commands = output / "native-route.commands"
    commands.write_text("pause\nstate route_before\nscreen_exit 7\nstate route_pipe\nscreen_exit 1\nstate route_return\nquit\n")
    env = os.environ.copy()
    env.update(SMW_ROM_PATH=str(args.rom.resolve()), SMW_ASSET_BASE_PATH=str(metadata))
    route = subprocess.run([str(ROOT / "tools/run.sh"), "--headless", "--no-import", "--no-build", "--smw-test-level=105",
                            "--smw-no-audio", "--smw-debug-command-file=" + str(commands)], cwd=ROOT, env=env,
                           stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=30)
    (output / "native-route.log").write_text(route.stdout)
    if route.returncode or "ERROR:" in route.stdout or "unresolved" in route.stdout:
        raise ValueError("Native entrance route failed; inspect native-route.log")
    assert "smw-runtime: entrance level=1CB source=1CB secondary=0 settings=4" in route.stdout
    assert "smw-runtime: entrance level=105 source=1CB secondary=1 settings=6" in route.stdout
    assert "smw-native-terrain: level=1CB source=rom " in route.stdout
    assert "smw-native-terrain: level=105 source=rom " in route.stdout
    print("smw native visuals: 105 -> 1CB -> 105 route works without entrance, level-stream, or terrain JSON")
    if args.oracle_palette:
        oracle = output / "oracle-palette"
        subprocess.run([str(ROOT / "tools/parity.sh"), "oracle", "--rom", str(args.rom.resolve()), "--frames", "120",
                        "--snapshot-at-exec", "0x00ACEC", "--out", str(oracle)], cwd=ROOT, check=True)
        ram = (oracle / "exec-00ACEC/wram.bin").read_bytes()
        same(output / "boot-palette-base.bin", ram[0x703:0x903])
        assert int.from_bytes(ram[0x703 + 0x102:0x703 + 0x104], "little") == 0x7FFF
        print("smw native visuals: all 512 boot palette mirror bytes match original-ROM execution")
    (output / "verification.json").write_text(json.dumps({"source_rom_sha1": smw.SMW_SHA1_US, "images_compared": image_count,
        "metadata_only_boots": len(manifest["levels"]), "oracle_palette_checked": args.oracle_palette,
        "metadata_pack": str(metadata), "player_metadata_imported": False, "palette_metadata_imported": False,
        "level_stream_metadata_imported": False, "entrance_metadata_imported": False,
        "terrain_metadata_imported": {"105": False, "1CB": False},
        "main_secondary_route": "105 -> 1CB -> 105",
        "scope": "native ROM visual data and CPU image transfer; framebuffer/gameplay parity remains unproven"}, indent=2) + "\n")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, AssertionError, subprocess.SubprocessError) as error:
        print(f"smw native visuals: failed: {error}", file=sys.stderr)
        raise SystemExit(1)
