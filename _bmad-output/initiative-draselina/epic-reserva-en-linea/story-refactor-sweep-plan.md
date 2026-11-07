---
title: 'Refactor sweep'
type: 'refactor'
ticket: '13'
created: '2026-11-06'
status: 'built'
baseline_revision: 'aff99de13c87acc552c59884b3d7e080deeb85d1'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Las revisiones de la épica 2 dejaron cuatro limpiezas pendientes: un dato de configuración repetido, una consulta que no usa el índice que ya existe, una prueba que falla de forma intermitente y una prueba en la clase equivocada.

**Approach:** Cuatro limpiezas sin cambio de comportamiento, cubiertas por las pruebas existentes; alcance decidido por Gerardo el 2026-11-06 (Notes de la épica, entrada 13).

## Boundaries & Constraints

**Always:**
- Ningún cambio de comportamiento visible ni de reglas: las pruebas existentes siguen pasando sin cambiar lo que comprueban (salvo la prueba intermitente, que se estabiliza sin debilitar lo que verifica).
- 1) El id de zona `America/Mexico_City` vive en un solo lugar que leen el dominio de Agenda y la interfaz (el Kernel es el candidato: lo pueden usar todas las capas); `HoraLocal` y `TusDatos.razor` lo leen de ahí.
- 2) `CitasActivas.EntreAsync` filtra por traslape de rangos semiabiertos `[inicio, fin)` con el mismo operador e índice GiST de la restricción `ex_cita_sin_traslape` (Citas no canceladas), de modo que el recorrido depende de la ventana y no del historial; el resultado es idéntico al actual.
- Decisión (Gerardo, 2026-11-06): con una ventana vacía o invertida (`hasta <= desde`), `EntreAsync` devuelve una lista vacía en lugar de lo que devolvía la consulta anterior; evita el error de `tstzrange` y no cambia nada visible (sin días que recorrer).
- 3) `ReintentosYOrdenTests.Un_handler_que_no_termina_a_tiempo_se_cancela_cuenta_como_fallido_y_no_detiene_a_los_demas` falló una vez bajo carga (entrada 2.10). Se encuentra la causa (repitiendo la prueba bajo carga) y se corrige en la prueba o en el arnés; si la causa está en el código de producción, se detiene y se informa.
- 4) La prueba de `IPacientesApi.ConTelefonoAsync` pasa de `ReservaEnLineaTests` a una clase de pruebas de Pacientes, sin cambiar su contenido.

**Never:**
- Tocar los hallazgos rechazados en las revisiones ni los detalles de `deferred-work.md`.
- Nuevas funciones, migraciones de esquema o cambios en la interfaz.

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/HoraLocal.cs` -- `IdDeZona`, `Zona`, `AUtc`, `DesdeUtc`, `SemanaDe`.
- `src/DraSelina.Consultorio.Web/UI/Pages/Reserva/TusDatos.razor` (~línea 283) -- `TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")` para la guarda de fechas.
- `src/DraSelina.Consultorio.Web/Kernel/` -- tipos que usan todas las capas (`IClock`, ids); `tests/DraSelina.Consultorio.ArchitectureTests/Reglas.cs` dice qué puede usar cada capa.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/CitasActivas.cs` -- `EntreAsync`: hoy `Estado != Cancelada && Inicio < hasta && Fin > desde`.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/Migrations/20261102223335_CrearCitas.cs` -- la restricción `EXCLUDE USING gist (tstzrange(inicio, fin, '[)') WITH &&) WHERE (estado <> 'Cancelada')`.
- `tests/DraSelina.Consultorio.IntegrationTests/ReintentosYOrdenTests.cs` (~línea 35) -- límite por handler de 300 ms, cota de 20 s para la vuelta; `EscenarioDeEntrega`.
- `tests/DraSelina.Consultorio.IntegrationTests/ReservaEnLineaTests.cs` -- la prueba de `ConTelefonoAsync`.

## Tasks & Acceptance

**Execution:**
- [x] Id de zona en el Kernel; `HoraLocal` y `TusDatos.razor` lo usan.
- [x] `CitasActivas.EntreAsync` por traslape de `tstzrange` (SQL o traducción de Npgsql) y una prueba de que el plan de la consulta usa el índice de la restricción.
- [x] Causa y corrección de la prueba intermitente; repetirla bajo carga (al menos 20 corridas de la suite de integración o de esa clase en paralelo) sin fallos.
- [x] Mover la prueba de `ConTelefonoAsync`.

**Acceptance Criteria:**
- Given la solución, when corren todas las pruebas, then pasan, incluidas las de arquitectura, y las de Horarios libres y tope semanal dan los mismos resultados.
- Given la prueba que era intermitente, when se repite bajo carga, then no falla.

## Implementation Notes

- 1) `Kernel/ZonaDelConsultorio.cs`: `Id` (`America/Mexico_City`) y `Zona` (resuelta una vez). `HoraLocal.Zona` la devuelve; se quitó `HoraLocal.IdDeZona`, que nadie usaba. `TusDatos.razor` usa `ZonaDelConsultorio.Zona`. Quedan, sin tocar, el comentario de `CandadoDeFecha` y el SQL de una prueba que escriben la zona como texto.
- 2) `EntreAsync` va por `Database.SqlQuery` con `tstzrange(inicio, fin, '[)') && tstzrange(@desde, @hasta, '[)')` y `estado <> 'Cancelada'` escrito literal (como parámetro, PostgreSQL no podría probar que el filtro es el del índice parcial con un plan genérico). Componer `FromSql` con la proyección de la propiedad compleja `Rango` falla en EF 10 (`d.Rango_Inicio does not exist`), por eso `SqlQuery` a una fila propia.
- 2) [ASSUMPTION] Guarda `hasta <= desde` → lista vacía: `tstzrange` rechaza un límite inferior mayor y la ventana sale invertida si la anticipación mínima supera la ventana (nada lo impide en la base). Antes, con `desde == hasta` o invertido, la consulta devolvía Citas que cubrían ese tramo; el handler no lo nota (no recorre ningún día en ese caso). Para periodos no vacíos el resultado es el mismo.
- 2) Pruebas nuevas en `CitasActivasTests`: bordes del periodo semiabierto y Cancelada; periodo vacío; plan con `enable_seqscan = off` que debe usar `ex_cita_sin_traslape` y no `Seq Scan`. Con la consulta anterior el plan iba por `ix_cita_inicio` y la prueba falla.
- 3) Causa (reproducida: 5–6 de 24 corridas paralelas de la prueba, con 12 procesos `yes` cargando la CPU): el límite de 300 ms también se aplica al handler sano, que en una web recién arrancada hace sus primeras dos consultas; con la máquina cargada pasaba de 300 ms, se cancelaba y el evento `siguiente` quedaba `Pendiente`. El registro lo mostró: los dos handlers "no terminó en 00:00:00.3000000". Es de la prueba, no de producción (allí el límite es 30 s). Corrección: límite de 5 s en la prueba; lo verificado no cambia (el colgado tarda 60 s, la vuelta sigue con cota de 20 s). Tras el cambio: 24 + 2×24 corridas paralelas bajo la misma carga, sin fallos.
- 4) La prueba pasó sin cambios a `PacientesApiTests`, que usa el escenario de `ReservaEnLineaTests` por un alias.

## Plan Change Log

## Review Triage Log

**Pasada 1 (quick), 2026-11-06.** Veredictos: high 0, medium 0, low 1, false 0, maybe-false 0.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | La guarda `hasta <= desde` en `EntreAsync` cambia el resultado con una ventana vacía o invertida, contra "el resultado es idéntico al actual" | low | resuelto con Gerardo | Sin efecto visible (el único llamador no recorre días con esa ventana) y necesaria para que `tstzrange` no lance; Gerardo la aceptó (decisión en el bloque congelado) |

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` -- expected: todas pasan (Docker arriba).
- AppHost y `scripts/verificar-arranque.sh` -- expected: las tres filas pasan.
