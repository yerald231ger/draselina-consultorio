---
title: 'Recordatorio programado'
type: 'feature'
ticket: '2'
created: '2026-11-08'
status: 'built'
baseline_revision: 'efb1c11ed562bd6f0e2870b4c09055b72ee46bb8'
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

**Problem:** El Paciente con correo no recibe el Recordatorio del día anterior (CAP-18, FR-22). Notificaciones no tiene envíos programados ni un servicio que los despache.

**Approach:**
- Una tabla `recordatorio_programado` que Notificaciones mantiene con los tres eventos de Agenda: lo programa con `CitaAgendada`, lo mueve con `CitaReprogramada` y lo borra con `CitaCancelada` (T-25, AD-20).
- Un servicio en segundo plano que cada minuto envía los que ya tocan. La entrada 6 lo reutiliza para la Agenda del día.

## Boundaries & Constraints

**Always:**
- **`notificaciones.recordatorio_programado`:**
  - Columnas: `cita_id` (PK), `paciente_id`, `inicio` (UTC) y `programado_para` (UTC).
  - `paciente_id` e `inicio` vienen del evento, así que al enviar no hace falta preguntar a Agenda. Amplían el `[ASSUMPTION: tabla completa]` del modelo de datos.
- **`programado_para`:** el día local anterior al inicio de la Cita, a la `hora_recordatorio` de `configuracion_de_envio`, en `ZonaDelConsultorio`. Ejemplo: una Cita del jueves a las 4:00 pm da el miércoles a las 9:00 am local.
- **Handlers** (cada uno es un `IEventHandler` nuevo, separado de `ConfirmacionAlAgendar`, y es idempotente porque el evento puede llegar dos veces):
  - **`CitaAgendada`:** si `programado_para` es posterior a `OcurridoEn` del evento, inserta la fila; si ya existe, no hace nada. Si no es posterior, no hay fila y registra un `mensaje` `Recordatorio` `Omitido` con el `EventoId` (FR-22).
  - **`CitaReprogramada`:** recalcula con `InicioNuevo` y deja la fila con el nuevo `inicio` y `programado_para`, la cree o la actualice. Si la nueva hora ya pasó respecto a `OcurridoEn`, borra la fila y registra `Omitido`.
  - **`CitaCancelada`:** borra la fila, si existe.
- **Envío:**
  - Un `BackgroundService` de Notificaciones con `PeriodicTimer` de 1 minuto llama a una vuelta (`EnviosProgramados.DarUnaVueltaAsync`).
  - La vuelta toma las filas con `programado_para <= ahora`, con `FOR UPDATE SKIP LOCKED`, cada una en su propia transacción.
  - Por cada una: pide `ObtenerContactos`. Con correo, envía "Mañana tienes cita", registra `mensaje` `Recordatorio` `Enviado` con `cita_id` y borra la fila.
  - Sin correo, borra la fila sin enviar nada.
  - Si el canal falla, deshace y deja la fila para la siguiente vuelta (AD-20).
  - El reloj es `IClock`.
- **El correo** (EXPERIENCE.md):
  - Asunto: "Mañana tienes cita: {hora}". `[ASSUMPTION]`
  - Contenido: la fecha y la hora en grande, el tipo (de `IAgendaApi.ObtenerTipoDeVisitaAsync`), el lugar con "Abrir en el mapa" y un solo botón, "Ver o cambiar mi cita". No pide confirmar.
  - Usa la misma estructura, el mismo formato y el mismo manejo del canal que la confirmación de la 4.1.
- Las pruebas pueden dar la vuelta a mano y con el reloj de prueba: `Arnes` quita el servicio en segundo plano cuando hay `RelojDePrueba`, igual que con la entrega de eventos.
- La vuelta nunca registra la dirección, el nombre ni el contenido (AD-23). El servicio no lanza por una fila que falla: la registra y sigue.

- Decision (Gerardo, 2026-11-08): `recordatorio_programado` lleva también `paciente_id` e `inicio`, y el plan se conserva completo aunque pasa de 1,600 tokens.

**Never:**
- La Agenda del día (entrada 6).
- Los avisos de cambio (entrada 4).
- Cambiar la hora por comando (entrada 7).
- Que Agenda llame a Notificaciones (E-14).
- Preguntar a Agenda si una Cita sigue vigente: los eventos llegan en orden por Cita y el borrado los acompaña.

## I/O & Edge-Case Matrix

Reloj de prueba. Cita del jueves 5 de noviembre a las 16:00 local, agendada el lunes 2. Hora del Recordatorio: 09:00.

| Escenario | Acción | Resultado |
|---|---|---|
| Programar | se entrega `CitaAgendada` | una fila con `programado_para` = miércoles 4 a las 09:00 local (15:00 UTC) |
| Antes de la hora | vuelta el miércoles a las 08:59 | no sale nada |
| A la hora | vuelta el miércoles a las 09:00 | sale "Mañana tienes cita" al Paciente; `mensaje` `Recordatorio` `Enviado`; la fila desaparece |
| Segunda vuelta | vuelta a las 09:01 | no sale otro |
| Reprogramar | `CitaReprogramada` al viernes 6 a las 17:00 | la fila pasa al jueves 5 a las 09:00; el miércoles no sale nada |
| Cancelar | `CitaCancelada` | sin fila; no sale nada |
| Ya pasó al agendar | `CitaAgendada` el miércoles a las 10:00 para el jueves | sin fila; `mensaje` `Recordatorio` `Omitido` |
| Evento repetido | el mismo `CitaAgendada` o `CitaReprogramada` dos veces | una sola fila y un solo `Omitido` |
| Sin correo | vuelta con la fila de un Paciente sin correo | no sale nada; la fila desaparece |
| Canal caído | vuelta con el canal fallando | la fila sigue; en la siguiente vuelta, con el canal de vuelta, sale |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/ConfirmacionAlAgendar.cs` y `PlantillaDeConfirmacion.cs` (4.1): el patrón de handler (transacción, `IMensajes`, contactos, canal) y de plantilla (HTML más texto, `FormatoDeFechaEnCorreo`, `EnlaceDelMapa`). No se modifican.
- `.../Notificaciones/Features/IMensajes.cs` e `Infrastructure/Mensajes.cs`: los tipos `Recordatorio` y los estados `Enviado` y `Omitido` ya están en el `CHECK`. `cita_id` es nulable.
- `.../Notificaciones/Infrastructure/NotificacionesDbContext.cs`, `FilaDeConfiguracionDeEnvio.cs` y `Migrations/`: agregar la tabla. La hora del Recordatorio está en `configuracion_de_envio`.
- `.../Notificaciones/Infrastructure/Registro.cs`: `AddEventHandler<T>()` registra cada `IEventHandler<T>` por reflexión. Agregar los tres handlers y el servicio.
- `src/DraSelina.Consultorio.Web/EntregaDeEventos.cs` y `ServicioDeEntregaDeEventos.cs`: el modelo de vuelta más servicio con `PeriodicTimer`, `FOR UPDATE SKIP LOCKED` y el registro sin lanzar.
- `.../Modules/Agenda/Contracts/CitaReprogramada.cs`, `CitaCancelada.cs` y `CitaAgendada.cs`: los eventos.
- `src/DraSelina.Consultorio.Web/Kernel/ZonaDelConsultorio.cs` e `IClock.cs`.
- `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs`: quita `ServicioDeEntregaDeEventos` cuando hay `RelojDePrueba`; hacer lo mismo con el servicio nuevo y exponer la vuelta, como `DarUnaVueltaDeEntregaAsync`. `Pruebas/CanalDePrueba.cs` (4.1) tiene el interruptor `Caido`.
- Prueba modelo: `tests/DraSelina.Consultorio.IntegrationTests/ConfirmacionPorCorreoTests.cs`.

## Tasks & Acceptance

**Execution:**
- [x] `.../Notificaciones/Infrastructure/NotificacionesDbContext.cs` y una migración nueva: `recordatorio_programado`.
- [x] `.../Notificaciones/Features/IRecordatorios.cs` e `Infrastructure/Recordatorios.cs`: el puerto (programar, mover, borrar y tomar los vencidos con bloqueo).
- [x] `.../Notificaciones/Features/RecordatorioAlAgendar.cs`, `RecordatorioAlReprogramar.cs` y `RecordatorioAlCancelar.cs` (o nombres equivalentes): los handlers.
- [x] `.../Notificaciones/Features/EnviosProgramados.cs` y `PlantillaDeRecordatorio.cs`: la vuelta y el correo.
- [x] `.../Notificaciones/Infrastructure/ServicioDeEnviosProgramados.cs` y `Registro.cs`: el `BackgroundService` y su registro.
- [x] `tests/.../IntegrationTests/Arnes.cs`: quitar el servicio con reloj de prueba y exponer la vuelta.
- [x] `tests/.../IntegrationTests/RecordatorioTests.cs` (nuevo): todas las filas de la matriz, y que la vuelta no registre la dirección ni el nombre.

**Acceptance Criteria:**
- Given una Cita del Consultorio, when se entrega su `CitaAgendada`, then su Recordatorio se programa igual que el de una en línea.
- Given la web arrancada sin reloj de prueba, when pasa un minuto, then el servicio da vueltas sin lanzar aunque no haya filas.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 2, low 2, false 0, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Un evento reintentado después de enviado el Recordatorio lo vuelve a crear y sale dos veces | medium | patch | Real: si cualquier handler del evento falla (por ejemplo la confirmación), todos lo reciben en el reintento, que llega hasta unas 17 h después; el handler no ve fila y la inserta. Arreglo: no programar ni mover si ya hay un `Recordatorio` `Enviado` para la Cita registrado desde `OcurridoEn` del evento. |
| La vuelta envía "Mañana tienes cita" de una Cita que ya empezó | medium | patch | Real: la vuelta no compara `inicio` con ahora; pasa si la web estuvo apagada o el evento llegó tarde. Arreglo: si `inicio <= ahora`, borrar la fila sin enviar. |
| La prueba del servicio no ve un tic del temporizador ni una vuelta sin filas | low | rechazado | Real, pero la prueba sí arranca la web real y ve la vuelta de arranque sin errores. Ver un tic exige esperar un minuto o hacer configurable el periodo, una opción nueva solo para la prueba. `PeriodicTimer` es del framework. |
| Dos comentarios XML pasan de 120 caracteres | low | patch | Real y cosmético; se reacomodan. |

Verificación final: la suite completa falló con `53300 too many clients already` (cuatro o cinco pruebas distintas por corrida). Las bases por prueba dejaban conexiones abiertas en el pool y la 4.2 sumó 17 bases y un servicio más por web. Arreglo solo en pruebas: el arnés quita `ServicioDeEnviosProgramados` salvo en la prueba que lo comprueba, y `Escenario.DisposeAsync` limpia el pool de su base. Con eso, el proyecto de integración pasó dos veces seguidas 390 de 390, y la suite completa 639 de 639.

Efecto aceptado de la primera corrección: si una Cita se reprograma segundos antes de que salga el Recordatorio de la fecha vieja, la fecha nueva puede quedarse sin Recordatorio. Es una ventana de segundos y evitarla complicaría la regla contra duplicados.

## Verification

**Commands:**
- `dotnet build`: debe compilar sin advertencias.
- `dotnet test tests/DraSelina.Consultorio.IntegrationTests --filter "FullyQualifiedName~RecordatorioTests|FullyQualifiedName~ConfirmacionPorCorreoTests"`: deben pasar.
- `dotnet test`: debe pasar la suite completa.
