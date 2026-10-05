---
type: epic
title: "El administrador opera las citas desde una sola pantalla"
parent: initiative-draselina
covers: [CAP-9, CAP-12, CAP-13, CAP-14, CAP-15, CAP-17]
after: []
assignee: ""
risk: high
---

# El administrador opera las citas desde una sola pantalla

## Description

Gerardo entra con segundo factor, ve la agenda del día y de la semana, captura las Citas pactadas por WhatsApp (incluso fuera de horario), marca ausencias, cancela a nombre del Consultorio y consulta Pacientes con su historial y la bitácora de cada Cita.

## Outcome

Las excepciones se resuelven en un solo lugar; la señal es SM-C1 y SM-C3 del PRD.

## Requirements

Se completan al dividir esta épica. La fuente son las capacidades de `covers` en el spec, con las consecuencias verificables de sus `FR-n` en el PRD.

## Done when

1. Sin sesión con segundo factor no responde ninguna página, comando ni consulta de administración; no existe registro público.
2. UJ-4 y la primera mitad de UJ-6 completos en local: Cita manual reconociendo al Paciente, y Cita en sábado con nota obligatoria que el público no ve.
3. Una Cita reservada en línea aparece en la agenda sin recargar a mano; se distinguen Atendida, "sin correo" y fuera de horario.
4. No se presentó solo se ofrece desde el inicio de la Cita; cancelar por el Consultorio, solo antes, y el Paciente recibe la disculpa.
5. La lista de Pacientes busca por nombre y su historial distingue Atendida, No se presentó, Cancelada y Cancelación tardía; el detalle de una Cita muestra su bitácora.

## Boundaries

El acceso (ASP.NET Core Identity en el host) y el marco de administración, la Agenda, el Detalle de Cita, Agendar cita y Pacientes. De CAP-17 entrega las entradas de sus cambios y la vista de la bitácora. Las páginas de administración no son indexables (parte de CAP-21 que le toca). No incluye el Calendario de atención ni Configuración (épica 6).

## References

- parent — _bmad-output/initiative-draselina/initiative-draselina.md
- spec — _bmad-output/initiative-draselina/spec-appointment-booking/spec-appointment-booking.md, sección Capabilities
- trazabilidad — _bmad-output/initiative-draselina/spec-appointment-booking/trazabilidad.md
- architecture — _bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md, AD-7 a AD-11, AD-16, AD-18, AD-22, AD-24, AD-25
- domain — _bmad-output/initiative-draselina/architecture-appointment-booking/ddd-and-data-model-appointment-booking.md, sección 2.2, RN-13, RN-14, RN-19, RN-29, RN-31
- design — _bmad-output/initiative-draselina/ux-appointment-booking/EXPERIENCE.md, Administración y State Patterns (Administrador)

## Notes

- Decision: "terminado" significa que corre en local con el AppHost y pasan sus pruebas de integración contra PostgreSQL real; no hay producción todavía (Gerardo, 2026-10-05).
- Waits on epic-plataforma-base because: necesita el marcador de administración y el servicio de actor.
- Waits on epic-reserva-en-linea because: necesita la Cita, Pacientes y la bitácora.
- Waits on epic-gestion-por-el-paciente because: necesita `CitaCancelada` con su motivo y la marca de Cancelación tardía que distingue el historial.
- Waits on epic-notificaciones because: necesita `ObtenerContactos` y la disculpa por cancelación del Consultorio para comprobar FR-17.
- Assumption: toda la administración se construye sin mockup, desde las tablas de `EXPERIENCE.md`.
- Open question: quién arma el historial de Citas de FR-18, dado que Pacientes no conoce citas (RN-29); se resuelve al dividir esta épica. AD-10 apunta a una consulta de Agenda que pide los contactos a Pacientes.
- Open question: qué pantallas de administración necesitan mockup antes de construirse (spec, Open Questions).
