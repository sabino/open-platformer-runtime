#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/project-env.sh"
smw_prepare_dotnet
GODOT_BIN="$(smw_find_godot)"
cd "$SMW_PROJECT_ROOT"

SMW_RUN_IMPORT=off
SMW_RUN_BUILD="${SMW_SKIP_DOTNET_BUILD:-0}"
SMW_RUN_ARGS=()
while (($#)); do
  case "$1" in
    --rom)
      [[ $# -ge 2 ]] || { echo "run: --rom requires a path" >&2; exit 2; }
      export SMW_ROM_PATH="$2"
      shift 2
      ;;
    --import-assets) SMW_RUN_IMPORT=always; shift ;;
    --no-import) SMW_RUN_IMPORT=off; shift ;;
    --no-build) SMW_RUN_BUILD=1; shift ;;
    --help|-h)
      echo 'usage: tools/run.sh [--rom ROM] [--import-assets] [--no-build] [GODOT_ARGS...]'
      exit 0
      ;;
    *) SMW_RUN_ARGS+=("$1"); shift ;;
  esac
done

if [[ "$SMW_RUN_IMPORT" == always ]]; then
  [[ -n "${SMW_ROM_PATH:-}" ]] || {
    echo 'run: set SMW_ROM_PATH, configure ignored local-rom.cfg, or pass --rom' >&2
    exit 2
  }
  tools/import-smw.sh --rom "$SMW_ROM_PATH"
fi
if [[ "$SMW_RUN_BUILD" != 1 ]]; then
  dotnet build SmwGodotNative.csproj
fi

# Godot selects the platform's native display and audio drivers.
exec "$GODOT_BIN" --path "$SMW_PROJECT_ROOT" "${SMW_RUN_ARGS[@]}"
