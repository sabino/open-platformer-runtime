#!/usr/bin/env python3
"""Slice a frame-counted SMW input script without touching save states."""

from __future__ import annotations

import argparse
from pathlib import Path


def parse_line(raw: str) -> tuple[int, str] | None:
    stripped = raw.strip()
    if not stripped or stripped.startswith("#") or stripped.startswith("@"):
        return None
    parts = stripped.split(maxsplit=1)
    try:
        frames = int(parts[0], 10)
    except ValueError:
        return None
    if frames <= 0:
        return None
    buttons = parts[1].strip() if len(parts) > 1 else "None"
    return frames, buttons or "None"


def slice_segments(
    source: Path,
    *,
    start_frame: int,
    max_frames: int | None,
    prepad_frames: int = 0,
) -> tuple[list[tuple[int, str]], int]:
    output: list[tuple[int, str]] = []
    source_emitted = 0
    if prepad_frames > 0:
        output.append((prepad_frames, "None"))
    cursor = 0
    end_frame = None if max_frames is None else start_frame + max_frames

    for raw in source.read_text(encoding="utf-8", errors="replace").splitlines():
        parsed = parse_line(raw)
        if parsed is None:
            continue
        frames, buttons = parsed
        segment_start = cursor
        segment_end = cursor + frames
        cursor = segment_end

        if segment_end <= start_frame:
            continue
        if end_frame is not None and segment_start >= end_frame:
            break

        keep_start = max(segment_start, start_frame)
        keep_end = segment_end if end_frame is None else min(segment_end, end_frame)
        keep_frames = keep_end - keep_start
        if keep_frames <= 0:
            continue
        if output and output[-1][1] == buttons:
            output[-1] = (output[-1][0] + keep_frames, buttons)
        else:
            output.append((keep_frames, buttons))
        source_emitted += keep_frames

    return output, source_emitted


def main() -> int:
    parser = argparse.ArgumentParser(description="Slice a frame-counted SMW .input script.")
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--start-frame", type=int, required=True)
    parser.add_argument("--frames", type=int)
    parser.add_argument(
        "--prepad-frames",
        type=int,
        default=0,
        help="Insert this many neutral frames before the sliced source input.",
    )
    args = parser.parse_args()

    if args.start_frame < 0:
        parser.error("--start-frame must be >= 0")
    if args.frames is not None and args.frames <= 0:
        parser.error("--frames must be > 0")
    if args.prepad_frames < 0:
        parser.error("--prepad-frames must be >= 0")

    segments, source_emitted = slice_segments(
        args.input,
        start_frame=args.start_frame,
        max_frames=args.frames,
        prepad_frames=args.prepad_frames,
    )
    emitted = source_emitted + args.prepad_frames
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write("# smw-input-slice v1\n")
        handle.write(f"# source={args.input}\n")
        handle.write(f"# start_frame={args.start_frame}\n")
        handle.write(f"# prepad_frames={args.prepad_frames}\n")
        if args.frames is not None:
            handle.write(f"# requested_frames={args.frames}\n")
        handle.write(f"# source_emitted_frames={source_emitted}\n")
        handle.write(f"# emitted_frames={emitted}\n")
        for frames, buttons in segments:
            handle.write(f"{frames} {buttons}\n")

    print(
        "input_slice_schema=1"
        f" source={args.input}"
        f" output={args.output}"
        f" start_frame={args.start_frame}"
        f" prepad_frames={args.prepad_frames}"
        f" source_emitted_frames={source_emitted}"
        f" emitted_frames={emitted}"
    )
    return 0 if emitted > 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
