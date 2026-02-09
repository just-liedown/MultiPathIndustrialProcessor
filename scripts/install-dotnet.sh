#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_DIR="$ROOT_DIR/.dotnet"
INSTALL_SCRIPT="$ROOT_DIR/.tmp/dotnet-install.sh"

mkdir -p "$DOTNET_DIR" "$ROOT_DIR/.tmp"

if [[ -x "$DOTNET_DIR/dotnet" ]]; then
  echo "dotnet already installed at: $DOTNET_DIR/dotnet"
  "$DOTNET_DIR/dotnet" --version
  exit 0
fi

echo "Downloading dotnet-install.sh..."
curl -sSL https://dot.net/v1/dotnet-install.sh -o "$INSTALL_SCRIPT"
chmod +x "$INSTALL_SCRIPT"

echo "Installing .NET 6 SDK into: $DOTNET_DIR"
bash "$INSTALL_SCRIPT" --channel 6.0 --install-dir "$DOTNET_DIR" --no-path

echo "Installed dotnet:"
"$DOTNET_DIR/dotnet" --info
