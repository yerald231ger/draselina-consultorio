---
title: 'Cancelar desde Tu cita'
type: 'feature'
ticket: '4'
created: '2026-11-07'
status: 'built'
baseline_revision: 'b20352368c6a408defdac4579f465277ca9ddd47'
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

**Problem:** El Paciente no puede cancelar su Cita con el enlace (CAP-8, FR-10, UJ-2 pasos 4 a 7). Tampoco existe `CitaCancelada`, que esperan las épicas 4 y 5.

**Approach:** Agregar `cita.Cancelar(now, reglas)` y el comando `CancelarCitaPorEnlace(token)`, que deja bitácora y `CitaCancelada` en la misma transacción y es idempotente por el estado. En la UI: "Cancelar cita" en Tu cita, la pantalla Cancelar cita (normal y tardía) y Cita cancelada.

## Boundaries & Constraints

**Always:**
- `cita.Cancelar(now, reglas)` (`ReglasDeAgenda`):
  - Solo procede si la Cita está Agendada y su inicio no ha pasado (RN-16, RN-31). Nunca se impide antes del inicio.
  - Si faltan menos de `UmbralCancelacionTardiaMin` minutos, el motivo es `Tarde`; si no, `Paciente`. Con exactamente el umbral, el motivo es `Paciente`, igual que el corte de `Reprogramar` (RN-17).
  - Fija `Estado = Cancelada`, `MotivoDeCancelacion` y `CanceladaEn = now` (INV-C8).
  - No cambia el rango.
- El handler:
  - busca solo por el hash del token (si el hash es nulo, no consulta la base);
  - abre la transacción y lee la Cita con `ObtenerPorTokenHashParaCambiarAsync`, el bloqueo de fila de la 3.2 (Decision del 2026-11-06);
  - no toma candado de fecha: AD-26 solo aplica a crear o mover.
- Resultados del comando:
  - Si no hay Cita o su inicio ya pasó, devuelve `CitaNoEncontrada`.
  - Si ya está Cancelada, devuelve éxito con su motivo guardado, sin bitácora ni evento (Decision del 2026-11-06).
  - Si no se presentó, devuelve `CitaNoAgendada`.
- En la misma transacción que el cambio:
  - bitácora `Cancelada`, con el actor de `IActorActual`, `actor_id` nulo y en el detalle el inicio, el fin y el motivo;
  - `CitaCancelada(EventoId, OcurridoEn, CitaId, PatientId, Actor, Inicio, Motivo)` por el outbox, con clave de orden `CitaId`. El motivo va como el texto de la columna (`paciente`, `tarde`), y Notificaciones no lo recalcula (E-16).
- El resultado es `CitaCanceladaPorEnlace(Fecha, Hora, TipoDeVisita, Tardia)`. `ToString` del comando oculta el token (AD-23).
- El horario cancelado vuelve a ofrecerse sin código nuevo: la restricción de exclusión y `CitasActivas` ya ignoran las canceladas.
- Tu cita (Agendada) muestra el botón "Cancelar cita" (estilo del mockup Gestionar: contorno, texto rojo), dentro o fuera del umbral. Lleva a `/c/{token}/cancelar`.
- `/c/{token}/cancelar` (SSR estático, sin scripts):
  - **GET:** "¿Cancelar tu cita?", la fecha y hora con el tipo, y los botones "Sí, cancelar mi cita" (POST con antiforgery) y "Conservar mi cita" (vuelve a `/c/{token}`).
  - **Dentro del umbral:** además, el `PanelDentroDelUmbral` de la 3.3, con el texto del mockup Cancelar: "Puedes cancelar sin problema; quedará registrada como cancelación tardía. Gracias por avisarnos, así la doctora puede atender a alguien más."
  - **Fuera del umbral:** sin panel (EXPERIENCE.md, "Cancelación normal").
  - Lleva el límite de frecuencia, el meta robots y los encabezados de `/c/`, igual que la 3.1.
- Cita cancelada, en la misma respuesta del POST:
  - "Tu cita está cancelada" `[ASSUMPTION: texto]`, con la fecha, la hora y el tipo.
  - Si fue tardía, además: "Gracias por avisarnos. Quedó registrada como cancelación tardía." `[ASSUMPTION: texto]`
  - El botón "Agendar otra cita" → `/reservar`.
- Salidas de la página:
  - GET de una Cita ya cancelada: "Esta cita ya está cancelada." con "Ver mi cita", el mismo texto de la 3.3.
  - Enlace no válido, en GET o POST: 404, igual que la 3.1.
  - Un segundo POST sobre una Cita ya cancelada: Cita cancelada con el motivo guardado.

- Decision (Gerardo, 2026-11-07): el plan se conserva completo aunque pasa de 1,600 tokens.

**Never:**
- La cancelación por el Consultorio (`CancelarPorConsultorio`, épica 5).
- Correos o la alerta de cancelación tardía (épica 4).
- Pedir confirmar dos veces.
- Recalcular la tardía fuera de `Cita`.

## I/O & Edge-Case Matrix

Con la Cita el martes 3 a las 16:00 local (22:00 UTC), con 30 min y umbral de 120.

| Escenario | Reloj | Resultado |
|---|---|---|
| Cancelación normal | 3 h antes | motivo `paciente`; bitácora `Cancelada` con actor Paciente; `CitaCancelada` pendiente con motivo `paciente`; Cita cancelada sin la frase de tardía |
| Justo en el umbral | 120 min antes | motivo `paciente` |
| Cancelación tardía | 1 h antes | motivo `tarde`; Cita cancelada con la frase de tardía |
| Pantalla fuera del umbral | 3 h antes, GET | "¿Cancelar tu cita?" sin panel ámbar |
| Pantalla dentro del umbral | 1 h antes, GET | panel ámbar con el texto de tardía y "¿Buscabas otro horario?" |
| Ya empezó | en el inicio o después | `cita_no_encontrada`; la página da 404 |
| Segundo envío | después de cancelar | éxito con el motivo guardado; sin otra bitácora ni otro evento |
| Horario liberado | después de cancelar | `HorariosLibres` vuelve a ofrecer el martes 3 a las 16:00 |
| Token inválido | — | `cita_no_encontrada`; 404 |
| Cancelar y reprogramar a la vez | concurrente | uno espera al otro por el bloqueo de fila; si reprogramar llega segundo, recibe `cita_no_agendada` |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs`: `Reprogramar` (3.2) es el modelo: rechazo como enum, corte del umbral con `TimeSpan.FromMinutes(reglas.UmbralCancelacionTardiaMin)`. `MotivoDeCancelacion` y `CanceladaEn` ya existen, con setters privados.
- `.../Agenda/Domain/ICitas.cs` y `Infrastructure/Citas.cs`: `ObtenerPorTokenHashParaCambiarAsync` (bloqueo de fila) y `GuardarAsync`.
- `.../Agenda/Features/ReprogramarCitaPorEnlaceHandler.cs`: el patrón completo (hash, transacción, bloqueo, rastro, códigos). Para `ReglasDeAgenda.Crear`, `agendaAtencion.ObtenerAsync(hoy).TramosVigentes(fecha de la Cita)`.
- `.../Agenda/Infrastructure/TextoDeEnumeraciones.cs`: `Motivo(MotivoDeCancelacion)` da el texto de la columna, que es el mismo que lleva el evento.
- `.../Agenda/Contracts/CitaReprogramada.cs` y `ReprogramarCitaPorEnlace.cs`: el modelo de evento y de comando. `CodigosDeAgenda` está en `AgendarCitaEnLinea.cs`.
- `.../Agenda/Infrastructure/Registro.cs`: registrar el handler.
- `.../UI/Pages/Gestion/TuCita.razor`: el bloque `case EstadoDelEnlace.Agendada`, donde va el botón.
- `.../UI/Pages/Gestion/Reprogramar.razor`: el patrón de página con `Vista`, el POST en SSR (formulario vacío mientras procesa) y el 404 con `Response.OnStarting`.
- `.../UI/Pages/Gestion/PanelDentroDelUmbral.razor`: recibe `ChildContent` para el texto de tardía.
- `_bmad-output/initiative-draselina/ux-appointment-booking/imports/mockup-canvas/Cancelar.dc.html`: la pantalla tardía. `Gestionar.dc.html`: el botón "Cancelar cita".
- Pruebas modelo: `tests/DraSelina.Consultorio.Domain.Tests/CitaTests.cs`, `tests/DraSelina.Consultorio.IntegrationTests/ReprogramarCitaTests.cs` (handler, bitácora, outbox, concurrencia) y `ReprogramarDesdeTuCitaTests.cs` (HTTP, antiforgery, 404).

## Tasks & Acceptance

**Execution:**
- [x] `.../Agenda/Domain/Cita.cs`: agregar `RechazoAlCancelar? Cancelar(DateTimeOffset now, ReglasDeAgenda reglas)` y su enum (`CitaNoAgendada`, `YaEmpezo`).
- [x] `.../Agenda/Contracts/CancelarCitaPorEnlace.cs` (nuevo): el comando y `CitaCanceladaPorEnlace`.
- [x] `.../Agenda/Contracts/CitaCancelada.cs` (nuevo): el evento, con las constantes de motivo.
- [x] `.../Agenda/Features/CancelarCitaPorEnlaceHandler.cs` (nuevo), y registrarlo en `Infrastructure/Registro.cs`.
- [x] `.../UI/Pages/Gestion/TuCita.razor`: el botón "Cancelar cita".
- [x] `.../UI/Pages/Gestion/Cancelar.razor` (nuevo, `@page "/c/{Token}/cancelar"`) y `CitaCancelada.razor`. El nombre del componente choca con el evento: usar `CitaCanceladaVista` o el nombre completo, como en la 3.3.
- [x] `wwwroot/css/consultorio.css`: el botón de contorno rojo y lo que falte.
- [x] `tests/.../Domain.Tests/CitaTests.cs`: normal, en el umbral, tardía, ya empezó, no Agendada, campos de cancelación.
- [x] `tests/.../IntegrationTests/CancelarCitaTests.cs` (nuevo): la matriz completa por el handler y por HTTP, incluida la concurrencia con reprogramar y que el token no aparezca en registros.

**Acceptance Criteria:**
- Given una Cita cancelada desde su enlace, when se abre Tu cita, then se ve Cancelada con "Agendar otra cita" (3.1).
- Given una cancelación rechazada o repetida, when se lee la base, then la bitácora y el outbox no tienen filas nuevas.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 1, low 1, false 0, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Un POST sobre una Cita ya cancelada por el Consultorio se muestra como si la hubiera cancelado el Paciente | medium | patch | Real: el resultado solo lleva `Tardia`, que es falso para `Consultorio`; la página pinta "Tu cita está cancelada" con "Agendar otra cita", mientras Tu cita muestra la disculpa. El plan pide mostrar el motivo guardado. Arreglo: `PorElConsultorio` en el resultado y la misma frase de Tu cita. |
| El handler duplica el texto del motivo de `TextoDeEnumeraciones.Motivo` | low | patch | Real: dos mapeos sin nada que los ate; si cambia el texto de la columna, el evento y la bitácora se desalinean sin que falle ninguna prueba (E-16). Arreglo directo: usar las constantes de `CitaCancelada` y una prueba que compare los dos. |

## Verification

**Commands:**
- `dotnet build`: debe compilar sin advertencias.
- `dotnet test tests/DraSelina.Consultorio.Domain.Tests --filter "FullyQualifiedName~CitaTests"`: deben pasar.
- `dotnet test tests/DraSelina.Consultorio.IntegrationTests --filter "FullyQualifiedName~CancelarCitaTests|FullyQualifiedName~TuCitaTests"`: deben pasar.
- `dotnet test`: debe pasar la suite completa.
