---
title: 'Refactor sweep'
type: 'refactor'
ticket: '6'
created: '2026-11-08'
status: done
baseline_revision: 'c11e1469beaf65c924cbb98440d033ce5597d15f'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Las páginas del Enlace de gestión que dejó la épica 3 repiten piezas. El 404 de Enlace no válido y el meta robots están tres veces (Tu cita, Reprogramar, Cancelar), y el enlace de WhatsApp está dos veces. Además, los componentes `CitaReprogramada` y `CitaCancelada` se llaman igual que los eventos de integración, y eso obliga a usar nombres completos.

**Approach:** Limpieza sin cambio de comportamiento, con el alcance que fijó Gerardo el 2026-11-08:
- `EnlaceNoValido` pone su propio 404.
- Un componente `SinIndice` pone el meta robots.
- Un componente pinta el enlace de WhatsApp, y lo usan `MensajeDeWhatsApp` y Tu cita.
- Los dos componentes se renombran a `ConfirmacionDeReprogramacion` y `ConfirmacionDeCancelacion`.

Quedan fuera los hallazgos rechazados en las revisiones y `deferred-work.md`. Las pruebas de la épica siguen pasando sin cambiar lo que comprueban.

</frozen-after-approval>

## Implementation Notes

Ruta oneshot: es limpieza mecánica en nueve archivos de UI y dos de pruebas (solo nombres).

- Sorpresa: al mover el 404 a `EnlaceNoValido`, 22 pruebas dieron 404 en Citas válidas. Blazor pinta una vez mientras `OnParametersSetAsync` espera la consulta, y en ese primer pintado la página mostraba Enlace no válido, que ya registraba el 404. Se agregó un estado de carga que no pinta nada: `_cargada` en Tu cita, y `Vista.Cargando` como primer valor en Reprogramar y Cancelar. Con eso, las 610 pruebas pasan.
- `EnlaceDeWhatsApp` es el único que arma `wa.me/52…`; `MensajeDeWhatsApp` y Tu cita lo usan, y sus helpers estáticos públicos desaparecieron.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 0, low 4, false 0, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| `EnlaceNoValido` pone un segundo `<SinIndice />` y su comentario dice que pone el meta robots | low | patch | Real: las tres páginas ya lo ponen; se quitó el duplicado y se corrigió el comentario. |
| La segunda línea de atributos quedó desalineada tras renombrar | low | patch | Real y cosmético; se realineó. |
| El alias de prueba conserva el nombre viejo `VistaCitaCancelada` | low | patch | Real; se renombró a `VistaDeConfirmacion`. |
| Las notas decían ocho archivos de UI y son nueve | low | patch | Real; se corrigió la nota. |

## Verification

**Commands:**
- `dotnet build`: debe compilar sin advertencias.
- `dotnet test`: debe pasar la suite completa.
