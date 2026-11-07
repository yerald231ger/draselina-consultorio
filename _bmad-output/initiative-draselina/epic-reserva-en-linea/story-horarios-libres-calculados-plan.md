---
title: 'Horarios libres calculados'
type: 'feature'
ticket: '3'
created: '2026-11-02'
status: 'done'
baseline_revision: '6fdd84577da520444f94d783f5a9f6b2266c82c9'
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

**Problem:** Nadie calcula todavía qué horarios puede reservar un Paciente: existen la agenda de atención, las reglas y las Citas, pero no la cuenta que las une.

**Approach:** `CalculadoraDeDisponibilidad` como función pura del dominio (AD-5, T-21) y la consulta `HorariosLibres` por Tipo de visita que la alimenta desde la base y la entrega a la interfaz.

## Boundaries & Constraints

**Always:**
- El algoritmo de la Parte I, sección 2.2: por cada fecha de la ventana, pedir a `AgendaAtencion` sus tramos vigentes (INV-V1); dentro de cada tramo, la rejilla desde su inicio en pasos de la duración del tipo, solo los que caben completos; quitar los que traslapan una Cita no cancelada de cualquier origen, `[inicio, fin)` (AD-8); quitar los anteriores a la anticipación mínima y los posteriores a la ventana.
- Anticipación mínima en días locales: con N días, la primera fecha es hoy + N (con 1, mañana); con 0, hoy, pero solo horarios cuyo inicio sea posterior a `now`. Ventana: hasta hoy + `VentanaMeses` meses, inclusive. Todo en hora local de `America/Mexico_City`, comparando contra Citas en UTC (AD-17).
- La calculadora no toca la base ni lee el reloj: recibe agenda, Citas activas, duración, reglas y `now`. Es la única implementación de la rejilla (T-21): la entrada 4 la reutiliza para validar una Cita.
- La consulta corre sin transacción y con `AsNoTracking` (Consistency Conventions); un tipo inexistente o no reservable en línea vuelve como `Fallo(agenda.tipo_no_reservable)`.
- Entrega a la interfaz cada fecha que tiene tramos vigentes dentro de la ventana, con sus horas libres; una fecha con tramos y sin horas libres va con la lista vacía (la entrada 5 la pinta "Lleno"); las fechas sin tramos no van.

**Never:**
- Persistir horarios libres o cualquier inventario de slots (RN-3).
- Validar o guardar una Cita (entrada 4), pantallas (entrada 5), candado por fecha (entrada 4).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Día hábil libre | Base sembrada, tipo de 30 min, mañana es martes | Mañana: 15:30, 16:00, 16:30, 17:00, 17:30 | — |
| Rejilla con sobrante | Tramo 15:30–17:45, tipo de 30 min | 15:30 … 17:00; 17:30 no (no cabe completo) | — |
| Cita en línea | Cita Agendada mañana 16:00–16:30 | Mañana sin 16:00 | — |
| Cita del consultorio fuera de rejilla | Cita 16:10–16:40 | Mañana sin 16:00 ni 16:30 | — |
| Cita cancelada | Cita Cancelada mañana 16:00 | 16:00 sigue libre | — |
| Hoy con anticipación 1 | Horarios de hoy | No aparece hoy | — |
| Anticipación 0 | Hoy a las 16:10 local | Hoy solo 16:30, 17:00, 17:30 | — |
| Fin de la ventana | Ventana 2 meses | Última fecha = hoy + 2 meses; la siguiente no aparece | — |
| Sábado sin custom; custom de cero tramos | Base sembrada | Esas fechas no aparecen | — |
| Custom que abre un sábado | Custom sábado 10:00–11:00 | Sábado: 10:00, 10:30 | — |
| Día lleno | Citas en las cinco horas de un martes | El martes aparece con la lista vacía | — |
| Tipo no reservable | Tipo inexistente o no reservable en línea | Sin horarios | `Fallo(agenda.tipo_no_reservable)` |

</frozen-after-approval>

## Code Map

Rutas bajo `src/DraSelina.Consultorio.Web/` salvo que digan otra cosa.

- `Modules/Agenda/Domain/AgendaAtencion.cs` -- `TramosVigentes(fecha)` devuelve tramos y origen; única fuente de tramos (INV-V1). No reimplementar la elección base/custom.
- `Modules/Agenda/Domain/Tramo.cs` -- `Inicio`, `Fin` (`TimeOnly` local), `Contiene(inicio, fin)`, `Duracion`.
- `Modules/Agenda/Domain/ReglasDeReserva.cs` -- `AnticipacionMinimaDias` (≥ 0), `VentanaMeses` (> 0).
- `Modules/Agenda/Domain/RangoDeTiempo.cs` -- `Traslapa` semiabierto; reutilizar para el choque con Citas.
- `Modules/Agenda/Domain/HoraLocal.cs` -- `AUtc(fecha, hora)` y `DesdeUtc(instante)`.
- `Modules/Agenda/Domain/TipoDeVisita.cs` -- `Duracion`, `SeAgendaEnLinea`.
- `Modules/Agenda/Domain/IAgendaAtencion.cs`, `IReglasDeReserva.cs`, `ITiposDeVisita.cs` -- puertos ya registrados; `ObtenerAsync(customsDesde)` con hoy local.
- `Modules/Agenda/Contracts/AgendarCitaEnLinea.cs` -- ahí viven `CodigosDeAgenda`; reutilizar `TipoNoReservable`.
- `BuildingBlocks/Contracts/IQuery.cs`, `IQueryHandler.cs` -- `HandleAsync(consulta, ct)` → `Resultado<T>`; `UnHandlerPorCasoDeUsoTests` exige un handler por consulta.
- `Modules/Agenda/Infrastructure/AgendaDbContext.cs` -- `Citas` (`estado`, `inicio`, `fin`); índice `ix_cita_inicio` para la ventana.
- `tests/DraSelina.Consultorio.Domain.Tests/` y `tests/DraSelina.Consultorio.IntegrationTests/` -- imitar `CitaTests.cs`, `AgendaAtencionTests.cs` y `ReservaEnLineaTests.cs` (`RelojDePrueba` para fijar `now`).

## Tasks & Acceptance

**Execution:**
- [x] `Modules/Agenda/Domain/CalculadoraDeDisponibilidad.cs` (+ `HorarioLibre`) -- función pura con el algoritmo de Boundaries; expone también si un inicio dado cae en la rejilla de un tramo vigente, para la entrada 4.
- [x] `Modules/Agenda/Features/ICitasActivas.cs` -- puerto de lectura: rangos de Citas no canceladas entre dos instantes.
- [x] `Modules/Agenda/Contracts/HorariosLibres.cs` -- consulta `HorariosLibres : IQuery<IReadOnlyList<DiaConHorarios>>` con `TipoDeVisitaId`; `DiaConHorarios(Fecha, IReadOnlyList<TimeOnly> Horas)`.
- [x] `Modules/Agenda/Features/HorariosLibresHandler.cs` -- lee tipo, reglas, agenda (custom desde hoy local) y Citas activas de la ventana; llama a la calculadora; agrupa por fecha.
- [x] `Modules/Agenda/Infrastructure/` -- implementación de `ICitasActivas` con `AsNoTracking`; registro del puerto y del handler.
- [x] `tests/DraSelina.Consultorio.Domain.Tests/CalculadoraDeDisponibilidadTests.cs` -- cada fila de la matriz sin base.
- [x] `tests/DraSelina.Consultorio.IntegrationTests/HorariosLibresTests.cs` -- la consulta contra la base migrada: día hábil sembrado, una Cita guardada que quita su horario, tipo no reservable.

**Acceptance Criteria:**
- Given la base migrada y `now` un lunes a las 10:00 local, when se consultan los horarios de "Primera consulta", then el martes ofrece 3:30, 4:00, 4:30, 5:00 y 5:30 pm y no aparecen el lunes, el sábado ni el domingo.
- Given la solución, when corren todas las pruebas, then pasan, incluidas las de arquitectura y la de un handler por caso de uso.

## Implementation Notes

- La calculadora devuelve `DisponibilidadDelDia(Fecha, IReadOnlyList<HorarioLibre>)` por fecha y no una lista plana de `HorarioLibre`: con la lista plana, el handler tendría que volver a saber qué fechas tienen tramos para mandar las llenas con la lista vacía, y eso reimplementaría parte del cálculo (T-21). El handler solo proyecta a `DiaConHorarios`.
- La ventana vive en la calculadora (`CalculadoraDeDisponibilidad.Ventana(reglas, now)` → `VentanaDeReserva`, con `EnUtc()`): el handler la usa para pedir las Citas activas sin recalcular fechas.
- `EstaEnLaRejilla(TramosVigentes, hora, duracion)` usa la misma rejilla privada que `Calcular`; recibe `TramosVigentes` porque es lo que el handler de la entrada 4 le pasará a la Cita.
- El filtro "inicio posterior a `now`" se aplica siempre; con anticipación ≥ 1 no quita nada porque la primera fecha ya es futura.
- `[ASSUMPTION]` Las reglas se leen con el puerto existente `IReglasDeReserva`, que rastrea su única fila (lo necesita para guardar). Tipo, agenda y Citas activas sí van con `AsNoTracking`. La consulta no abre transacción.
- La fila "Cita cancelada" de la matriz se prueba en integración: la calculadora solo recibe Citas activas, y quien descarta las canceladas es `CitasActivas`.

## Plan Change Log

## Review Triage Log

**Pasada 1 (quick), 2026-11-02.** Veredictos: high 0, medium 0, low 3, false 0, maybe-false 0. Sin parches.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | `HorariosLibresHandler` lee las reglas con el puerto `IReglasDeReserva`, que rastrea la fila: se aparta de "con `AsNoTracking`" | low | rechazado | La fila rastreada vive solo en el ámbito de la petición y la consulta nunca llama a `SaveChanges`; sin daño observable. Corregirlo pide un puerto de lectura nuevo (más superficie) |
| 2 | `CitasActivas` filtra `inicio < hasta AND fin > desde`: el índice de `inicio` solo acota por arriba y el recorrido crece con el historial | low | rechazado | Real, pero con el volumen de un consultorio no se nota en años; acotar por abajo exige suponer una duración máxima de Cita que el modelo no fija. Candidato para el Refactor sweep (2.13) |
| 3 | Una fecha con tramos y lista vacía sale también cuando hoy ya pasó (anticipación 0) o cuando el tipo no cabe en el tramo; la entrada 5 la pintaría "Lleno" | low | rechazado | Coincide con la intención congelada ("una fecha con tramos y sin horas libres va con la lista vacía"); se lleva a la entrada 5 como pregunta para Gerardo |

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` -- expected: todas pasan (Docker arriba).
