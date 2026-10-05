#!/usr/bin/env bash
# Levanta el AppHost del prototipo con el SDK y el runtime locales (.dotnet/).
# Uso: ./arrancar.sh [propio|nativo]   (modo de validación; por defecto propio)
# Hace falta el entorno porque global.json solo redirige el SDK: los procesos que
# lanza el AppHost buscan el runtime 11 en DOTNET_ROOT, y el del sistema no lo tiene.
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_ROOT="$PWD/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export Validacion__Modo="${1:-propio}"
exec dotnet run --project Arranque.AppHost
