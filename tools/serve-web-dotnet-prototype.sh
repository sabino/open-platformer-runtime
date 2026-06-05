#!/usr/bin/env bash
set -euo pipefail

ROOT="${1:-web-export/out}"
PORT="${PORT:-8060}"

python3 tools/serve-cross-origin-isolated.py --root "$ROOT" --port "$PORT"
