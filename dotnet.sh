#!/usr/bin/env bash
# Ejecuta el `dotnet` que resuelve el SDK de global.json (11.0.100 o una banda posterior de .NET 11).
# Uso: ./dotnet.sh build | ./dotnet.sh test | ./dotnet.sh <cualquier comando de dotnet>
# Primero el `dotnet` del PATH. Si ese no resuelve el SDK de global.json desde la carpeta actual (la misma en la que
# corre el comando) y hay uno en ~/.dotnet, usa ~/.dotnet (por ejemplo, cuando el PATH trae un dotnetup sin el SDK
# de .NET 11).
set -euo pipefail
export DOTNET_CLI_TELEMETRY_OPTOUT="${DOTNET_CLI_TELEMETRY_OPTOUT:-1}"
export DOTNET_NOLOGO="${DOTNET_NOLOGO:-1}"
if ! dotnet --version >/dev/null 2>&1 && [ -x "$HOME/.dotnet/dotnet" ]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
fi
exec dotnet "$@"
