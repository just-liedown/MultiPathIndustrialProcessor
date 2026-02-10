#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"

# Build without copying files into the game folder (useful while the game is running and has the DLL locked).
"$ROOT_DIR/dotnetw" build "$ROOT_DIR/src/MultiPathIndustrialProcessor/MultiPathIndustrialProcessor.csproj" -c Debug /p:EnableModDeploy=false
"$ROOT_DIR/dotnetw" build "$ROOT_DIR/src/OnlineShopping/OnlineShopping.csproj" -c Debug /p:EnableModDeploy=false
