#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path


def quote(value: str) -> str:
    return value.replace("\\", "\\\\").replace('"', '\\"')


def replace_option(text: str, key: str, value: str) -> str:
    prefix = f"{key}="
    lines = text.splitlines()
    for index, line in enumerate(lines):
        if line.startswith(prefix):
            lines[index] = f'{prefix}"{quote(value)}"'
            return "\n".join(lines) + "\n"
    raise ValueError(f"missing export option: {key}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--preset", type=Path, default=Path("export_presets.cfg"))
    parser.add_argument("--debug")
    parser.add_argument("--release")
    args = parser.parse_args()

    text = args.preset.read_text()
    if args.debug:
        text = replace_option(text, "custom_template/debug", args.debug)
    if args.release:
        text = replace_option(text, "custom_template/release", args.release)
    args.preset.write_text(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
