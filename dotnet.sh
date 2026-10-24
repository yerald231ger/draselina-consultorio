#!/usr/bin/env bash
# Ejecuta el `dotnet` del sistema. global.json fija el SDK 10.0.402 o una banda posterior de .NET 10.
# Uso: ./dotnet.sh build | ./dotnet.sh test | ./dotnet.sh <cualquier comando de dotnet>
# Se conserva para que los comandos documentados sigan funcionando; con .NET 10 ya no hace falta un SDK local.
set -euo pipefail
export DOTNET_CLI_TELEMETRY_OPTOUT="${DOTNET_CLI_TELEMETRY_OPTOUT:-1}"
export DOTNET_NOLOGO="${DOTNET_NOLOGO:-1}"
exec dotnet "$@"
