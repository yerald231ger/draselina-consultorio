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
- Resuelto (2026-11-04): qué límite de frecuencia se usa (spec, Open Questions); ver la Decision de la entrada 11.
- Open question: si hace falta `btree_gist` con la restricción sobre un solo rango; se comprueba en la primera migración de Agenda.
- Resuelto (2026-11-04): siete columnas o solo días con atención en el calendario de escritorio; ver la Decision de la entrada 12.
- Source conflict: arquitectura, Stack — "Anti-bots: Cloudflare Turnstile" (y el addendum del PRD, FR-7) vs épica 1, entradas 1.8 y 8 — el sitio no carga scripts y ninguna visita pide nada a un CDN. Turnstile necesita cargar un script de Cloudflare en la página. Resuelto (2026-11-04): defensa sin scripts de terceros, documentos alineados; ver la Decision de la entrada 11.
- Decision: la elección entre Turnstile (con una excepción a "sin scripts ni CDN" solo en Tus datos) y una defensa sin scripts de terceros (campo trampa, tiempo mínimo de envío y límite de frecuencia) queda como pregunta abierta de la entrada 11, junto con el valor del límite de frecuencia (Gerardo, 2026-11-02).
- Decision: desglose en trece entradas aprobado (Gerardo, 2026-11-02). Bala trazadora: la entrada 1, de Tus datos a Cita confirmada por Agenda, Pacientes, Contenido y PostgreSQL.
- Decision: dos carriles después de la entrada 1. Disponibilidad: 2 → 3 → 5. Manejador de la reserva: 4 → 6 → 7 → 8 → 9 → 10, y después 11 y 12, porque todas cambian AgendarCitaEnLinea o la página Tus datos. La 4 espera a la 3 para que la rejilla tenga una sola implementación (T-21); la 11 espera a la 8 porque la verificación anti-bots va en el mismo formulario (2026-11-02).
- Decision: el recorrido de UJ-1 desde un teléfono y desde el escritorio (Done when 1) lo verifica Gerardo en la entrada 12 (2026-11-02).
- Decision: `btree_gist` no hace falta. La migración `CrearCitas` crea `EXCLUDE USING gist (tstzrange(inicio, fin, '[)') WITH &&) WHERE (estado <> 'Cancelada')` sin la extensión; PostgreSQL 18.3 la acepta, la base del AppHost solo tiene `plpgsql` y las pruebas de integración comprueban que un traslape vuelve como `agenda.horario_ocupado`. Se necesitaría si la restricción combinara el rango con una igualdad sobre una columna escalar (entrada 1, 2026-11-02).
- Decision: en Día y hora, "Lleno" se muestra solo cuando el día tiene horarios en la rejilla del tipo y todos están ocupados por Citas. Un día sin ningún horario posible para el tipo (hoy ya pasado con anticipación 0, o un tipo que no cabe en el tramo) no se muestra. La consulta `HorariosLibres` distingue los dos casos; el ajuste entra con la entrada 5 (Gerardo).
- Decision (entrada 6): `agenda.bitacora.actor_id` se crea opcional y las reservas del Paciente lo escriben nulo, como dice el modelo de datos. Cómo se obtiene el id del Administrador se decide en la épica 5, cuando exista la identidad; sigue abierta la pregunta de la épica 1 sobre `Actor` sin id de usuario (Gerardo, 2026-11-02).
- Decision (entrada 6): el MigrationService registra `ActorSistema`, un `IActorActual` que siempre responde `Sistema`, solo para que el host pase la validación de Development (ese proceso no ejecuta casos de uso ni tiene sesiones). Es una excepción documentada a AD-22 limitada a ese proceso; la web conserva su único servicio de actor (`ActorDelHost`) y sigue abierta la pregunta de la épica 1 sobre cómo obtienen `Sistema` los trabajos programados. El arnés de pruebas ejecuta el MigrationService en Development, como el AppHost, para que un registro faltante rompa las pruebas (Gerardo, 2026-11-03).
- Decision (entrada 8): `SolicitudId` se queda como `Guid` simple, no como id tipado del Kernel: no identifica una entidad, solo un envío de formulario que se usa en un único lugar para detectar el reenvío (Gerardo, 2026-11-03).
- Decision (entrada 8): la vista de horario tomado muestra solo el día del horario perdido y los 6 siguientes de la ventana, con un enlace "Ver otros días" a Día y hora que avisa que ahí se escriben los datos de nuevo (Gerardo, 2026-11-03).
- Decision (entrada 10): el endpoint del archivo de calendario no pide actor: es una lectura que no cambia nada, no escribe bitácora ni publica eventos. La pregunta de la épica 1 sobre el actor en Minimal API sigue abierta para el primer endpoint que escriba (Gerardo, 2026-11-03).
- Decision (entrada 11): la verificación anti-bots es una defensa sin scripts de terceros (campo trampa oculto, tiempo mínimo entre pintar y enviar Tus datos, y límite de frecuencia), no Cloudflare Turnstile. Se mantienen "sin scripts" y "sin CDN" de la épica 1; Turnstile se reconsidera si aparece abuso real. Esto aparta la fila Anti-bots de la tabla Stack de la arquitectura (Gerardo, 2026-11-04).
- Decision (entrada 11): límite de frecuencia por dirección IP: 10 envíos de Tus datos cada 10 minutos y 60 visitas por minuto a las páginas de reserva, en configuración; al pasarlo, un mensaje amable, nunca un error técnico (Gerardo, 2026-11-04).
- Decision (entrada 12): el calendario de escritorio de Día y hora tiene siete columnas siempre, de lunes a domingo; los días sin atención o fuera de la ventana se ven deshabilitados y un sábado abierto por custom aparece en su lugar (Gerardo, 2026-11-04).
- Decision (entrada 12): Gerardo recorrió UJ-1 en escritorio y en vista de teléfono e importó el .ics: funciona (Done when 1). Tres detalles quedan como deuda técnica para el final de todas las épicas, en deferred-work.md de la iniciativa (Gerardo, 2026-11-06).
