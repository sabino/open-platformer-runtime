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
