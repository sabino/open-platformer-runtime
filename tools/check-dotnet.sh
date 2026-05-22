#!/usr/bin/env bash
set -euo pipefail

dotnet restore SmwGodotNative.csproj
dotnet build SmwGodotNative.csproj --no-restore
dotnet run --project tests/SmwPhysicsSmoke/SmwPhysicsSmoke.csproj
