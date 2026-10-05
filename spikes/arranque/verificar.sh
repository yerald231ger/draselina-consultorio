#!/usr/bin/env bash
# Ejecuta las filas de la matriz del plan contra el prototipo YA levantado (./arrancar.sh).
# Sale con 0 solo si todas las filas pasan. Las formas B y C del comando (pregunta 3,
# segunda mitad) se reportan como observación y no cambian el código de salida.
# Si no hay contenedor postgres-* en ejecución sale con 99, que no puede confundirse con
# un número de filas fallidas.
# Uso: ./verificar.sh [url-base]      (por defecto http://localhost:5180)
set -uo pipefail

BASE="${1:-http://localhost:5180}"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
FALLAS=0

ok()    { echo "  PASA   $*"; }
falla() { echo "  FALLA  $*"; FALLAS=$((FALLAS + 1)); }
nota()  { echo "  OBS    $*"; }

CONTENEDOR="$(docker ps --filter 'name=^postgres-' --format '{{.Names}}' | head -n 1)"
[ -n "$CONTENEDOR" ] || { echo "No hay contenedor postgres-* en ejecución. ¿Está arriba el AppHost?"; exit 99; }

# La contraseña la generó Aspire; se lee dentro del contenedor y nunca sale de él.
sql()   { docker exec "$CONTENEDOR" sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" psql -U postgres -d arranque -tAc "$1"' sh "$1"; }
filas() { sql "select count(*) from arranque.notas"; }
diag()  { curl -s -m 10 "$BASE/diag" | python3 -c "import sys,json; print(json.load(sys.stdin)['$1'])"; }

# Pide la página (cookie y token antiforgery) y envía el formulario como lo haría el navegador.
# Deja el HTML de la respuesta en $TMP/resp.html y escribe el código HTTP.
enviar() { # ruta texto
  local ruta="$1" texto="$2" token
  curl -s -m 10 -c "$TMP/cookies" "$BASE$ruta" -o "$TMP/form.html"
  token="$(sed -n 's/.*name="__RequestVerificationToken" value="\([^"]*\)".*/\1/p' "$TMP/form.html" | head -n 1)"
  curl -s -m 20 -b "$TMP/cookies" -o "$TMP/resp.html" -w '%{http_code}' -X POST "$BASE$ruta" \
    --data-urlencode "_handler=registrar-nota" \
    --data-urlencode "__RequestVerificationToken=$token" \
    --data-urlencode "Comando.Texto=$texto"
}
estado()    { sed -n 's/.*id="estado" data-estado="\([^"]*\)".*/\1/p' "$TMP/resp.html" | head -n 1; }
excepcion() { sed -n 's/.*data-excepcion="\([^"]*\)".*/\1/p' "$TMP/resp.html" | head -n 1; }
# Texto del mensaje junto al campo Comando.Texto (vacío si no hay).
error_de_campo() { sed -n 's/.*<div class="validation-message"[^>]*data-valmsg-for="Comando.Texto"[^>]*>\([^<]\{1,\}\)<\/div>.*/\1/p' "$TMP/resp.html" | head -n 1; }

# Fila "Comando válido". $3: pasa|obs
valido() { # ruta forma modo
  local antes despues http est
  antes="$(filas)"; http="$(enviar "$1" "v-$2")"; est="$(estado)"; despues="$(filas)"
  local n; n="$(sql "select count(*) from arranque.notas where \"Forma\"='$2' and \"Texto\"='v-$2'")"
  if [ "$http" = "200" ] && [ "$est" = "guardada" ] && [ "$despues" = "$((antes + 1))" ] && [ "$n" -ge 1 ]; then
    [ "$3" = "obs" ] && nota "válido $1 ($2): fila nueva ($antes -> $despues)" || ok "válido $1 ($2): fila nueva ($antes -> $despues)"
  else
    local m="válido $1 ($2): http=$http estado='$est' excepcion='$(excepcion)' filas $antes -> $despues"
    [ "$3" = "obs" ] && nota "NO FUNCIONA $m" || falla "$m"
  fi
}

# Fila "Comando inválido". $2: texto enviado, $3: etiqueta, $4: pasa|obs
invalido() { # ruta texto etiqueta modo
  local antes despues http est err h0 h1 e0 e1
  antes="$(filas)"; h0="$(diag ejecucionesDelHandler)"; e0="$(diag erroresRegistrados)"
  http="$(enviar "$1" "$2")"; est="$(estado)"; err="$(error_de_campo)"
  despues="$(filas)"; h1="$(diag ejecucionesDelHandler)"; e1="$(diag erroresRegistrados)"
  local m="inválido $1 ($3): http=$http estado='$est' error-en-Comando.Texto='$err' excepcion='$(excepcion)' filas $antes -> $despues handler $h0 -> $h1 errores-en-log $e0 -> $e1"
  if [ "$http" = "200" ] && [ "$est" = "invalida" ] && [ -n "$err" ] && [ "$despues" = "$antes" ] && [ "$h1" = "$h0" ] && [ "$e1" = "$e0" ]; then
    [ "$4" = "obs" ] && nota "$m" || ok "$m"
  else
    [ "$4" = "obs" ] && nota "NO FUNCIONA $m" || falla "$m"
  fi
}

echo "== Fila: Arranque"
HTTP="$(curl -s -m 10 -o /dev/null -w '%{http_code}' "$BASE/")"
[ "$HTTP" = "200" ] && ok "GET / -> 200" || falla "GET / -> $HTTP"
echo "  runtime=$(diag runtime) wolverine=$(diag wolverine) efcore=$(diag efcore) modo-de-validacion=$(diag modo)"
echo "  contenedor=$CONTENEDOR $(sql 'select version()' | cut -d' ' -f1-2)"
echo "  tablas de wolverine: $(sql "select count(*) from information_schema.tables where table_schema='wolverine'")"

echo "== Fila: Comando válido (forma A, clase con set)"
valido / clase-set pasa

echo "== Fila: Comando inválido (forma A)  [con modo=nativo esta fila contesta la de 'Sin soporte nativo']"
invalido / "" "requerido vacío" pasa
invalido / "este texto pasa de veinte caracteres" "más de 20 caracteres" pasa

echo "== Observación: formas B (record con init) y C (record posicional)"
valido /init record-init obs
invalido /init "" "requerido vacío" obs
valido /posicional record-posicional obs
invalido /posicional "" "requerido vacío" obs

echo "== Fila: sin excepción no manejada"
echo "  errores (Error/Critical) en el registro de la web desde el arranque: $(diag erroresRegistrados)"
curl -s -m 10 "$BASE/diag" | python3 -c "import sys,json; [print('   ', e) for e in json.load(sys.stdin)['ultimosErrores']]"

echo "== Notas en la base"
sql 'select "Forma", "Texto" from arranque.notas order by "CreadaEn"' | sed 's/^/  /'

echo
if [ "$FALLAS" -eq 0 ]; then echo "RESULTADO: todas las filas pasan"; else echo "RESULTADO: $FALLAS fila(s) fallan"; fi
exit "$FALLAS"
