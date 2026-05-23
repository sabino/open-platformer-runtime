#!/usr/bin/env bash
set -euo pipefail

HOST="${SMW_DEBUG_RCON_HOST:-127.0.0.1}"
PORT="${SMW_DEBUG_RCON_PORT:-4600}"

if [[ "$#" -eq 0 ]]; then
  cat >&2 <<EOF
usage: $0 <command> [args...]

Sends one newline-terminated command to $HOST:$PORT.

Examples:
  $0 pause
  $0 step 1
  $0 input 12 left run
  $0 spawn 2025 256
  $0 state
  $0 oam
EOF
  exit 2
fi

python3 - "$HOST" "$PORT" "$*" <<'PY'
import socket
import sys

host = sys.argv[1]
port = int(sys.argv[2])
command = sys.argv[3] + "\n"

with socket.create_connection((host, port), timeout=2.0) as sock:
    sock.settimeout(2.0)
    try:
        banner = sock.recv(4096)
        if banner:
            sys.stdout.write(banner.decode("utf-8", "replace"))
    except socket.timeout:
        pass

    sock.sendall(command.encode("utf-8"))
    try:
        response = sock.recv(4096)
        if response:
            sys.stdout.write(response.decode("utf-8", "replace"))
    except socket.timeout:
        pass
PY
