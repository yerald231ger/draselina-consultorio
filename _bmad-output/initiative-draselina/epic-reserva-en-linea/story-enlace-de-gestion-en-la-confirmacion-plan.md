---
title: 'Enlace de gestión en la confirmación'
type: 'feature'
ticket: '9'
created: '2026-11-03'
status: 'built'
baseline_revision: '39daed5f34ab9c8f1faadf4c12d6c73b27dceaca'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-draselina/ux-appointment-booking/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** El Paciente termina la reserva sin ninguna forma de volver a su Cita: no existe el Enlace de gestión que FR-5 pide en la confirmación y que las épicas 3 y 4 necesitan.

**Approach:** El Enlace de gestión derivado (AD-19): token `HMAC-SHA256(clave del host, CitaId)`, del que la Cita guarda solo el hash, y que solo Agenda arma por `IAgendaApi.ObtenerEnlaceDeGestion(CitaId)`; Cita confirmada lo muestra con "Guarda este enlace".

## Boundaries & Constraints

**Always:**
- AD-19 y RN-22: el token se deriva, no se genera al azar; la clave vive fuera de la base (configuración del host, parámetro secreto del AppHost en local, nunca en el repositorio); `agenda.cita.token_hash` (`bytea`, 32 bytes, SHA-256 del token, `not null`, único) se fija al crear la Cita en línea. Agenda es la única que arma el enlace.
- La dirección del enlace es `<url base>/c/<token>` con el token en base64url sin relleno (mockup `Confirmada`: `citas.[dominio].mx/c/7Kq…`). La URL base sale de la configuración del host; en local, la de la web del AppHost. La página que atiende `/c/…` es de la épica 3.
- AD-23: el token y el enlace nunca van a registros, telemetría, bitácora, eventos ni outbox. El enlace solo aparece en la respuesta de Cita confirmada (y después en los correos, épica 4).
- Cita confirmada muestra el panel "Guarda este enlace" con "Con él puedes reprogramar hasta 2 horas antes o cancelar en cualquier momento. No necesitas cuenta." y el enlace completo como texto seleccionable; el "2 horas" sale del umbral de `ReglasDeReserva`, no está escrito en la página. Sin scripts: no hay botón "Copiar".
- Sin correo (EXPERIENCE.md, "Confirmada, sin correo"): en lugar de la frase del correo, "No recibirás recordatorio. Guarda este enlace o agrega la cita a tu calendario." y el panel "Guarda este enlace" sube de posición.
- Un doble envío (entrada 8) devuelve la misma confirmación con el mismo enlace.
- Decisión (Gerardo, 2026-11-03): con correo, Cita confirmada no muestra todavía ninguna frase de correo ni de recordatorio (nada se envía hasta la épica 4); la épica 4 la agrega con la hora real del Recordatorio y el estado "Recordatorio omitido". Sin correo, sí se muestra la frase de "sin correo".

**Never:**
- Validar el enlace, la página `/c/…`, reprogramar o cancelar (épica 3); enviar correos o recordatorios (épica 4); el archivo de calendario (entrada 10).
- Guardar el token en claro o generarlo al azar.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Reserva válida | Martes 4:00 pm | Cita con `token_hash`; confirmación con "Guarda este enlace" y el enlace `/c/<token>` | — |
| Estabilidad | `ObtenerEnlaceDeGestion` dos veces para la misma Cita | El mismo enlace | — |
| Distinto por Cita | Dos Citas | Enlaces distintos; hashes distintos | — |
| Solo el hash | Leer `agenda.cita` | `token_hash` = SHA-256 del token; el token no aparece en ninguna columna | — |
| Otra clave | La misma Cita con otra clave | Otro enlace | — |
| Sin correo | Reserva sin correo | Frase de "sin correo" y el panel antes | — |
| Doble envío | Mismo `SolicitudId` dos veces | Misma confirmación y mismo enlace | — |
| Sin clave | El host arranca sin la clave configurada | No arranca, con un mensaje que nombra la configuración que falta | Falla al arrancar, no al reservar |

</frozen-after-approval>

## Code Map

Rutas bajo `src/DraSelina.Consultorio.Web/` salvo que digan otra cosa.

- `src/DraSelina.Consultorio.AppHost/AppHost.cs` -- hoy: `postgres`, `migraciones`, `web`. Agregar el parámetro secreto de la clave y la URL base para la web.
- `Modules/Agenda/Domain/Cita.cs` -- `Agendar(solicitud, paciente, tipo, inicio, motivo, consentimiento, reglas, citasDeLaSemana, now, out cita)`; la Cita recibe el hash de su token (`EnlaceDeGestion` es objeto de valor de la Parte I).
- `Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs` -- arma la confirmación (`CitaAgendadaEnLinea`), también desde una Cita existente por `SolicitudId`; agregar el enlace.
- `Modules/Agenda/Contracts/AgendarCitaEnLinea.cs` -- `CitaAgendadaEnLinea`; ahí entra el enlace. `IAgendaApi` es nueva en `Contracts`.
- `Modules/Agenda/Infrastructure/AgendaDbContext.cs` + migración `AgregarTokenHashACita` -- `token_hash` con único; el comentario de `agenda.cita` dice que llega con esta historia.
- `Modules/Agenda/Domain/ReglasDeReserva.cs` -- `UmbralCancelacionTardiaMin` para el texto de "2 horas".
- `UI/Pages/Reserva/CitaConfirmada.razor`, `wwwroot/css/consultorio.css` -- hoy fecha, hora, tipo, lugar y "Volver al inicio".
- `src/DraSelina.Consultorio.MigrationService/Program.cs` -- registra los servicios de Agenda y valida en Development: lo que pida la clave también debe resolverse ahí.
- `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs` -- arranca la web y el MigrationService: darles una clave de prueba.

## Tasks & Acceptance

**Execution:**
- [x] Dominio y puerto -- `EnlaceDeGestion` (token y hash) y la Cita con su `token_hash`.
- [x] `Modules/Agenda/Contracts/IAgendaApi.cs` + implementación en `Features` -- `ObtenerEnlaceDeGestion(CitaId)`; opciones de la clave y la URL base con validación al arrancar.
- [x] `AgendarCitaEnLineaHandler.cs`, `CitaAgendadaEnLinea` -- el enlace en la confirmación, también al devolver una Cita existente.
- [x] Migración, `AgendaDbContext`, AppHost, MigrationService y arnés -- columna, parámetro secreto, URL base, clave de prueba.
- [x] `CitaConfirmada.razor` + CSS -- panel "Guarda este enlace" y estados con y sin correo.
- [x] Pruebas -- cada fila de la matriz; ninguna entrada de registro, bitácora ni outbox contiene el token.

**Acceptance Criteria:**
- Given una reserva válida, when se confirma, then Cita confirmada muestra "Guarda este enlace" con `<url base>/c/<token>` y la base solo guarda el hash del token.
- Given la solución, when corren todas las pruebas y arranca el AppHost, then todo pasa.

## Implementation Notes

- Token: `HMAC-SHA256(clave, CitaId.ToByteArray(bigEndian: true))`, base64url sin relleno (43 caracteres); `token_hash` = SHA-256 de esos 32 bytes. `ClaveDeGestion` y `EnlaceDeGestion` viven en `Agenda.Domain`; `Cita.Agendar` recibe la clave y guarda solo el hash. `ToString` de ambos no muestra secreto; `CitaAgendadaEnLinea.ToString` omite el enlace.
- La clave es texto de configuración (`EnlaceDeGestion:Clave`), al menos 32 bytes en UTF-8; la URL base, `EnlaceDeGestion:UrlBase`. `ValidateOnStart`: la web no arranca sin ellas y el mensaje nombra la clave que falta.
- AppHost: parámetro secreto `clave-enlace-de-gestion` con `GenerateParameterDefault` (64 caracteres, sin persistir): sin valor en user-secrets o configuración se genera uno por arranque, igual que la base vacía. URL base: el endpoint `http` de la web.
- MigrationService: sin cambios. Registra `AddAgenda`, pero no arranca el host, así que no valida las opciones ni pide la clave; la validación de Development solo exige que todo esté registrado. El arnés da la clave solo a la web.
- `IAgendaApi.ObtenerEnlaceDeGestion` es síncrona: deriva, no lee la base ni comprueba que la Cita exista.
- "Con correo" lo decide Pacientes: `IPacientesApi.TieneCorreoAsync(PatientId)` (solo sí o no, RN-29), en los dos caminos de la confirmación; va en `CitaAgendadaEnLinea.ConCorreo`. Un Paciente con correo guardado que lo deja en blanco sigue "con correo" (RN-30), y reenviar la misma solicitud con otro correo no cambia el estado.
- Migración `AgregarTokenHashACita`: `bytea not null` sin valor por defecto, único `ux_cita_token_hash` y `ck_cita_token_hash` (`octet_length = 32`). Falla sobre una tabla con Citas; hoy ninguna base con Citas existe fuera de local.

## Plan Change Log

## Review Triage Log

**Pasada 1 (quick), 2026-11-03.** Veredictos: high 0, medium 1, low 2, false 0, maybe-false 0. Incluye un hallazgo del propio reporte del implementador (punto 3), agrupado con el del revisor por la misma causa.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | "Con/sin correo" sigue al formulario enviado (`Comando.Correo`), no al Paciente: un Paciente que vuelve sin escribir su correo conserva el guardado (RN-30) y ve "No recibirás recordatorio"; un reenvío de la misma solicitud con otro correo cambia el estado de la misma Cita | medium | patch | Única lectura posible de "sin correo": el Paciente no tiene correo guardado, que es adonde irán los correos (épica 4). Lectura mínima en `IPacientesApi` y el estado en `CitaAgendadaEnLinea` para los dos caminos |
| 2 | README, "Arrancar", no menciona el parámetro secreto `clave-enlace-de-gestion` ni que sin él los enlaces cambian en cada arranque | low | patch | Corrección directa de documentación operativa |
| 3 | EXPERIENCE.md (línea ~106) describe un botón "Copiar" que no existe (sin scripts) | low | a Gerardo | Documento de UX: su redacción la aprueba Gerardo; se le presenta el texto |

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` -- expected: todas pasan (Docker arriba).
- AppHost y `scripts/verificar-arranque.sh` -- expected: las tres filas pasan.
