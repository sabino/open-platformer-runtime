#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/project-env.sh"
SMW_DEBUG_COMMAND_FILE="${SMW_DEBUG_COMMAND_FILE:-$SMW_PROJECT_ROOT/.godot_local/debug/commands}"
SMW_DEBUG_RCON_PORT="${SMW_DEBUG_RCON_PORT:-4600}"
mkdir -p "$(dirname "$SMW_DEBUG_COMMAND_FILE")"
: >"$SMW_DEBUG_COMMAND_FILE"
export SMW_DEBUG_COMMAND_FILE SMW_DEBUG_RCON_PORT
exec "$SMW_PROJECT_ROOT/tools/run.sh" \
  --smw-test-autostart --smw-debug-overlays \
  --smw-debug-command-file="$SMW_DEBUG_COMMAND_FILE" \
  --smw-debug-rcon="$SMW_DEBUG_RCON_PORT" "$@"
