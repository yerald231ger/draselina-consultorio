---
type: initiative
title: "La agenda de la Dra. Selina se lleva sola"
parent: none
covers: [CAP-1, CAP-2, CAP-3, CAP-4, CAP-5, CAP-6, CAP-7, CAP-8, CAP-9, CAP-10, CAP-11, CAP-12, CAP-13, CAP-14, CAP-15, CAP-16, CAP-17, CAP-18, CAP-19, CAP-20, CAP-21]
after: []
assignee: ""
risk: high
---

# La agenda de la Dra. Selina se lleva sola

## Description

Los Pacientes reservan, reprograman y cancelan sus Citas por sí mismos; el Administrador atiende las excepciones desde una sola pantalla; Selina solo recibe un correo con sus citas del día. El spec es dueño de las capacidades, restricciones y no objetivos; esta iniciativa las entrega para el piloto.

## Outcome

Selina deja de administrar su agenda por WhatsApp; la señal es la del spec (sección Success signal).

## Done when

1. Las 21 capacidades del spec funcionan de punta a punta en local, levantadas por el AppHost.
2. Dos reservas simultáneas del mismo horario producen una sola Cita, comprobado contra PostgreSQL real.
3. El recorrido completo de un Paciente (reservar, recibir confirmación y recordatorio, reprogramar, cancelar tarde) y el de una Agenda custom que cancela unas Citas y conserva otras pasan como pruebas de integración.
4. Las pruebas de arquitectura de AD-1 pasan y ningún nombre usa los términos vetados por E-7.
5. Ningún evento, registro ni dirección contiene Motivo de consulta, nombre, teléfono, correo ni Enlace de gestión (AD-23).

## Boundaries

Corte por resultado, no por módulo. Fuera: todo lo de la sección Non-goals del spec, y el aprovisionamiento y despliegue a Azure, diferidos por la arquitectura. Camino trazador: un Paciente reserva una Cita en línea y la ve confirmada (épica 2), sobre la plataforma de la épica 1.

- Touch point: proveedor de correo (Azure Communication Services) — se consume detrás de `INotificationChannel`; owner: epic-notificaciones
- Touch point: verificación anti-bots (Cloudflare Turnstile) — se consume en las páginas de reserva; owner: epic-reserva-en-linea
- Touch point: mapa — solo un enlace externo; owner: epic-sitio-del-consultorio

## References

- spec — _bmad-output/initiative-draselina/spec-appointment-booking/spec-appointment-booking.md
- constraint — _bmad-output/initiative-draselina/spec-appointment-booking/spec-appointment-booking.md, sección Constraints
- trazabilidad — _bmad-output/initiative-draselina/spec-appointment-booking/trazabilidad.md

## Notes

- Decision: siete épicas en este orden; Notificaciones antes que Administración, para probar pronto el recorrido completo del Paciente con correos (Gerardo, 2026-10-05).
- Decision: "terminado" significa que corre en local con el AppHost y pasan sus pruebas de integración contra PostgreSQL real; no hay producción todavía (Gerardo, 2026-10-05).
- Decision: la primera historia es la prueba de arranque de .NET 11 con Wolverine 6 y el AppHost de Aspire; si falla, se retrocede a .NET 10 (Gerardo).
- Decision: la prueba de arranque pasó; la solución se crea sobre .NET 11 (Gerardo, 2026-10-05).
- Decision: las decisiones que más de una épica adopta ya viven en la arquitectura (AD-3 eventos, AD-10 `Resultado`, AD-19 enlace de gestión, AD-22 actor); no hay historias de decisión.
- Propiedad compartida — Bitácora (CAP-17): la épica 2 construye el mecanismo; cada épica escribe las entradas de sus cambios; la épica 5 la muestra.
- Propiedad compartida — Configuración (CAP-16): la épica 2 crea y siembra `ReglasDeReserva` y `TipoDeVisita`; la épica 4, las horas de envío; la épica 6 entrega la pantalla y la edición.
- Propiedad compartida — Agenda de atención: la épica 2 crea las tablas de base y custom y la lectura de tramos vigentes (INV-V1); la épica 6, los comandos que las cambian y la política P-1.
- Propiedad compartida — `IPacientesApi.ObtenerContactos`: lo entrega la épica 4, la primera que lo necesita; lo usa también la 5.
- Propiedad compartida — No indexable (CAP-21): cada épica lo pone en sus propias páginas (la 3 en los Enlaces de gestión, la 5 en administración); la 7 entrega títulos y legibilidad del sitio público.
- Propiedad compartida — Contenido: la épica 2 crea el módulo con los datos del consultorio y la página del Aviso de privacidad con su versión (la casilla de Consentimiento la enlaza y la guarda, AD-24); la épica 7 agrega los textos del sitio y enlaza el Aviso desde el pie.
- Decision: sin integración continua, despliegue ni operación en la épica de plataforma; la arquitectura los difiere hasta preparar el piloto (sección Deferred) (Gerardo, 2026-10-05).
- Revisión independiente del corte (2026-10-05): confirmó cobertura de CAP-1 a CAP-21, el reparto de bitácora y configuración, y los touch points; corrigió prerrequisitos de las épicas 5, 6 y 7 y los dueños compartidos de arriba.
- Decision: se construye épica por épica, una a la vez, sin carriles en paralelo entre épicas; no hay prisa (Gerardo, 2026-10-05).
- Open question: las del spec, sección Open Questions; cada épica nombra las que la afectan.
