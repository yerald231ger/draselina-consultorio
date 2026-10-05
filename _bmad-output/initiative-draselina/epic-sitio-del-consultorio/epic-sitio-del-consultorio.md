---
type: epic
title: "El sitio presenta el consultorio y lleva a agendar"
parent: initiative-draselina
covers: [CAP-21]
after: []
assignee: ""
risk: low
---

# El sitio presenta el consultorio y lleva a agendar

## Description

La página de inicio del sistema es el sitio del Consultorio: presenta a la doctora, sus padecimientos y la ubicación, con "Agendar cita" siempre a la mano, y puede encontrarse en buscadores.

## Outcome

Quien busca la especialidad en la ciudad llega y reserva; la señal es SM-2 del PRD.

## Requirements

Se completan al dividir esta épica. La fuente son las capacidades de `covers` en el spec, con las consecuencias verificables de sus `FR-n` en el PRD.

## Done when

1. Las secciones de FR-27 y el Aviso de privacidad se ven en teléfono y escritorio como en los mockups.
2. "Agendar cita" es visible sin desplazarse a 390 px y lleva al primer paso de la reserva.
3. Los días y horas mostrados salen de la Agenda base, no de texto fijo.
4. Las páginas públicas se leen sin interfaz interactiva y tienen título con especialidad y ciudad; administración y Enlaces de gestión no son indexables.

## Boundaries

El sitio y sus textos en Contenido; enlaza desde el pie el Aviso de privacidad que entregó la épica 2. De CAP-21, el "no indexable" de los Enlaces de gestión y de administración lo entregan las épicas 3 y 5. No incluye editar el contenido desde administración. No se publica hasta cumplir las condiciones de lanzamiento del PRD §6.

## References

- parent — _bmad-output/initiative-draselina/initiative-draselina.md
- spec — _bmad-output/initiative-draselina/spec-appointment-booking/spec-appointment-booking.md, sección Capabilities
- trazabilidad — _bmad-output/initiative-draselina/spec-appointment-booking/trazabilidad.md
- architecture — _bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md, AD-11, AD-15
- design — _bmad-output/initiative-draselina/ux-appointment-booking/EXPERIENCE.md, Sitio · Inicio y su composición; _bmad-output/initiative-draselina/ux-appointment-booking/DESIGN.md; mockups `SitioInicio` y `SitioMovil`

## Notes

- Decision: "terminado" significa que corre en local con el AppHost y pasan sus pruebas de integración contra PostgreSQL real; no hay producción todavía (Gerardo, 2026-10-05).
- Waits on epic-reserva-en-linea because: necesita Contenido con los datos del consultorio, la entrada a la reserva y la lectura de la Agenda base para mostrar los horarios.
- Assumption: una sola página con secciones, en lugar de las páginas separadas de FR-27 (`EXPERIENCE.md`).
- Open question: contenido pendiente de Selina (fotos, semblanza, cédulas, padecimientos, dirección, WhatsApp) y el dato UANL frente a UASLP; sin él la épica termina con textos marcados como pendientes.
