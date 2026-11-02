---
title: 'Agenda de atención y reglas de reserva sembradas'
type: 'feature'
ticket: '2'
created: '2026-11-02'
status: 'built'
baseline_revision: 'edba453ad9c2968eb6750f35d62938827ada0a1b'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-draselina/architecture-appointment-booking/ddd-and-data-model-appointment-booking.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Agenda no sabe cuándo se atiende ni con qué reglas: no existen la agenda base, las agendas custom ni `ReglasDeReserva`, y las entradas 3 y 4 los necesitan para calcular y validar horarios.

**Approach:** El agregado `AgendaAtencion` (base por día de la semana y custom por fecha, con sus invariantes y la respuesta única a "¿qué tramos valen esta fecha?", INV-V1) y el agregado `ReglasDeReserva`, con sus tablas, sus puertos de escritura y los datos iniciales que siembra la migración.

## Boundaries & Constraints

**Always:**
- Parte I del documento de dominio tal como está: INV-B1 a B5, INV-A1 a A6, INV-V1, T-1 (la duración mínima llega como parámetro; el agregado no lee configuración), T-3 y T-4 (`AgendaAtencion` no conoce citas), RN-4 (solo `AgendaAtencion` resuelve los tramos vigentes).
- Tramos en hora local de `America/Mexico_City` (`time`) y fechas locales (`date`), AD-17; día de la semana ISO, 1 = lunes.
- Datos iniciales de la Parte II: base de lunes a viernes 15:30–18:00; reglas: anticipación mínima 1 día, ventana 2 meses, umbral de cancelación tardía 120 min, tope semanal 3.
- El agregado se carga con las agendas custom de hoy en adelante (supuesto de la Parte I).
- Decisión (Gerardo, 2026-11-02): en `ReglasDeReserva`, la anticipación mínima y el umbral de cancelación tardía admiten 0 (reservar el mismo día; ninguna cancelación tardía); la ventana y el tope semanal deben ser mayores que 0; ningún valor puede ser negativo.

**Never:**
- Comandos, pantallas o handlers que cambien base, custom o reglas, y los eventos `AgendaBaseFueCambiada` y `AgendaCustomFue*` con la política P-1: épica 6.
- Calcular horarios libres (entrada 3) o validar una Cita contra los tramos (entrada 4).
- Revalidar tramos guardados cuando cambia la duración de un tipo (T-6).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Día hábil sin custom | Base sembrada, un martes | `TramosVigentes` = [15:30–18:00], origen base | — |
| Sábado sin custom | Base sembrada, un sábado | Sin tramos, origen base | — |
| Fecha con custom | Custom ese martes con [16:00–17:00] | [16:00–17:00], origen custom | — |
| Custom "no se atiende" | Custom con cero tramos y motivo | Sin tramos, origen custom | — |
| Tramos encimados | Base o custom con [15:30–17:00] y [16:30–18:00] | No se guarda | Rechazo del dominio (INV-B2, INV-A2) |
| Tramo más corto que el tipo más corto | Tramo de 20 min con duración mínima 30 | No se guarda | Rechazo (INV-B5, INV-A6) |
| Segunda custom en la misma fecha | `RegistrarCustom` sobre una fecha que ya tiene | No se registra | Rechazo (INV-A1) |
| Custom sin motivo | Motivo vacío | No se registra | Rechazo (INV-A4) |
| Reglas inválidas | Ventana 0, umbral negativo o tope 0 | No se aceptan | Rechazo de `ReglasDeReserva` |

</frozen-after-approval>

## Code Map

Rutas bajo `src/DraSelina.Consultorio.Web/` salvo que digan otra cosa. Se apoya en lo que dejó la entrada 1.

- `Modules/Agenda/Domain/` -- ya tiene `Cita`, `RangoDeTiempo`, `TipoDeVisita`, `HoraLocal` (zona `America/Mexico_City`), puertos `ICitas`/`ITiposDeVisita` con resultado por enum (`GuardadoDeCita`) en lugar de excepción. Imitar ese estilo: el dominio solo usa `Kernel` y la biblioteca base; rechazos como enum o valor, no códigos de `Contracts`.
- `Modules/Agenda/Infrastructure/AgendaDbContext.cs` -- columnas `snake_case` explícitas, `HasCheckConstraint`, `HasData` con ids fijos (ver `TiposDeVisitaSembrados.cs`); SQL explícito en la migración solo donde EF no llega.
- `Modules/Agenda/Infrastructure/Registro.cs` -- registrar los puertos nuevos uno por línea.
- `Modules/Agenda/Infrastructure/Migrations/` -- crear con `./dotnet.sh ef migrations add CrearAgendaDeAtencion --project src/DraSelina.Consultorio.Web --context AgendaDbContext --output-dir Modules/Agenda/Infrastructure/Migrations --namespace DraSelina.Consultorio.Agenda.Infrastructure.Migrations` (README, "Crear una migración"); nunca `database update`.
- `tests/DraSelina.Consultorio.Domain.Tests/` -- pruebas de dominio sin base (imitar `CitaTests.cs`).
- `tests/DraSelina.Consultorio.IntegrationTests/` -- `Arnes.CrearBaseMigradaAsync` + `ArrancarWeb…`; imitar `ReservaEnLineaTests.cs`. `BaseMigradaTests` fija la lista de esquemas con historial: no cambia (no hay contexto nuevo).
- `IClock` (Kernel) -- "hoy" local para cargar las custom vigentes, vía `HoraLocal`.

## Tasks & Acceptance

**Execution:**
- [ ] `Modules/Agenda/Domain/Tramo.cs` -- objeto de valor: hora local de inicio y fin, inicio < fin, `SeEnciman`, `Contiene(inicio, fin)` y duración -- INV-B1, INV-A2; lo reutilizan las entradas 3 y 4.
- [ ] `Modules/Agenda/Domain/AgendaAtencion.cs` (+ entidad interna `AgendaCustom`, valor `TramosVigentes` con su origen base/custom) -- `CambiarBase`, `RegistrarCustom`, `CambiarCustom`, `BorrarCustom`, `TramosVigentes(fecha)`, `FechasConCustom()`; rechazos por enum -- INV-B1 a B5, INV-A1 a A6, INV-V1.
- [ ] `Modules/Agenda/Domain/ReglasDeReserva.cs` -- agregado único: anticipación mínima (días), ventana (meses), umbral de cancelación tardía (min), tope semanal; `Cambiar` valida rangos positivos.
- [ ] `Modules/Agenda/Domain/IAgendaAtencion.cs`, `IReglasDeReserva.cs` -- puertos: obtener (custom desde una fecha) y guardar.
- [ ] `Modules/Agenda/Infrastructure/` -- tablas `agenda_base_tramo`, `agenda_custom`, `agenda_custom_tramo` (cascada desde la custom) y `reglas_de_reserva` (`CHECK (id = 1)`) con sus `CHECK` y únicos de la Parte II; implementaciones de los puertos que arman el agregado desde las filas; datos iniciales con ids fijos; registro; migración `CrearAgendaDeAtencion`.
- [ ] `tests/DraSelina.Consultorio.Domain.Tests/` -- `TramoTests`, `AgendaAtencionTests`, `ReglasDeReservaTests`: cada fila de la matriz.
- [ ] `tests/DraSelina.Consultorio.IntegrationTests/AgendaDeAtencionTests.cs` -- lee la agenda y las reglas sembradas desde la base migrada; guarda una custom y la vuelve a leer con sus tramos; una custom de ayer no se carga.

**Acceptance Criteria:**
- Given la base migrada, when se obtiene `AgendaAtencion`, then lunes a viernes tienen [15:30–18:00] y sábado y domingo no tienen tramos.
- Given la base migrada, when se obtiene `ReglasDeReserva`, then valen 1 día, 2 meses, 120 min y 3.
- Given la solución, when corren todas las pruebas, then pasan, incluidas las de arquitectura.

## Implementation Notes

## Plan Change Log

## Review Triage Log

**Pasada 1 (quick), 2026-11-02.** Veredictos: high 0, medium 1, low 3, false 0, maybe-false 0.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | `RegistrarCustom` sobre una fecha anterior a `CustomsDesde` pasa el `ContainsKey` y al guardar sobrescribe en silencio la custom pasada (INV-A1) | medium | patch (grupo A) | Confirmado en `AgendaAtencionPersistida.SincronizarCustomsAsync`: el filtro `c.Fecha >= desde \|\| fechas.Contains(c.Fecha)` carga la fila pasada y `CambiarFila` la reemplaza |
| 2 | `AgendaAtencion.Crear()` (`CustomsDesde = MinValue`) guardado por el puerto borra todo el historial de custom | low | patch (grupo A) | Misma causa: la sincronización no se limita a lo que el agregado cargó ni a hoy en adelante. Sin llamador hoy, pero el supuesto de la Parte I dice que las pasadas no se tocan |
| 3 | Reglas `int` convertidas a `smallint` con `(short)` sin comprobar: 40000 se guarda como -25536 | low | patch | Confirmado: `Validar` solo exige `> 0` y la conversión de EF no comprueba; corrección directa en la conversión |
| 4 | `Ordenados` entrega el `Tramo[]` interno como `IReadOnlyList`; se puede convertir de vuelta y mutar, saltándose INV-B2/A2/B5/A6 | low | patch | Confirmado; corrección directa: entregar una vista de solo lectura |

Grupo A (1 y 2): la sincronización de custom solo toca fechas desde `max(CustomsDesde, hoy local)`, y una custom del agregado anterior a esa fecha es un error de programación que lanza.

## Design Notes

- **`AgendaAtencion` se arma a mano desde sus filas.** Es un solo agregado sobre tres tablas y sin id propio; el puerto lee la base completa y las custom desde una fecha, y al guardar sincroniza filas. Evita forzar EF a modelar un agregado sin llave.
- **Los métodos que cambian base y custom entran aquí, sin comandos.** Son donde viven INV-B y INV-A, y las pruebas de INV-V1 los necesitan para preparar fechas con custom. Los comandos, la pantalla y los eventos que levantan llegan con la épica 6.

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` -- expected: todas pasan (Docker arriba).
- `./dotnet.sh run --project src/DraSelina.Consultorio.AppHost` y `scripts/verificar-arranque.sh` -- expected: las tres filas pasan; `agenda` con 4 migraciones en su historial.
