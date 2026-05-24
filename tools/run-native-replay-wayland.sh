#!/usr/bin/env bash
set -euo pipefail

cat >&2 <<'EOF'
run-native-replay-wayland: disabled.

The old implementation replayed native save slot 0 and could mutate the live
smw/ save-state directory. Use tools/run-native-input-wayland.sh with a
frame-counted .input script instead.
EOF
exit 2
