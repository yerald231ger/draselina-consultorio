---
title: 'Agenda del día'
type: 'feature'
ticket: '6'
created: '2026-11-09'
status: 'built'
baseline_revision: 'bc1aef10e8091985df3142328f7885c53c56f22d'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Selina no recibe "Tus citas de hoy" (CAP-20, FR-25, UJ-5). Sin ese correo tiene que preguntar a quién verá.

**Approach:**
- **Agenda:** agrega `IAgendaApi.AgendaDelDiaAsync(fecha)`, que responde si la fecha tiene Tramos vigentes y cuáles son sus Citas Agendadas.
- **Notificaciones:** la vuelta de envíos programados de la 4.2 envía, a la hora de la Agenda del día, una sola vez por fecha:
  - si la fecha tiene Tramos o alguna Cita, "Tus citas de hoy" a `correo_doctora`, con hora, Paciente y motivo de cada Cita (Decision del 2026-11-08);
  - si no tiene Citas, "No hay citas hoy".

Notificaciones no decide si un día se atiende (T-27).

## Boundaries & Constraints

**Always:**
- **`IAgendaApi.AgendaDelDiaAsync(DateOnly fecha)`**, la fecha local del consultorio, devuelve `AgendaDelDia(TieneTramos, Citas)`:
  - `TieneTramos` sale de `AgendaAtencion.TramosVigentes(fecha)` (INV-V1).
  - `Citas` son las Citas `Agendada` de esa fecha local, de cualquier origen, ordenadas por inicio: `CitaDelDia(Hora local, PatientId, MotivoDeConsulta?)`.
  - El motivo es nulo si la Cita del Consultorio no lo tiene.
  - Es una lectura sin rastrear y sin transacción. Nunca registra el motivo (AD-23): su `ToString` lo oculta.
  - Notificaciones no lee tablas de Agenda (AD-20).
- **`notificaciones.mensaje`** agrega la columna `fecha` (`date`, nulable) y un índice único parcial en `(tipo, fecha)` donde `fecha` no es nula. Así una fecha deja un solo registro de `AgendaDelDia`. Amplía el `[ASSUMPTION: tabla completa]` del modelo de datos.
- **La vuelta (`EnviosProgramados.DarUnaVueltaAsync`)**, además de los Recordatorios:
  - Con la hora local de `IClock` en o después de `hora_agenda_del_dia` (de `configuracion_de_envio`) y sin `mensaje` `AgendaDelDia` para la fecha local de hoy, pide `AgendaDelDiaAsync(hoy)`.
  - Sin Tramos y sin Citas, registra `Omitido` y no envía nada. Así no vuelve a preguntar ese día.
  - Si no, pide el nombre de cada Paciente con `ObtenerContactosAsync`, arma el correo, registra `Enviado` con `fecha`, envía y confirma, todo en una transacción.
  - Si el canal falla, deshace y lo intenta en la siguiente vuelta del mismo día.
  - No intenta fechas pasadas: si la web estuvo apagada todo un día, ese día no se envía.
- **El correo:**
  - Va a `correo_doctora`.
  - Asunto: "Tus citas de hoy: {fecha larga}". `[ASSUMPTION]`
  - Saludo: "Hola, doctora:". `[ASSUMPTION]`
  - Cuerpo: una línea por Cita, "4:00 pm · Ana Sofía López · {motivo}", sin el motivo cuando es nulo.
  - Sin Citas, "No hay citas hoy".
  - Sin botones. Con `MarcoDeCorreo` (4.4), HTML y texto.
- Nada de esto va a registros: ni nombres, ni motivos, ni direcciones (AD-23).

- Decision (Gerardo, 2026-11-09): `notificaciones.mensaje` agrega `fecha` con índice único parcial en `(tipo, fecha)`, y el plan se conserva completo aunque pasa de 1,600 tokens.

**Never:**
- Cambiar la hora por comando (4.7).
- Leer tablas de Agenda desde Notificaciones.
- Enviar a los Pacientes.
- Recuperar días anteriores.
- Recalcular en Notificaciones si una fecha se atiende.

## I/O & Edge-Case Matrix

Reloj de prueba. Hora de la Agenda del día: 14:00 local. Agenda base sembrada (lunes a viernes, 15:30 a 18:00).

| Escenario | Estado | Resultado |
|---|---|---|
| Día con Citas | martes 3 con Citas a las 16:00 y 17:00 (una del Consultorio sin motivo); vuelta a las 14:00 | un correo a `doctora@consultorio.local` con las dos líneas en orden, la del Consultorio sin motivo; `mensaje` `AgendaDelDia` `Enviado` con `fecha` 2026-11-03 |
| Antes de la hora | vuelta a las 13:59 | nada |
| Otra vuelta el mismo día | vuelta a las 14:05 | nada nuevo |
| Día con Tramos sin Citas | martes sin Citas | "No hay citas hoy" |
| Día sin Tramos ni Citas | sábado sin custom ni Citas | nada; `mensaje` `Omitido` con su fecha |
| Sábado con una Cita manual | sábado sin Tramos, con una Cita del Consultorio | correo con esa Cita |
| Cita cancelada | martes con una Cita Cancelada y otra Agendada | solo la Agendada |
| Canal caído | vuelta a las 14:00 con el canal fallando | sin fila; a las 14:01, con el canal de vuelta, sale |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/EnviosProgramados.cs` (4.2): la vuelta, con un ámbito y una transacción por envío. Se agrega el paso de la Agenda del día sin cambiar el de los Recordatorios.
- `.../Notificaciones/Infrastructure/ServicioDeEnviosProgramados.cs`: no cambia.
- `.../Notificaciones/Features/IConfiguracionDeEnvio.cs` e `Infrastructure/ConfiguracionDeEnvio.cs` (4.5): agregar `CorreoDeLaDoctoraAsync` y `HoraDeLaAgendaDelDiaAsync`.
- `.../Notificaciones/Features/IMensajes.cs` e `Infrastructure/Mensajes.cs`, `FilaDeMensaje.cs`: agregar `fecha` y la consulta "¿ya hay `AgendaDelDia` para esta fecha?". El índice único va en una migración nueva.
- `.../Notificaciones/Features/MarcoDeCorreo.cs` y `PlantillaDeAlertaTardia.cs` (4.5): el modelo de una plantilla interna.
- `.../Modules/Agenda/Contracts/IAgendaApi.cs`, `Features/AgendaApi.cs`: el modelo de método; `ObtenerTipoDeVisitaAsync` (4.1) es el más parecido.
- `.../Agenda/Domain/AgendaAtencion.cs` (`TramosVigentes`) e `IAgendaAtencion`: la lectura de Tramos.
- `.../Agenda/Features/ICitasActivas.cs` e `Infrastructure/CitasActivas.cs`: el modelo de consulta de Citas por rango. Puede agregarse una lectura nueva.
- `.../Agenda/Domain/HoraLocal.cs`: de la fecha local al rango UTC del día.
- `tests/DraSelina.Consultorio.IntegrationTests/RecordatorioTests.cs`, con `Arnes.DarUnaVueltaDeEnviosProgramadosAsync` y `CanalDePrueba`: el modelo de prueba.

## Tasks & Acceptance

**Execution:**
- [ ] `.../Agenda/Contracts/IAgendaApi.cs`, `AgendaDelDia.cs` (nuevo) y `Features/AgendaApi.cs`, con la lectura de Citas del día que haga falta.
- [ ] `.../Notificaciones/Infrastructure/FilaDeMensaje.cs`, `NotificacionesDbContext.cs` y una migración nueva: `fecha` y el índice.
- [ ] `.../Notificaciones/Features/IMensajes.cs`, `IConfiguracionDeEnvio.cs` y sus implementaciones.
- [ ] `.../Notificaciones/Features/PlantillaDeAgendaDelDia.cs` y `EnviosProgramados.cs`.
- [ ] `tests/.../IntegrationTests/AgendaDelDiaTests.cs` (nuevo): cada fila de la matriz, y que ni nombres ni motivos vayan a registros.
- [ ] `tests/.../IntegrationTests/AgendaApiTests.cs` o similar: `AgendaDelDiaAsync` con base, custom y Citas canceladas.

**Acceptance Criteria:**
- Given un día con un Recordatorio vencido y la hora de la Agenda del día cumplida, when corre una vuelta, then salen los dos sin que uno impida al otro.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 0, low 1, false 1, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| El criterio "sin que uno impida al otro" solo se prueba cuando falla la Agenda del día | low | rechazado | El revisor confirmó en el código que una falla de los Recordatorios se atrapa y la vuelta sigue con la Agenda del día. Probarlo exige inyectar una falla de lectura solo para la prueba. |
| Una Cita cuyo Paciente no existe bloquea el correo del día y registra un aviso cada minuto | false | rechazado | No es alcanzable: Pacientes no tiene camino para borrar un Paciente y toda Cita guarda el suyo. Es el mismo trato de estado imposible que en la 3.1 (`CitaPorEnlaceHandler`) y la 4.5. Si algún día Pacientes borra, esto se revisa. |

Pendiente para el Refactor sweep (4.8), reportado por el implementador: `AgendaDelDiaAsync` lee la Agenda base por `IAgendaAtencion`, documentado como puerto de escritura; un puerto de lectura propio encajaría mejor.

## Verification

**Commands:**
- `dotnet build`: debe compilar sin advertencias.
- `dotnet test tests/DraSelina.Consultorio.IntegrationTests --filter "FullyQualifiedName~AgendaDelDiaTests|FullyQualifiedName~RecordatorioTests"`: deben pasar.
- `dotnet test`: debe pasar la suite completa.
