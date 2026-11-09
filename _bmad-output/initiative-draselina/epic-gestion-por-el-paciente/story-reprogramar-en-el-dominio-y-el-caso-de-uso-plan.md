---
title: 'Reprogramar en el dominio y el caso de uso'
type: 'feature'
ticket: '2'
created: '2026-11-06'
status: done
baseline_revision: 'fcc4b6c112c093f03b3f6702e851c64ac1c7001d'
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

**Problem:** La Cita solo sabe agendarse. No hay forma de moverla a otro Horario libre conservando su identidad y su enlace (CAP-7, FR-9, AD-7, RN-15).

**Approach:** Agregar `cita.Reprogramar(nuevoRango, reglas, now)` al agregado y un comando `ReprogramarCitaPorEnlace(token, fecha, hora)`. El comando toma el candado de la fecha nueva, valida con las mismas reglas que una reserva sin tope semanal, guarda bajo la restricción de exclusión y deja bitácora y `CitaReprogramada` en la misma transacción. La entrada 3.3 lo envía.

## Boundaries & Constraints

**Always:**
- `cita.Reprogramar` solo procede si la Cita está `Agendada` y faltan al menos `UmbralCancelacionTardiaMin` minutos para su inicio actual (RN-17).
- Sobre el rango nuevo exige INV-C4 (no en el pasado) e INV-C5 (cabe en un tramo vigente, empieza en la rejilla, dentro de la ventana). Reutiliza `CalculadoraDeDisponibilidad.Ventana` y `EstaEnLaRejilla` (T-21).
- No aplica el tope semanal (RN-15).
- Conserva `Id`, `TokenHash`, `Origen`, `SolicitudId`, `Consentimiento` y `MotivoDeConsulta`.
- El fin nuevo es el inicio nuevo más la duración del tipo de visita en ese momento (INV-C3).
- No revisa si el tipo se agenda en línea: la tabla de `cita.Reprogramar` en el dominio no lo pide. `[ASSUMPTION]`
- El handler:
  - busca solo por el hash del token (`EnlaceDeGestion.HashDelToken`; si es nulo, no consulta la base);
  - abre la transacción y toma `ICandadoDeFecha` de la fecha local nueva antes de leer tramos, reglas y la Cita (AD-26, RN-20);
  - arma `ReglasDeAgenda` con los tramos vigentes de la fecha nueva.
- Un token sin Cita, o de una Cita cuyo inicio actual ya pasó, devuelve `CitaNoEncontrada` (RN-22).
- Si la Cita ya está `Agendada` exactamente en el inicio pedido, se devuelve éxito sin cambiar nada y sin bitácora ni evento (Decision del 2026-11-06). Esta comprobación va antes del umbral.
- Un traslape al guardar devuelve `HorarioOcupado` y deshace todo (T-22).
- En la misma transacción que el cambio:
  - bitácora `Reprogramada`, con el actor de `IActorActual`, `actor_id` nulo y en el detalle inicio y fin anteriores y nuevos;
  - `CitaReprogramada(EventoId, OcurridoEn, CitaId, PatientId, Actor, InicioAnterior, InicioNuevo, FinNuevo)` por el outbox, con clave de orden `CitaId`.
- El token no va a registros: `ToString` del comando lo oculta (AD-23). Ni la bitácora ni el evento llevan nombre, contacto, motivo ni enlace.
- Decision (Gerardo, 2026-11-06): la Cita se lee con `SELECT … FOR UPDATE` dentro de la transacción, siempre después del candado de fecha. Un segundo cambio a la misma Cita espera al primero y ve su estado ya cambiado; la 3.4 usa la misma lectura. El orden fijo (fecha y luego fila) evita el bloqueo mutuo.
- Decision (Gerardo, 2026-11-06): el plan se conserva completo aunque pasa de 1,600 tokens.
- Códigos de rechazo nuevos en `CodigosDeAgenda`:
  - `agenda.cita_no_agendada`: la Cita está cancelada o no se presentó;
  - `agenda.reprogramacion_cerrada`: ya falta menos que el umbral.
- Se reutilizan los códigos `EnElPasado`, `FueraDeHorario`, `FueraDeVentana` y `HorarioOcupado`.

**Never:**
- Pantallas, ni cambios a `HorariosLibres` (entrada 3.3).
- Cancelar (entrada 3.4).
- Crear otra Cita, o cancelar la anterior (AD-7).
- Un `SolicitudId` para el reenvío (Decision del 2026-11-06).
- Tocar `AgendarCitaEnLinea` más allá de compartir piezas.

## I/O & Edge-Case Matrix

Con el reloj en el lunes 2 de noviembre de 2026 a las 18:00 UTC, y la Cita Agendada el martes 3 a las 16:00 local (30 min).

| Escenario | Pedido | Resultado |
|---|---|---|
| Mover a otro horario libre | jueves 5, 17:00 | éxito; mismo CitaId y token_hash; rango 17:00 a 17:30; una bitácora `Reprogramada` con actor Paciente; un `CitaReprogramada` pendiente |
| Tres Citas del mismo teléfono en la semana | mover una al viernes 6, 17:00 | éxito: no hay tope semanal |
| Dentro del umbral | reloj a menos de 120 min del inicio actual | `reprogramacion_cerrada`, sin cambios |
| Fuera de rejilla | jueves 5, 16:15 | `fuera_de_horario` |
| Sin tramos | sábado sin custom | `fuera_de_horario` |
| Fuera de ventana | hoy, o más allá de la ventana | `fuera_de_ventana` |
| Horario ocupado | jueves 5, 17:00, ocupado por otra Cita | `horario_ocupado`, sin cambios ni bitácora ni evento |
| Misma hora otra vez | martes 3, 16:00, o un segundo envío idéntico | éxito, sin bitácora ni evento nuevos |
| Cita cancelada | cualquier hora | `cita_no_agendada` |
| Token inexistente, mal formado o Cita ya empezada | cualquier hora | `cita_no_encontrada` |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs`: `Agendar` es el modelo (rechazo como enum, `HoraLocal.DesdeUtc`, `CalculadoraDeDisponibilidad.Ventana/EstaEnLaRejilla`). `Rango` tiene setter privado. Agregar `Reprogramar` y el enum `RechazoAlReprogramar` junto a `RechazoAlAgendar`.
- `.../Agenda/Domain/ReglasDeAgenda.cs`: ya trae `UmbralCancelacionTardiaMin` y `TramosVigentes`.
- `.../Agenda/Domain/ICitas.cs` y `Infrastructure/Citas.cs`: agregar la lectura con rastreo y bloqueo de fila por `token_hash`. `GuardarAsync` ya traduce la exclusión (23P01) a `HorarioOcupado`, también en un UPDATE.
- `.../Agenda/Infrastructure/AgendaDbContext.cs`: `Rango` es un `ComplexProperty` (columnas `inicio` y `fin`). Reemplazar el `RangoDeTiempo` marca ambas columnas como cambiadas. No hay token de concurrencia (`xmin`).
- `.../Agenda/Features/AgendarCitaEnLineaHandler.cs`: el patrón de transacción, candado, reglas, `agendaAtencion.ObtenerAsync(hoy).TramosVigentes(fecha)`, `DejarRastroAsync` (bitácora y outbox) y `Codigo(rechazo)`. No se modifica.
- `.../Agenda/Features/IBitacoraDeAgenda.cs`: `AccionDeBitacora.Reprogramada` ya existe.
- `.../Agenda/Contracts/CitaAgendada.cs`: el modelo para `CitaReprogramada` (actor como texto con `JsonStringEnumConverter`).
- `.../Agenda/Contracts/AgendarCitaEnLinea.cs`: el modelo de comando (propiedades anulables con `[Required]` y mensajes) y `CodigosDeAgenda`.
- `.../Agenda/Contracts/CitaPorEnlace.cs`: cómo se oculta el token en `ToString`.
- `.../Agenda/Infrastructure/Registro.cs`: registrar el handler.
- `tests/DraSelina.Consultorio.Domain.Tests/CitaTests.cs`: pruebas de dominio.
- `tests/DraSelina.Consultorio.IntegrationTests/ReservaEnLineaTests.cs`: `Escenario` (`AgendarAsync`, `AgendarTresAsync`, `Reloj`, `EjecutarAsync`, `EscalarAsync`, `SinSesion`, `Ahora`) y `BitacoraYCitaAgendadaTests.cs` para leer bitácora y outbox.

## Tasks & Acceptance

**Execution:**
- [x] `.../Agenda/Domain/Cita.cs`: agregar `RechazoAlReprogramar? Reprogramar(RangoDeTiempo nuevoRango, ReglasDeAgenda reglas, DateTimeOffset now)`, que cambia `Rango` solo si no hay rechazo. Lanza si los tramos no son de la fecha local del nuevo inicio.
- [x] `.../Agenda/Domain/ICitas.cs`, `Infrastructure/Citas.cs`: agregar `ObtenerPorTokenHashParaCambiarAsync(byte[], ct)`, con rastreo y `FOR UPDATE` (por ejemplo, `FromSql` sobre `agenda.cita`). Exige que haya una transacción abierta.
- [x] `.../Agenda/Contracts/ReprogramarCitaPorEnlace.cs` (nuevo):
  - el comando con `Token`, `Fecha` y `Hora` requeridos y `ToString` que oculta el token;
  - el resultado `CitaReprogramadaPorEnlace(Fecha, Hora)`;
  - los dos códigos nuevos en `CodigosDeAgenda`.
- [x] `.../Agenda/Contracts/CitaReprogramada.cs` (nuevo): el evento de integración.
- [x] `.../Agenda/Features/ReprogramarCitaPorEnlaceHandler.cs` (nuevo): el caso de uso según Boundaries.
- [x] `.../Agenda/Infrastructure/Registro.cs`: registrar el handler.
- [x] `tests/.../Domain.Tests/CitaTests.cs`: las filas de dominio de la matriz (umbral en el límite exacto de 120 min y un minuto menos, rejilla, sin tramos, ventana, no Agendada, conserva id y token_hash).
- [x] `tests/.../IntegrationTests/ReprogramarCitaTests.cs` (nuevo): todas las filas de la matriz a través del handler, con bitácora y outbox leídos de la base, una prueba de que el token no aparece en registros, y una prueba de concurrencia: dos reprogramaciones simultáneas de la misma Cita a fechas distintas dejan exactamente una bitácora y un evento por cada cambio aplicado, y la segunda parte del estado que dejó la primera.

**Acceptance Criteria:**
- Given una Cita reprogramada, when se abre su Enlace de gestión, then el mismo enlace muestra la nueva fecha en Tu cita.
- Given una reprogramación que falla por cualquier rechazo, when se lee la base, then la Cita, la bitácora y el outbox quedan como antes.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 0, low 3, false 0, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| El comentario de `CodigosDeAgenda.CitaNoEncontrada` dice que cubre "no está Agendada" | low | patch | Real: el handler de reprogramar devuelve `CitaNoAgendada` para una Cita cancelada futura; los dos comentarios se contradicen. Se corrige el texto. |
| Las pruebas de Cita cancelada no comprueban el outbox (y la segunda, nada más que el código) | low | patch | Real como hueco frente al segundo criterio. Arreglo directo: comparar inicio, fin y conteos de bitácora y outbox. |
| La prueba de horario ocupado no revisa `fin` y filtra el outbox por clave | low | patch | Real como hueco. El código es correcto (la exclusión aborta y el handler deshace antes del rastro). Arreglo directo en la prueba. |

## Verification

**Commands:**
- `dotnet build`: debe compilar sin advertencias.
- `dotnet test tests/DraSelina.Consultorio.Domain.Tests --filter "FullyQualifiedName~CitaTests"`: deben pasar.
- `dotnet test tests/DraSelina.Consultorio.IntegrationTests --filter "FullyQualifiedName~ReprogramarCitaTests"`: deben pasar.
- `dotnet test`: debe pasar la suite completa.
