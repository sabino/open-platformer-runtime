#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path


def find_call_end(text: str, open_paren: int) -> int:
    depth = 0
    quote: str | None = None
    escape = False
    line_comment = False
    block_comment = False

    for index in range(open_paren, len(text)):
        char = text[index]
        next_char = text[index + 1] if index + 1 < len(text) else ""

        if line_comment:
            if char in "\r\n":
                line_comment = False
            continue

        if block_comment:
            if char == "*" and next_char == "/":
                block_comment = False
            continue

        if quote:
            if escape:
                escape = False
            elif char == "\\":
                escape = True
            elif char == quote:
                quote = None
            continue

        if char == "/" and next_char == "/":
            line_comment = True
            continue

        if char == "/" and next_char == "*":
            block_comment = True
            continue

        if char in ("'", '"', "`"):
            quote = char
            continue

        if char == "(":
            depth += 1
            continue

        if char == ")":
            depth -= 1
            if depth == 0:
                end = index + 1
                while end < len(text) and text[end].isspace():
                    end += 1
                if end < len(text) and text[end] == ";":
                    end += 1
                return end

    raise ValueError("unterminated DOTNET.setup call")


def remove_dotnet_setup(text: str) -> tuple[str, int]:
    needle = "DOTNET.setup"
    count = 0
    cursor = 0
    output: list[str] = []

    while True:
        start = text.find(needle, cursor)
        if start < 0:
            output.append(text[cursor:])
            return "".join(output), count

        open_paren = text.find("(", start + len(needle))
        if open_paren < 0:
            raise ValueError("DOTNET.setup was found without an opening parenthesis")

        output.append(text[cursor:start])
        cursor = find_call_end(text, open_paren)
        count += 1


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("index_js", type=Path)
    parser.add_argument("--allow-missing", action="store_true")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    text = args.index_js.read_text()
    patched, count = remove_dotnet_setup(text)
    if count == 0:
        if args.allow_missing:
            print(f"{args.index_js}: no DOTNET.setup call found")
            return 0
        raise SystemExit(f"{args.index_js}: no DOTNET.setup call found")

    if not args.check:
        args.index_js.write_text(patched)

    print(f"{args.index_js}: removed {count} DOTNET.setup call(s)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
