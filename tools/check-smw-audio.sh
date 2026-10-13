#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/project-env.sh"
smw_prepare_dotnet
cd "$SMW_PROJECT_ROOT"
exec python3 tools/check-smw-audio.py --rom "$SMW_ROM_PATH" "$@"
