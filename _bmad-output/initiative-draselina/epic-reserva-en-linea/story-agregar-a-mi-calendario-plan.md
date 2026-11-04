---
title: 'Agregar a mi calendario'
type: 'feature'
ticket: '10'
created: '2026-11-03'
status: 'built'
baseline_revision: '5ab3eef0c2c5a7a0180f3d3e59e6b293dab8cee8'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** El Paciente no puede guardar su Cita en el calendario del teléfono: FR-5 pide "agregar a mi calendario" en la confirmación, con el Enlace de gestión dentro del evento, aunque no haya dado correo.

**Approach:** Un generador único del archivo de calendario en Agenda (arquitectura, Consistency Conventions "Archivo de calendario"), servido por un endpoint al que se llega con el token del Enlace de gestión, y el botón "Agregar a mi calendario" en Cita confirmada.

## Boundaries & Constraints

**Always:**
- Un solo generador, en Agenda, expuesto por `IAgendaApi` para que Notificaciones (épica 4) lo reutilice; el lugar lo pide a `IContenidoApi` (AD-15).
- El evento: inicio y fin de la Cita en UTC (el teléfono lo muestra en su hora local), título con el tipo y la doctora, lugar y dirección del consultorio, y en la descripción el Enlace de gestión (FR-5). Formato iCalendar (RFC 5545), `text/calendar`, un `UID` estable por Cita.
- AD-23: el archivo no lleva motivo de consulta, nombre, teléfono ni correo.
- El endpoint se alcanza con el token del Enlace de gestión (`/c/<token>/calendario.ics`), nunca con el `CitaId`: Agenda busca la Cita por el hash del token (AD-19). Solo una Cita `Agendada` cuyo inicio no ha pasado entrega el archivo; cualquier otro caso (token desconocido, Cita cancelada o pasada) responde 404 sin decir por qué (RN-22).
- Decisión (Gerardo, 2026-11-03): el endpoint no pide actor; es una lectura sin bitácora ni eventos.
- AD-23 en la ruta: ni los registros ni las trazas llevan el token; la telemetría enmascara la ruta `/c/…` (por ejemplo, `/c/{token}`).
- Cita confirmada enlaza "Agregar a mi calendario" a ese endpoint; sin scripts.

**Never:**
- La página `/c/<token>` para ver, reprogramar o cancelar (épica 3); correos con el archivo adjunto (épica 4).
- Actor, bitácora, transacción o escritura alguna en este endpoint.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Descarga válida | Token de una Cita Agendada futura | 200, `text/calendar`, un evento con su inicio y fin en UTC, lugar, dirección y el Enlace de gestión | — |
| Sin datos personales | La Cita tiene motivo y el Paciente correo | El archivo no contiene motivo, nombre, teléfono ni correo | — |
| UID estable | Dos descargas de la misma Cita | El mismo `UID` | — |
| Token desconocido | Token que no corresponde a ninguna Cita | 404 | Sin detalle |
| Cita pasada | El inicio ya pasó | 404 | Sin detalle |
| Cita cancelada | Estado `Cancelada` (fila por SQL) | 404 | Sin detalle |
| Sin token en la telemetría | Una descarga | Ninguna traza ni registro contiene el token | — |
| Botón | Cita confirmada | "Agregar a mi calendario" apunta a `/c/<token>/calendario.ics` | — |

</frozen-after-approval>

## Code Map

Rutas bajo `src/DraSelina.Consultorio.Web/` salvo que digan otra cosa.

- `Modules/Agenda/Contracts/IAgendaApi.cs`, `Features/AgendaApi.cs` -- hoy `ObtenerEnlaceDeGestion(CitaId)`; agregar el archivo de calendario.
- `Modules/Agenda/Domain/EnlaceDeGestion.cs` -- `ClaveDeGestion`, token base64url y hash; `ICitas` busca por solicitud; agregar buscar por hash del token (lectura).
- `Modules/Agenda/Domain/Cita.cs`, `EstadoCita` -- `Rango`, `Estado`, `TipoDeVisitaId`; "ya pasó" se mide desde el inicio (RN-31).
- `Modules/Contenido/Contracts/IContenidoApi.cs` -- `DatosDelConsultorio` (lugar, dirección, ciudad).
- `Program.cs` -- hoy `MapStaticAssets`, `MapRazorComponents`, `MapDefaultEndpoints`; el endpoint se mapea aquí o con un registro de Agenda.
- `src/DraSelina.Consultorio.ServiceDefaults/Extensions.cs` -- `AddAspNetCoreInstrumentation` con `Filter`; ahí va el enmascarado de `/c/…`. `appsettings.json` deja `Microsoft.AspNetCore` en `Warning`.
- `UI/Pages/Reserva/CitaConfirmada.razor` -- panel "Guarda este enlace"; agregar el enlace del calendario.
- `tests/DraSelina.Consultorio.IntegrationTests/EnlaceDeGestionTests.cs`, `TusDatosTests.cs` -- reloj fijo, arnés con clave de prueba; imitar.

## Tasks & Acceptance

**Execution:**
- [x] Agenda: generador iCalendar (dominio o `Features`), `IAgendaApi` con el archivo de una Cita, búsqueda por hash del token.
- [x] Endpoint `GET /c/{token}/calendario.ics` sin actor ni transacción; 404 para lo que no sea una Cita Agendada futura.
- [x] ServiceDefaults: enmascarar la ruta `/c/…` en las trazas.
- [x] `CitaConfirmada.razor` + CSS -- "Agregar a mi calendario".
- [x] Pruebas -- cada fila de la matriz; validar el archivo (BEGIN/END, DTSTART/DTEND en UTC, LOCATION, DESCRIPTION con el enlace, UID); trazas y registros sin el token.

**Acceptance Criteria:**
- Given una Cita confirmada, when se toca "Agregar a mi calendario", then se descarga un `.ics` con su fecha y hora, el lugar y el Enlace de gestión, sin datos personales.
- Given la solución, when corren todas las pruebas y arranca el AppHost, then todo pasa.

## Implementation Notes

- Generador: `Agenda/Features/GeneradorDeCalendario.cs` (puro, sin base). VCALENDAR/VEVENT con `DTSTART`/`DTEND`/`DTSTAMP` en UTC, `SUMMARY` "<tipo> con la Dra. Selina Medina Castillo", `LOCATION` lugar + dirección + ciudad de `IContenidoApi`, `DESCRIPTION` y `URL` con el Enlace de gestión, `UID:cita-<CitaId>@<host de UrlBase>`. Escapa TEXT y pliega a 75 octetos con CRLF (RFC 5545). DTO `Agenda/Contracts/ArchivoDeCalendario.cs` (`text/calendar; charset=utf-8`, `cita.ics`; `ToString` oculto).
- AD-10: el endpoint pide `IQueryHandler<ArchivoDeCalendarioPorEnlace, ArchivoDeCalendario>` (consulta en Contracts, `ToString` oculto); fuera de una Cita Agendada futura devuelve `CodigosDeAgenda.CitaNoEncontrada` y el endpoint responde 404 vacío. `IAgendaApi` conserva solo `ObtenerArchivoDeCalendarioAsync(CitaId)` para Notificaciones; handler y API comparten `AgendaApi.Archivo` (misma regla, mismo generador). `AgendaApi` pasa de singleton a scoped (lee la base); las pruebas que lo pedían a la raíz ahora abren un ámbito.
- Búsqueda por hash: `EnlaceDeGestion.HashDelToken` (dominio; nulo si el token no es base64url canónico de 32 bytes, sin tocar la base) y el puerto de lectura `Features/ICitasDelCalendario` + `Infrastructure/CitasDelCalendario` (AsNoTracking, proyección con el nombre del tipo). Se eligió un puerto de lectura de Features en lugar de ampliar `ICitas` (escritura) por AD-4 y la convención "Lecturas".
- Endpoint: `Registro.MapAgenda()` en Agenda Infrastructure, llamado desde `Program.cs`. `Cache-Control: no-store`; 404 con cuerpo vacío.
- Registros: `Microsoft.AspNetCore.Hosting.Diagnostics` en `None` (appsettings.json): sin el ámbito con `RequestPath` ni las líneas "Request starting/finished", que llevarían el token (AD-23).
- Telemetría: `EnrichWithHttpRequest` en ServiceDefaults reescribe `url.path` a `/c/{token}/...` (`Extensions.RutaEnmascarada`). `http.route` ya es la plantilla.
- UI: botón secundario (DESIGN.md: blanco, borde vino 2 px) `.dsm-boton-secundario` bajo la tarjeta, con o sin correo; `href` `/c/<token>/calendario.ics` armado con el último segmento del enlace (funciona aunque `UrlBase` lleve ruta).

## Plan Change Log

## Review Triage Log

**Pasada 1 (quick), 2026-11-04.** Veredictos: high 0, medium 2, low 1, false 0, maybe-false 0. El hallazgo 1 también lo había reportado el implementador.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | El scope de registro por petición de ASP.NET Core (`RequestPath=/c/<token>/…`) viaja a OTLP con `IncludeScopes = true`; el "Request starting" solo lo frena el nivel | medium | patch | AD-23. La prueba no lo ve: `RegistrosDePrueba.BeginScope` devuelve `null`. Se apaga la categoría `Microsoft.AspNetCore.Hosting.Diagnostics` (crea el scope y esas líneas); las trazas conservan la correlación por `TraceId` |
| 2 | El endpoint llama a `IAgendaApi` en lugar de una consulta `IQuery` con su handler (AD-10) | medium | patch | AD-10 pide que la interfaz pida el handler del caso de uso; Minimal API es solo el transporte. Consulta y handler nuevos; `IAgendaApi` por `CitaId` se queda para Notificaciones |
| 3 | El `href` del calendario sale de la ruta de `UrlBase`; con una base con ruta (`https://x.mx/citas`) apunta a una dirección que el endpoint no sirve | low | patch | Corrección directa: `/c/<token>/calendario.ics` desde el token |

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` -- expected: todas pasan (Docker arriba).
- AppHost y `scripts/verificar-arranque.sh` -- expected: las tres filas pasan.

**Manual checks (if no CLI):**
- El `.ics` se importa en el calendario de un teléfono con la hora correcta de Ciudad de México.
