#!/usr/bin/env python3
"""Select a level-start frame from a native input marker sidecar."""

from __future__ import annotations

import argparse
import re
from pathlib import Path


def parse_markers(path: Path) -> list[tuple[str, int]]:
    markers: list[tuple[str, int]] = []
    for raw in path.read_text(encoding="utf-8", errors="replace").splitlines():
        fields = dict(re.findall(r"([A-Za-z0-9_]+)=([^ \t\r\n]+)", raw))
        name = fields.get("marker")
        frame = fields.get("frame")
        if name is None or frame is None:
            continue
        try:
            markers.append((name, int(frame, 10)))
        except ValueError:
            continue
    return markers


def main() -> int:
    parser = argparse.ArgumentParser(description="Pick a native input marker frame.")
    parser.add_argument("markers", type=Path)
    parser.add_argument(
        "--prefer",
        action="append",
        default=[],
        help="Marker name to prefer, in priority order. Defaults to manual_k then auto_level_start.",
    )
    parser.add_argument("--output-frame-file", type=Path)
    args = parser.parse_args()

    preferences = args.prefer or ["manual_k", "auto_level_start"]
    markers = parse_markers(args.markers)
    for wanted in preferences:
        for name, frame in markers:
            if name == wanted:
                if args.output_frame_file is not None:
                    args.output_frame_file.parent.mkdir(parents=True, exist_ok=True)
                    args.output_frame_file.write_text(f"{frame}\n", encoding="utf-8")
                print(frame)
                return 0

    available = ", ".join(f"{name}:{frame}" for name, frame in markers) or "none"
    print(f"select-input-marker: no preferred marker found in {args.markers}; available={available}", flush=True)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())

