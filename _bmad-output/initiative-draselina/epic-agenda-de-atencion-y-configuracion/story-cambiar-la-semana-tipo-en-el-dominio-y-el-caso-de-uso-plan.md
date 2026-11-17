---
title: 'Cambiar la semana tipo, en el dominio y el caso de uso'
type: 'feature'
ticket: '4'
created: '2026-11-17'
status: 'built'
baseline_revision: '900d7d598f092f826c3912cb0886965a85832ca8'
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

**Problem:** La semana tipo solo cambia por base de datos: `AgendaAtencion.CambiarBase` existe, pero ningún caso de uso la usa (CAP-10).

**Approach:** Una consulta previa dice qué Citas quedarían fuera y qué fechas con custom no cambian; el comando `CambiarAgendaBase` recibe la semana completa (Decision del 2026-11-17), cambia cada día distinto en el agregado y deja una bitácora por día, sin cancelar nada ni publicar eventos.

## Boundaries & Constraints

**Always:**
- El comando trae los siete días, cada uno una vez, con sus Tramos (vacío: no se atiende); si falta o se repite un día, inválido.
- Solo los días cuyos Tramos cambian pasan por `CambiarBase`, con la duración del Tipo de visita activo más corto (INV-B5); si uno no vale, no se guarda ninguno y el error va bajo `Dias[i]` (Tramos encimados o donde no cabe una Cita).
- No cancela ni mueve Citas (RN-6, RN-8) y no toca las custom (RN-7). Sin evento (Decision de la 6.2).
- Bitácora `AgendaBase/Cambiada` por día cambiado, a nombre del Administrador con su id, `EntidadId` el día ISO (1 lunes … 7 domingo, como `agenda_base_tramo`), detalle con los Tramos antes y después.
- La respuesta del comando trae los días cambiados y las fechas con custom de hoy en adelante (RN-7: "al guardar, lista de fechas que no cambian").
- La consulta previa (`IRequiereAdministrador`) lista las Citas Agendadas que todavía no empiezan, en fechas sin custom de los días que cambian, que no caben en la base nueva (regla de `Cita.QuedaFueraDe`), con fecha, hora, Paciente, sin correo y origen; y las fechas con custom de hoy en adelante.
- La duración mínima se calcula en un solo lugar, compartido con `GuardarAgendaCustomHandler`.

**Never:**
- La pantalla (6.5). Candado de fecha `[ASSUMPTION: AD-26 solo lo pide para la custom; cambiar la base no cancela]`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Semana nueva | Lun, mié y vie a 15:30–17:00; Cita lunes 17:30; custom el martes | Vista previa: la Cita del lunes y el martes; al guardar, base nueva, Cita Agendada, 3 bitácoras | — |
| Sin cambios | La misma semana | Éxito, ningún día cambiado, sin bitácora | — |
| Un día no vale | Miércoles encimado | No se guarda ningún día | `Invalido` bajo `Dias[2]` |
| Tramo corto | Jueves 15:30–15:45 | No se guarda | `Invalido` bajo `Dias[3]` |
| Días incompletos | Seis días, o lunes dos veces | Nada | `Invalido` de forma |
| Siguiente reserva | Tras guardar | `HorariosLibres` del lunes termina a las 4:30 | — |
| Sin sesión | — | — | No autorizado |

</frozen-after-approval>

## Code Map

- `Modules/Agenda/Domain/AgendaAtencion.cs` -- `CambiarBase(dia, tramos, duracionMinima)`, `TramosBase(dia)`, `FechasConCustom()`; no cambia.
- `Modules/Agenda/Infrastructure/AgendaAtencionPersistida.cs` -- `GuardarAsync` ya sincroniza la base por (día, inicio).
- `Modules/Agenda/Infrastructure/FilasDeAgendaAtencion.cs:25` -- `FilaDeTramoBase.DiaIso`; en Features se repite la numeración ISO.
- `Modules/Agenda/Features/GuardarAgendaCustomHandler.cs` -- patrón (actor, transacción, bitácora) y `DuracionMinimaAsync` a extraer.
- `Modules/Agenda/Features/CitasQueQuedanFuera.cs` -- se agrega la versión para la base: varias fechas, salta las que tienen custom.
- `Modules/Agenda/Features/ICitasDeLaAgenda.cs` -- Citas de un rango UTC; para la base, de ahora en adelante.
- `Modules/Agenda/Features/CitasFueraDelHorarioHandler.cs` -- patrón de contactos en lote para la vista previa.
- `Modules/Agenda/Contracts/GuardarAgendaCustom.cs` -- `TramoAGuardar` se reusa.
- Pruebas: escenario de `GuardarAgendaCustomTests` (lunes 2 nov 2026, base lun–vie 15:30–18:00).

## Tasks & Acceptance

**Execution:**
- [x] `Contracts/CambiarAgendaBase.cs` -- comando (`Dias`: día de la semana + Tramos) con validación de forma; respuesta con días cambiados y fechas con custom.
- [x] `Contracts/CambioDeAgendaBase.cs` -- consulta previa con la misma semana → Citas fuera (con fecha) y fechas con custom.
- [x] `Features/DuracionMinima.cs` -- extraído; `GuardarAgendaCustomHandler` lo usa.
- [x] `Features/CitasQueQuedanFuera.cs` -- `DeLaBaseAsync(agendaNueva, diasCambiados)`.
- [x] `Features/CambiarAgendaBaseHandler.cs`, `Features/CambioDeAgendaBaseHandler.cs` + alta en `Registro.cs`.
- [x] Pruebas de integración de cada fila de la matriz y de la consulta previa.

**Acceptance Criteria:**
- Given el cambio de la primera fila guardado, when se lee la bitácora, then hay tres `AgendaBase/Cambiada` con días 1, 3 y 5 y Tramos antes y después.
- Given una Cita del Consultorio un sábado fuera de horario, when se cambia solo el lunes, then la vista previa no la lista.

## Implementation Notes

- Implementado y revisado en esta sesión (el entorno niega los subagentes; misma decisión que en las entradas anteriores).
- `DuracionMinima` es un servicio con ámbito; `GuardarAgendaCustomHandler` y `CambiarAgendaBaseHandler` lo usan.
- `CitasQueQuedanFuera.DeLaBaseAsync` lee las Citas de ahora en adelante (`ICitasDeLaAgenda.EntreAsync(ahora, DateTimeOffset.MaxValue)`) y salta las fechas con custom y los días que no cambian.
- Los errores del dominio van como `Invalido` bajo `Dias[i]`, con mensajes de `CambiarAgendaBase`, para que la 6.5 los pinte en su día.
- La vista previa lee la agenda con `IAgendaDelRango.LeerAsync(hoy, DateOnly.MaxValue)`, no con el puerto de escritura.
- Pruebas: `CambiarAgendaBaseTests` (6). Suite completa: 1,082 pasan.

## Plan Change Log

## Review Triage Log

**Pasada 1 (lente Quick, en esta sesión): 0 high, 0 medium, 2 low, 0 false, 0 maybe-false.**

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| `AgendaAtencionPersistida.GuardarAsync` sincroniza la base completa también al guardar una custom: si la base cambia entre que ese handler lee la agenda y guarda, el guardado de la custom la regresa a la anterior. Cambiar la base no toma candado. | low | rechazado | Requiere dos guardados del mismo Administrador en el mismo instante; el arreglo (candado de la base o sincronizar solo lo que cambió) agrega mecanismo. Se le informa a Gerardo. |
| La vista previa responde "La semana tiene los siete días…" también ante un Tramo al revés. | low | rechazado | Solo la llama la pantalla de la 6.5, que valida los Tramos antes; el comando da el mensaje correcto. |

## Design Notes

`[ASSUMPTION]` La vista previa solo mira los días que cambian: una Cita que ya estaba fuera en un día que no cambia (la del sábado de UJ-6) no se lista en cada cambio de base.

## Verification

**Commands:**
- `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"; dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.
