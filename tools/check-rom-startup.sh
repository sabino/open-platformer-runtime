#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/project-env.sh"
smw_prepare_dotnet
export GODOT_BIN="$(smw_find_godot)"
exec python3 "$SMW_PROJECT_ROOT/tools/check-rom-startup.py" "$@"
