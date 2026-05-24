#!/usr/bin/env python3
"""Convert SNES TAS movie input logs to smw-godot input scripts."""

from __future__ import annotations

import argparse
import io
import struct
import sys
import zipfile
import zlib
from dataclasses import dataclass
from pathlib import Path


BUTTON_NAMES = {
    "P1 Up": "Up",
    "P1 Down": "Down",
    "P1 Left": "Left",
    "P1 Right": "Right",
    "P1 Select": "Select",
    "P1 Start": "Start",
    "P1 Y": "Y",
    "P1 B": "B",
    "P1 X": "X",
    "P1 A": "A",
    "P1 L": "L",
    "P1 R": "R",
}

SCRIPT_ORDER = [
    "Up",
    "Down",
    "Left",
    "Right",
    "Select",
    "Start",
    "Y",
    "B",
    "X",
    "A",
    "L",
    "R",
]

LSMV_ORDER = ["B", "Y", "Select", "Start", "Up", "Down", "Left", "Right", "A", "X", "L", "R"]

SMV_BUTTONS = [
    (0x0800, "Up"),
    (0x0400, "Down"),
    (0x0200, "Left"),
    (0x0100, "Right"),
    (0x2000, "Select"),
    (0x1000, "Start"),
    (0x4000, "Y"),
    (0x8000, "B"),
    (0x0040, "X"),
    (0x0080, "A"),
    (0x0020, "L"),
    (0x0010, "R"),
]


class ConversionError(Exception):
    pass


@dataclass
class MovieConversion:
    source: str
    movie_format: str
    frames: list[tuple[str, ...]]
    directives: tuple[str, ...] = ()
    sram: bytes | None = None


def collapse_segments(frames: list[tuple[str, ...]]) -> list[tuple[int, tuple[str, ...]]]:
    segments: list[tuple[int, tuple[str, ...]]] = []
    last_buttons: tuple[str, ...] | None = None
    last_count = 0
    for buttons in frames:
        if buttons == last_buttons:
            last_count += 1
            continue
        if last_buttons is not None:
            segments.append((last_count, last_buttons))
        last_buttons = buttons
        last_count = 1
    if last_buttons is not None:
        segments.append((last_count, last_buttons))
    if not segments:
        raise ConversionError("movie contained no frame rows after slicing")
    return segments


def read_movie(path: Path, max_source_frames: int | None) -> MovieConversion:
    try:
        data = path.read_bytes()
    except OSError as exc:
        raise ConversionError(f"unable to read {path}") from exc
    return read_movie_bytes(str(path), data, max_source_frames)


def read_movie_bytes(source: str, data: bytes, max_source_frames: int | None) -> MovieConversion:
    if data.startswith(b"SMV\x1a"):
        frames, sram, directives = convert_smv(data, max_source_frames)
        return MovieConversion(source, "Snes9x SMV", frames, directives, sram)

    try:
        with zipfile.ZipFile(io.BytesIO(data)) as archive:
            names = archive.namelist()
            if "Input Log.txt" in names:
                text = archive.read("Input Log.txt").decode("utf-8-sig")
                return MovieConversion(source, "BizHawk BK2", convert_bk2_input_log(text, max_source_frames))
            if "input" in names:
                text = archive.read("input").decode("utf-8-sig")
                return MovieConversion(source, "lsnes LSMV", convert_lsmv_input(text, max_source_frames))

            movie_names = [name for name in names if name.lower().endswith((".bk2", ".lsmv", ".smv"))]
            if len(movie_names) == 1:
                name = movie_names[0]
                nested = read_movie_bytes(f"{source}:{name}", archive.read(name), max_source_frames)
                return nested
            if movie_names:
                raise ConversionError(f"{source} contains multiple movie files; pass one directly")
    except zipfile.BadZipFile as exc:
        raise ConversionError(f"{source} is not a supported BK2/LSMV/SMV movie or TASVideos zip") from exc

    raise ConversionError(f"{source} does not contain Input Log.txt, an LSMV input file, or a single nested movie")


def parse_log_key(line: str) -> list[str]:
    if not line.startswith("LogKey:"):
        raise ConversionError("Input Log.txt is missing a LogKey line")
    tokens = line[len("LogKey:") :].split("|")
    if tokens and tokens[-1] == "":
        tokens.pop()
    return [token.lstrip("#") for token in tokens]


def row_groups(line: str) -> list[str]:
    if not line.startswith("|") or not line.endswith("|"):
        raise ConversionError(f"invalid input row: {line!r}")
    return line[1:-1].split("|")


def p1_columns(log_keys: list[str], groups: list[str]) -> list[tuple[str, int]]:
    if len(groups) == 2 and len(log_keys) >= 3:
        p1_keys = log_keys[len(groups[0]) :]
        if len(groups[1]) != len(p1_keys):
            raise ConversionError("input row width does not match LogKey controller width")
        return [(BUTTON_NAMES[key], i) for i, key in enumerate(p1_keys) if key in BUTTON_NAMES]

    if len(groups) != len(log_keys):
        raise ConversionError("unsupported BK2 input grouping")
    return [(BUTTON_NAMES[key], i) for i, key in enumerate(log_keys) if key in BUTTON_NAMES]


def buttons_from_row(line: str, columns: list[tuple[str, int]]) -> tuple[str, ...]:
    groups = row_groups(line)
    controller = groups[1] if len(groups) == 2 else "".join(groups)
    pressed = [name for name, index in columns if index < len(controller) and controller[index] != "."]
    return tuple(name for name in SCRIPT_ORDER if name in pressed)


def convert_bk2_input_log(text: str, max_source_frames: int | None) -> list[tuple[str, ...]]:
    lines = text.splitlines()
    try:
        start = lines.index("[Input]")
    except ValueError as exc:
        raise ConversionError("Input Log.txt is missing [Input]") from exc

    if start + 1 >= len(lines):
        raise ConversionError("Input Log.txt has no LogKey line")
    log_keys = parse_log_key(lines[start + 1])

    frames: list[tuple[str, ...]] = []
    columns: list[tuple[str, int]] | None = None
    for line in lines[start + 2 :]:
        if line == "[/Input]":
            break
        if not line:
            continue
        groups = row_groups(line)
        if columns is None:
            columns = p1_columns(log_keys, groups)
        frames.append(buttons_from_row(line, columns))
        if max_source_frames is not None and len(frames) >= max_source_frames:
            break

    if not frames:
        raise ConversionError("Input Log.txt contained no frame rows")
    return frames


def convert_lsmv_input(text: str, max_source_frames: int | None) -> list[tuple[str, ...]]:
    frames: list[tuple[str, ...]] = []
    for line in text.splitlines():
        if not line.startswith("F"):
            continue
        if "|" not in line:
            raise ConversionError(f"invalid LSMV input row: {line!r}")
        controller = line.split("|", 1)[1][: len(LSMV_ORDER)]
        if len(controller) != len(LSMV_ORDER):
            raise ConversionError(f"unsupported LSMV controller row width: {line!r}")
        pressed = [
            button
            for char, button in zip(controller, LSMV_ORDER)
            if char != "." and not char.isspace()
        ]
        frames.append(tuple(button for button in SCRIPT_ORDER if button in pressed))
        if max_source_frames is not None and len(frames) >= max_source_frames:
            break
    if not frames:
        raise ConversionError("LSMV input contained no frame rows")
    return frames


def extract_smv_sram(data: bytes, save_offset: int, controller_offset: int) -> bytes | None:
    if controller_offset <= save_offset:
        return None
    try:
        sram = zlib.decompress(data[save_offset:controller_offset], wbits=31)
    except zlib.error as exc:
        raise ConversionError("SMV reset SRAM block is not gzip-compressed") from exc
    if len(sram) != 0x20000:
        raise ConversionError(f"unsupported SMV reset SRAM size {len(sram)}")
    return sram


def convert_smv(data: bytes, max_source_frames: int | None) -> tuple[list[tuple[str, ...]], bytes | None, tuple[str, ...]]:
    if len(data) < 32:
        raise ConversionError("SMV file is shorter than the v1.43 header")
    version = struct.unpack_from("<I", data, 4)[0]
    if version not in (1, 4, 5):
        raise ConversionError(f"unsupported SMV version {version}")

    frame_count = struct.unpack_from("<I", data, 0x10)[0]
    controller_mask = data[0x14]
    sync_options = data[0x17]
    controller_count = controller_mask.bit_count()
    if controller_count == 0:
        raise ConversionError("SMV file does not record a standard controller")
    controller_offset = struct.unpack_from("<I", data, 0x1C)[0]
    save_offset = struct.unpack_from("<I", data, 0x18)[0]
    frame_size = controller_count * 2
    movie_rows = frame_count + 1
    wanted_frames = movie_rows if max_source_frames is None else min(movie_rows, max_source_frames)
    required_size = controller_offset + wanted_frames * frame_size
    if controller_offset < 32 or required_size > len(data):
        raise ConversionError("SMV controller data is outside the file")
    if save_offset < 32 or save_offset > controller_offset:
        raise ConversionError("SMV reset SRAM data is outside the file")

    directives: list[str] = []
    sync_data_exists = (sync_options & 0x01) != 0
    if sync_data_exists and (sync_options & 0x04) != 0:
        directives.append("@allow-opposing-directions 1")

    frames: list[tuple[str, ...]] = []
    for frame in range(wanted_frames):
        value = struct.unpack_from("<H", data, controller_offset + frame * frame_size)[0]
        if value == 0xFFFF:
            buttons: tuple[str, ...] = ()
        else:
            pressed = [button for mask, button in SMV_BUTTONS if value & mask]
            buttons = tuple(button for button in SCRIPT_ORDER if button in pressed)
        frames.append(buttons)
    return frames, extract_smv_sram(data, save_offset, controller_offset), tuple(directives)


def format_buttons(buttons: tuple[str, ...]) -> str:
    return "+".join(buttons) if buttons else "None"


def slice_frames(frames: list[tuple[str, ...]], skip_frames: int, max_frames: int | None) -> list[tuple[str, ...]]:
    if skip_frames >= len(frames):
        raise ConversionError(f"--skip-frames {skip_frames} skips past movie length {len(frames)}")
    end = len(frames) if max_frames is None else min(len(frames), skip_frames + max_frames)
    sliced = frames[skip_frames:end]
    if not sliced:
        raise ConversionError("slice contained no frame rows")
    return sliced


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("movie", type=Path, help=".bk2, .lsmv, .smv, or TASVideos zip containing one movie")
    parser.add_argument("-o", "--output", type=Path, help="write the Godot input script here")
    parser.add_argument("--sram-output", type=Path, help="write embedded Snes9x reset SRAM here when present")
    parser.add_argument("--skip-frames", type=int, default=0, help="drop the first COUNT source frames before writing")
    parser.add_argument("--max-frames", type=int, help="write at most COUNT frames after --skip-frames")
    args = parser.parse_args(argv)

    if args.skip_frames < 0:
        parser.error("--skip-frames expects a non-negative integer")
    if args.max_frames is not None and args.max_frames <= 0:
        parser.error("--max-frames expects a positive integer")

    read_limit = None if args.max_frames is None else args.skip_frames + args.max_frames
    try:
        movie = read_movie(args.movie, read_limit)
        frames = slice_frames(movie.frames, args.skip_frames, args.max_frames)
        segments = collapse_segments(frames)
    except (UnicodeDecodeError, ConversionError) as exc:
        print(f"convert-tas-input: {exc}", file=sys.stderr)
        return 1

    lines = [
        f"# Converted from {movie.movie_format} input.",
        f"# Source: {movie.source}",
        f"# Source frames skipped: {args.skip_frames}",
        f"# Frames in this script: {sum(count for count, _ in segments)}",
        "# Format: frames buttons",
    ]
    lines.extend(movie.directives)
    if movie.sram is not None:
        lines.append("# Embedded SMV reset SRAM: exported only for emulator/native-source sync checks.")
    lines.extend(f"{count} {format_buttons(buttons)}" for count, buttons in segments)
    output = "\n".join(lines) + "\n"

    if args.sram_output:
        if movie.sram is None:
            print("convert-tas-input: movie does not contain embedded reset SRAM", file=sys.stderr)
            return 1
        args.sram_output.write_bytes(movie.sram)

    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(output, encoding="utf-8")
    else:
        sys.stdout.write(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
