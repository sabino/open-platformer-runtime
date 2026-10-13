#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/project-env.sh"
smw_prepare_dotnet
cd "$SMW_PROJECT_ROOT"

dotnet restore SmwGodotNative.csproj
dotnet build SmwGodotNative.csproj --no-restore
dotnet restore src/SmwAssets/SmwAssets.csproj
dotnet build src/SmwAssets/SmwAssets.csproj --no-restore
dotnet restore tools/SmwAssetTool/SmwAssetTool.csproj
dotnet build tools/SmwAssetTool/SmwAssetTool.csproj --no-restore
dotnet run --project tests/SmwPhysicsSmoke/SmwPhysicsSmoke.csproj
dotnet run --project tests/SmwRomSmoke/SmwRomSmoke.csproj
dotnet run --project tools/SmwAudioTool/SmwAudioTool.csproj -c Release -- self-test

if [[ -f generated/smw/manifest.json ]]; then
  dotnet run --project tests/SmwAssetCheck/SmwAssetCheck.csproj -- generated/smw
else
  echo "smw-godot C# asset contract: skipped (generated/smw/manifest.json missing)"
fi

ROM_PATH="${SMW_ROM_PATH:-}"
if [[ -n "$ROM_PATH" && -f "$ROM_PATH" && -f generated/smw/manifest.json ]]; then
  python3 tools/check-native-graphics.py --rom "$ROM_PATH"
  python3 tools/check-native-level-data.py --rom "$ROM_PATH"
  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-core "$ROM_PATH" generated/smw
  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-audio-previews "$ROM_PATH" generated/smw
  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-player-metadata "$ROM_PATH" generated/smw
  dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-entrance-tables "$ROM_PATH" generated/smw
  if python3 - <<'PY'
import json
from pathlib import Path
levels = json.loads(Path("generated/smw/manifest.json").read_text()).get("levels", {})
raise SystemExit(0 if {"105", "1CB"}.issubset(levels) else 1)
PY
  then
    dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-levels "$ROM_PATH" generated/smw
    dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-palettes "$ROM_PATH" generated/smw
  else
    echo "smw-asset-tool: skipped 105/1CB-specific level and palette verification for requested-level manifest"
  fi
else
  echo "smw-asset-tool: skipped native ROM verification (set SMW_ROM_PATH and generate assets to enable)"
fi
