---
type: epic
title: "Un paciente reserva su cita en línea"
parent: initiative-draselina
covers: [CAP-1, CAP-2, CAP-3, CAP-4, CAP-5, CAP-17]
after: []
assignee: ""
risk: high
---

# Un paciente reserva su cita en línea

## Description

Cualquier persona reserva una Cita desde el teléfono, sin cuenta, y la ve confirmada al instante con su Enlace de gestión y la opción de agregarla a su calendario. Es el camino trazador de la iniciativa: atraviesa interfaz pública, Agenda, Pacientes, Contenido y la base de datos.

## Outcome

El Paciente aparta un horario seguro en menos de un minuto sin esperar a nadie; la señal es SM-2 y SM-3 del PRD.

## Requirements

Se completan al dividir esta épica. La fuente son las capacidades de `covers` en el spec, con las consecuencias verificables de sus `FR-n` en el PRD.

## Done when

1. UJ-1 completo en local desde un teléfono: tipo, día y hora, datos, confirmación con enlace y archivo de calendario.
2. Dos confirmaciones simultáneas del mismo horario producen una sola Cita y la otra persona ve los Horarios libres restantes; confirmar dos veces el mismo formulario devuelve la misma Cita.
3. Los Horarios libres salen del cálculo sobre la Agenda base sembrada; ninguna hora ni día está escrito en código.
4. Mismo teléfono y nombre reconoce al Paciente; el tope semanal y la verificación anti-bots rechazan lo que deben.
5. Cada Cita creada deja su entrada de bitácora y su evento `CitaAgendada` en la misma transacción, sin datos de contacto ni motivo.

## Boundaries

La reserva pública y lo que necesita por primera vez: `Cita.Agendar`, `AgendaAtencion` para leer tramos vigentes, con las tablas de base y de custom y la base inicial sembrada (INV-V1 completa, aunque todavía nadie pueda registrar una custom); `ReglasDeReserva` y `TipoDeVisita` creados y sembrados, sin pantalla de edición; `CalculadoraDeDisponibilidad`; Pacientes con `BuscarOCrear`; el mecanismo de bitácora; y Contenido con los datos del consultorio y la página del Aviso de privacidad con su versión. De CAP-17 entrega el mecanismo y las entradas de reserva. No incluye cambiar la base, las custom, las reglas ni los tipos (épica 6), ver ni cambiar la Cita con el enlace (épica 3), ni enviar correos (épica 4).

## References

- parent — _bmad-output/initiative-draselina/initiative-draselina.md
- spec — _bmad-output/initiative-draselina/spec-appointment-booking/spec-appointment-booking.md, sección Capabilities
- trazabilidad — _bmad-output/initiative-draselina/spec-appointment-booking/trazabilidad.md
- architecture — _bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md, AD-5, AD-7, AD-8, AD-11, AD-13, AD-18, AD-19, AD-24, AD-26, AD-27, AD-28
- domain — _bmad-output/initiative-draselina/architecture-appointment-booking/ddd-and-data-model-appointment-booking.md, secciones 2.2 y 2.3
- design — _bmad-output/initiative-draselina/ux-appointment-booking/EXPERIENCE.md, Reserva 1 a 3 y Cita confirmada; mockups en _bmad-output/initiative-draselina/ux-appointment-booking/imports/mockup-canvas/

## Notes

- Decision: "terminado" significa que corre en local con el AppHost y pasan sus pruebas de integración contra PostgreSQL real; no hay producción todavía (Gerardo, 2026-10-05).
- Waits on epic-plataforma-base because: necesita la solución, el despacho de casos de uso, las migraciones y la base visual.
- Open question: el texto del Aviso de privacidad lo revisa un abogado antes del primer Paciente; aquí se entrega la página y su versión con el texto disponible.
- Open question: qué límite de frecuencia se usa (spec, Open Questions).
- Open question: si hace falta `btree_gist` con la restricción sobre un solo rango; se comprueba en la primera migración de Agenda.
- Open question: siete columnas o solo días con atención en el calendario de escritorio (spec, Open Questions).
- Source conflict: arquitectura, Stack — "Anti-bots: Cloudflare Turnstile" (y el addendum del PRD, FR-7) vs épica 1, entradas 1.8 y 8 — el sitio no carga scripts y ninguna visita pide nada a un CDN. Turnstile necesita cargar un script de Cloudflare en la página.
- Decision: la elección entre Turnstile (con una excepción a "sin scripts ni CDN" solo en Tus datos) y una defensa sin scripts de terceros (campo trampa, tiempo mínimo de envío y límite de frecuencia) queda como pregunta abierta de la entrada 11, junto con el valor del límite de frecuencia (Gerardo, 2026-11-02).
- Decision: desglose en trece entradas aprobado (Gerardo, 2026-11-02). Bala trazadora: la entrada 1, de Tus datos a Cita confirmada por Agenda, Pacientes, Contenido y PostgreSQL.
- Decision: dos carriles después de la entrada 1. Disponibilidad: 2 → 3 → 5. Manejador de la reserva: 4 → 6 → 7 → 8 → 9 → 10, y después 11 y 12, porque todas cambian AgendarCitaEnLinea o la página Tus datos. La 4 espera a la 3 para que la rejilla tenga una sola implementación (T-21); la 11 espera a la 8 porque la verificación anti-bots va en el mismo formulario (2026-11-02).
- Decision: el recorrido de UJ-1 desde un teléfono y desde el escritorio (Done when 1) lo verifica Gerardo en la entrada 12 (2026-11-02).
