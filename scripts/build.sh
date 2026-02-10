#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
"$ROOT_DIR/dotnetw" build "$ROOT_DIR/src/MultiPathIndustrialProcessor/MultiPathIndustrialProcessor.csproj" -c Debug
"$ROOT_DIR/dotnetw" build "$ROOT_DIR/src/OnlineShopping/OnlineShopping.csproj" -c Debug
