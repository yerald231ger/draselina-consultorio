---
title: 'Tu cita con el enlace'
type: 'feature'
ticket: '1'
created: '2026-11-06'
status: 'built'
baseline_revision: '42118ab52e8457db3392288461403a86eeebeedc'
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

**Problem:** El Enlace de gestión (`/c/<token>`) que muestra Cita confirmada solo sirve para descargar el `.ics`; abrirlo no lleva a ninguna página. El Paciente no puede ver su Cita sin cuenta (CAP-6, FR-8).

**Approach:** Una consulta `CitaPorEnlace` busca la Cita por el hash del token, igual que el calendario, y una página Tu cita en SSR estático en `/c/{token}` la muestra según el mockup Gestionar, en sus tres estados (Decision del 2026-11-06). Todo lo que no vale lleva a Enlace no válido. Todo `/c/...` va sin índice, sin caché y con el límite de frecuencia de la reserva.

## Boundaries & Constraints

**Always:**
- La búsqueda es solo por `token_hash` (AD-19), y un token mal formado no consulta la base (`EnlaceDeGestion.HashDelToken`).
- "Ya pasó" se mide desde el inicio (RN-31). Un enlace cuyo inicio ya pasó no vale (RN-22), cualquiera que sea el estado de la Cita.
- Enlace no válido no dice por qué: inexistente, alterado, mal formado y ya empezada se ven igual y responden 404.
- El token nunca va a registros, trazas ni a la cadena de consulta (AD-23). `CitaPorEnlace.ToString()` lo oculta, como `ArchivoDeCalendarioPorEnlace`.
- Toda respuesta `/c/...`, el `.ics` incluido, lleva `X-Robots-Tag: noindex, nofollow` y `Cache-Control: no-store`. Las páginas llevan además `<meta name="robots" content="noindex, nofollow">` (CAP-21).
- Tu cita y el `.ics` usan la política `DemasiadosIntentos.Politica` (AD-22, ampliada el 2026-11-06).
- La consulta no tiene actor, transacción, bitácora ni eventos.
- Textos de tú, en español de México. Sin scripts (AD-11).
- Decision (Gerardo, 2026-11-06): Tu cita muestra la fila "Paciente" del mockup con el nombre que da `IPacientesApi.NombreAsync(PatientId)`, método nuevo de Pacientes (RN-29). El nombre no va a registros: `CitaDelEnlace.ToString()` lo oculta (AD-23).
- Decision (Gerardo, 2026-11-06): el plan se conserva completo, aunque pasa de 1,600 tokens.

**Never:**
- Los botones Reprogramar y Cancelar (entradas 3.3 y 3.4), ni el panel ámbar del umbral.
- Leer el motivo de consulta o datos de contacto.
- Mostrar el CitaId en la página o en una dirección.
- Cambiar Cita confirmada o la redirección de `deferred-work.md`.

## I/O & Edge-Case Matrix

| Escenario | Estado | Resultado |
|---|---|---|
| Agendada futura | token válido, `Agendada`, inicio > ahora | 200 Tu cita: chip "Agendada", día y hora, tipo · duración, filas Paciente y Lugar, "Agregar a mi calendario" (`/c/<token>/calendario.ics`) y "¿Otra duda? Escríbenos por WhatsApp" |
| Cancelada por el Paciente o tarde | motivo `paciente` o `tarde`, inicio > ahora | 200: chip "Cancelada", filas Paciente y Lugar, "Cancelaste esta cita." `[ASSUMPTION: texto]`, botón "Agendar otra cita" → `/reservar`, sin calendario |
| Cancelada por el Consultorio | motivo `consultorio`, inicio > ahora | 200: chip "Cancelada", filas Paciente y Lugar, "Lo sentimos: el consultorio tuvo que cancelar tu cita. Elige otro horario cuando te acomode." `[ASSUMPTION: texto]`, botón "Elegir otro horario" → `/reservar`, sin calendario |
| Ya empezada | cualquier estado, inicio <= ahora | 404 Enlace no válido |
| Inexistente, alterado o mal formado | sin Cita para el hash, o `HashDelToken` nulo | 404 Enlace no válido: "Este enlace ya no está disponible" y "Agendar cita" → `/reservar` |
| Límite pasado | 61 visitas en un minuto desde una IP | 429 Demasiados intentos |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/ArchivoDeCalendarioPorEnlace.cs`: el modelo para el contrato nuevo (record con token oculto en `ToString`).
- `.../Agenda/Contracts/AgendarCitaEnLinea.cs`: `CodigosDeAgenda.CitaNoEncontrada` ya existe y se reutiliza.
- `.../Agenda/Features/ArchivoDeCalendarioPorEnlaceHandler.cs`: el patrón del handler nuevo (hash → puerto → regla).
- `.../Agenda/Features/ICitasDelCalendario.cs` y `Infrastructure/CitasDelCalendario.cs`: la proyección sin rastrear con join a `TiposDeVisita`. No se cambia: su contrato es el del `.ics`.
- `.../Agenda/Infrastructure/Registro.cs`: registro de handlers y puertos. `MapAgenda` sirve el `.ics` en `RutaDelCalendario`, que ya pone `no-store`.
- `.../Agenda/Domain/EstadoCita.cs`: `EstadoCita` y `MotivoDeCancelacion`. Son de dominio y no se exponen en Contracts.
- `.../Contenido/Contracts/IContenidoApi.cs`: `DatosDelConsultorio.Lugar` y `WhatsApp`.
- `src/DraSelina.Consultorio.Web/UI/Pages/Reserva/CitaConfirmada.razor`: las clases `dsm-tarjeta`, `dsm-lugar` y `dsm-confirmada__calendario`, y cómo arma `/c/<token>/calendario.ics`. `FormatoDeCita.Dia/FechaYHora` formatean la fecha.
- `.../UI/Componentes/MensajeDeWhatsApp.razor`: el contacto.
- `.../UI/Pages/DemasiadosIntentos.razor`: `Politica`. Las páginas la piden con `[EnableRateLimiting(...)]`, como `DiaYHora.razor`.
- `src/DraSelina.Consultorio.Web/Program.cs`: el pipeline (`UseRateLimiter`, `UseAntiforgery`, `MapAgenda`).
- `src/DraSelina.Consultorio.ServiceDefaults/Extensions.cs`: `RutaEnmascarada` ya enmascara `/c/{token}`. No se toca.
- `tests/DraSelina.Consultorio.IntegrationTests/CalendarioTests.cs`: el patrón de prueba: `ReservaEnLineaTests.Escenario`, `escenario.Reloj`, cancelar con `update agenda.cita set ...`, y las pruebas de token fuera de registros y trazas.
- `tests/.../LimiteDeFrecuenciaYAntiBotsTests.cs`: cómo se prueba el 429.
- `_bmad-output/initiative-draselina/ux-appointment-booking/imports/mockup-canvas/Gestionar.dc.html`: el mockup (encabezado, "Tu cita", tarjeta con chip, filas Paciente y Lugar, contacto por WhatsApp al pie).

## Tasks & Acceptance

**Execution:**
- [ ] `.../Agenda/Contracts/CitaPorEnlace.cs`: crear `CitaPorEnlace(string Token) : IQuery<CitaDelEnlace>` con el token oculto, `CitaDelEnlace(Fecha, Hora, TipoDeVisita, DuracionMin, Paciente, Lugar, EstadoDelEnlace Estado)` (con `ToString` que oculta el nombre) y el enum `EstadoDelEnlace { Agendada, CanceladaPorElPaciente, CanceladaPorElConsultorio }`. Es el contrato que leen las entradas 3.3 y 3.4.
- [ ] `.../Agenda/Features/ICitasDelEnlace.cs` y `Infrastructure/CitasDelEnlace.cs`: un puerto de lectura `PorTokenHashAsync` que proyecta inicio, fin, estado, motivo de cancelación, `PacienteId` y nombre del tipo, sin rastrear.
- [ ] `.../Pacientes/Contracts/IPacientesApi.cs`, `Features/PacientesApi.cs` y `tests/.../PacientesApiTests.cs`: agregar `Task<string?> NombreAsync(PatientId, CancellationToken)`, que devuelve el nombre tal como se guardó, o nulo si el Paciente no existe, con su prueba.
- [ ] `.../Agenda/Features/CitaPorEnlaceHandler.cs`: aplicar hash nulo o sin Cita o inicio <= `reloj.UtcNow` → `Fallo(CitaNoEncontrada)`. Si no, mapear el estado (motivo `Paciente` y `Tarde` → `CanceladaPorElPaciente`) y la fecha y hora locales con `HoraLocal.DesdeUtc`. El nombre lo pide a `IPacientesApi.NombreAsync`; un Paciente inexistente es un error de programación, no un `Resultado`.
- [ ] `.../Agenda/Infrastructure/Registro.cs`: registrar el puerto y el handler. Al endpoint del `.ics`, agregarle `.RequireRateLimiting(DemasiadosIntentos.Politica)`.
- [ ] `src/DraSelina.Consultorio.Web/Program.cs`: agregar un middleware antes de las rutas que, para `/c` y sus subrutas, ponga `X-Robots-Tag` y `Cache-Control: no-store`.
- [ ] `.../UI/Pages/Gestion/TuCita.razor` (nuevo, `@page "/c/{Token}"`): la página con `[EnableRateLimiting]`, `<PageTitle>`, `<HeadContent>` con el meta robots, los tres estados de la matriz y Enlace no válido con 404 (vía `HttpContext` en cascada).
- [ ] `.../UI/Pages/Gestion/EnlaceNoValido.razor`: un componente reutilizable por 3.3 y 3.4.
- [ ] `wwwroot/css/consultorio.css`: el chip de estado y la fila etiqueta-valor, si no existen. Reutilizar los tokens.
- [ ] `tests/.../TuCitaTests.cs` (nuevo): una prueba por fila de la matriz. Además: el enlace de la Cita B muestra la fecha de B y no la de A; los encabezados y el meta en Tu cita, en Enlace no válido y en el `.ics`; el token no aparece en registros ni en trazas.

**Acceptance Criteria:**
- Given una Cita reservada con el AppHost, when se abre el Enlace de gestión de Cita confirmada, then aparece Tu cita con su fecha y hora.
- Given dos Citas, when se abre el enlace de una, then la página no contiene la fecha ni la hora de la otra.
- Given cualquier respuesta bajo `/c/`, when se inspeccionan sus encabezados, then trae `X-Robots-Tag: noindex, nofollow` y `Cache-Control: no-store`.

## Design Notes

`EstadoDelEnlace` junta Paciente y Tarde en un solo valor porque Tu cita los muestra igual (Decision del 2026-11-06). La pantalla Cita cancelada de 3.4, que sí reconoce la tardía, es otra respuesta. El estado vive en Contracts para que la UI no vea el dominio (AD-1 a AD-3).

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 0, low 5, false 0, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Un 500 bajo `/c/` sale sin `X-Robots-Tag` ni `no-store` | low | rechazado | Real: Kestrel limpia los encabezados al escribir el 500. Solo se alcanza con un Paciente inexistente, que viola la integridad de datos. Un buscador no indexa un 500 y un caché no lo guarda. Arreglarlo pide un `OnStarting` más. |
| La página 429 bajo `/c/` no lleva el meta robots | low | rechazado | Real, pero la respuesta trae `X-Robots-Tag: noindex, nofollow`, que ya basta para los buscadores (la prueba lo comprueba). Agregar el meta pide condicionar `DemasiadosIntentos`/`App`. |
| El enlace de WhatsApp está escrito dos veces y no hay prueba del número | low | rechazado | Real: el formato `wa.me/52…` vive en `TuCita.razor` y en `MensajeDeWhatsApp.razor`, y la regla de dígitos es compartida. El número no se puede probar mientras `WhatsAppDelConsultorio` sea nulo. Unificarlo pide un componente o parámetro nuevo. |
| "Ya empezada" solo se prueba en el límite (inicio = ahora) | low | patch | Real como hueco de prueba. El arreglo es directo: agregar casos con inicio una hora antes (Agendada y Cancelada). |
| La rama `_ => null` del estado oculta datos incoherentes | low | rechazado | Cancelada sin motivo lo impide el `CHECK` de `agenda.cita` (INV-C8), y NoSePresento con inicio futuro lo impide `MarcarNoSePresento` (épica 5). Mostrar Enlace no válido es una salida segura. |

## Verification

**Commands:**
- `dotnet build` desde la raíz: debe compilar sin advertencias nuevas.
- `dotnet test tests/DraSelina.Consultorio.IntegrationTests --filter "FullyQualifiedName~TuCitaTests|FullyQualifiedName~CalendarioTests"`: deben pasar.
- `dotnet test`: debe pasar la suite completa, arquitectura incluida.

**Manual checks:**
- Con el AppHost, reservar, abrir el enlace mostrado en Cita confirmada y ver Tu cita a 390 px.
