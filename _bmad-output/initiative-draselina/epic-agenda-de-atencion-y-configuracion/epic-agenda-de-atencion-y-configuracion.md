---
type: epic
title: "El administrador decide cuándo se atiende y con qué reglas"
parent: initiative-draselina
covers: [CAP-10, CAP-11, CAP-16, CAP-17]
after: []
assignee: ""
risk: high
---

# El administrador decide cuándo se atiende y con qué reglas

## Description

Gerardo cambia la semana tipo, da a una fecha su horario propio para recortarla, cerrarla o abrirla, y ajusta las reglas de reserva y los Tipos de visita. Ninguna Cita cambia sola: cuando una Agenda custom deja Citas fuera, él elige cita por cita cuál se cancela y cuál se conserva.

## Outcome

Un día de cirugía, unas vacaciones o un sábado abierto se registran en un minuto y los Pacientes afectados reciben su disculpa; la señal es SM-1 del PRD.

## Requirements

Se completan al dividir esta épica. La fuente son las capacidades de `covers` en el spec, con las consecuencias verificables de sus `FR-n` en el PRD.

## Done when

1. UJ-3, la segunda mitad de UJ-6 y UJ-7 completos en local: recortar un día cancelando una Cita, abrir un sábado al público, y cerrar un sábado conservando la Cita de las 12:30.
2. Guardar o borrar una custom no toca ninguna Cita; las elegidas se cancelan después, cada una en su transacción, a nombre del Administrador, y procesar el evento dos veces no cambia el resultado.
3. Si la lista que vio el Administrador ya no coincide con las Citas que quedan fuera, el comando devuelve conflicto y no cambia nada.
4. Cambiar la base lista las Citas que quedan fuera y las fechas con custom, y no cancela nada.
5. Se rechaza un Tramo encimado o donde no quepa una Cita del tipo más corto.
6. Cambiar una regla o un Tipo de visita surte efecto en la siguiente reserva, sin desplegar.

## Boundaries

El Calendario de atención y Configuración: los métodos de escritura de `AgendaAtencion`, la política P-1 y la edición de `ReglasDeReserva` y `TipoDeVisita` (los agregados, sus tablas y la lectura de tramos vigentes ya existen desde la épica 2). De CAP-16 entrega la pantalla y la edición de las reglas de Agenda y los Tipos de visita; las horas de envío son de la épica 4 y esta pantalla las compone. De CAP-17 entrega las entradas de base y custom.

## References

- parent — _bmad-output/initiative-draselina/initiative-draselina.md
- spec — _bmad-output/initiative-draselina/spec-appointment-booking/spec-appointment-booking.md, sección Capabilities
- trazabilidad — _bmad-output/initiative-draselina/spec-appointment-booking/trazabilidad.md
- architecture — _bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md, AD-5, AD-6, AD-14, AD-17, AD-22, AD-26
- domain — _bmad-output/initiative-draselina/architecture-appointment-booking/ddd-and-data-model-appointment-booking.md, sección 2.2: `AgendaAtencion`, Políticas, RN-4 a RN-12, T-29 a T-31
- design — _bmad-output/initiative-draselina/ux-appointment-booking/EXPERIENCE.md, Calendario de atención, Configuración y sus estados

## Notes

- Decision: "terminado" significa que corre en local con el AppHost y pasan sus pruebas de integración contra PostgreSQL real; no hay producción todavía (Gerardo, 2026-10-05).
- Waits on epic-administracion-de-citas because: necesita el acceso, el marco de administración y `CancelarPorConsultorio`, que usa P-1.
- Waits on epic-notificaciones because: la pantalla de Configuración compone las horas de envío y la disculpa de CAP-11 sale por ahí.
- Waits on epic-reserva-en-linea because: necesita `AgendaAtencion` y la `Cita`.
- Assumption: en la lista, las Citas en línea no vienen preseleccionadas y las del Consultorio vienen marcadas "conservar" (RN-10).
- Traspaso de la épica 5 (Gerardo, 2026-11-09): la navegación de administración muestra solo las pantallas que existen; esta épica agrega "Calendario de atención" y "Configuración" al construir sus pantallas.
