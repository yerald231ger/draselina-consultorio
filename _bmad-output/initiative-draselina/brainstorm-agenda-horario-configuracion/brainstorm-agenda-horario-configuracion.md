---
title: "Decisión: modelo de disponibilidad — agenda base y agenda custom"
type: intent
status: complete
created: 2026-10-04
decided_by: Gerardo
source: .memlog.md
---

# Decisión: agenda base y agenda custom

Resultado de la sesión de lluvia de ideas del 4 de octubre de 2026. Los nueve puntos fueron cerrados uno por uno por Gerardo. Este documento es la entrada para la arquitectura y para corregir el PRD y el UX.

## El modelo

La disponibilidad se **calcula al vuelo**. Los slots nunca se guardan ni se crean por adelantado.

**Agenda base.** La semana tipo de Selina, sin fechas. Por cada día de la semana, cero o más tramos (inicio, fin). Un día sin tramos no se atiende. Hoy: lunes a viernes, 15:30–18:00.

**Agenda custom.** El horario propio de una fecha concreta. Cero o más tramos que no se enciman entre sí, más un motivo. Cero tramos significa "ese día no se atiende". A lo más una por fecha.

**El cálculo.**

```
para cada fecha dentro de la ventana de reserva:
    si la fecha tiene agenda custom → usar sus tramos
    si no                           → usar los tramos de la base para ese día de la semana
    cortar en slots según la duración del tipo de visita
    quitar los slots que traslapan una cita activa
= slots disponibles
```

**Sin precedencia.** La custom reemplaza por completo a la base en su fecha. No hay tramos que sumen o resten, ni dos excepciones compitiendo.

## Tablas

`agenda_base` — una fila por tramo y día de la semana.

| día | inicio | fin |
|---|---|---|
| lunes | 15:30 | 18:00 |
| martes | 15:30 | 18:00 |
| … | … | … |

`agenda_custom` — por fecha; una fecha sin tramos es "no se atiende".

| fecha | inicio | fin | motivo |
|---|---|---|---|
| vie 9 oct 2026 | 15:30 | 17:00 | cirugía a las 5 |
| jue 15 oct 2026 | 15:30 | 16:30 | junta |
| jue 15 oct 2026 | 17:00 | 18:00 | junta |
| sáb 17 oct 2026 | 10:00 | 13:00 | sábado abierto |
| mié 23 dic 2026 | — | — | vacaciones |

`citas` — como en el plan, más **origen** (en línea / consultorio) y **nota** (obligatoria en la cita manual fuera de horario).

## Reglas decididas

1. **La base se toca poco.** Se llena una vez y se repite sola. Solo cambia cuando Selina cambia de rutina.
2. **Los cambios a la base aplican de inmediato.** Sin fecha de inicio futura ni versiones.
3. **Cambiar la base respeta las citas existentes.** Las que queden fuera del horario nuevo se muestran antes de guardar; no se cancelan solas y el administrador decide.
4. **Cambiar la base no altera las fechas con custom.** El sistema avisa cuáles son.
5. **Una custom no cancela nada por sí sola; el administrador decide cita por cita.** Antes de guardar se listan las citas que quedan fuera y, para cada una, se elige cancelar o conservar (decidido el 4 de octubre al definir el diseño de dominio; sustituye a "la custom cancela las citas en línea"). Las elegidas quedan canceladas por el consultorio, con disculpa e invitación a elegir otro horario. Las conservadas no cambian. Aplica igual si la fecha era de horario normal o un día abierto por excepción.
6. **Se puede registrar una custom con cualquier anticipación.** Vacaciones de diciembre se capturan hoy.
7. **Rehabilitar** es editar o borrar la custom de la fecha; el día vuelve a la base. Si borrarla deja citas fuera, el sistema pregunta qué hacer con cada una, igual que en la regla 5 (decidido el 5 de octubre al cerrar el diseño de dominio).
8. **Abrir un día fuera de la base al público** es crear la custom de esa fecha. Es puntual, la decide Selina, no se repite y caduca sola.
9. **Cita manual del consultorio.** Solo la registra el administrador, con nota obligatoria. Ignora base y custom, incluso en una fecha "no se atiende". Reglas duras: no traslapa otra cita y no cae en el pasado. Nunca aparece como disponible al público.
10. **Competencia por un slot.** Si dos pacientes intentan el mismo horario a la vez, uno lo obtiene y al otro se le muestran los restantes.
11. **Ventana de reserva.** Desde mañana hasta dos meses; se recorre sola. No se "habilita" ningún periodo.

La diferencia entre las reglas 3 y 5 es intencional: la base es un cambio de rutina; la custom dice "ese día no puedo".

## Enfoques descartados

- **Inventario de slots** (crear los slots por adelantado con un proceso semanal). Necesita igual una regla y los cierres futuros, y agrega una tercera tabla y un proceso que pueden desincronizarse. No se puede bloquear lo que aún no existe.
- **Tramos que abren y cierran** (excepciones como rangos sueltos que se enciman). Obliga a recordar qué gana y a partir un cierre para rehabilitar una parte.
- **Habilitar la agenda por periodos.** Convierte cada semana en un registro y obliga a re-habilitar.

## Vocabulario

"Agenda base" y "agenda custom" sustituyen a "Horario de atención" y "Bloqueo" en todo el proyecto.

## Qué hay que corregir

**PRD** (`prd-appointment-booking`)

- Glosario: salen *Horario de atención* y *Bloqueo*; entran *Agenda base* y *Agenda custom*.
- FR-12: la base admite varios tramos por día; se elimina el supuesto de una sola franja.
- FR-13: el bloqueo pasa a ser agenda custom por fecha, que puede recortar, cerrar o abrir un día.
- FR-15: la cita manual puede ir fuera de horario y sobre una fecha cerrada, con nota obligatoria.
- FR-2 y FR-25: los días de atención salen de la base y de las custom.

**UX** (`ux-appointment-booking`)

- La pantalla Bloqueos pasa a ser un calendario donde se toca una fecha para darle agenda custom.
- Configuración: la semana tipo con varios tramos por día y "aplicar a varios días".
- Agendar cita (administración): campo de nota y aviso cuando la cita cae fuera de horario.
- El calendario de escritorio del paciente ya no puede asumir cinco columnas fijas.

**Arquitectura** (`architecture-appointment-booking`, en curso)

- La pregunta pendiente "¿dónde vive el horario?" queda resuelta: dos objetos dentro de Agenda, base y custom. (El 5 de octubre se unieron en un solo agregado, `AgendaAtencion`; las tablas no cambian.)
- La cita recibe los tramos vigentes como entrada al validarse; la cita del consultorio no los valida.

## Abierto

- Qué ve Selina en "Tus citas de hoy" un día abierto por custom o con una cita manual fuera de horario (se asume que se envía cualquier día que tenga tramos o citas).
- No se confirmó qué hacen Cal.com y Calendly con las citas existentes al poner un horario propio; aquí se decidió primero cancelarlas y después, el 4 de octubre, que el administrador elige cita por cita (regla 5).
