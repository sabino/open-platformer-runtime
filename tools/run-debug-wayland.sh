#!/usr/bin/env bash
set -euo pipefail

COMMAND_FILE="${SMW_DEBUG_COMMAND_FILE:-/tmp/smw-godot-debug.commands}"
RCON_PORT="${SMW_DEBUG_RCON_PORT:-4600}"
mkdir -p "$(dirname "$COMMAND_FILE")"
: >"$COMMAND_FILE"

echo "smw debug command file: $COMMAND_FILE"
echo "smw rcon: 127.0.0.1:$RCON_PORT"
echo "examples:"
echo "  tools/smw-control.sh pause"
echo "  tools/smw-control.sh step 1"
echo "  tools/smw-control.sh input 16 left run"
echo "  tools/smw-control.sh spawn 930 304"
echo "  tools/smw-control.sh state"

exec tools/run-wayland.sh \
  --smw-test-autostart \
  --smw-debug-overlays \
  --smw-debug-command-file="$COMMAND_FILE" \
  --smw-debug-rcon="$RCON_PORT" \
  "$@"
