#!/usr/bin/env bash
set -euo pipefail

dotnet restore SmwGodotNative.csproj
dotnet build SmwGodotNative.csproj --no-restore
dotnet run --project tests/SmwPhysicsSmoke/SmwPhysicsSmoke.csproj

if [[ -f generated/smw/manifest.json ]]; then
  dotnet run --project tests/SmwAssetCheck/SmwAssetCheck.csproj -- generated/smw
else
  echo "smw-godot C# asset contract: skipped (generated/smw/manifest.json missing)"
fi

ROM_PATH="${SMW_ROM_PATH:-/path/to/compatible-rom.sfc}"
if [[ -f "$ROM_PATH" && -f generated/smw/manifest.json ]]; then
  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-core "$ROM_PATH" generated/smw
  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-levels "$ROM_PATH" generated/smw
  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-audio-previews "$ROM_PATH" generated/smw
  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-player-metadata "$ROM_PATH" generated/smw
else
  echo "smw-asset-tool: skipped native ROM verification (ROM or generated assets missing)"
fi
