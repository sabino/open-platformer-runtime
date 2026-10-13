#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/project-env.sh"

COMMAND_FILE="${SMW_DEBUG_COMMAND_FILE:-$SMW_PROJECT_ROOT/.godot_local/debug/commands}"

if [[ "$#" -eq 0 ]]; then
  cat >&2 <<EOF
usage: $0 <command> [args...]

Commands are appended to: $COMMAND_FILE

Examples:
  $0 pause
  $0 step 1
  $0 input 12 left run
  $0 spawn 2025 256
  $0 powerup small
  $0 state
  $0 capture /tmp/smw-frame.png 2
EOF
  exit 2
fi

mkdir -p "$(dirname "$COMMAND_FILE")"
printf '%s\n' "$*" >>"$COMMAND_FILE"
echo "smw-control: appended '$*' to $COMMAND_FILE"
