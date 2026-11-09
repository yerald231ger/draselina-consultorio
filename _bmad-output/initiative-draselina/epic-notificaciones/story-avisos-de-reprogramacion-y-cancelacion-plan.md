---
title: 'Avisos de reprogramación y cancelación'
type: 'feature'
ticket: '4'
created: '2026-11-08'
status: done
baseline_revision: 'a744f07064bb81dc4796b5658ed3f7e94fa6ac98'
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

**Problem:** Cuando una Cita se reprograma o se cancela, el Paciente con correo no recibe aviso (CAP-18, FR-23, UJ-2). Tampoco existe la disculpa del Consultorio que la épica 5 necesita.

**Approach:** Dos handlers nuevos de Notificaciones, uno de `CitaReprogramada` y otro de `CitaCancelada`, con el mismo patrón que `ConfirmacionAlAgendar` (4.1). Envían "Cita reprogramada", "Cita cancelada" (amable si fue tardía) o la disculpa del Consultorio, según el motivo que decidió Agenda (E-16). Cada uno sale una sola vez por `EventoId`.

## Boundaries & Constraints

**Always:**
- **Patrón de la 4.1:**
  - Transacción de Notificaciones; `IMensajes.ExisteAsync(EventoId, tipo)`.
  - `ObtenerContactosAsync`; sin correo no envía ni registra nada.
  - Registra `Enviado`, envía y confirma con `CancellationToken.None`. Si el canal lanza, la excepción sale y el outbox reintenta.
  - Nada va a registros (AD-23).
  - Son handlers separados de los del Recordatorio (4.2).
- **`CitaReprogramada`** → `TipoDeMensaje.Reprogramada`:
  - Asunto: "Tu cita se reprogramó: {fecha larga}, {hora}". `[ASSUMPTION]`
  - Cuerpo: la nueva fecha y hora en grande (de `InicioNuevo`), el tipo, el lugar con "Abrir en el mapa" y "Ver o cambiar mi cita" con el Enlace de gestión.
  - Lleva adjunto el archivo de calendario actualizado si `ObtenerArchivoDeCalendarioAsync` lo da. Es el mismo UID, así que el calendario del Paciente mueve el evento. `[ASSUMPTION]`
- **`CitaCancelada`**, según `Motivo`:
  - **`paciente`** → `TipoDeMensaje.Cancelada`.
    - Asunto: "Tu cita está cancelada". `[ASSUMPTION]`
    - Cuerpo: la fecha y la hora de la Cita y el botón "Agendar otra cita", hacia `/reservar` del sitio.
  - **`tarde`** → el mismo tipo y el mismo correo, más la frase de la pantalla de la 3.4: "Gracias por avisarnos. Quedó registrada como cancelación tardía."
  - **`consultorio`** → `TipoDeMensaje.CanceladaPorConsultorio`.
    - Asunto: "Tuvimos que cancelar tu cita". `[ASSUMPTION]`
    - Cuerpo: la disculpa sincera en nombre del Consultorio, sin excusas técnicas: "Lo sentimos mucho: tuvimos que cancelar tu cita del {fecha}, {hora}. Elige otro horario cuando te acomode." `[ASSUMPTION: texto]`
    - Botón: "Elegir otro horario", hacia `/reservar`.
    - Su prueba de punta a punta es de la épica 5.
- **La dirección del sitio para `/reservar`:** sale de `Notificaciones:UrlDelSitio`, que el AppHost pasa con el endpoint de la web, igual que `EnlaceDeGestion__UrlBase`, y que el arnés de pruebas fija.
- **Contenido de los correos:**
  - Fecha y hora salen del evento, en `ZonaDelConsultorio`, con `FormatoDeFechaEnCorreo`.
  - Tipo, con `IAgendaApi.ObtenerTipoDeVisitaAsync`.
  - El encabezado y la estructura HTML con versión de texto son los de las plantillas de la 4.1 y la 4.2.
- Ningún correo pide confirmar. La cancelación no lleva Enlace de gestión, porque la Cita ya no se puede cambiar.

- Decision (Gerardo, 2026-11-08): el plan se conserva completo aunque pasa de 1,600 tokens.

**Never:**
- La alerta al Administrador (4.5).
- Recalcular si una cancelación fue tardía (E-16, T-27).
- Avisar a un Paciente sin correo (es pregunta abierta del spec para la épica 5).
- Cambiar los handlers del Recordatorio o de la confirmación.
- Un archivo de calendario de cancelación.

## I/O & Edge-Case Matrix

| Escenario | Evento | Resultado |
|---|---|---|
| Reprogramada | `CitaReprogramada` al jueves 5 a las 17:00 | correo "Tu cita se reprogramó: Jueves 5 de noviembre, 5:00 pm" con "Ver o cambiar mi cita" y el `.ics` nuevo; `mensaje` `Reprogramada` `Enviado` |
| Cancelada a tiempo | `CitaCancelada` con motivo `paciente` | "Tu cita está cancelada" con "Agendar otra cita" y sin la frase de tardía; `mensaje` `Cancelada` |
| Cancelada tarde | motivo `tarde` | el mismo correo con "Gracias por avisarnos. Quedó registrada como cancelación tardía." |
| Por el Consultorio | motivo `consultorio` | la disculpa con "Elegir otro horario"; `mensaje` `CanceladaPorConsultorio` |
| Sin correo | cualquiera de los tres | nada |
| Evento dos veces | el mismo `EventoId` otra vez | un solo correo y una sola fila |
| Canal caído | el canal lanza | sin fila; el outbox reintenta y sale después |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/ConfirmacionAlAgendar.cs` y `PlantillaDeConfirmacion.cs`: el patrón de handler y de plantilla que se repite.
- `.../Notificaciones/Features/PlantillaDeRecordatorio.cs` (4.2): cómo reutiliza la estructura. Si conviene, se extrae una base común; si no, se imita.
- `.../Notificaciones/Features/IMensajes.cs`: `TipoDeMensaje.Reprogramada`, `Cancelada` y `CanceladaPorConsultorio` ya existen y están en el `CHECK` de la tabla.
- `.../Notificaciones/Infrastructure/Registro.cs`: `AddEventHandler<T>()`. `OpcionesDeNotificaciones.cs` lleva `Remitente`; ahí se agrega `UrlDelSitio`.
- `src/DraSelina.Consultorio.AppHost/AppHost.cs`: las variables de entorno de la web, junto a `Notificaciones__Remitente`.
- `.../Modules/Agenda/Contracts/CitaReprogramada.cs` y `CitaCancelada.cs` (con las constantes `MotivoPaciente`, `MotivoTarde` y `MotivoConsultorio`, 3.4).
- `.../UI/Pages/Gestion/ConfirmacionDeCancelacion.razor`: la frase de tardía exacta de la pantalla.
- `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs`: `UseSetting` para la configuración, `CanalDePrueba`, `DarUnaVueltaDeEntregaAsync`.
- Pruebas modelo: `ConfirmacionPorCorreoTests.cs` y `RecordatorioTests.cs` (eventos escritos en `agenda.outbox` o producidos por los comandos reales de la 3.2 y la 3.4).

## Tasks & Acceptance

**Execution:**
- [x] `.../Notificaciones/Features/AvisoAlReprogramar.cs`, `AvisoAlCancelar.cs`, `PlantillaDeReprogramacion.cs` y `PlantillaDeCancelacion.cs` (o nombres equivalentes).
- [x] `.../Notificaciones/Infrastructure/OpcionesDeNotificaciones.cs` y `Registro.cs`; `src/DraSelina.Consultorio.AppHost/AppHost.cs`; `tests/.../Arnes.cs`: `UrlDelSitio`.
- [x] `tests/.../IntegrationTests/AvisosDeCambioTests.cs` (nuevo): cada fila de la matriz, con un reprogramar y un cancelar reales por los comandos de la 3.2 y la 3.4, y que la dirección y el nombre no vayan a registros.

**Acceptance Criteria:**
- Given una Cita reprogramada y luego cancelada por el Paciente, when se entregan los eventos, then el Paciente recibe en orden la confirmación, el aviso de reprogramación y el de cancelación.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 0, low 3, false 1, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| El `.ics` del aviso de reprogramación puede traer una fecha posterior a la del correo si la Cita se movió dos veces antes de la entrega | low | rechazado | Real, pero el `.ics` lleva el mismo UID y la fecha vigente de la Cita, así que el calendario del Paciente queda en la hora correcta. El siguiente aviso, que llega en orden por Cita, explica la fecha nueva. Comparar fechas exigiría leer el contenido del archivo. |
| La prueba de AD-23 no pasa por la disculpa del Consultorio | low | patch | Real; se agrega el caso y el tipo `CanceladaPorConsultorio` a la consulta. |
| El README no lista `Notificaciones__UrlDelSitio` | low | patch | Real; se agrega con su propósito. |
| Un motivo desconocido lanza y bloquea el evento, decisión no escrita en el plan | false | rechazado | No es alcanzable: el `CHECK` de `agenda.cita.motivo_de_cancelacion` solo admite `paciente`, `tarde` y `consultorio`, y el evento se arma con las constantes de `CitaCancelada`. Fallar con ruido ante un estado imposible es lo correcto, no una decisión de negocio. |

## Verification

**Commands:**
- `dotnet build`: debe compilar sin advertencias.
- `dotnet test tests/DraSelina.Consultorio.IntegrationTests --filter "FullyQualifiedName~AvisosDeCambioTests|FullyQualifiedName~ConfirmacionPorCorreoTests|FullyQualifiedName~RecordatorioTests"`: deben pasar.
- `dotnet test`: debe pasar la suite completa.
