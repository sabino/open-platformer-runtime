#!/usr/bin/env python3
"""Convert smw/ native StateRecorder snapshots into Godot input scripts."""

from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path


BUTTONS = (
    "B",
    "Y",
    "Select",
    "Start",
    "Up",
    "Down",
    "Left",
    "Right",
    "A",
    "X",
    "L",
    "R",
)


class SnapshotError(Exception):
    pass


def read_vl(log: bytes, pos: int) -> tuple[int, int]:
    value = 0
    while True:
        if pos >= len(log):
            raise SnapshotError("truncated variable-length integer in input log")
        byte = log[pos]
        pos += 1
        value += byte
        if byte != 255:
            return value, pos


def format_buttons(mask: int) -> str:
    names = [name for bit, name in enumerate(BUTTONS) if mask & (1 << bit)]
    return "+".join(names) if names else "None"


def append_segment(segments: list[tuple[int, int]], frames: int, mask: int) -> None:
    if frames <= 0:
        return
    if segments and segments[-1][1] == mask:
        segments[-1] = (segments[-1][0] + frames, mask)
    else:
        segments.append((frames, mask))


def read_snapshot(path: Path) -> tuple[list[int], bytes]:
    data = path.read_bytes()
    if len(data) < 64:
        raise SnapshotError(f"{path}: too small to be a native snapshot")

    header = list(struct.unpack_from("<16I", data, 0))
    if header[0] != 2:
        raise SnapshotError(
            f"{path}: unsupported snapshot header version {header[0]} "
            "(only native StateRecorder v2 is supported)"
        )

    log_size = header[2]
    log_start = 64
    log_end = log_start + log_size
    if log_end > len(data):
        raise SnapshotError(
            f"{path}: truncated input log; header wants {log_size} bytes, "
            f"file has {max(0, len(data) - log_start)}"
        )

    return header, data[log_start:log_end]


def decode_segments(
    header: list[int],
    log: bytes,
    *,
    start_frame: int,
    max_frames: int | None,
) -> tuple[list[tuple[int, int]], dict[str, int]]:
    total_frames = header[1]
    frames_since_last = 0
    replay_next_cmd_at = 0
    replay_cmd = 0
    replay_pos = 0
    replay_pos_last_complete = 0
    last_inputs = 0
    cur_player = 0
    ignored_patches = 0
    player2_toggles = 0
    malformed_markers = 0
    segments: list[tuple[int, int]] = []

    frame_limit = total_frames
    if max_frames is not None:
        frame_limit = min(frame_limit, start_frame + max_frames)

    for frame in range(total_frames):
        while frames_since_last >= replay_next_cmd_at:
            pos = replay_pos
            if pos != replay_pos_last_complete:
                cmd = replay_cmd
                frames_since_last = 0
                if cmd < 0xC0:
                    bit = (cmd >> 4) + cur_player * 12
                    if bit < 12:
                        last_inputs ^= 1 << bit
                    else:
                        player2_toggles += 1
                elif cmd < 0xD0:
                    byte_count = 1 + ((cmd >> 2) & 3)
                    if byte_count == 4:
                        extra, pos = read_vl(log, pos)
                        byte_count += extra
                    pos += 2 + byte_count
                    ignored_patches += 1
                    if pos > len(log):
                        raise SnapshotError("truncated patch command in input log")
                else:
                    raise SnapshotError(f"unsupported recorder command 0x{cmd:02X}")

            replay_pos_last_complete = pos
            if pos >= len(log):
                replay_pos = pos
                replay_next_cmd_at = 0xFFFFFFFF
                break

            while True:
                if pos >= len(log):
                    replay_pos = pos
                    replay_next_cmd_at = 0xFFFFFFFF
                    break
                cmd = log[pos]
                pos += 1
                if cmd < 0xFC:
                    break
                if cmd in (0xFC, 0xFD):
                    cur_player = cmd - 0xFC
                    continue
                malformed_markers += 1
                raise SnapshotError(f"unsupported recorder marker 0x{cmd:02X}")

            if replay_next_cmd_at == 0xFFFFFFFF:
                break

            delay_mask = 0x0F if cmd < 0xC0 else 0x01
            delay = cmd & delay_mask
            if delay == delay_mask:
                extra, pos = read_vl(log, pos)
                delay += extra
            replay_next_cmd_at = delay
            replay_cmd = cmd
            replay_pos = pos

        if start_frame <= frame < frame_limit:
            append_segment(segments, 1, last_inputs & 0x0FFF)

        frames_since_last += 1
        if frame + 1 >= frame_limit:
            break

    stats = {
        "total_frames": total_frames,
        "emitted_frames": sum(frames for frames, _mask in segments),
        "segments": len(segments),
        "log_bytes": len(log),
        "last_inputs": header[3],
        "frames_since_last": header[4],
        "base_snapshot_bytes": header[5],
        "runtime_snapshot_bytes": header[6],
        "ignored_patches": ignored_patches,
        "player2_toggles": player2_toggles,
        "malformed_markers": malformed_markers,
    }
    return segments, stats


def write_script(
    path: Path,
    source: Path,
    segments: list[tuple[int, int]],
    stats: dict[str, int],
    *,
    start_frame: int,
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write("# smw-native-snapshot-input v1\n")
        handle.write(f"# source={source}\n")
        handle.write(f"# start_frame={start_frame}\n")
        for key in (
            "total_frames",
            "emitted_frames",
            "segments",
            "log_bytes",
            "last_inputs",
            "frames_since_last",
            "base_snapshot_bytes",
            "runtime_snapshot_bytes",
            "ignored_patches",
            "player2_toggles",
        ):
            handle.write(f"# {key}={stats[key]}\n")
        for frames, mask in segments:
            handle.write(f"{frames} {format_buttons(mask)}\n")


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Convert a native smw/ .sav snapshot joypad log into a Godot .input script."
    )
    parser.add_argument("snapshot", type=Path, help="Native smw/ snapshot, for example save1.sav")
    parser.add_argument("-o", "--output", type=Path, required=True, help="Output Godot input script")
    parser.add_argument("--start-frame", type=int, default=0, help="Drop this many leading recorded frames")
    parser.add_argument("--max-frames", type=int, default=0, help="Emit at most this many frames after --start-frame")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    if args.start_frame < 0:
        raise SnapshotError("--start-frame must be non-negative")
    max_frames = args.max_frames if args.max_frames > 0 else None

    header, log = read_snapshot(args.snapshot)
    segments, stats = decode_segments(
        header,
        log,
        start_frame=args.start_frame,
        max_frames=max_frames,
    )
    write_script(args.output, args.snapshot, segments, stats, start_frame=args.start_frame)
    print("native_snapshot_input_schema=1")
    print(f"snapshot={args.snapshot}")
    print(f"output={args.output}")
    for key in ("total_frames", "emitted_frames", "segments", "ignored_patches", "player2_toggles"):
        print(f"{key}={stats[key]}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main(sys.argv[1:]))
    except SnapshotError as exc:
        print(f"convert-native-snapshot-input: {exc}", file=sys.stderr)
        raise SystemExit(2)
