---
type: epic
title: "Los avisos salen solos y nunca frenan la reserva"
parent: initiative-draselina
covers: [CAP-18, CAP-19, CAP-20, CAP-16]
after: []
assignee: ""
risk: medium
---

# Los avisos salen solos y nunca frenan la reserva

## Description

El Paciente con correo recibe confirmación, Recordatorio y avisos de cambio; el Administrador se entera de inmediato de una Cancelación tardía; Selina recibe sus citas del día. Una falla del proveedor de correo no afecta a ninguna reserva.

## Outcome

Selina sabe cada día a quién verá sin abrir nada, y nadie se queda sin aviso por una falla de correo; la señal es SM-1 del PRD.

## Requirements

Se completan al dividir esta épica. La fuente son las capacidades de `covers` en el spec, con las consecuencias verificables de sus `FR-n` en el PRD.

## Done when

1. Cada evento de Agenda produce su correo una sola vez, aunque el evento se procese dos veces.
2. Con el proveedor de correo caído la reserva se completa y el correo sale después, con reintentos.
3. El Recordatorio se programa al agendar, se mueve al reprogramar, se elimina al cancelar y se omite si su hora ya pasó.
4. Una Cancelación tardía alerta al Administrador sin esperar a un proceso periódico.
5. "Tus citas de hoy" sale a la hora configurada en cada fecha con Tramos o con alguna Cita.
6. Las dos horas de envío se cambian sin desplegar.

## Boundaries

El módulo Notificaciones completo: plantillas, canal de correo detrás de `INotificationChannel`, los envíos programados (Recordatorio y Agenda del día, con su servicio en segundo plano) y su propia configuración (la parte de CAP-16 que son las horas de envío y los destinatarios). Entrega `IPacientesApi.ObtenerContactos`, que también usará la épica 5. Incluye la plantilla de disculpa por cancelación del Consultorio, que se prueba de punta a punta en la épica 5. No decide nada del negocio de citas (T-27).

## References

- parent — _bmad-output/initiative-draselina/initiative-draselina.md
- spec — _bmad-output/initiative-draselina/spec-appointment-booking/spec-appointment-booking.md, sección Capabilities
- trazabilidad — _bmad-output/initiative-draselina/spec-appointment-booking/trazabilidad.md
- architecture — _bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md, AD-3, AD-14, AD-19, AD-20, AD-23
- domain — _bmad-output/initiative-draselina/architecture-appointment-booking/ddd-and-data-model-appointment-booking.md, sección 2.4 y esquema `notificaciones`
- design — _bmad-output/initiative-draselina/ux-appointment-booking/EXPERIENCE.md, Mensajes por correo y Voice and Tone

## Notes

- Decision: "terminado" significa que corre en local con el AppHost y pasan sus pruebas de integración contra PostgreSQL real; no hay producción todavía (Gerardo, 2026-10-05).
- Waits on epic-reserva-en-linea because: necesita `CitaAgendada`, `IAgendaApi` (enlace de gestión, archivo de calendario, citas del día) y `IContenidoApi`.
- Waits on epic-gestion-por-el-paciente because: necesita `CitaReprogramada` y `CitaCancelada`.
- Assumption: los correos no tienen mockup; se construyen desde la tabla de `EXPERIENCE.md`.
- Open question: si el Motivo de consulta viaja en la Agenda del día y a qué hora sale cuando hay Citas por la mañana (spec, Open Questions).
- Traspaso de la épica 2 (entrada 9, Gerardo, 2026-11-03): Cita confirmada no muestra todavía la frase "Te enviamos los detalles a tu correo. Un día antes, a las 9:00 am, te llegará un recordatorio." ni el estado "Recordatorio omitido" (EXPERIENCE.md). Esta épica los agrega cuando los correos existan, con la hora real del Recordatorio.
