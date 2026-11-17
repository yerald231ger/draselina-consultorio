---
title: 'Elegir cita por cita: evento interno y P-1'
type: 'feature'
ticket: '2'
created: '2026-11-17'
status: 'built'
baseline_revision: 'e51234bb7d6ffd61cd783288832fe12518c61741'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-draselina/architecture-appointment-booking/ddd-and-data-model-appointment-booking.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Desde la 6.1, una custom que deja Citas fuera solo devuelve conflicto: Gerardo no puede cerrar el sábado cancelando tres Citas y conservando la de las 12:30 (UJ-7), ni borrar una custom (RN-12).

**Approach:** `GuardarAgendaCustom` y el nuevo `BorrarAgendaCustom` reciben la lista vista con la decisión por Cita (RN-9). Guardan solo la custom y, en la misma transacción, un evento interno de Agenda con las Citas elegidas y el actor (AD-6, T-30). La política P-1 lo recibe y cancela cada una en su propia transacción a nombre de ese Administrador (T-31), con la misma cancelación que `CancelarCitaPorConsultorio`.

## Boundaries & Constraints

**Always:**
- La lista vista coincide si sus `CitaId` son exactamente los de `CitasQueQuedanFuera` para los Tramos nuevos (en borrar, los de la base de ese día); si no, `Fallo(CitasFuera)` y nada cambia. Lista vacía con ninguna fuera es éxito, como en la 6.1.
- Guardar o borrar la custom no toca ninguna Cita (RN-10). El evento lleva fecha, ids de las elegidas para cancelar, actor e id del Administrador; nunca datos del Paciente (AD-23). Si no hay elegidas, no se publica.
- P-1: por cada Cita, su transacción, su fila con `FOR UPDATE`, `cita.CancelarPorConsultorio`, bitácora con el actor y su id del evento, `CitaCancelada` motivo consultorio. Omite, sin bitácora ni evento, la que ya no está Agendada o ya empezó (T-29, RN-31). Entregar el evento dos veces no cambia nada.
- Un solo camino para cancelar por el Consultorio: la lógica de `CancelarCitaPorConsultorioHandler` se comparte con P-1; el comando conserva su comportamiento y sus pruebas.
- Decisión (Gerardo, 2026-11-17): el handler arma el evento interno, como `CitaCancelada` hoy; `AgendaAtencion` no cambia y no levanta eventos. `AgendaBaseFueCambiada` no se crea: nadie la escucha (T-23).
- `BorrarAgendaCustom`: `IRequiereAdministrador`, candado de fecha, fecha pasada `FechaPasada`, fecha sin custom `Fallo(FechaSinCustom)`, bitácora `AgendaCustom/Borrada` con el antes.

**Never:**
- La pantalla (lista, "cancelar todas", quitar): es de la 6.3; la página de la 6.1 manda la lista vacía.
- Cancelar dentro de la transacción de la custom, o como `Sistema` (T-31).
- La consulta que la pantalla usará para pintar la lista (6.3).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| UJ-7 | Sáb. con 4 Citas, cero Tramos; cancelar 3, conservar 12:30 | Custom guardada; tras la entrega, 3 Canceladas por el Consultorio a nombre del Administrador con disculpa; 12:30 Agendada, sin aviso | — |
| Conservar todas | Igual, conservar las 4 | Custom guardada, sin evento | — |
| Lista que no coincide | Falta una Cita, sobra una, o llegó una nueva | Nada cambia | `Fallo(agenda.citas_fuera)` |
| Entrega doble | Mismo evento dos veces | Una bitácora y un `CitaCancelada` por Cita | — |
| Ya no Agendada / ya empezó | La Cita se canceló o empezó antes de la entrega | Se omite; las demás se cancelan | — |
| Borrar custom que abría sábado | Sáb. 10–13 con 2 Citas; borrar cancelando 1 | Custom borrada; 1 Cancelada, 1 conservada | — |
| Borrar sin custom / fecha pasada | — | Nada cambia | `FechaSinCustom` / `FechaPasada` |
| Sin sesión | Guardar o borrar | — | No autorizado |

</frozen-after-approval>

## Code Map

- `Modules/Agenda/Contracts/GuardarAgendaCustom.cs` -- agregar la lista `Citas` (CitaId, Cancelar) con su validación de forma.
- `Modules/Agenda/Features/GuardarAgendaCustomHandler.cs` -- de la 6.1: conflicto con cualquier fuera → comparar con la lista; publicar el evento.
- `Modules/Agenda/Features/CitasQueQuedanFuera.cs` -- de la 6.1: da los ids a comparar.
- `Modules/Agenda/Features/CancelarCitaPorConsultorioHandler.cs` -- lógica a compartir: `ObtenerPorIdParaCambiarAsync`, `CancelarPorConsultorio`, `DejarRastroAsync` (bitácora + `CitaCancelada`).
- `Modules/Agenda/Features/IOutboxDeAgenda.cs` -- `PublicarAsync(evento, eventoId, claveDeOrden, ocurridoEn)`; el nombre del tipo es la columna `tipo` y debe ser único (`CatalogoDeEventos`).
- `EntregaDeEventos.cs` -- entrega cada evento a cada handler en un ámbito propio; reintenta si lanza.
- `Modules/Notificaciones/Infrastructure/Registro.cs:45` -- cómo se registra un `IEventHandler<T>`; Agenda lo hará en su `Registro.cs`.
- `Modules/Notificaciones/Features/AvisoAlCancelar.cs` -- la disculpa ya sale con `CitaCancelada` motivo consultorio.
- Pruebas: `tests/…IntegrationTests/GuardarAgendaCustomTests.cs` (escenario de la 6.1), `NoSePresentoYCancelarPorConsultorioTests.cs`, `Arnes.DarUnaVueltaDeEntregaAsync`, `Arnes.Canal(web).Enviados`.

## Tasks & Acceptance

**Execution:**
- [x] `Contracts/GuardarAgendaCustom.cs` + `Contracts/BorrarAgendaCustom.cs` -- lista de decisiones; código `FechaSinCustom`.
- [x] `Features/EventosDeAgendaCustom.cs` -- los tres eventos internos, armados por los handlers: `AgendaCustomFueRegistrada`, `AgendaCustomFueCambiada`, `AgendaCustomFueBorrada`.
- [x] `Features/CancelacionPorConsultorio.cs` -- servicio compartido; `CancelarCitaPorConsultorioHandler` lo usa.
- [x] `Features/GuardarAgendaCustomHandler.cs`, `Features/BorrarAgendaCustomHandler.cs` -- comparar la lista, guardar, bitácora, evento.
- [x] `Features/CancelarCitasElegidas.cs` -- P-1, `IEventHandler` de los tres eventos.
- [x] `Infrastructure/Registro.cs` -- alta.
- [x] Pruebas de integración de cada fila de la matriz; las de `CancelarCitaPorConsultorio` siguen pasando.

**Acceptance Criteria:**
- Given UJ-7 entregado, when se lee la bitácora, then cada Cancelada tiene actor Administrador y su id, y la custom tiene una entrada `Registrada`.
- Given una entrega que falla a la mitad, when se reintenta, then las ya canceladas no se repiten y las demás se cancelan.

## Implementation Notes

- Implementado y revisado en esta sesión: el entorno niega los subagentes; Gerardo eligió en la 6.1 que la revisión se haga aquí, y se repitió.
- `CancelacionPorConsultorio` es el único camino: `CancelarCitaPorConsultorioHandler` quedó como orquestador de la transacción; sus pruebas pasan sin cambios.
- Los eventos viven en `Features/EventosDeAgendaCustom.cs` con la interfaz `IEventoDeAgendaCustom`; `GuardarAgendaCustomHandler.PublicarAsync` los publica con su tipo concreto (el outbox guarda el nombre del tipo). Clave de orden: el propio `EventoId`.
- P-1 es `CancelarCitasElegidas`, registrado tres veces, una por evento.
- `CitasQueQuedanFuera.Coincide` compara conjuntos de ids; la forma del comando rechaza ids repetidos o vacíos bajo `Citas[i]`.
- La bitácora de la custom agrega `citasACancelar` al detalle.
- La página de la 6.1 manda la lista vacía: con Citas fuera sigue diciendo que no se guardó (la lista es de la 6.3).
- Pruebas: `ElegirCitaPorCitaTests` (14). Suite completa: 1,066 pasan.

## Plan Change Log

## Review Triage Log

**Pasada 1 (lente Quick, en esta sesión): 0 high, 1 medium, 0 low, 2 false, 0 maybe-false.**

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Guardar el mismo horario eligiendo cancelar una Cita conservada antes devolvía "sin cambios" y no la cancelaba. | medium | patch | El atajo de sin cambios iba antes de comparar la lista; ahora solo aplica sin Citas elegidas. Prueba `Guardar_el_mismo_horario_cancelando_una_conservada_antes_la_cancela`. |
| P-1 podría dejar Citas a medias si una cancelación lanza. | false | rechazado | Cada Cita confirma en su transacción; el evento se reintenta y las ya canceladas se omiten (`YaCancelada`); prueba de entrega doble. |
| El seguimiento de cambios de EF podría arrastrar una Cita entre iteraciones de P-1. | false | rechazado | `TransaccionDeAgenda.AbrirAsync` limpia el `ChangeTracker` en cada apertura. |

## Design Notes

`[ASSUMPTION]` Sin Citas elegidas para cancelar no se publica el evento: nadie más lo escucha (T-23).

`[ASSUMPTION]` P-1 omite una Cita que ya empezó cuando llega el evento: el Consultorio ya no puede cancelarla (RN-31) y queda como Atendida.

## Verification

**Commands:**
- `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"; dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.
