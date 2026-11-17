---
title: 'Refactor sweep'
type: 'refactor'
ticket: '10'
created: '2026-11-17'
status: 'built'
baseline_revision: '1500f1c8a85e3d45cbf9aff3b1dd6f0cbc54472f'
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

**Problem:** Quedaron tres pendientes de la épica: `HorariosLibresHandler` lee la agenda por el puerto de escritura; dos guardados de la agenda de atención a la vez pueden deshacer uno al otro (riesgo de la 6.4); y el error de Tramos encimados del Calendario no dice cuáles se enciman.

**Approach:** `HorariosLibresHandler` lee por el puerto de lectura `IAgendaDelRango`; los tres writers de la agenda de atención se serializan; el dominio dice qué Tramos chocan y el error va bajo esas filas. Lo resuelto se marca en `deferred-work.md`.

## Boundaries & Constraints

**Always:**
- `HorariosLibres` lee la agenda solo para las fechas de la ventana (`IAgendaDelRango.LeerAsync(PrimeraFecha, UltimaFecha)`); mismos horarios que hoy.
- Concurrencia: un candado global de la agenda de atención (`pg_advisory_xact_lock` con otra clase de llave, dentro de la transacción): lo toman cambiar la base, guardar y borrar una custom antes de leer la agenda; las custom, después de su candado de fecha. Las reservas no lo toman y no esperan más que hoy (Gerardo, 2026-11-17).
- Encimados: el dominio expone qué pares de Tramos se enciman (índices en el orden recibido) con la regla de `Tramo.SeEncimaCon`; `Validar` la usa. `GuardarAgendaCustom` responde `Invalido` con un error bajo cada Tramo del par: "Se encima con el tramo N." `[ASSUMPTION: texto]`; la página lo pinta bajo su fila con `aria-invalid`. La semana tipo no cambia (ya marca el día).
- `deferred-work.md`: los puntos resueltos llevan "**Resuelto (fecha, entrada, commit):**" como el de la 4.3 — el de `HorariosLibresHandler` y el del sábado en `/reservar` (6.9).

**Never:**
- Cambiar la regla de qué es encimarse, los horarios que se ofrecen, ni el candado de fecha de las reservas (AD-26).
- Los demás puntos de `deferred-work.md` ni los hallazgos `low` rechazados de las revisiones (siguen rechazados).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Horarios libres | Ventana de 2 meses, custom el día 10 y otra fuera de la ventana | Mismos días y horas que antes | — |
| Encimados en una custom | Tramos 15:30–18:00 y 15:45–16:30 | Nada cambia | Bajo el tramo 1: "Se encima con el tramo 2."; bajo el 2: "Se encima con el tramo 1." |
| Tres tramos, dos encimados | 10–11, 12–14, 13–15 | Nada cambia | Error bajo el 2 y el 3, no bajo el 1 |
| Base y custom a la vez | Cambiar la base mientras se guarda una custom | Los dos cambios quedan | — |
| Dos custom de fechas distintas a la vez | Guardar el 21 y el 24 al mismo tiempo | Las dos quedan | — |

</frozen-after-approval>

## Code Map

- `Modules/Agenda/Features/HorariosLibresHandler.cs:17,35` -- cambia `IAgendaAtencion` por `IAgendaDelRango`, con la ventana de `CalculadoraDeDisponibilidad.Ventana`.
- `Modules/Agenda/Features/IAgendaDelRango.cs` -- su doc dice "para la Agenda del Administrador": agregar horarios libres.
- `Modules/Agenda/Infrastructure/AgendaAtencionPersistida.cs:30` -- `GuardarAsync` sincroniza la base completa (`SincronizarBaseAsync`) y todas las custom desde hoy (`SincronizarCustomsAsync` borra las filas que no están en el agregado): con dos guardados a la vez, el último deshace al otro, también entre dos custom de fechas distintas (sus candados de fecha no chocan).
- Writers: `CambiarAgendaBaseHandler.cs:38-69` (sin candado), `GuardarAgendaCustomHandler.cs:53-92` y `BorrarAgendaCustomHandler.cs:45-66` (candado de fecha, luego leen).
- `Modules/Agenda/Infrastructure/CandadoDeFecha.cs` -- `pg_advisory_xact_lock(clase, día)`; patrón para un candado nuevo.
- `Modules/Agenda/Domain/AgendaAtencion.cs:142` -- `Validar`; `Tramo.SeEncimaCon` (`Domain/Tramo.cs:39`).
- `Modules/Agenda/Features/GuardarAgendaCustomHandler.cs:72,152` -- hoy `Fallo(TramosEncimados)`.
- `UI/Pages/Administracion/CalendarioDeAtencion.razor:600` -- mensaje general; `ErroresDe(PropiedadDelTramo(i))` ya pinta bajo cada fila (l. ~150).
- Pruebas: `AgendaAtencionTests` (95, 106, 215), `GuardarAgendaCustomTests:175`, `CalendarioDeAtencionTests:168`, `HorariosLibres` (integración), y una de concurrencia con dos ámbitos.

## Tasks & Acceptance

**Execution:**
- [ ] `HorariosLibresHandler.cs`, `IAgendaDelRango.cs` -- leer por el puerto de lectura.
- [ ] `Features/ICandadoDeLaAgendaDeAtencion.cs`, `Infrastructure/CandadoDeLaAgendaDeAtencion.cs` (+ `Registro.cs`), los tres handlers -- el candado global, con su prueba de integración de dos guardados a la vez.
- [ ] `AgendaAtencion.cs` -- `Encimados(tramos)`; `Validar` la usa.
- [ ] `GuardarAgendaCustomHandler.cs`, `GuardarAgendaCustom.cs` -- `Invalido` bajo cada Tramo del par, con su mensaje.
- [ ] `CalendarioDeAtencion.razor` -- quitar el caso `TramosEncimados` del mensaje general si ya no llega.
- [ ] `deferred-work.md` -- marcar los dos resueltos.
- [ ] Pruebas de cada fila de la matriz.

**Acceptance Criteria:**
- Given la suite completa, when corre, then pasa sin advertencias.

## Implementation Notes

- Implementado y revisado en esta sesión (el entorno niega los subagentes; misma decisión que en las entradas anteriores).
- `HorariosLibresHandler` lee con `IAgendaDelRango.LeerAsync(PrimeraFecha, UltimaFecha)` de la ventana; la calculadora solo pregunta por esas fechas.
- `ICandadoDeLaAgendaDeAtencion` / `CandadoDeLaAgendaDeAtencion` (`pg_advisory_xact_lock(0x41474154 "AGAT", 0)`). Lo toman `CambiarAgendaBaseHandler` (al abrir la transacción), y `GuardarAgendaCustomHandler` y `BorrarAgendaCustomHandler` después de su candado de fecha, siempre antes de leer. Sin orden inverso en ningún camino: no hay bloqueo mutuo con las reservas (solo fecha) ni con P-1.
- `AgendaAtencion.Encimados(tramos)` da los pares por posición; `Validar` la usa. `GuardarAgendaCustomHandler` responde `Invalido` bajo cada Tramo del par con `GuardarAgendaCustom.MensajeDeEncimado(n)`; se quitaron `CodigosDeAgenda.TramosEncimados` y el mensaje general de la página, que ya no llegan.
- `CandadoDeLaAgendaDeAtencionTests` (3): otra transacción toma el candado y cambia la agenda sin confirmar; el handler espera y no deshace el cambio. Sin el candado en los handlers las tres fallan (comprobado y regresado).
- `deferred-work.md`: marcados como resueltos el del puerto (6.10) y el del sábado en `/reservar` (6.9).
- Suite completa: 1,130 pasan.

## Plan Change Log

## Review Triage Log

**Pasada 1 (lente Quick, en esta sesión): 0 high, 0 medium, 0 low, 0 false, 0 maybe-false.**

Revisado: orden de candados (custom: fecha → agenda; base: agenda; reservas y P-1: solo fecha; sin ciclo), fechas que pide la calculadora contra las que lee `IAgendaDelRango`, y que `Encimados` aplica la misma regla que el rechazo (todo par, no solo vecinos ordenados: mismo resultado). Sin hallazgos.

## Verification

**Commands:**
- `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"; dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.
