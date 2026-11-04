# Addendum — Sistema de citas, Dra. Selina Medina

Contenido que pertenece a arquitectura, diseño de solución o planeación, no al PRD. Actualizado el 5 de octubre de 2026 con lo que la arquitectura cambió después; ante diferencia, mandan la arquitectura y el documento de dominio. La fuente completa es `docs/Appointment Booking System — Solution Plan.md` (3 oct 2026); aquí solo se indexa para que los flujos posteriores sepan qué ya está decidido.

## Decisiones técnicas ya tomadas (ver el plan)

| Tema | Decisión | Sección del plan |
| --- | --- | --- |
| Forma del sistema | Monolito modular en .NET 10, un solo despliegue, una base PostgreSQL | Architecture |
| Contextos | Agenda (núcleo), Pacientes, Notificaciones, Contenido; Expediente clínico futuro | Dominio (DDD) → Contextos delimitados |
| No doble reserva | Restricción de exclusión en PostgreSQL sobre el rango de tiempo de Citas no canceladas | Reglas de negocio; Agenda por dentro |
| Disponibilidad | Función pura, sin base de datos ni reloj propio | Agenda por dentro |
| Estados de la Cita | Se guardan Agendada, No se presentó, Cancelada; Atendida se calcula | Ciclo de vida de la Cita |
| Identidad del Paciente | Guid v7; emparejamiento por teléfono E.164 y nombre normalizado | Pacientes por dentro |
| Enlace de gestión | Token derivado de la Cita con una clave del servidor (AD-19); se guarda solo el hash; deja de valer cuando el inicio de la Cita ya pasó | Key design decisions |
| Mensajería y tareas | Outbox propio en PostgreSQL y servicios en segundo plano, sin broker ni librería de mensajería | Reglas de la estructura |
| Interfaz | Blazor: SSR estático en lo público, Interactive Server en administración | Reglas de la estructura |
| Acceso | ASP.NET Core Identity con 2FA | Technology choices |
| Correo | Azure Communication Services detrás de `INotificationChannel` | Notifications |
| Hospedaje | Azure App Service B1 y PostgreSQL Flexible Server B1ms | Deployment |
| Pruebas y CI | xUnit v3, bUnit, Testcontainers, pruebas de arquitectura; Playwright solo local | Reglas de la estructura |

## Alternativas descartadas

- MediatR y MassTransit v9 (licencia comercial); MassTransit v8 (fin de mantenimiento).
- Hangfire. Wolverine (se adoptó y se quitó el 2026-10-06: pedía más código para rodearlo del que ahorraba).
- Blazor WebAssembly standalone (obligaría a una API aparte y primera visita lenta).
- Azure SQL (más caro que PostgreSQL).
- FluentAssertions (licencia comercial).
- Usar el DbContext directo en los handlers (reemplazado por repositorios como puertos).

## Costos y esfuerzo estimados

- Operación: alrededor de US$30–40 al mes (cifras aproximadas, sin cotizar).
- Fase 1: 4–6 semanas de trabajo a tiempo parcial.

## Puntos de extensión previstos

- `INotificationChannel` para WhatsApp.
- InsurerId opcional en la Cita; Price opcional en el Tipo de visita.
- Expediente clínico como módulo nuevo; implica NOM-004-SSA3-2012 y NOM-024-SSA3-2012.

## Contenido pendiente de Selina para el sitio

- Foto profesional y fotos del Consultorio.
- Biografía breve, formación y afiliaciones hospitalarias.
- Cédula profesional y número de certificación de especialidad.
- Lista de padecimientos y tratamientos.
- Dirección y número de WhatsApp a mostrar.

## Diferencias entre el PRD y el plan (para arquitectura)

- **Agenda base y Agenda custom (FR-12, FR-13).** El plan fija "solo lunes a viernes" y "termina a más tardar a las 6:00 pm" en el algoritmo de disponibilidad y en las invariantes de la Cita, y modela `WorkingHours` + `ScheduleBlock`. El PRD pide una semana tipo con varios tramos por día y un horario propio por fecha que la reemplaza; la disponibilidad se calcula al vuelo. Decisión completa en `../brainstorm-agenda-horario-configuracion/brainstorm-agenda-horario-configuracion.md`.
- **Cita manual fuera de horario (FR-15).** El plan prohíbe a `AgendarPorConsultorio` salir de 3:30–6:00, de lunes a viernes y agendar sobre un bloqueo. El PRD lo permite con nota obligatoria; solo quedan dos reglas duras: sin traslape y no en el pasado.
- **Margen entre Citas.** El plan prevé `BufferMin` por Tipo de visita; el PRD no lo pide porque las cinco Citas de 30 min llenan exactamente la franja actual. Queda como punto de extensión.
- **Mecanismos de búsqueda.** Marcado schema.org Physician y Perfil de Negocio de Google son el cómo de FR-29.
- **Protección contra abuso.** El limitador de frecuencia de ASP.NET Core por IP y una verificación anti-bots sin scripts de terceros (campo trampa y tiempo mínimo entre pintar y enviar Tus datos) son el cómo de FR-7; Turnstile se reconsidera si aparece abuso real.
