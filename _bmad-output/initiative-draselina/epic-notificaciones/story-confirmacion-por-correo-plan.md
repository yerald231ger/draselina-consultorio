---
title: 'Confirmación por correo'
type: 'feature'
ticket: '1'
created: '2026-11-08'
status: done
baseline_revision: '8022daf15fa9aafbe1dea8ffce3e94c81a084535'
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

**Problem:** Una Cita agendada no avisa a nadie. El Paciente con correo no recibe su confirmación (CAP-18, FR-21) y el módulo Notificaciones no tiene canal, tablas ni handlers.

**Approach:** Esta es la bala trazadora de la épica 4. Se agregan Mailpit en el AppHost, un canal SMTP detrás de `INotificationChannel`, las tablas `configuracion_de_envio` y `mensaje`, `IPacientesApi.ObtenerContactos`, `EnlaceDelMapa` en Contenido y el handler de `CitaAgendada`. El handler envía la confirmación una sola vez por `EventoId`, y deja que el outbox reintente si el canal falla (AD-3, AD-20, T-26).

## Boundaries & Constraints

**Always:**
- **AppHost:** agrega Mailpit (integración de Aspire del Community Toolkit) y la web lo referencia. Las versiones de los paquetes van en `Directory.Packages.props`.
- **El canal:**
  - `INotificationChannel` vive en Notificaciones.
  - La implementación SMTP usa MailKit y lee el servidor de la cadena de conexión que da Aspire.
  - El remitente sale de la configuración (`Notificaciones:Remitente`), con `citas@consultorio.local` en desarrollo. `[ASSUMPTION]`
- **`notificaciones.configuracion_de_envio`:** una sola fila con `CHECK (id = 1)`. Su semilla es `09:00`, `14:00`, `doctora@consultorio.local` y `administrador@consultorio.local` (Decision del 2026-11-08). Esta entrada solo la crea; la leen las entradas 2, 5 y 6.
- **`notificaciones.mensaje`:**
  - Es como la del modelo de datos: `id`, `evento_id`, `cita_id`, `tipo`, `canal`, `estado`, `registrado_en`, con índice único en `(evento_id, tipo)`.
  - Nunca guarda la dirección ni el contenido (AD-23).
- **El handler de `CitaAgendada`:**
  - Si ya hay un `mensaje` con ese `EventoId` y tipo `Confirmacion`, no hace nada.
  - Si no, pide los contactos a `IPacientesApi.ObtenerContactos`.
  - Sin correo, no envía y no registra nada `[ASSUMPTION]`.
  - Con correo, arma el mensaje, lo envía y registra `Enviado` en la transacción de Notificaciones.
  - Si el canal lanza, la transacción se deshace y la excepción sale, para que el outbox reintente.
  - Lo mismo aplica a las Citas del Consultorio (FR-21, `[ASSUMPTION]` del PRD).
- **El correo** (EXPERIENCE.md, "Mensajes por correo"):
  - **Asunto:** "Tu cita está confirmada: {fecha larga}, {hora}" `[ASSUMPTION]`.
  - **Contenido:** encabezado con el nombre de la doctora y la especialidad, sin imágenes `[ASSUMPTION]`; la fecha y la hora en grande; el tipo; el lugar con dirección y ciudad; "Abrir en el mapa" cuando existe `EnlaceDelMapa`.
  - **Botón:** uno solo, "Ver o cambiar mi cita", con `IAgendaApi.ObtenerEnlaceDeGestion`.
  - **Adjunto:** el archivo de `IAgendaApi.ObtenerArchivoDeCalendarioAsync`; si es nulo, se envía sin adjunto.
  - Versión HTML y versión de texto plano legible. No pide confirmar.
  - De tú y en español de México. Fecha y hora en la zona de `ZonaDelConsultorio`, con el mismo formato que la UI (por ejemplo, "Martes 3 de noviembre, 4:00 pm").
- **`IPacientesApi.ObtenerContactos(PatientId)`:** devuelve `Contactos(Nombre, Correo?)`, o nulo si no existe. Sin registros. Lo usarán las entradas 4 a 6 y la épica 5.
- **`DatosDelConsultorio`:** agrega `EnlaceDelMapa`, una búsqueda de Google Maps con lugar, dirección y ciudad codificados en la URL (Decision del 2026-11-08).
- Ni la dirección, ni el nombre, ni el contenido van a registros o trazas (AD-23).
- El correo nunca retrasa la reserva: sale después, por el outbox (FR-26).

- Decision (Gerardo, 2026-11-08): el plan se conserva completo aunque pasa de 1,600 tokens.

**Never:**
- El Recordatorio, los avisos de cambio, la alerta y la Agenda del día (entradas 2 a 6).
- Las frases de Cita confirmada (entrada 3).
- Azure Communication Services o WhatsApp.
- Guardar en `mensaje` el contenido o la dirección.
- Cambiar `AgendarCitaEnLinea` o el outbox.

## I/O & Edge-Case Matrix

| Escenario | Estado | Resultado |
|---|---|---|
| Paciente con correo | se entrega `CitaAgendada` | un correo al Paciente con asunto, fecha y hora, lugar, "Abrir en el mapa", "Ver o cambiar mi cita" y el `.ics` adjunto; una fila `mensaje` con `Confirmacion` y `Enviado` |
| Paciente sin correo | se entrega `CitaAgendada` | ningún correo y ninguna fila |
| El mismo evento dos veces | el outbox lo entrega otra vez | un solo correo y una sola fila |
| Canal caído | el canal lanza | la reserva ya está confirmada; el handler lanza, no queda fila y el outbox reintenta |
| Canal de vuelta | la siguiente entrega | sale el correo y queda la fila |
| Cita del Consultorio | `CitaAgendada` con origen `consultorio` | la misma confirmación |
| Sin archivo de calendario | `ObtenerArchivoDeCalendarioAsync` devuelve nulo | el correo sale sin adjunto |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.AppHost/AppHost.cs`: agregar el recurso de Mailpit y `.WithReference(...)` en `web`. Sin volumen, como PostgreSQL.
- `Directory.Packages.props`: gestión central de versiones. Agregar la integración de Mailpit para el AppHost y MailKit para la web.
- `src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/IEventHandler.cs`: el contrato del handler. `Outbox.cs` (`CatalogoDeEventos`) descubre los `IEventHandler<T>` registrados.
- `src/DraSelina.Consultorio.Web/EntregaDeEventos.cs`: llama a cada handler en su propio ámbito. Si uno lanza, el evento se reintenta para todos, así que cada handler debe ser idempotente.
- `.../Modules/Notificaciones/Infrastructure/NotificacionesDbContext.cs`, `Registro.cs` y `TransaccionDeNotificaciones.cs`, y `Features/ITransaccionDeNotificaciones.cs`: el módulo vacío donde va todo. El modelo a seguir es la migración de Agenda (`Modules/Agenda/Infrastructure/Migrations/`) y su `HasData` de reglas.
- `.../Modules/Agenda/Contracts/CitaAgendada.cs` e `IAgendaApi.cs`: el evento, `ObtenerEnlaceDeGestion` y `ObtenerArchivoDeCalendarioAsync`.
- `.../Modules/Pacientes/Contracts/IPacientesApi.cs`, `Features/PacientesApi.cs`, `Domain/IPacientes.cs` e `Infrastructure/Pacientes.cs`: el patrón de `NombreAsync` (3.1) para `ObtenerContactos`.
- `.../Modules/Contenido/Contracts/IContenidoApi.cs` y `Contenido/ContenidoApi.cs`: `DatosDelConsultorio`.
- `.../UI/Pages/Reserva/FormatoDeCita.cs`: el formato de fecha y hora. Es de la UI, y Notificaciones no puede usarlo según las reglas de arquitectura. Replicar el formato o moverlo a un lugar común es decisión del implementador, siempre que pasen `tests/DraSelina.Consultorio.ArchitectureTests`.
- `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs`: `ConfigureServices` del arnés, donde las pruebas reemplazan `INotificationChannel` por un canal de prueba que guarda los envíos y puede fallar a pedido. `DarUnaVueltaDeEntregaAsync` y `ArrancarWebSinEntregaAutomatica` permiten entregar el evento a mano.
- Prueba modelo: `tests/DraSelina.Consultorio.IntegrationTests/BitacoraYCitaAgendadaTests.cs` y `EntregaDeEventosTests.cs`.

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Packages.props`, `src/DraSelina.Consultorio.AppHost/*`: agregar Mailpit.
- [x] `.../Notificaciones/Features/INotificationChannel.cs` y `Infrastructure/CanalDeCorreoSmtp.cs`: el canal, con el mensaje (destinatario, asunto, HTML, texto, adjuntos).
- [x] `.../Notificaciones/Infrastructure/NotificacionesDbContext.cs` y su migración: las dos tablas y la semilla.
- [x] `.../Notificaciones/Features/ConfirmacionAlAgendar.cs` (o un nombre equivalente): el `IEventHandler<CitaAgendada>` y su plantilla.
- [x] `.../Notificaciones/Infrastructure/Registro.cs`: el canal, el handler y la configuración.
- [x] `.../Pacientes/*`: `ObtenerContactos`.
- [x] `.../Contenido/*`: `EnlaceDelMapa`.
- [x] `tests/.../IntegrationTests/Arnes.cs`: el canal de prueba.
- [x] `tests/.../IntegrationTests/ConfirmacionPorCorreoTests.cs` (nuevo): cada fila de la matriz, el contenido del correo, y que ni la dirección ni el nombre aparezcan en registros ni en `mensaje`.
- [x] `tests/.../IntegrationTests/PacientesApiTests.cs`: `ObtenerContactos`, con y sin correo.

**Acceptance Criteria:**
- Given el AppHost arriba, when se reserva con un correo, then el correo aparece en Mailpit con su `.ics` adjunto y el enlace "Ver o cambiar mi cita" abre Tu cita.
- Given las pruebas de arquitectura, when corren, then Notificaciones solo depende de `Contracts` de otros módulos, del Kernel y de BuildingBlocks.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 1, low 4, false 0, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Un `DisconnectAsync` que falla después de un envío aceptado hace que el handler deshaga y el outbox reenvíe: correo duplicado | medium | patch | Real: el QUIT está en el mismo `try` que `SendAsync` y con el token del límite de 30 s. Arreglo: desconectar con `CancellationToken.None` y tragarse el error tras un envío aceptado. |
| Registra antes de enviar, al revés de como lo dice el plan | low | rechazado | Real, pero el resultado visible es el mismo: los dos pasos van en una sola transacción y la matriz se cumple. El único costo es la transacción abierta durante el SMTP, y cambiar el orden no la cierra antes. |
| Envía "confirmada" aunque la Cita ya se haya cancelado o movido tras reintentos largos | low | rechazado | El outbox no entrega un evento mientras uno anterior con la misma clave (`CitaId`) siga pendiente, así que el aviso de cancelación o reprogramación (entrada 4.4) llega después de la confirmación y la historia del Paciente queda en orden. Revisar el estado actual agregaría otra rama de decisión a Notificaciones (T-27). |
| Los comentarios dicen que el canal SMTP sirve para Azure Communication Services, pero no autentica | low | patch | Real; se corrigen los comentarios para decir que haría falta autenticación al desplegar. |
| `CanalDeCorreoSmtp` no tiene pruebas automáticas, y el criterio de Mailpit es manual | low | patch (parcial) | El implementador hizo la prueba manual con el AppHost (Mailpit recibió el correo con su `.ics` y el enlace abrió Tu cita). Se agrega una prueba del canal real contra un puerto cerrado que comprueba que la excepción no lleva la dirección ni el asunto (AD-23). |

## Verification

**Commands:**
- `dotnet build`: debe compilar sin advertencias.
- `dotnet test tests/DraSelina.Consultorio.IntegrationTests --filter "FullyQualifiedName~ConfirmacionPorCorreoTests|FullyQualifiedName~PacientesApiTests"`: deben pasar.
- `dotnet test`: debe pasar la suite completa, arquitectura incluida.

**Manual checks:**
- Con el AppHost, reservar con un correo y abrir Mailpit desde el dashboard de Aspire.
