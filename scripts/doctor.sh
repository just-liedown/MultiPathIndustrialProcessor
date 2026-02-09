#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ ! -x "$ROOT_DIR/.dotnet/dotnet" ]]; then
  echo "Missing local .NET SDK. Run: ./scripts/install-dotnet.sh"
  exit 1
fi

echo ".NET: $("$ROOT_DIR/dotnetw" --version)"

if [[ ! -f "$ROOT_DIR/stardewvalley.targets" ]]; then
  echo "Missing stardewvalley.targets (optional if ModBuildConfig can auto-detect)."
  echo "Create it via: cp stardewvalley.targets.example stardewvalley.targets"
  exit 0
fi

game_path="$(rg -No --pcre2 '(?<=<GamePath>).*?(?=</GamePath>)' "$ROOT_DIR/stardewvalley.targets" || true)"
if [[ -z "${game_path}" ]]; then
  echo "stardewvalley.targets exists but <GamePath> is empty."
  exit 1
fi

if [[ ! -d "${game_path}" ]]; then
  echo "GamePath does not exist: ${game_path}"
  exit 1
fi

echo "GamePath: ${game_path}"
echo "OK"

