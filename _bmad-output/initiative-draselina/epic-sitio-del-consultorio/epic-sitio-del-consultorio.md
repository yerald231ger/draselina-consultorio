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

La fuente es CAP-21 en el spec, con las consecuencias verificables de FR-27 a FR-30 en el PRD (§4.5). El desglose está en `tickets.toml`.

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
- Waits on epic-reserva-en-linea because: necesita Contenido con los datos del consultorio, la entrada a la reserva y la Agenda base sembrada.
- Waits on epic-agenda-de-atencion-y-configuracion because: `HorarioDeAtencion` devuelve `DiaDeLaBase` (entrada 6.4) y el trazador se verifica cambiando la semana tipo en Configuración.
- Decision: una sola página con secciones (Inicio, Sobre mí, Padecimientos, Ubicación y horarios) con navegación por anclas; el Aviso de privacidad es página aparte. FR-27, `EXPERIENCE.md` y el spec lo dicen así (Gerardo, 2026-11-17).
- Decision: los días y horas del sitio salen de una consulta pública nueva, `HorarioDeAtencion : IQuery<IReadOnlyList<DiaDeLaBase>>`, sin `IRequiereAdministrador` y de solo lectura; `SemanaTipo` sigue siendo solo de administración. La página agrupa los días con los mismos Tramos: los días seguidos como rango ("Lunes a viernes · 3:30 a 6:00 pm") y los sueltos como lista ("Lunes, miércoles y viernes"). Las Agendas custom no se muestran (Gerardo, 2026-11-17).
- Decision: el mapa es un iframe de Google Maps sin clave (`https://www.google.com/maps?q=...&output=embed`, `loading="lazy"`, con `title`) más el enlace "Cómo llegar" con `EnlaceDelMapa`; es el único contenido de terceros del sitio (Gerardo, 2026-11-17).
- Decision: FR-29 en la épica es título con especialidad y ciudad, meta description, schema.org `Physician` en JSON-LD (horarios de `HorarioDeAtencion`, dirección de Contenido), `robots.txt` que bloquea `/admin` y `/c/` y apunta a `sitemap.xml`, y `sitemap.xml` con `/` y `/aviso-de-privacidad`. El Perfil de Negocio de Google queda fuera: es un trámite de Selina, pendiente de lanzamiento (Gerardo, 2026-11-17).
- Decision: camino trazador es la entrada 1, Ubicación y horarios desde la Agenda base: pasa por Agenda (`HorarioDeAtencion`), Contenido y la interfaz pública, y lleva lo menos seguro, el agrupado de días. Las entradas 1 a 4 son un solo carril porque tocan `Inicio.razor`, `LayoutPublico` y `consultorio.css`; la 1 entrega `HorarioDeAtencion` a la 4 y la 2 los datos de la doctora a la 3 y la 4. Recorrido hitl (5) y sus ajustes (6) antes del Refactor sweep (7), como en la épica 6 (Gerardo, 2026-11-17, inception aprobada).
- Assumption: el encabezado con anclas y "Agendar cita" va solo en `/` y en el Aviso de privacidad; la reserva y la gestión conservan el suyo (entrada 1).
- Assumption: `sitemap.xml` arma sus URLs con el host de la petición, no con `UrlBase` de Agenda (entrada 4).
- Source conflict: `ux-appointment-booking/imports/mockup-canvas/SitioInicio.dc.html` — el mockup muestra el WhatsApp 81 2699 2831 vs `ContenidoApi.WhatsAppDelConsultorio`, que es nulo porque "todavía no se conoce" (Gerardo, 2026-11-02).
- Decision: se construye con los datos firmes del mockup (Céd. Prof. 12093135, Céd. Especialidad 14899218, Consejo de Ortopedia 24/8379/2, los seis padecimientos por nombre) en Contenido; lo que el mockup tiene entre corchetes (semblanza, afiliaciones, descripción de cada padecimiento) va como texto visible de pendiente, y la foto como un recuadro con las iniciales "SM". El WhatsApp sigue nulo ("escríbenos por WhatsApp") y la universidad de la cédula queda sin decidir. Cada dato se cambia después en una constante de `ContenidoApi` (Gerardo, 2026-11-17).
- Open question: el contenido de Selina (foto, semblanza, afiliaciones, descripciones de los padecimientos), el WhatsApp (el mockup dice 81 2699 2831) y la UANL frente a la UASLP. El sitio no se publica sin ellos (Boundaries).
