---
title: 'Bitácora y CitaAgendada con cada reserva'
type: 'feature'
ticket: '6'
created: '2026-11-02'
status: 'built'
baseline_revision: '22816c3c08bfac94db24954fe484953a7147b337'
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

**Problem:** Una reserva hoy no deja rastro: no hay bitácora de quién creó la Cita, y Notificaciones (épica 4) no tiene evento del cual enterarse.

**Approach:** El mecanismo de bitácora de Agenda (`agenda.bitacora`, solo inserción) y el evento de integración `CitaAgendada` en el outbox, ambos escritos por `AgendarCitaEnLinea` en la misma transacción que la Cita (CAP-17, RN-24, AD-3).

## Boundaries & Constraints

**Always:**
- `agenda.bitacora` según la Parte II: `id` Guid v7, `ocurrido_en`, `actor_tipo` (`Paciente`, `Administrador`, `Sistema`), `actor_id` opcional, `entidad`, `entidad_id`, `accion`, `detalle` jsonb; índice `(entidad, entidad_id, ocurrido_en)`. Solo se inserta; nunca se actualiza ni se borra.
- Decisión (Gerardo, 2026-11-02): `actor_id` se crea opcional y una reserva del Paciente lo escribe nulo; el id del Administrador se resuelve en la épica 5.
- El actor sale de `IActorActual` (AD-22); una reserva en línea sin sesión es `Paciente`.
- La entrada de una reserva: `entidad = Cita`, `entidad_id` = el `CitaId`, `accion = Agendada`, `detalle` con inicio, fin, tipo de visita y origen.
- `CitaAgendada` (AD-3): `EventoId`, `OcurridoEn`, `CitaId`, `PatientId`, `Actor`, `Inicio`, `Fin`, `Origen`; se publica con `IOutboxDeAgenda.PublicarAsync` y clave de orden el `CitaId`.
- Bitácora, evento y Cita se confirman juntos o ninguno (T-30): todo dentro de la transacción del handler y después de guardar la Cita sin traslape.
- AD-23: ni la bitácora ni el evento llevan nombre, teléfono, correo, motivo de consulta ni enlace de gestión.

**Never:**
- Handlers que consuman `CitaAgendada` (épica 4); bitácora de reprogramar, cancelar, no se presentó o cambios de agenda (épicas 3, 5 y 6); mostrar la bitácora (épica 5).
- Cambiar el mecanismo de outbox o su entrega (épica 1, entrada 11).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Reserva válida | Martes 4:00 pm, sin sesión | Una fila de bitácora (`Paciente`, `actor_id` nulo, `Cita`, `Agendada`) y un `CitaAgendada` pendiente en `agenda.outbox` con los datos de la Cita | — |
| Traslape | Ya hay una Cita en ese horario | Ni bitácora ni evento | `Fallo(agenda.horario_ocupado)` |
| Rechazo del dominio | Fuera de horario, de ventana o en el pasado | Ni bitácora ni evento | Su código |
| Sin datos personales | Reserva con nombre, teléfono, correo y motivo | `detalle` y el evento no contienen ninguno de esos valores | — |
| Entrega | El servicio de entrega corre | El evento se entrega (o queda `Entregado` sin handler, con aviso) | Comportamiento ya existente |

</frozen-after-approval>

## Code Map

Rutas bajo `src/DraSelina.Consultorio.Web/` salvo que digan otra cosa.

- `Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs` -- flujo: tx → candado → reglas y agenda → `Cita.Agendar` → `citas.GuardarAsync` (traslape → deshace) → `ConfirmarAsync`. Bitácora y evento van entre el guardado exitoso y la confirmación.
- `Modules/Agenda/Features/IOutboxDeAgenda.cs` -- `PublicarAsync(evento, eventoId, claveDeOrden, ocurridoEn, ct)`; lanza sin transacción abierta. `Infrastructure/OutboxDeAgenda.cs`, `FilaDeOutbox.cs`: no se tocan. El ejemplo de uso completo está en `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/Anotar.cs`.
- `Kernel/IActorActual.cs`, `Kernel/Actor.cs` -- `ObtenerAsync()` → `Paciente`, `Administrador` o `Sistema`.
- `Modules/Agenda/Domain/Cita.cs` -- `Id`, `PacienteId`, `Rango`, `TipoDeVisitaId`, `Origen`.
- `Modules/Agenda/Infrastructure/AgendaDbContext.cs`, `TextoDeEnumeraciones.cs` -- mapeo `snake_case`, enumeraciones como `text` con `CHECK`.
- `Modules/Agenda/Contracts/` -- ahí vive el evento de integración (lo consumirá Notificaciones).
- `tests/DraSelina.Consultorio.IntegrationTests/ReservaEnLineaTests.cs` -- `Escenario` con reloj fijo y `ArrancarWebSinEntregaAutomatica`; imitar. `Los_registros_no_llevan_nombre_telefono_correo_ni_motivo` es el patrón para AD-23.

## Tasks & Acceptance

**Execution:**
- [x] `Modules/Agenda/Contracts/CitaAgendada.cs` -- el evento de integración con los campos de Boundaries.
- [x] `Modules/Agenda/Features/IBitacoraDeAgenda.cs` + `Infrastructure/` (fila, mapeo, implementación, registro) + migración `CrearBitacora` -- la tabla y su índice; escribir una entrada dentro de la transacción abierta.
- [x] `Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs` -- pide el actor; tras guardar la Cita, escribe la bitácora y publica `CitaAgendada`; después confirma.
- [x] `tests/DraSelina.Consultorio.IntegrationTests/` -- cada fila de la matriz contra la base migrada: la fila de bitácora y la del outbox tras una reserva; ninguna tras traslape o rechazo; `detalle` y el evento sin datos personales.

**Acceptance Criteria:**
- Given una reserva válida, when se confirma, then `agenda.bitacora` tiene su entrada con actor `Paciente` y `agenda.outbox` su `CitaAgendada` pendiente; given que la Cita no se confirma, then no queda ninguno.
- Given la solución, when corren todas las pruebas, then pasan, incluidas las de arquitectura.

## Implementation Notes

- `CitaAgendada.Origen` es `string` con los mismos textos que `agenda.cita.origen` (`en_linea`, `consultorio`), como constantes del record: `Contracts` solo ve `Kernel` y `BuildingBlocks`, así que no puede usar `OrigenDeCita`. `[ASSUMPTION]`
- `detalle` de la entrada de reserva: `{inicio, fin, tipoDeVisitaId, origen}`; el tipo de visita va por id, no por nombre. `[ASSUMPTION]`
- `agenda.bitacora` lleva `CHECK` en `actor_tipo`, `entidad` y `accion` con los conjuntos completos de la Parte II; en código son `Actor`, `EntidadDeBitacora` y `AccionDeBitacora` guardados por su nombre. `BitacoraDeAgenda` inserta con SQL directo (como `OutboxDeAgenda`) y lanza sin transacción abierta.
- El handler pide el actor al principio, antes de crear al Paciente. Las pruebas que llaman al handler fuera de una petición fijan un usuario anónimo en el ámbito (`ReservaEnLineaTests.SinSesion`); sin eso `ActorDelHost` lanza "no tiene actor".

## Plan Change Log

## Review Triage Log

**Verificación, 2026-11-03.** El AppHost no arrancaba: `migraciones` terminaba con 134 porque el MigrationService registra los handlers de Agenda y no tenía `IActorActual`; el arnés no lo veía porque no corría en Development. Parche: `ActorSistema` en el MigrationService y arnés en Development (decisión de Gerardo en la épica, entrada 6). Después, las tres filas de `verificar-arranque.sh` pasan.

**Pasada 1 (quick), 2026-11-03.** Veredictos: high 0, medium 1, low 3, false 0, maybe-false 0.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | `ActorSistema` y el arnés en Development son decisiones no planeadas ni registradas; AD-22 pide un único servicio de actor | medium | resuelto con Gerardo | Se le presentó; eligió mantener `ActorSistema` como excepción documentada solo en el MigrationService (Notes de la épica) |
| 2 | Ninguna prueba hace fallar una escritura después de guardar la Cita (T-30) | low | rechazado | La garantía es de la transacción del módulo y ya la cubren `CasoDeUsoTests.Lo_que_el_handler_guarda_y_no_confirma_no_queda`, `Un_fallo_inesperado_sale_como_excepcion_y_no_deja_fila` y `OutboxTests.Si_el_comando_no_confirma_despues_de_publicar_no_queda_ni_el_cambio_ni_el_evento` |
| 3 | `CitaAgendada.Actor` se guarda como número en el outbox | low | patch | Confirmado; ahora por nombre con `JsonStringEnumConverter<Actor>` y prueba |
| 4 | La base no impide actualizar o borrar la bitácora | low | rechazado | Ni el plan ni la Parte II piden un mecanismo en la base; solo se inserta por `BitacoraDeAgenda` |

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` -- expected: todas pasan (Docker arriba).
- `./dotnet.sh run --project src/DraSelina.Consultorio.AppHost` y `scripts/verificar-arranque.sh` -- expected: las tres filas pasan; `agenda` con su migración nueva.
