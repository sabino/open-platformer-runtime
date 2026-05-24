#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="${GODOT_BIN:-godot4-mono}"
FRAMES="${SMW_TAS_RUN_FRAMES:-7200}"
VISIBLE=0
OVERLAYS=0
RCON_PORT=""
EXTRA_ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --visible)
      VISIBLE=1
      ;;
    --headless)
      VISIBLE=0
      ;;
    --frames)
      shift
      FRAMES="${1:?--frames expects a value}"
      ;;
    --overlays|--gizmos)
      OVERLAYS=1
      ;;
    --rcon)
      shift
      RCON_PORT="${1:?--rcon expects a port}"
      ;;
    --)
      shift
      EXTRA_ARGS+=("$@")
      break
      ;;
    *)
      EXTRA_ARGS+=("$1")
      ;;
  esac
  shift
done

prepare_output="$("$ROOT/tools/prepare-tas-diagnostic.sh")"
printf '%s\n' "$prepare_output"
input_script="$(printf '%s\n' "$prepare_output" | awk -F= '$1 == "slice" { print $2 }')"

if [[ -z "$input_script" || ! -s "$input_script" ]]; then
  echo "run-tas-diagnostic: missing prepared TAS slice" >&2
  exit 2
fi

godot_args=(
  --smw-test-autostart
  --smw-test-powerup=small
  --smw-input-script="$input_script"
)

if [[ "$OVERLAYS" == "1" ]]; then
  godot_args+=(--smw-debug-overlays)
fi
if [[ -n "$RCON_PORT" ]]; then
  godot_args+=(--smw-debug-rcon="$RCON_PORT")
fi
godot_args+=("${EXTRA_ARGS[@]}")

if [[ "$VISIBLE" == "1" ]]; then
  echo "run-tas-diagnostic: visible Wayland TAS run input=$input_script"
  exec "$ROOT/tools/run-wayland.sh" "${godot_args[@]}"
fi

if [[ ! -x "$GODOT_BIN" ]]; then
  echo "Godot executable not found or not executable: $GODOT_BIN" >&2
  exit 2
fi

echo "run-tas-diagnostic: headless TAS run frames=$FRAMES input=$input_script"
exec "$GODOT_BIN" --headless --path "$ROOT" --quit-after "$FRAMES" "${godot_args[@]}"
