---
title: 'Refactor sweep'
type: 'chore'
ticket: '10'
created: '2026-10-16'
status: done
baseline_revision: '2fb0a1623fb123813ead507c1e441da872b08f71'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
followup_review_recommended: false
context: []
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** La épica deja pendientes anotados en sus planes y revisiones: un diferido de la entrada 5, un mensaje de error y dos líneas de la arquitectura que ya no cuadran con el código, y riesgos de planes viejos que tocan historias futuras sin estar en la épica.

**Approach:** Alcance fijado por Gerardo el 2026-10-16. Corregir:
- el mensaje de `ActorDelHost` y los comentarios que dicen que un trabajo programado declara `Sistema`: lo declara quien crea el ámbito, desde el host;
- en la arquitectura, `IActorActual` en la fila Kernel;
- la convención Naming, que pasa de "un namespace plano por capa" a "un namespace por capa; los sub-namespaces dentro de una capa (`Infrastructure.Migrations`, `UI.Layout`) no tienen reglas propias".

Registrar en las Notes de la épica:
- el diferido de la entrada 5, en la pregunta abierta del contexto en Interactive Server;
- como preguntas abiertas, cuatro riesgos de planes viejos: `/health` sin base, `Actor` sin id de usuario, comando sin valor, y AD-23 en las trazas.

Las pruebas siguen pasando.

</intent-contract>

## Implementation Notes

Ruta oneshot: un mensaje de excepción, tres comentarios y texto de documentos; menos de 30 líneas.

- No se tocó la Parte I del documento de dominio (`ddd-and-data-model-appointment-booking.md`, líneas 172 y 582), que dice que el Kernel tiene "solo ids tipados, `IClock`, `Resultado` y `Actor`": es invariante y solo Gerardo la cambia. Gerardo lo aprobó después de la revisión: se agregó `IActorActual` en las dos líneas y `updated` pasó a 2026-10-16.

## Plan Change Log

## Review Triage Log

### 2026-10-16 — Review pass
- verdicts: 2 findings — high 0, medium 0, low 2, false 0, maybe-false 0
- findings:
  - `[low]` `[patch]` La arquitectura cambió pero su `updated:` seguía en 2026-10-15 y su `.memlog.md` no registraba la decisión, como sí hicieron los cambios anteriores — `updated: '2026-10-16'` y línea `(decision by user)` en el memlog de la arquitectura.
  - `[low]` `[patch]` El diferido de la entrada 5 seguía en su plan sin decir a dónde se movió — su `evidence` suma una línea que apunta a la pregunta abierta de la épica.

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` (tres veces seguidas) -- expected: 128 de 128.

## Auto Run Result

- **Cambio:**
  - El mensaje de `ActorDelHost` (y los comentarios de `Program.cs` y `ActorYAdministracionTests`) dicen que `Sistema` lo declara quien crea el ámbito, desde el host.
  - Arquitectura: `IActorActual` en la fila Kernel y en el árbol; la convención Naming admite sub-namespaces dentro de una capa; `updated` y memlog al día.
  - Épica: el diferido de la entrada 5 queda en la pregunta abierta del contexto en Interactive Server, con una referencia en su plan; cuatro preguntas abiertas nuevas: `/health` sin base, `Actor` sin id de usuario, comando sin valor y AD-23 en las trazas.
- **Archivos:**
  - `src/DraSelina.Consultorio.Web/ActorDelHost.cs`, `Program.cs`, `tests/.../ActorYAdministracionTests.cs` — redacción de `Sistema`.
  - `architecture-appointment-booking.md` y su `.memlog.md` — fila Kernel, Naming, árbol, fecha.
  - `epic-plataforma-base.md` y su `.memlog.md` — decisiones y preguntas abiertas.
  - `story-un-caso-de-uso-…-plan.md` — referencia del diferido.
- **Revisión:** 2 hallazgos low, los 2 corregidos; nada diferido ni rechazado. Revisión de seguimiento recomendada: no.
- **Verificación:** `./dotnet.sh build` 0 advertencias, 0 errores; `./dotnet.sh test` tres veces: 128 de 128. Los parches de la revisión solo tocan documentos.
- **Riesgos:** ninguno pendiente; la Parte I del dominio ya nombra `IActorActual` en el Kernel (aprobado por Gerardo).
