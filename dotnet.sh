#!/usr/bin/env bash
# Ejecuta `dotnet` con el SDK y el runtime locales del repositorio (.dotnet/).
# Uso: ./dotnet.sh build | ./dotnet.sh test | ./dotnet.sh <cualquier comando de dotnet>
# Hace falta porque global.json solo redirige el SDK: los procesos que se lanzan
# después (las pruebas, y más adelante el AppHost) buscan el runtime 11 en DOTNET_ROOT,
# y la instalación del sistema no lo tiene.
set -euo pipefail
RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ ! -x "$RAIZ/.dotnet/dotnet" ]; then
  echo "Falta el SDK local de .NET 11 en $RAIZ/.dotnet. Instálalo con:" >&2
  echo "  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version 11.0.100-rc.1.26425.128 --install-dir \"$RAIZ/.dotnet\"" >&2
  exit 1
fi
export DOTNET_ROOT="$RAIZ/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT="${DOTNET_CLI_TELEMETRY_OPTOUT:-1}"
export DOTNET_NOLOGO="${DOTNET_NOLOGO:-1}"
exec dotnet "$@"
