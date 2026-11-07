---
title: 'Reserva en escritorio'
type: 'feature'
ticket: '12'
created: '2026-11-04'
status: 'done'
baseline_revision: 'dd87cfcffcc21d15db6b295de107fa20699d996b'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-draselina/ux-appointment-booking/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** En una pantalla ancha la reserva se ve como un teléfono estirado: una columna angosta, la fila de días y sin el panel "Tu cita" de los mockups `WebReserva`, `WebDatos` y `WebConfirmada`.

**Approach:** Las mismas páginas y direcciones, con un acomodo de dos columnas en pantallas anchas: Día y hora con la lista de tipos y un calendario de mes; Tus datos con el formulario y el panel "Tu cita"; Cita confirmada con la cita y el enlace lado a lado. El móvil no cambia.

## Boundaries & Constraints

**Always:**
- Mismas rutas, mismos pasos, mismos estados y mismas reglas que en móvil (entradas 1, 5, 8, 9, 10, 11): esta historia solo cambia el acomodo. Sin scripts; elegir día, hora o tipo sigue siendo un enlace o un botón de formulario (AD-11).
- Decisión (Gerardo, 2026-11-04): el calendario de escritorio tiene siete columnas siempre, de lunes a domingo; los días sin atención o fuera de la ventana se ven deshabilitados; un día con horarios del tipo y todos ocupados se ve "Lleno o no disponible", como la leyenda del mockup; un sábado abierto por custom aparece en su columna.
- Día y hora en escritorio (`WebReserva`): a la izquierda los tipos reservables, que cambian el tipo conservando la fecha; al centro el calendario del mes con "Hasta el …" y la nota "Hora del centro de México · desde mañana y hasta 2 meses" derivada de las reglas; las horas del día elegido; a la derecha el panel "Tu cita" (consulta, fecha y hora, lugar) con "Continuar". Si el día elegido no tiene horarios: "Este día ya no tiene horarios libres. Elige otro día en el calendario."
- Tus datos en escritorio (`WebDatos`): formulario a la izquierda; panel "Tu cita" con "Cambiar" a la derecha; "Volver a horarios".
- Cita confirmada en escritorio (`WebConfirmada`): la cita con "Agregar a mi calendario" y el lugar a un lado; "Guarda este enlace" al otro; mismas reglas de "con/sin correo" (sin la frase del correo, entrada 9) y sin "Copiar" ni "Abrir en el mapa" (Contenido no tiene enlace de mapa todavía).
- Accesibilidad: el calendario es una tabla con encabezados de día y cada día con su fecha completa para lectores de pantalla.
- El horario de escritorio y la fila de días del móvil salen de la misma consulta `HorariosLibres`; ninguna fecha u hora escrita en la página.

**Never:**
- Rutas nuevas, un paso combinado distinto al móvil, scripts o "Copiar".
- Cambiar reglas de negocio o la consulta.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Calendario | Ancho de escritorio, base sembrada | Tabla de 7 columnas Lun–Dom; días hábiles de la ventana como enlaces; sábados y domingos deshabilitados | — |
| Sábado abierto | Custom sábado 10:00–11:00 | Ese sábado aparece habilitado en su columna | — |
| Día lleno | Todas las horas del día con Cita | "Lleno o no disponible", no se puede elegir | — |
| Cambiar tipo | Elegir Consulta de seguimiento en la columna de tipos | Mismo día elegido, horarios de ese tipo | — |
| Tu cita | Hora elegida | Panel con consulta, fecha y hora, lugar y "Continuar" a Tus datos | — |
| Tus datos | Ancho de escritorio | Formulario y panel "Tu cita" con "Cambiar" | — |
| Confirmada | Ancho de escritorio | Cita y lugar a un lado, "Guarda este enlace" al otro | — |
| Móvil | Ancho de 390 px | Igual que hoy | — |

</frozen-after-approval>

## Code Map

Rutas bajo `src/DraSelina.Consultorio.Web/` salvo que digan otra cosa.

- `UI/Pages/Reserva/DiaYHora.razor` -- fila de chips, "Lleno", horas como enlaces, resumen y "Continuar"; `?tipo=&fecha=&hora=` con fragmento `#dia-…`.
- `UI/Pages/Reserva/TipoDeConsulta.razor` -- formulario GET con radios; la consulta `TiposDeVisitaReservables`.
- `UI/Pages/Reserva/TusDatos.razor`, `HorarioTomado.razor`, `CitaConfirmada.razor` -- móvil; en escritorio se acomodan, no cambian su lógica.
- `UI/Pages/Reserva/FormatoDeCita.cs` -- fechas en español (`MesYAnio`, `MesCorto`, `Largo`…).
- `UI/Layout/LayoutPublico.razor` -- encabezado y pie.
- `wwwroot/css/consultorio.css` -- tokens `--dsm-*`; media queries para el ancho de escritorio.
- `Modules/Agenda/Contracts/HorariosLibres.cs`, `ReglasDeReserva` (ventana, anticipación) -- de ahí la nota del calendario.
- `tests/DraSelina.Consultorio.IntegrationTests/TipoDeConsultaYDiaYHoraTests.cs`, `TusDatosTests.cs` -- el HTML se prueba igual para cualquier ancho; probar la tabla del calendario, el sábado abierto, el "Lleno", la columna de tipos y los paneles.

## Tasks & Acceptance

**Execution:**
- [x] `DiaYHora.razor` + CSS -- columna de tipos, calendario de 7 columnas y panel "Tu cita" en escritorio.
- [x] `TusDatos.razor`, `HorarioTomado.razor`, `CitaConfirmada.razor` + CSS -- acomodo de dos columnas y panel "Tu cita".
- [x] Pruebas -- cada fila de la matriz (estructura HTML; el ancho lo resuelve el CSS).

**Acceptance Criteria:**
- Given un navegador de escritorio, when se recorre la reserva, then cada pantalla se parece a `WebReserva`, `WebDatos` y `WebConfirmada` y funciona igual que en móvil.
- Given el teléfono y el escritorio, when Gerardo recorre UJ-1 completo en local (tipo, día y hora, datos, confirmación con enlace y archivo de calendario), then funciona en los dos, y el `.ics` se importa en el teléfono con la hora de Ciudad de México.
- Given la solución, when corren todas las pruebas, then pasan.

## Implementation Notes

- La nota "desde mañana y hasta 2 meses" necesita las reglas en la interfaz: consulta nueva de solo lectura `VentanaDeReservaPublica` (Agenda.Contracts) → `VentanaDeReservaVigente(AnticipacionMinimaDias, VentanaMeses)`. `HorariosLibres` no cambia. El nombre `VentanaDeReserva` ya existe en Agenda.Domain.
- Un solo HTML para los dos anchos: `dsm-solo-movil` / `dsm-solo-escritorio` desde 992 px. Un solo bloque de horas y un solo "Continuar"/"Confirmar cita"; el CSS los acomoda (grid en escritorio, `display: contents` en el teléfono para Día y hora).
- Sin scripts, el mes vecino del calendario es un enlace `?tipo=&fecha=` al primer día con horas de ese mes (o su primer día de la ventana); no hay parámetro nuevo.
- Día lleno pedido por la dirección: en escritorio el calendario se queda en él con "Este día ya no tiene horarios libres…"; el teléfono sigue eligiendo el primero con horas.
- "Volver a horarios" y "Cambiar" llevan a Día y hora con tipo, fecha y hora; solo en escritorio.
- La prueba `Un_dia_sin_ningun_horario_posible_para_el_tipo_no_aparece` cambió: el día ya no sale como chip, pero el calendario sí pinta su celda "no disponible" con la fecha completa.

## Plan Change Log

## Review Triage Log

**Pasada 1 (quick), 2026-11-04.** Veredictos: high 0, medium 1, low 4, false 0, maybe-false 0.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | Cambiar a un tipo sin ningún horario posible en el día elegido salta en silencio a otro día | medium | patch | Esa fecha no viene en `_dias`, así que `_diaLleno` es nulo y se elige el primer día con horas; contradice "Mismo día elegido". Aviso sin día elegido y prueba |
| 2 | El día "Lleno" elegido no lleva `aria-current` | low | patch | Los días elegidos con horas sí lo llevan; corrección directa |
| 3 | En el teléfono, el `aside` "Tu cita" crea un landmark nombrado por un encabezado oculto a ese ancho | low | patch | "Móvil igual que hoy"; un `div` sin rol |
| 4 | El bloque de día lleno usa `h3` donde el normal usa `h2` | low | patch | Corrección directa |
| 5 | La columna izquierda de horario tomado en escritorio no tiene estilo de panel | low | patch | Cosmético; mismo estilo que las otras columnas |
| 6 | El recorrido manual de UJ-1 no consta | — | a Gerardo | Paso hitl de la entrada: lo hace Gerardo después del commit |

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` -- expected: todas pasan (Docker arriba).
- AppHost y `scripts/verificar-arranque.sh` -- expected: las tres filas pasan.

**Manual checks (if no CLI):**
- Gerardo: UJ-1 en el teléfono y en el escritorio con el AppHost arriba; importar el `.ics` en el teléfono.
