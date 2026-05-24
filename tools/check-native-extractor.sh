#!/usr/bin/env bash
set -euo pipefail

ROM_PATH="${1:-${SMW_ROM_PATH:-/path/to/compatible-rom.sfc}}"
GENERATED_DIR="${2:-generated/smw}"

dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-core "$ROM_PATH" "$GENERATED_DIR"
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-levels "$ROM_PATH" "$GENERATED_DIR"
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-audio-previews "$ROM_PATH" "$GENERATED_DIR"
