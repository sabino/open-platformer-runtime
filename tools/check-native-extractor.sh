#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/project-env.sh"
smw_prepare_dotnet
cd "$SMW_PROJECT_ROOT"

ROM_PATH="${1:-${SMW_ROM_PATH:-}}"
GENERATED_DIR="${2:-generated/smw}"

if [[ -z "$ROM_PATH" ]]; then
  echo "usage: tools/check-native-extractor.sh /path/to/compatible-rom.sfc [generated-dir]" >&2
  echo "or set SMW_ROM_PATH" >&2
  exit 2
fi

dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-core "$ROM_PATH" "$GENERATED_DIR"
python3 tools/check-native-graphics.py --rom "$ROM_PATH"
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-levels "$ROM_PATH" "$GENERATED_DIR"
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-audio-previews "$ROM_PATH" "$GENERATED_DIR"
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-player-metadata "$ROM_PATH" "$GENERATED_DIR"
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-entrance-tables "$ROM_PATH" "$GENERATED_DIR"
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-palettes "$ROM_PATH" "$GENERATED_DIR"
