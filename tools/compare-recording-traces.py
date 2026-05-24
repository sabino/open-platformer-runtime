#!/usr/bin/env python3
"""Compare native smw/ state traces against Godot replay traces."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


GODOT_TRACE_PREFIX = "smw-debug-trace:"


def parse_value(text: str) -> int | float | str:
    if re.fullmatch(r"-?\d+", text):
        return int(text)
    if re.fullmatch(r"-?\d+\.\d+", text):
        return float(text)
    return text


def parse_key_values(text: str) -> dict[str, int | float | str]:
    fields: dict[str, int | float | str] = {}
    for token in text.strip().split():
        if "=" not in token:
            continue
        key, value = token.split("=", 1)
        fields[key] = parse_value(value)
    return fields


def parse_native_trace(path: Path) -> list[dict[str, int | float | str]]:
    records: list[dict[str, int | float | str]] = []
    current: dict[str, int | float | str] | None = None
    for raw in path.read_text(encoding="utf-8", errors="replace").splitlines():
        line = raw.strip()
        if line == "state_trace_schema=2":
            if current:
                records.append(current)
            current = {}
            continue
        if current is None or "=" not in line:
            continue
        key, value = line.split("=", 1)
        current[key] = parse_value(value)
    if current:
        records.append(current)
    return records


def parse_godot_trace(path: Path) -> list[dict[str, int | float | str]]:
    records: list[dict[str, int | float | str]] = []
    for raw in path.read_text(encoding="utf-8", errors="replace").splitlines():
        line = raw.strip()
        if not line.startswith(GODOT_TRACE_PREFIX):
            continue
        records.append(parse_key_values(line[len(GODOT_TRACE_PREFIX) :]))
    return records


def write_jsonl(path: Path | None, records: list[dict[str, int | float | str]]) -> None:
    if path is None:
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        for record in records:
            handle.write(json.dumps(record, sort_keys=True, separators=(",", ":")) + "\n")


def drop_records(
    records: list[dict[str, int | float | str]],
    count: int,
) -> list[dict[str, int | float | str]]:
    if count <= 0:
        return records
    return records[min(count, len(records)) :]


def filter_native_start(
    records: list[dict[str, int | float | str]],
    *,
    frame: int | None,
    game_mode: int | None,
    level: int | None,
) -> tuple[list[dict[str, int | float | str]], int]:
    if frame is not None:
        for index, record in enumerate(records):
            value = record.get("frame")
            if isinstance(value, int) and value >= frame:
                return records[index:], index
        return [], len(records)
    if game_mode is None and level is None:
        return records, 0
    for index, record in enumerate(records):
        if game_mode is not None and record.get("game_mode") != game_mode:
            continue
        if level is not None and record.get("level") != level:
            continue
        return records[index:], index
    return [], len(records)


def number(record: dict[str, int | float | str], key: str) -> float | None:
    value = record.get(key)
    if isinstance(value, (int, float)):
        return float(value)
    return None


def native_position(record: dict[str, int | float | str], coarse_key: str, sub_key: str) -> float | None:
    coarse = number(record, coarse_key)
    if coarse is None:
        return None
    sub = number(record, sub_key)
    if sub is None:
        return coarse
    return coarse + sub / 256.0


def format_id(value: int | float | str | None) -> str:
    if isinstance(value, int):
        return f"{value:02X}"
    if isinstance(value, float) and value.is_integer():
        return f"{int(value):02X}"
    if isinstance(value, str):
        return value
    return "?"


def actor_summary(native: dict[str, int | float | str], godot: dict[str, int | float | str]) -> str:
    native_id = format_id(native.get("near_sprite_id"))
    godot_id = format_id(godot.get("near_id"))
    native_status = native.get("near_sprite_status", "?")
    godot_state = godot.get("near_state", "?")
    native_x = native.get("near_sprite_x", "?")
    native_y = native.get("near_sprite_y", "?")
    godot_x = godot.get("near_x", "?")
    godot_y = godot.get("near_y", "?")
    native_xspeed = native.get("near_sprite_xspeed", "?")
    native_yspeed = native.get("near_sprite_yspeed", "?")
    godot_xspeed = godot.get("near_xs", "?")
    godot_yspeed = godot.get("near_ys", "?")
    return (
        f"native_near={native_id}:{native_status}:{native_x},{native_y}:xs={native_xspeed}:ys={native_yspeed} "
        f"godot_near={godot_id}:{godot_state}:{godot_x},{godot_y}:xs={godot_xspeed}:ys={godot_yspeed}"
    )


def player_motion_summary(native: dict[str, int | float | str], godot: dict[str, int | float | str]) -> str:
    return (
        f"native_sub={native.get('player_subx', '?')},{native.get('player_suby', '?')} "
        f"godot_sub={godot.get('subx', '?')},{godot.get('suby', '?')} "
        f"native_speed={native.get('player_xspeed', '?')},{native.get('player_yspeed', '?')} "
        f"godot_speed={godot.get('xs', '?')},{godot.get('ys', '?')} "
        f"native_subspeed={native.get('player_subxspeed', '?')},{native.get('player_subyspeed', '?')} "
        f"godot_subspeed={godot.get('subxs', '?')},{godot.get('subys', '?')}"
    )


def counter_summary(native: dict[str, int | float | str], godot: dict[str, int | float | str]) -> str:
    return (
        f"native_counters=coins:{native.get('coins', '?')}:dragon:{native.get('yoshi_coins', '?')}:score:{native.get('score', '?')} "
        f"godot_counters=coins:{godot.get('coins', '?')}:dragon:{godot.get('dragon', '?')}:score:{godot.get('score', '?')}"
    )


def compare(
    native: list[dict[str, int | float | str]],
    godot: list[dict[str, int | float | str]],
    *,
    tolerance: float,
    native_y_offset: float,
    expected_comparable_records: int | None,
) -> tuple[int, str]:
    count = min(len(native), len(godot))
    if count == 0:
        return 2, "no comparable trace records"
    if expected_comparable_records is not None and count < expected_comparable_records:
        return 2, (
            f"not_enough_trace_records expected={expected_comparable_records} "
            f"native={len(native)} godot={len(godot)} comparable={count}"
        )

    for index in range(count):
        n = native[index]
        g = godot[index]
        nx = native_position(n, "player_x", "player_subx")
        ny = native_position(n, "player_y", "player_suby")
        gx = number(g, "x")
        gy = number(g, "y")
        npow = number(n, "powerup")
        gpow = number(g, "pow")
        ncoins = number(n, "coins")
        gcoins = number(g, "coins")
        nyoshi = number(n, "yoshi_coins")
        gdragon = number(g, "dragon")
        if (
            nx is None or
            ny is None or
            gx is None or
            gy is None or
            npow is None or
            gpow is None or
            ncoins is None or
            gcoins is None or
            nyoshi is None or
            gdragon is None
        ):
            return 2, f"missing comparison fields at index={index}"
        adjusted_ny = ny + native_y_offset
        if (
            abs(nx - gx) > tolerance or
            abs(adjusted_ny - gy) > tolerance or
            int(npow) != int(gpow) or
            int(ncoins) != int(gcoins) or
            int(nyoshi) != int(gdragon)
        ):
            return 1, (
                f"first_divergence index={index} "
                f"native_frame={n.get('frame', '?')} godot_frame={g.get('frame', '?')} "
                f"native_x={nx:.2f} native_x_raw={n.get('player_x', '?')}:{n.get('player_subx', '?')} "
                f"godot_x={gx:.2f} dx={gx - nx:.2f} "
                f"native_y={ny:.2f} native_y_raw={n.get('player_y', '?')}:{n.get('player_suby', '?')} "
                f"native_y_adjusted={adjusted_ny:.2f} godot_y={gy:.2f} dy={gy - adjusted_ny:.2f} "
                f"native_powerup={int(npow)} godot_powerup={int(gpow)} "
                f"{counter_summary(n, g)} "
                f"{player_motion_summary(n, g)} "
                f"{actor_summary(n, g)}"
            )
    if len(native) != len(godot):
        if expected_comparable_records is not None and count >= expected_comparable_records:
            return 0, (
                f"traces_match comparable_records={count} tolerance={tolerance} "
                f"native_extra={len(native) - count} godot_extra={len(godot) - count}"
            )
        return 1, f"trace_length_mismatch native={len(native)} godot={len(godot)} comparable={count}"
    return 0, f"traces_match records={count} tolerance={tolerance}"


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Compare native and Godot SMW replay traces.")
    parser.add_argument("--native-log", type=Path, required=True)
    parser.add_argument("--godot-log", type=Path, required=True)
    parser.add_argument("--native-jsonl", type=Path)
    parser.add_argument("--godot-jsonl", type=Path)
    parser.add_argument("--tolerance", type=float, default=1.0)
    parser.add_argument("--native-drop-records", type=int, default=0)
    parser.add_argument("--godot-drop-records", type=int, default=0)
    parser.add_argument(
        "--native-start-frame",
        type=int,
        help="Drop native records before this absolute native frame. Takes precedence over game_mode/level filters.",
    )
    parser.add_argument(
        "--native-start-game-mode",
        type=int,
        help="Drop native records before the first record with this game_mode.",
    )
    parser.add_argument(
        "--native-start-level",
        type=int,
        help="Drop native records before the first record with this level id.",
    )
    parser.add_argument(
        "--native-y-offset",
        type=float,
        default=-64.0,
        help="Offset applied to native player_y before comparing to Godot world Y.",
    )
    parser.add_argument(
        "--expected-comparable-records",
        type=int,
        help="Minimum number of overlapping records required for a successful match.",
    )
    args = parser.parse_args(argv)

    native = parse_native_trace(args.native_log)
    godot = parse_godot_trace(args.godot_log)
    native, native_auto_dropped = filter_native_start(
        native,
        frame=args.native_start_frame,
        game_mode=args.native_start_game_mode,
        level=args.native_start_level,
    )
    native = drop_records(native, args.native_drop_records)
    godot = drop_records(godot, args.godot_drop_records)
    write_jsonl(args.native_jsonl, native)
    write_jsonl(args.godot_jsonl, godot)
    code, message = compare(
        native,
        godot,
        tolerance=args.tolerance,
        native_y_offset=args.native_y_offset,
        expected_comparable_records=args.expected_comparable_records,
    )
    print("trace_compare_schema=1")
    print(f"native_records={len(native)}")
    print(f"godot_records={len(godot)}")
    print(f"status={'match' if code == 0 else 'diverged' if code == 1 else 'error'}")
    print(f"native_auto_dropped={native_auto_dropped}")
    print(f"native_drop_records={args.native_drop_records}")
    print(f"godot_drop_records={args.godot_drop_records}")
    print(f"native_y_offset={args.native_y_offset}")
    print(message)
    return code


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
