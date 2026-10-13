#!/usr/bin/env bash
# Shared local setup; source this file from project tools.

SMW_PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ -f "$SMW_PROJECT_ROOT/local-rom.cfg" ]]; then
  source "$SMW_PROJECT_ROOT/local-rom.cfg"
fi

smw_prepare_dotnet() {
  command -v dotnet >/dev/null || {
    echo "smw: a .NET SDK is required" >&2
    return 2
  }
  # The project targets net8.0. Respect an explicit runtime selection while
  # allowing installed newer runtimes to run the development tools.
  if [[ -z "${DOTNET_ROLL_FORWARD+x}" ]] &&
      ! dotnet --list-runtimes | awk '$1 == "Microsoft.NETCore.App" && $2 ~ /^8\./ { found=1 } END { exit !found }'; then
    export DOTNET_ROLL_FORWARD=Major
  fi
}

smw_find_godot() {
  local candidate
  if [[ -n "${GODOT_BIN:-}" ]]; then
    candidate="$(command -v "$GODOT_BIN" || true)"
    [[ -x "$candidate" ]] || {
      echo "smw: Godot executable not found: $GODOT_BIN" >&2
      return 2
    }
    printf '%s\n' "$candidate"
    return
  fi
  for candidate in godot4-mono godot-mono godot4 godot; do
    if command -v "$candidate" >/dev/null; then
      command -v "$candidate"
      return
    fi
  done
  for candidate in /Applications/Godot_mono.app/Contents/MacOS/Godot \
      "$HOME/Applications/Godot_mono.app/Contents/MacOS/Godot"; do
    if [[ -x "$candidate" ]]; then
      printf '%s\n' "$candidate"
      return
    fi
  done
  echo "smw: Godot .NET is required; set GODOT_BIN to its executable" >&2
  return 2
}
