---
type: epic
title: "El paciente ve, reprograma y cancela con su enlace"
parent: initiative-draselina
covers: [CAP-6, CAP-7, CAP-8, CAP-17]
after: []
assignee: ""
risk: medium
---

# El paciente ve, reprograma y cancela con su enlace

## Description

Con el Enlace de gestión, sin cuenta, el Paciente ve su Cita, la mueve a otro Horario libre o la cancela. Cancelar siempre es posible; dentro del umbral de cancelación tardía ya no se reprograma.

## Outcome

El Paciente cambia o cancela sin escribirle a Selina; la señal es SM-1 del PRD.

## Requirements

Se completan al dividir esta épica. La fuente son las capacidades de `covers` en el spec, con las consecuencias verificables de sus `FR-n` en el PRD.

## Done when

1. UJ-2 completo en local: reprogramar tres días antes y, otro día, cancelar a una hora de la Cita.
2. Reprogramar conserva identificador y enlace, y respeta las mismas reglas que una reserva nueva, sin tope semanal.
3. Dentro del umbral no se ofrece reprogramar; la cancelación queda marcada como tardía y el horario vuelve a ofrecerse.
4. El enlace muestra solo esa Cita y deja de valer cuando su inicio ya pasó.
5. Cada cambio deja bitácora con actor Paciente y publica `CitaReprogramada` o `CitaCancelada`.

## Boundaries

Las superficies Tu cita, Cancelar cita, Cita cancelada, Cita reprogramada y Enlace no válido, y los métodos `Reprogramar` y `Cancelar` de la Cita. De CAP-17 entrega las entradas de reprogramación y cancelación. Las páginas del Enlace de gestión no son indexables (parte de CAP-21 que le toca). No incluye los correos de aviso (épica 4) ni la cancelación por el Consultorio (épica 5).

## References

- parent — _bmad-output/initiative-draselina/initiative-draselina.md
- spec — _bmad-output/initiative-draselina/spec-appointment-booking/spec-appointment-booking.md, sección Capabilities
- trazabilidad — _bmad-output/initiative-draselina/spec-appointment-booking/trazabilidad.md
- architecture — _bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md, AD-7, AD-9, AD-19, AD-22, AD-23, AD-26
- domain — _bmad-output/initiative-draselina/architecture-appointment-booking/ddd-and-data-model-appointment-booking.md, sección 2.2, RN-15 a RN-17, RN-22, RN-31
- design — _bmad-output/initiative-draselina/ux-appointment-booking/EXPERIENCE.md, Tu cita y Cancelar cita

## Notes

- Decision: "terminado" significa que corre en local con el AppHost y pasan sus pruebas de integración contra PostgreSQL real; no hay producción todavía (Gerardo, 2026-10-05).
- Waits on epic-reserva-en-linea because: necesita la Cita, el Enlace de gestión y la superficie Día y hora.
