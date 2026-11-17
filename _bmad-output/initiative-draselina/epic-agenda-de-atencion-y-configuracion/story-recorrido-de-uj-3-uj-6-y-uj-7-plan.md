---
title: 'Recorrido de UJ-3, UJ-6 y UJ-7'
type: 'chore'
ticket: '8'
created: '2026-11-17'
status: done
baseline_revision: '37c2f889f1d86db3e5207b9c4a6f5a69a0bdeba0'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: ''
review_source: ''
lenses_ran: []
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Las entradas 6.1 a 6.7 están probadas automáticamente, pero ninguna prueba recorre el Calendario de atención y Configuración en un navegador real con el AppHost y Mailpit; sin mockups (Decision 2026-11-17), este recorrido es donde Gerardo aprueba o pide cambios (Done when 1 de la épica).

**Approach:** Sin código. Gerardo sigue la guía de abajo; lo que no le guste va a la entrada 9 (ajustes), junto con el texto de `/reservar` para un día que no se atiende que ya está asignado ahí.

</frozen-after-approval>

## Implementation Notes

Ruta oneshot sin cambios de código; no hace falta el reloj desplazado. Fechas de la guía con hoy martes 17 de noviembre de 2026: se usa la semana siguiente (sábado 21, martes 24, miércoles 25, viernes 27). Si el recorrido se hace otro día, se corren a fechas equivalentes. La base sembrada es de lunes a viernes, 3:30 a 6:00 pm; las reglas, 1 día, 2 meses, 120 minutos y 3 citas por celular a la semana (por eso cada Paciente usa su propio celular).

## Guía del recorrido

**Preparación**
1. `dotnet run --project src/DraSelina.Consultorio.AppHost`; del panel, abrir la web y Mailpit. Sin `Reloj:Desplazamiento` en `appsettings.Development.local.json`.
2. Entrar a `/admin` con la cuenta del Administrador y el código de la app autenticadora. La navegación dice Agenda, Pacientes, Calendario de atención y Configuración.

**1. UJ-6, segunda mitad: abrir un sábado al público (6.1)**
- "Calendario de atención": el mes de noviembre de lunes a domingo; los días pasados se ven pero no se tocan; sábados y domingos dicen "No se atiende".
- Tocar el sábado 21: "Hoy vale: No se atiende". Agregar el tramo 10:00 a 1:00 pm, motivo "sábado abierto", Guardar. El 21 dice "Día abierto · sábado abierto" con otro estilo.
- En una ventana privada, `/reservar`: el sábado 21 se ofrece de 10:00 am a 12:30 pm; el sábado 28 no.

**2. Preparar UJ-7: cuatro Citas el sábado 21**
- En la ventana privada, reservar en línea el sábado 21: 10:00 am "Ana López" con correo; 10:30 am "Bruno Díaz" **sin correo**; 11:00 am "Carmen Soto" con correo. Un celular distinto para cada uno.
- En administración, "Agendar cita" el sábado 21 a las 12:30 pm para "Diego Paz", con correo y Consentimiento.

**3. UJ-7: cerrar el sábado conservando la de las 12:30 (6.2, 6.3)**
- Calendario, tocar el sábado 21: quitar el tramo, motivo "asunto personal", Guardar.
- Aparece "4 citas quedan fuera de este horario": Bruno con "sin correo"; las tres en línea sin elegir y la de Diego (del Consultorio) en "Conservar". Guardar sin elegir: "Elige cancelar o conservar en cada cita."
- "Cancelar todas", cambiar la de las 12:30 a "Conservar", Guardar: "Se guardó… Las 3 citas elegidas se cancelan en unos instantes…".
- El 21 dice "No se atiende · asunto personal". En la Agenda del sábado (con "Mostrar canceladas"): tres "Canceladas por el consultorio" y la de las 12:30 Agendada.
- En Mailpit: la disculpa para Ana y para Carmen; nada para Bruno (sin correo) ni para Diego.
- En el Detalle de una cancelada, la bitácora dice Administrador.

**4. UJ-3: un día de cirugía (6.1 a 6.3)**
- En la ventana privada, reservar el martes 24 a las 5:30 pm ("Elena Ruiz", con correo).
- Calendario, martes 24: "Hoy vale: Horario de la semana tipo · 3:30 pm a 6:00 pm". Cambiar el fin a 5:00 pm, motivo "cirugía", Guardar: la lista muestra la Cita de las 5:30. Elegir "Cancelar" y Guardar.
- El 24 dice "Horario especial · cirugía"; la Cita queda Cancelada por el consultorio y llega la disculpa con "Elegir otro horario". El miércoles 25 sigue normal.

**5. Conflicto: la lista cambió (6.3)**
- En la ventana privada, reservar el miércoles 25 a las 5:30 pm ("Gabriel Mora").
- Calendario, miércoles 25: fin 5:00 pm, motivo "junta", Guardar: la lista muestra la Cita de las 5:30. Elegir "Conservar" y **no guardar todavía**.
- En la ventana privada, reservar el miércoles 25 a las 5:00 pm ("Hilda Ríos").
- En administración, Guardar: aviso "La lista de citas cambió mientras la revisabas…" y la lista recargada con las dos Citas; la de las 5:30 conserva "Conservar" y la de las 5:00 queda sin elegir. Elegir "Conservar" y Guardar: el 25 dice "Horario especial · junta" y las dos Citas siguen Agendadas.

**6. Quitar el horario propio (6.3)**
- Calendario, miércoles 25: "Quitar horario propio": "El día vuelve a la semana tipo: 3:30 pm a 6:00 pm.", sin lista (las dos Citas caben en la base). Confirmar: el 25 vuelve a verse normal.
- Sábado 21: "Quitar horario propio" dice "ese día no se atiende" y lista la de las 12:30 (quedaría fuera de la base); elegir "Conservar" y confirmar.

**7. Configuración: la semana tipo (6.4, 6.5)**
- En la ventana privada, reservar el viernes 27 a las 5:30 pm ("Fernanda Gil").
- Configuración, Semana tipo: el lunes de 3:30 a 6:00 pm, sábado y domingo "No se atiende". Cambiar el lunes a 3:30 a 5:00 pm, "Copiar a otros días" → miércoles y viernes → "Copiar". Guardar: la lista avisa que la Cita del viernes 27 a las 5:30 queda fuera y que no se cancela; "Guardar de todos modos". El mensaje lista las fechas con horario propio que no cambiaron.
- `/reservar`: el viernes termina a las 4:30 pm. La Cita de Fernanda sigue Agendada.
- Regresar lunes, miércoles y viernes a 3:30 a 6:00 pm y guardar.

**8. Configuración: reglas y avisos (6.6)**
- Reglas de reserva: ventana de 2 a 3 meses, "Guardar reglas", "Se guardó.". `/reservar` ya ofrece fechas de febrero. Probar −1 en la anticipación: el mensaje bajo el campo. Regresar la ventana a 2.
- Avisos por correo: ver las dos horas y los dos correos; un correo inválido muestra su mensaje. (No cambiar las horas, o regresarlas.)

**9. Configuración: Tipos de consulta (6.7)**
- Agregar "Infiltración", 45 minutos, sin "Se reserva en línea": aparece en Agendar cita con 45 min y no en `/reservar`.
- Cambiar la duración de "Primera consulta" a 45: `/reservar` ofrece horarios cada 45 minutos. Regresarla a 30.
- Desactivar "Infiltración" (pide confirmar): "Se desactivó Infiltración." Ya no está en Agendar cita.

**10. Textos y detalles por revisar** (`[ASSUMPTION: texto]` de las entradas)
- Calendario: la ayuda arriba del mes, "Hoy vale…", "Sin tramos, ese día no se atiende.", el título y la ayuda de la lista de Citas fuera, los mensajes de conflicto y de éxito, "Quitar horario propio".
- Configuración: ayudas de la semana tipo, "Copiar a otros días", "Guardar de todos modos", etiquetas y ayudas de las reglas (unidades), de los avisos y de los Tipos de consulta.
- De la revisión de la 6.3: al aparecer la lista de Citas fuera, el foco no se mueve; ver si con lector de pantalla se nota.
- Ya asignado a la entrada 9: en `/reservar`, un sábado sin Tramos dice que no tiene horarios para ese tipo de consulta (el texto se decide ahí).

## Resultado del recorrido

Recorrido el 2026-11-17 por la sesión tester con Gerardo en su navegador (AppHost local). Reporte completo en la sesión
tester; aquí lo que decide.

**Arreglado durante el recorrido** (fixes fuera de entrada, con Gerardo):
- `26c70e7`: el Calendario de atención y el calendario de `/reservar` compartían el bloque CSS `dsm-calendario`; el de
  administración pasa a `dsm-atencion`. Visto bien en las dos pantallas.
- `ab3fee4`: los campos de hora se vaciaban al escribir. `blazor.web.js` manda `type=time` como `HH:mm:ss` y el
  `@bind:format="HH:mm"` no lo convertía; se quita el formato en los seis campos (Calendario, Semana tipo, Avisos).
  bUnit no lo vio porque no corre ese JS; sus pruebas mandan ahora lo que manda el navegador.
- `4928ac4`: `/reservar` de 992 a 1279 px dejaba cada día de ~11 px; ahí las horas van debajo del calendario. Visto a
  1024 y 1440 px. Existía desde antes de la épica.
- Configuración cargaba una sola sección (las reglas nunca): las secciones consultan a la vez con el mismo `DbContext`
  del circuito. Decorador `TurnoDelAmbito` (un handler a la vez por ámbito), opción A de Gerardo; prueba de integración
  con tres consultas a la vez en un ámbito.

**Pasos:** 1 a 7 y 9 pasan. El 7 mostró 3 Citas fuera y no 1: correcto, porque el 25 volvió a la semana tipo en el
paso 6; la guía esperaba una. El 8 falló en Reglas por la carga de Configuración (arriba) y queda por repetir; Avisos
pasa.

**Va a la entrada 9** (Gerardo, 2026-11-17):
- El editor del día del Calendario de atención como modal (`<dialog>` en escritorio, hoja desde abajo en teléfono, con
  scroll adentro); foco al título al abrir y de vuelta al día al cerrar. Cierra el foco pendiente de la revisión de la
  6.3 (hoy queda en `body` al aparecer la lista).
- "Fuera de horario" en la Agenda para toda Cita que no cabe en los Tramos vigentes, no solo las del Consultorio: con
  esta épica una Cita en línea conservada puede quedar fuera.
- Textos: días de la semana con minúscula a media frase ("del sábado 21"); "Hoy vale:" → "Horario actual:"; con una
  sola Cita fuera, ayuda en singular y sin "Cancelar todas"; el éxito al cancelar no promete disculpa a quien no tiene
  correo; "El día vuelve a la semana tipo: ese día no se atiende." sin repetir "día"; unificar "(por el Consultorio)"
  con la épica 5 "(por el consultorio)"; separador antes de "sin correo" en la lista de la semana tipo.
- Avisos por correo: validación del correo con mensaje de la app (`novalidate`), no el globo del navegador; hoy acepta
  `a@b`.
- El texto de `/reservar` para un día que no se atiende (ya estaba en la entrada).
- La ventana de reserva con máximo de 12 meses (hoy se guardan 13), con su mensaje bajo el campo.
- "Tope semanal por celular (citas)" y "Escribe el tope semanal por celular.": la etiqueta y el error decían
  "teléfono"; la ayuda y el resto de la app dicen "celular".

**Paso 8 repetido** con el turno por ámbito: Configuración carga sus cuatro secciones en 12 de 12 cargas; la ventana a
3 meses llega a `/reservar` sin reiniciar; los cuatro valores inválidos y el vacío dan su mensaje bajo el campo y no
guardan; reglas regresadas a 1 / 2 / 120 / 3. Textos de Reglas claros, salvo "teléfono" (arriba).

**Recorrido terminado** (Gerardo, 2026-11-17): lo que pide cambiar está en la entrada 9.

## Verification

**Manual checks:**
- Los pasos 1 a 10 de la guía, hechos con el AppHost por Gerardo.
