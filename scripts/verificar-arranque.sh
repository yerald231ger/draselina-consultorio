#!/usr/bin/env bash
# Verifica el arranque contra el AppHost YA levantado en otra terminal:
#   ./dotnet.sh run --project src/DraSelina.Consultorio.AppHost
# Ejecuta tres filas: arranque completo, historial de migraciones por esquema y salud.
# Sale con 0 solo si todas pasan; si no, con el número de filas que fallan.
# Necesita docker, curl y kubectl. kubectl se usa para preguntarle al orquestador de Aspire (DCP) el
# estado y el código de salida de cada recurso; no hace falta ningún clúster.
# Uso: scripts/verificar-arranque.sh [url-de-la-web]      (por defecto http://localhost:5080)
set -uo pipefail

WEB="${1:-http://localhost:5080}"
BASE="consultorio"
FALLAS=0
FILA_OK=1

fila()  { echo "== Fila: $*"; FILA_OK=1; }
ok()    { echo "  PASA   $*"; }
falla() { echo "  FALLA  $*"; FILA_OK=0; }
cierra() { [ "$FILA_OK" -eq 1 ] || FALLAS=$((FALLAS + 1)); }

# --- Dónde está cada cosa -------------------------------------------------------------------------

# El contenedor de PostgreSQL que levantó este AppHost: el postgres-* que tiene la base del consultorio.
# La contraseña la generó Aspire; se usa dentro del contenedor y nunca sale de él.
psql_en() { # contenedor base sql
  docker exec "$1" sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" psql -U postgres -d "$1" -tAc "$2"' sh "$2" "$3" 2>/dev/null
}
CONTENEDOR=""
for c in $(docker ps --filter 'name=^postgres-' --format '{{.Names}}' 2>/dev/null); do
  if [ "$(psql_en "$c" postgres "select 1 from pg_database where datname='$BASE'")" = "1" ]; then CONTENEDOR="$c"; break; fi
done
sql() { psql_en "$CONTENEDOR" "$BASE" "$1"; }

# El kubeconfig del orquestador de Aspire que tiene el recurso "migraciones".
KUBECONFIG_DCP=""
if command -v kubectl >/dev/null 2>&1; then
  for k in $(ps -Ao command | sed -n 's/.*--kubeconfig[ =]"\{0,1\}\([^" ]*kubeconfig\).*/\1/p' | sort -u); do
    if kubectl --kubeconfig "$k" get executables -o name 2>/dev/null | grep -q '/migraciones-'; then KUBECONFIG_DCP="$k"; break; fi
  done
fi
# Estado y código de salida de un recurso: "Running <none>", "Finished 0"...
recurso() { # tipo prefijo
  kubectl --kubeconfig "$KUBECONFIG_DCP" get "$1" --no-headers \
    -o custom-columns=NOMBRE:.metadata.name,ESTADO:.status.state,SALIDA:.status.exitCode 2>/dev/null \
    | awk -v p="^$2-" '$1 ~ p { print $2, $3 }' | head -n 1
}

# --- Fila 1 ---------------------------------------------------------------------------------------
fila "Arranque completo"
if [ -z "$CONTENEDOR" ]; then
  falla "no hay contenedor postgres-* en ejecución con la base '$BASE'. ¿Está arriba el AppHost?"
else
  ok "PostgreSQL en ejecución: $CONTENEDOR, $(sql 'select version()' | cut -d' ' -f1-2)"
fi

if [ -z "$KUBECONFIG_DCP" ]; then
  falla "no se pudo preguntar al orquestador de Aspire por los recursos (¿kubectl instalado? ¿AppHost arriba?)"
else
  ESTADO="$(recurso containers postgres)"
  [ "$ESTADO" = "Running <none>" ] && ok "recurso postgres: en ejecución" || falla "recurso postgres: '$ESTADO' (se esperaba en ejecución)"
  ESTADO="$(recurso executables migraciones)"
  [ "$ESTADO" = "Finished 0" ] && ok "recurso migraciones: terminado con código 0" || falla "recurso migraciones: '$ESTADO' (se esperaba 'Finished 0')"
  ESTADO="$(recurso executables web)"
  [ "$ESTADO" = "Running <none>" ] && ok "recurso web: en ejecución" || falla "recurso web: '$ESTADO' (se esperaba en ejecución)"
fi

CUERPO="$(mktemp)"; trap 'rm -f "$CUERPO"' EXIT
HTTP="$(curl -s -m 10 -o "$CUERPO" -w '%{http_code} %{content_type}' "$WEB/")"
if [ "${HTTP%% *}" = "200" ] && [[ "$HTTP" == *text/html* ]] && grep -qi '<html' "$CUERPO"; then
  ok "GET $WEB/ -> 200 con HTML"
else
  falla "GET $WEB/ -> '$HTTP' (se esperaba 200 con HTML)"
fi
cierra

# --- Fila 2 ---------------------------------------------------------------------------------------
fila "Historial por esquema"
if [ -z "$CONTENEDOR" ]; then
  falla "sin PostgreSQL no se puede consultar"
else
  for esquema in agenda pacientes notificaciones; do
    N="$(sql "select count(*) from information_schema.tables where table_schema='$esquema' and table_name='__EFMigrationsHistory'")"
    if [ "$N" = "1" ]; then
      ok "$esquema.__EFMigrationsHistory existe, con $(sql "select count(*) from $esquema.\"__EFMigrationsHistory\"") migraciones aplicadas"
    else
      falla "no existe $esquema.__EFMigrationsHistory"
    fi
  done
  N="$(sql "select count(*) from information_schema.tables where table_schema='public' and table_name='__EFMigrationsHistory'")"
  [ "$N" = "0" ] && ok "ningún historial de migraciones en public" || falla "hay un historial de migraciones en public"
fi
cierra

# --- Fila 3 ---------------------------------------------------------------------------------------
fila "Salud"
HTTP="$(curl -s -m 10 -o /dev/null -w '%{http_code}' "$WEB/health")"
[ "$HTTP" = "200" ] && ok "GET $WEB/health -> 200" || falla "GET $WEB/health -> $HTTP"
cierra

echo
if [ "$FALLAS" -eq 0 ]; then echo "RESULTADO: las tres filas pasan"; else echo "RESULTADO: $FALLAS fila(s) fallan"; fi
exit "$FALLAS"
