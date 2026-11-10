---
title: 'No se presentó y cancelar por el Consultorio, en el dominio y los casos de uso'
type: 'feature'
ticket: '5'
created: '2026-11-10'
status: 'built'
baseline_revision: '4b7b52d8ffeb5ea248becf477c63cffc8cff038c'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** El Administrador no puede marcar que un Paciente no se presentó ni cancelar una Cita a nombre del Consultorio (CAP-14, FR-16, FR-17); la `Cita` solo sabe cancelarse por el Paciente, y la épica 6 necesita `CancelarPorConsultorio` para su política P-1.

**Approach:** Dos transiciones nuevas en `Cita` (AD-9) y dos comandos de administración que las aplican por `CitaId`, con la fila bloqueada, la bitácora del Administrador y, al cancelar, `CitaCancelada` con motivo consultorio, que dispara la disculpa que ya existe (4.4). La interfaz es de la entrada 6.

## Boundaries & Constraints

**Always:**
- `cita.MarcarNoSePresento(now)`: solo si está Agendada y su inicio ya llegó (RN-31: a las 4:10 una Cita de 4:00 ya se puede; en el inicio mismo, también). Queda `NoSePresento`, final (INV-C7), sin motivo de cancelación (INV-C8).
- `cita.CancelarPorConsultorio(now)`: solo si está Agendada y su inicio no ha llegado (en el inicio mismo ya no). Queda `Cancelada` con motivo `Consultorio` y `CanceladaEn` (INV-C8). Sin umbral de tardía: el Consultorio nunca cancela "tarde". Es el mismo método que usará P-1 de la épica 6.
- Rechazos en el dominio, nunca en el handler (T-13): Cita no Agendada → `CodigosDeAgenda.CitaNoAgendada`; marcar antes del inicio → código nuevo `CodigosDeAgenda.AunNoEmpieza`; cancelar desde el inicio → código nuevo `CodigosDeAgenda.YaEmpezo` `[ASSUMPTION: nombres]`. Cita que no existe → `CitaNoEncontrada`.
- Repetir la misma acción no cambia nada: marcar No se presentó a una Cita que ya está `NoSePresento`, o cancelar por el Consultorio una ya Cancelada por el Consultorio, devuelve éxito con lo guardado, sin otra bitácora ni otro evento (el mismo trato que la cancelación por el Paciente, Gerardo 2026-11-06). Cualquier otro estado previo es `CitaNoAgendada`.
- Los dos comandos llevan `IRequiereAdministrador` (AD-22) y el `CitaId`. Un comando, una transacción (Consistency Conventions): leen la Cita por id con `FOR UPDATE` dentro de la transacción (dominio, `agenda.cita`, concurrencia), aplican la transición, guardan, escriben la bitácora (actor Administrador con su id; acción `NoSePresento` o `Cancelada`; detalle con estado anterior y nuevo, y el motivo de cancelación; nunca motivo de consulta, nota ni contacto, AD-23) y confirman.
- Sin candado de fecha: solo `FOR UPDATE` sobre la Cita, como la cancelación por el Paciente; cancelar o marcar no crea ni mueve rangos (AD-26 no lo pide) (Gerardo, 2026-11-10).
- Plan conservado completo, por encima de 1,600 tokens (Gerardo, 2026-11-10).
- Cancelar publica `CitaCancelada` (Inicio, Motivo `consultorio`, actor Administrador) en el outbox de la misma transacción; marcar No se presentó no publica nada (T-23).
- La disculpa al Paciente la envía el handler existente de Notificaciones (4.4) sin cambios; el Recordatorio se borra con el de 4.2.

**Never:** interfaz (entrada 6), revertir No se presentó, cancelar varias Citas a la vez o la política P-1 (épica 6), avisar a un Paciente sin correo, cambiar Notificaciones.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Marcar después del inicio | Cita 4:00 Agendada, ahora 4:10 | `NoSePresento`; bitácora del Administrador; sin evento | — |
| Marcar en el inicio | Ahora 4:00 | `NoSePresento` | — |
| Marcar antes | Ahora 3:50 | `AunNoEmpieza`; sin cambios | — |
| Marcar dos veces | Ya `NoSePresento` | Éxito; sin otra bitácora | — |
| Marcar una cancelada | Cancelada por el Paciente | `CitaNoAgendada` | — |
| Cancelar antes | Ahora 3:50 | `Cancelada`, motivo consultorio; bitácora; `CitaCancelada` en el outbox | — |
| Cancelar en el inicio o después | Ahora 4:00 o 4:10 | `YaEmpezo`; sin cambios | — |
| Cancelar dos veces | Ya Cancelada por el Consultorio | Éxito; sin otra bitácora ni evento | — |
| Cancelar una marcada | Ya `NoSePresento` | `CitaNoAgendada` | — |
| Disculpa de punta a punta | Cancelar por el Consultorio una Cita de Paciente con correo | Tras la entrega, la disculpa está en el canal de prueba; el Recordatorio ya no existe | — |
| Paciente sin correo | Igual, sin correo | Se cancela; no sale correo | — |
| Horario liberado | Cancelada por el Consultorio una Cita en línea de mañana 4:00 | `HorariosLibres` vuelve a ofrecer 4:00 | — |
| Cita inexistente | `CitaId` desconocido | `CitaNoEncontrada` | — |
| Sin sesión | Paciente | No autorizado; el handler no corre | — |
| Simultáneos | Cancelar y marcar a la vez sobre la misma Cita | Uno gana; el otro ve el estado ya cambiado (`CitaNoAgendada`) | — |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs` -- `Cancelar(now, reglas)` (Paciente) es el modelo de transición; `RechazoAlCancelar { CitaNoAgendada, YaEmpezo }` (línea ~400) ya existe. Agregar `MarcarNoSePresento`, `CancelarPorConsultorio` y el rechazo `AunNoEmpieza` (enum propio o compartido, como quede más claro). `EstadoCita.NoSePresento`, `MotivoDeCancelacion.Consultorio` ya existen.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Features/CancelarCitaPorEnlaceHandler.cs` -- modelo del handler: transacción, lectura `...ParaCambiarAsync` con `FOR UPDATE`, segundo envío devuelve lo guardado sin bitácora ni evento, `GuardarAsync` distinto de `Guardada` es excepción (cancelar no mueve el rango), `DejarRastroAsync` con bitácora y `CitaCancelada`. No toma candado de fecha.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/ICitas.cs`, `Infrastructure/Citas.cs:15` -- `ObtenerPorTokenHashParaCambiarAsync` con `FOR UPDATE` y la guarda de transacción abierta; agregar la lectura por `CitaId`.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/CitaCancelada.cs` -- el evento; `Motivo` como texto (`paciente`, `tarde`, `consultorio`).
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/AgendarCitaEnLinea.cs` -- `CodigosDeAgenda`: `CitaNoEncontrada`, `CitaNoAgendada` existen; agregar `AunNoEmpieza` y `YaEmpezo`.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Features/IBitacoraDeAgenda.cs` -- `AccionDeBitacora.NoSePresento` y `Cancelada` existen.
- `src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/AvisoAlCancelar.cs`, `RecordatorioAlCancelar.cs` -- disculpa por motivo consultorio y borrado del Recordatorio; no se tocan.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/Registro.cs` -- registrar los dos handlers.
- `tests/DraSelina.Consultorio.Domain.Tests/` (`CitaDelConsultorioTests.cs` de la 5.3 como modelo), `tests/DraSelina.Consultorio.IntegrationTests/CancelarCitaPorEnlaceTests.cs` (o el de la épica 3), `AgendarCitaPorConsultorioTests.cs` (sesión de Administrador, `Arnes.Canal`, `DarUnaVueltaDeEntregaAsync`).

## Tasks & Acceptance

**Execution:**
- [x] `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs` -- las dos transiciones y su rechazo.
- [x] `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/MarcarNoSePresento.cs`, `CancelarCitaPorConsultorio.cs` -- comandos (`IRequiereAdministrador`, `CitaId` requerido) con su resultado (id y estado mostrado); códigos nuevos.
- [x] `src/DraSelina.Consultorio.Web/Modules/Agenda/Features/` + `Domain/ICitas.cs` + `Infrastructure/Citas.cs` + `Registro.cs` -- lectura por id con `FOR UPDATE` y los dos handlers.
- [x] Pruebas de dominio (cada regla de tiempo y estado) e integración con el reloj de prueba (cada fila de la matriz, incluida la disculpa de punta a punta y la concurrencia).

**Acceptance Criteria:**
- Given una Cancelación por el Consultorio confirmada, when se entrega el outbox, then el Paciente con correo recibe la disculpa con "Elegir otro horario" (FR-17, 4.4).
- Given la 5.2, when se cancela o se marca una Cita, then `DiasDeLaAgenda` la muestra "Cancelada (por el consultorio)" o "No se presentó".

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 0, low 0, false 0, maybe-false 0; sin hallazgos. El lente revisó el dominio, los dos handlers, la lectura con `FOR UPDATE`, el registro y los dos criterios de aceptación, y corrió las pruebas de dominio, de arquitectura y las de integración nuevas.

## Verification

**Commands:**
- `dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.
