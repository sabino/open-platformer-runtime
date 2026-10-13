#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/project-env.sh"
smw_prepare_dotnet
cd "$SMW_PROJECT_ROOT"
dotnet build SmwGodotNative.csproj
exec python3 "$SMW_PROJECT_ROOT/tools/check-native-visuals.py" "$@"
