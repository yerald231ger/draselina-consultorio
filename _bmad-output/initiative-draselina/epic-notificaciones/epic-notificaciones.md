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
- Open question: a qué hora sale la Agenda del día cuando hay Citas por la mañana (spec, Open Questions).
- Decision: la Agenda del día lleva hora, Paciente y Motivo de consulta de cada Cita, como dice FR-25. El correo va solo a la dirección de la doctora; AD-23 prohíbe el motivo en eventos, registros y telemetría, y `notificaciones.mensaje` no guarda contenido (Gerardo, 2026-11-08).
- Traspaso de la épica 2 (entrada 9, Gerardo, 2026-11-03): Cita confirmada no muestra todavía la frase "Te enviamos los detalles a tu correo. Un día antes, a las 9:00 am, te llegará un recordatorio." ni el estado "Recordatorio omitido" (EXPERIENCE.md). Esta épica los agrega cuando los correos existan, con la hora real del Recordatorio.
- Decision: en local los correos van por SMTP a Mailpit, que levanta el AppHost como contenedor y muestra lo enviado, con adjuntos, desde el dashboard de Aspire. El canal de `INotificationChannel` es SMTP (MailKit); Azure Communication Services también acepta SMTP, así que desplegar solo cambiaría la configuración (Gerardo, 2026-11-08).
- Decision: `IContenidoApi.DatosDelConsultorio` agrega `EnlaceDelMapa`, armado como búsqueda de Google Maps con la dirección que ya tiene Contenido; la confirmación muestra "Abrir en el mapa" cuando existe. Lo podrán usar Cita confirmada y la épica 7 (Gerardo, 2026-11-08).
- Decision: la semilla de `configuracion_de_envio` lleva `doctora@consultorio.local` y `administrador@consultorio.local`; Mailpit las atrapa en local y las reales se ponen con el comando de configuración, sin dejar direcciones personales en el repositorio (Gerardo, 2026-11-08).
- Decision: desglose en ocho entradas aprobado (Gerardo, 2026-11-08). Bala trazadora: la entrada 1, la confirmación por correo desde CitaAgendada por el outbox, Notificaciones, Pacientes, Contenido, Agenda y SMTP hasta Mailpit, con el reintento si el canal falla. Después la 2, el Recordatorio, por ser lo menos seguro.
- Decision: 1 → 2, y después tres carriles: 3 (Cita confirmada), 4 → 5 (handlers de cancelación) y 6 (Agenda del día); la 7 espera a la 2 y a la 6 porque cambia la configuración que leen (2026-11-08).
- Open question (entrada 7): al cambiar la hora del Recordatorio, si los ya programados se mueven a la nueva hora o conservan la suya.
- Decision (entrada 5): la alerta de cancelación tardía sale sin el botón "Abrir la Agenda", con el Paciente y la hora de la Cita (FR-24); la épica 5 agrega el botón cuando exista la Agenda de administración, sin inventar hoy su ruta (Gerardo, 2026-11-08).
