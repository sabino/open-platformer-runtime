#!/usr/bin/env bash
set -euo pipefail

ROM_PATH="${1:-${SMW_ROM_PATH:-/path/to/compatible-rom.sfc}}"
OUT_DIR="${2:-generated/smw}"

tools/import-smw.sh "$ROM_PATH" "$OUT_DIR"

python3 - "$OUT_DIR" <<'PY'
import json
import sys
from pathlib import Path

out_dir = Path(sys.argv[1])
manifest = json.loads((out_dir / "manifest.json").read_text())
levels = manifest["levels"]
assert "105" in levels, "missing Yoshi Island 1 level 105"
assert "0CB" in levels, "missing direct pipe target level 0CB"

level_105 = json.loads((out_dir / levels["105"]["file"]).read_text())
exits = level_105["screen_exits"]
assert exits, "level 105 should contain at least one screen exit"
pipe_exit = next((exit for exit in exits if exit["screen"] == 7), None)
assert pipe_exit is not None, "level 105 screen 07 pipe exit missing"
assert pipe_exit["exit_low"] == 0xCB, pipe_exit
assert pipe_exit["vanilla_destination"] == 0x0CB, pipe_exit
assert pipe_exit["vanilla_secondary"] == 0, pipe_exit
assert pipe_exit["raw_r11"] == 0, pipe_exit

print("smw-import check: YI1 pipe route 105 screen 07 -> 0CB secondary=0")
PY

