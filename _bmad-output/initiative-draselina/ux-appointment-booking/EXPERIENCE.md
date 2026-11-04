---
title: "Experiencia — Reserva de citas, Dra. Selina Medina"
status: final
created: 2026-10-04
updated: 2026-10-05
sources:
  - ../prd-appointment-booking/prd-appointment-booking.md
  - imports/mockup-canvas/
---

# EXPERIENCE.md — Dra. Selina Medina, reserva de citas

Cómo se comporta el producto. La identidad visual vive en `DESIGN.md`; los requisitos, en el PRD (`FR-n`, `UJ-n`). Si un mockup y este documento se contradicen, gana este documento. Lo inferido lleva `[ASSUMPTION]` y se indexa al final.

## Foundation

- **Superficies:** web responsiva, móvil primero. Sin app. El Paciente llega casi siempre desde el teléfono; el Administrador trabaja en escritorio `[ASSUMPTION]`.
- **Idioma:** español de México, de tú. Horas en hora del centro de México, formato de 12 horas ("4:30 pm").
- **Sin cuenta para el Paciente.** Solo el Administrador inicia sesión.
- **Sistema de interfaz:** CSS propio sobre Bootstrap, según el plan de solución; los tokens de `DESIGN.md` mandan sobre los valores por defecto de Bootstrap.
- **Dos lectores de la misma reserva:** tres pasos más confirmación en móvil; en escritorio, tipo y horario se unen en el primer paso y un panel "Tu cita" acompaña todo el recorrido.

## Information Architecture

Los mockups están en [imports/mockup-canvas/](imports/mockup-canvas/) (fuente de cada artboard; el lienzo vivo es https://claude.ai/code/artifact/6fd180a8-5cb0-4a65-b13b-82d2f57b6dd4). La columna Referencia nombra el artboard.

**Público (sin sesión)**

| Superficie | Se llega desde | Propósito | Referencia |
|---|---|---|---|
| Sitio · Inicio | Buscador, enlace directo | Presentar a la doctora y llevar a "Agendar cita" | `SitioInicio`, `SitioMovil` |
| Reserva 1 · Tipo de consulta | "Agendar cita" | Elegir entre los tipos reservables en línea (FR-1) | `Main` |
| Reserva 2 · Día y hora | Paso 1 | Elegir un Horario libre (FR-2) | `Horario` |
| Reserva 3 · Tus datos | Paso 2 | Datos de contacto, motivo y Consentimiento (FR-3) | `Datos` |
| Cita confirmada | Confirmar | Mostrar la Cita, calendario y Enlace de gestión (FR-5) | `Confirmada` |
| Reserva en escritorio | "Agendar cita" | Los mismos pasos en dos columnas | `WebReserva`, `WebDatos`, `WebConfirmada` |
| Tu cita | Enlace de gestión | Ver, reprogramar o cancelar (FR-8 a FR-10) | `Gestionar` |
| Cancelar cita | Tu cita | Confirmar la cancelación; variante tardía | `Cancelar` (solo la tardía) |
| Cita cancelada | Cancelar | Confirmar y ofrecer volver a agendar | solo texto |
| Cita reprogramada | Reprogramar | Confirmar el nuevo horario | solo texto |
| Enlace no válido | Enlace vencido o inexistente | Explicar y llevar a agendar | solo texto |
| Aviso de privacidad | Pie del sitio, casilla de Consentimiento | Texto legal | solo texto |

El sitio es **una sola página con secciones** (Inicio, Sobre mí, Padecimientos, Ubicación y horarios) y navegación por anclas, como en el mockup. `[ASSUMPTION: esto sustituye a las páginas separadas que describe FR-27; el Aviso de privacidad sí es una página aparte]`

**Composición de Sitio · Inicio**, en orden, según el mockup:

1. Encabezado: logotipo, navegación por anclas y botón "Agendar cita".
2. Portada: especialidad, nombre, una frase de qué atiende, "Agendar cita en línea" y "WhatsApp", cédulas y certificación, retrato.
3. Sobre mí: semblanza, formación, certificación, cédulas, afiliaciones.
4. Padecimientos y tratamientos: "¿En qué te puedo ayudar?" con seis tarjetas (Rodilla, Hombro, Columna, Cadera, Fracturas, Lesiones deportivas).
5. Ubicación y horarios: lugar, dirección, horario, WhatsApp, "Cómo llegar", mapa.
6. Franja final: "Agenda tu consulta en menos de un minuto" con "Agendar cita".
7. Pie: nombre, especialidad, cédulas y Aviso de privacidad.

Los textos entre corchetes del mockup (foto, semblanza, descripciones, afiliaciones, mapa) son contenido pendiente de Selina.

**Reprogramar** reutiliza la superficie Día y hora, con la Cita actual visible y sin volver a pedir datos.

**Administración (con sesión)** — sin mockups; se construye desde estas tablas.

| Superficie | Propósito | FR |
|---|---|---|
| Iniciar sesión | Correo, contraseña y verificación en dos pasos | FR-11 |
| Agenda | Vista de día y de semana; punto de entrada | FR-14 |
| Detalle de Cita | Datos, estado, bitácora; marcar No se presentó, cancelar | FR-16, FR-17, FR-20 |
| Agendar cita | Capturar una Cita pedida por teléfono o WhatsApp | FR-15 |
| Calendario de atención | Ver el mes; tocar una fecha para darle, editar o quitar su Agenda custom | FR-13 |
| Pacientes | Lista con búsqueda e historial de Citas | FR-18 |
| Configuración | Agenda base (semana tipo), reglas, Tipos de consulta | FR-12, FR-19 |

`[ASSUMPTION: navegación de administración con cuatro entradas — Agenda, Pacientes, Calendario de atención, Configuración — y "Agendar cita" como acción principal de la Agenda]`

## Voice and Tone

Cercana, breve y segura. Le habla al Paciente de tú. Dice lo que pasa y lo que puede hacer; nunca regaña.

| Sí | No |
|---|---|
| "Agenda en un minuto" | "Sistema de gestión de citas" |
| "Sin cuenta ni app. Tu cita queda confirmada al instante." | "Su solicitud será revisada" |
| "Solo ves horarios libres." | "Seleccione un horario disponible" |
| "Tu cita está confirmada" | "¡Reserva exitosa!" |
| "Puedes cancelar sin problema; quedará registrada como cancelación tardía. Gracias por avisarnos…" | "Las cancelaciones tardías serán penalizadas" |
| "A esta hora ya no se puede reprogramar. Cancela y vuelve a agendar…" | "Operación no permitida" |
| "Conservar mi cita" / "Sí, cancelar mi cita" | "Aceptar" / "Cancelar" |

Reglas:

- La ayuda bajo cada campo explica **para qué** se pide el dato ("Ahí te llegan la confirmación, el recordatorio y el enlace…").
- La palabra visible es **"consulta"** ("Tipo de consulta", "Motivo de la consulta"), como en el diseño. Equivale a *Tipo de visita* y *Motivo de consulta* del Glosario del PRD.
- La disculpa por una Cita cancelada por el Consultorio es sincera y en nombre del Consultorio, sin excusas técnicas.
- Los horarios de atención que se muestran salen de la configuración, nunca se escriben a mano.

## Component Patterns

| Componente | Dónde | Reglas de comportamiento |
|---|---|---|
| Tarjeta de opción | Tipo de consulta | Selección única. El primer tipo viene preseleccionado. Toda la tarjeta es el área táctil. Solo aparecen los tipos reservables en línea. |
| Chip de día | Día y hora (móvil) | Fila con desplazamiento horizontal, desde mañana. Muestra "N libres" o "Lleno". Un día lleno no se puede elegir. Elegir otro día borra la hora elegida. |
| Calendario | Día y hora (escritorio) | Muestra cualquier día de la semana que tenga Tramos, no solo lunes a viernes: un sábado abierto por Agenda custom debe poder aparecer. Punto vino si hay horarios. Días fuera de la Ventana de reserva o sin horarios, deshabilitados. |
| Hora | Día y hora | Selección única. Solo se listan Horarios libres; los ocupados no aparecen. |
| Resumen de selección | Barra inferior (móvil), panel "Tu cita" (escritorio) | Siempre refleja lo elegido. Sin hora elegida dice "Elige una hora" y "Continuar" queda deshabilitado `[ASSUMPTION]`. |
| Barra de progreso | Los tres pasos | Informativa; no es navegación. Se regresa con la flecha o con "Cambiar". |
| Formulario de datos | Tus datos | Nombre ("Si agendas para otra persona, escribe el nombre del paciente"), celular (+52 fijo, 10 dígitos), correo opcional, motivo (una o dos líneas), Consentimiento, verificación anti-bots. Valida al salir del campo y al enviar `[ASSUMPTION]`. |
| Enlace de gestión | Cita confirmada | El enlace completo se muestra como texto seleccionable en el panel "Guarda este enlace", con el plazo para reprogramar tomado de las reglas. Sin scripts no hay botón "Copiar": el Paciente lo selecciona y copia con el gesto de su teléfono. |
| Agregar a mi calendario | Cita confirmada | Entrega un evento con fecha, lugar y el Enlace de gestión. |
| Acciones de la Cita | Tu cita | "Reprogramar" visible solo hasta 2 horas antes; "Cancelar cita" siempre. |
| Contacto por WhatsApp | Tu cita, sitio | Abre la conversación con el Consultorio. |

## State Patterns

**Paciente**

| Estado | Superficie | Tratamiento |
|---|---|---|
| Día lleno | Día y hora | Chip o celda punteada, "Lleno", no seleccionable. En escritorio, si se elige: "Este día ya no tiene horarios libres. Elige otro día en el calendario." |
| Sin horarios en toda la ventana | Día y hora | Mensaje único con el contacto de WhatsApp `[ASSUMPTION]`. |
| Horario tomado al confirmar | Tus datos | La misma respuesta de Tus datos muestra el aviso "Ese horario se acaba de ocupar. Elige otro; tus datos se conservan." y los Horarios libres restantes del tipo para el día del horario perdido y los 6 siguientes, con la misma regla de "Lleno" que Día y hora; "Ver otros días" lleva a Día y hora, donde los datos se escriben de nuevo. Elegir otra hora vuelve a Tus datos con los datos llenos y el nuevo horario; no se reserva hasta confirmar. Los datos viajan solo en el cuerpo del formulario (AD-11). Nunca una pantalla de error (FR-4). |
| Campo inválido | Tus datos | Mensaje bajo el campo, en lenguaje llano; el foco va al primer error. Celular imposible: "Revisa tu número: deben ser 10 dígitos." `[ASSUMPTION: texto]` |
| Sin Consentimiento | Tus datos | No se puede confirmar; el aviso aparece junto a la casilla. |
| Tope semanal alcanzado | Tus datos | Explica que ese número ya tiene el máximo de Citas esta semana y ofrece WhatsApp `[ASSUMPTION]`. |
| Confirmada, con correo | Cita confirmada | "Te enviamos los detalles a tu correo. Un día antes, a las 9:00 am, te llegará un recordatorio." |
| Confirmada, sin correo | Cita confirmada | Sin la frase del correo. En su lugar: no recibirás recordatorio; guarda el enlace o agrégala a tu calendario. El panel "Guarda este enlace" sube de posición `[ASSUMPTION]`. |
| Recordatorio omitido | Cita confirmada | Si la hora del Recordatorio ya pasó, no se promete. |
| Más de 2 horas antes | Tu cita | "Reprogramar" y "Cancelar cita" disponibles. |
| Menos de 2 horas antes | Tu cita, Cancelar cita | Sin "Reprogramar". Panel ámbar "Faltan menos de 2 horas" y tarjeta "¿Buscabas otro horario?". |
| Cancelación normal | Cancelar cita | La misma pantalla sin el panel ámbar `[ASSUMPTION]`. |
| Cita cancelada | Cita cancelada | Confirma la cancelación y ofrece "Agendar otra cita". Si fue tardía, lo reconoce con amabilidad. |
| Cancelada por el Consultorio | Tu cita (desde el correo) | Disculpa, la Cita cancelada y un botón para elegir otro horario. |
| Cita pasada o enlace no válido | Enlace no válido | "Este enlace ya no está disponible" y "Agendar cita" `[ASSUMPTION: texto]`. |
| Enviando | Confirmar, cancelar | Sin scripts en el sitio, el botón no se deshabilita. El servidor evita el doble efecto: confirmar dos veces el mismo formulario devuelve la misma Cita (AD-27). Cancelar lo resuelve la épica 3. |

**Administrador** `[ASSUMPTION: todos los tratamientos de esta tabla]`

| Estado | Superficie | Tratamiento |
|---|---|---|
| Cita Agendada futura | Agenda | Tarjeta con hora, Paciente, tipo y motivo. |
| Atendida (derivada) | Agenda | Misma tarjeta, atenuada, con la etiqueta "Atendida". |
| No se presentó | Agenda | Etiqueta propia; la acción aparece solo después de la hora de la Cita. |
| Cancelada | Agenda | Oculta por defecto; visible con un filtro. Distingue por Paciente, tardía y por el Consultorio. |
| Sin correo | Agenda, detalle | Etiqueta "sin correo": hay que avisar a mano. |
| Día sin Citas | Agenda | "No hay citas este día." |
| Cita nueva en línea | Agenda | Aparece sin recargar. |
| Agenda custom que deja Citas fuera | Calendario de atención | Antes de guardar: lista de las Citas que quedan fuera, con las que no tienen correo señaladas. Por cada una, el Administrador elige cancelar o conservar; hay un atajo "cancelar todas". Nada se cancela sin esa elección. Al guardar, las elegidas pasan a canceladas en unos instantes. |
| Fecha con Agenda custom | Calendario de atención, Agenda | Se distingue de un día normal y muestra su motivo; "no se atiende" se lee distinto de "horario recortado" y de "día abierto". |
| Fecha sin Agenda custom | Calendario de atención | Muestra los Tramos de la Agenda base de ese día de la semana. |
| Quitar Agenda custom | Calendario de atención | El día vuelve a la Agenda base; se dice cuál es. Si eso deja Citas fuera, aparece antes la misma lista de "Agenda custom que deja Citas fuera", con la elección de cancelar o conservar por Cita. |
| Cambio de Agenda base con Citas fuera | Configuración | Antes de guardar: lista de Citas que quedan fuera; no se cancelan solas. |
| Cambio de Agenda base con fechas custom | Configuración | Al guardar: lista de fechas con Agenda custom que no cambian. |
| Cita fuera de horario | Agendar cita | Aviso de que cae fuera de los Tramos vigentes o en un día cerrado; la nota pasa a ser obligatoria. No impide guardar. |
| Conflicto al agendar | Agendar cita | Solo dos reglas impiden guardar: traslapa otra Cita, o cae en el pasado. |
| Paciente reconocido | Agendar cita | Al capturar teléfono y nombre, indica que ya existe y muestra su última Cita. |

## Interaction Primitives

- Un toque para elegir; un botón principal para avanzar. Sin gestos ocultos.
- Se regresa con la flecha superior o con "Cambiar"; lo elegido se conserva.
- La fila de días se desplaza horizontalmente en móvil; es el único desplazamiento lateral.
- Las acciones destructivas siempre piden una segunda pantalla con dos botones de texto explícito.
- Enlaces externos: mapa y WhatsApp.
- **Prohibido:** ventanas emergentes de confirmación del navegador, carruseles, cuentas regresivas, pedir confirmar la Cita, mostrar precios.

## Accessibility Floor

- Controles reales: `button`, `a`, `input` con `label`; radios dentro de `fieldset` con leyenda.
- Cada día anuncia fecha completa y disponibilidad ("Martes 6 de octubre, 3 horarios libres"); la selección se expone con `aria-pressed`.
- La barra de progreso anuncia "Paso N de 3".
- Botones de solo icono con nombre accesible ("Regresar").
- Objetivo táctil de 44 px como mínimo.
- El estado nunca depende solo del color: los días llenos dicen "Lleno"; el estado de la Cita lleva texto.
- Los errores se anuncian y reciben el foco.
- El recorrido completo se puede hacer con teclado, en orden de lectura.
- El texto resiste ampliación al 200 % sin cortar controles `[ASSUMPTION]`.

## Responsive & Platform

- **Teléfono (referencia 390 px):** una columna, acción al pie, tres pasos separados.
- **Escritorio (contenedor de 1160 px):** encabezado del sitio con navegación y "Agendar cita"; reserva en dos columnas con el panel "Tu cita"; tipo y horario en el mismo paso; calendario mensual en vez de chips.
- Las rejillas colapsan solas a una columna; el sitio en teléfono es la misma página apilada.
- **Administración:** pensada para escritorio; en teléfono debe permitir al menos ver la Agenda del día y marcar No se presentó `[ASSUMPTION]`.

## Mensajes por correo

Sin mockups. Todos comparten encabezado con logotipo, la fecha y hora de la Cita en grande y un solo botón. Texto plano legible si las imágenes no cargan `[ASSUMPTION]`.

| Mensaje | Para | Contenido | Botón | FR |
|---|---|---|---|---|
| Cita confirmada | Paciente | Fecha, hora, lugar, mapa, archivo de calendario | Ver o cambiar mi cita | FR-21 |
| Recordatorio | Paciente | "Mañana tienes cita"; no pide confirmar | Ver o cambiar mi cita | FR-22 |
| Cita reprogramada | Paciente | Nueva fecha y hora | Ver o cambiar mi cita | FR-23 |
| Cita cancelada | Paciente | Confirmación; si fue tardía, lo reconoce con amabilidad | Agendar otra cita | FR-23 |
| Cancelada por el Consultorio | Paciente | Disculpa sincera; la Cita cancelada | Elegir otro horario | FR-23 |
| Cancelación tardía | Administrador | Paciente y hora de la Cita | Abrir la Agenda | FR-24 |
| Tus citas de hoy | Dra. Selina | Lista: hora, Paciente, motivo; o "No hay citas hoy" | ninguno | FR-25 |

## Key Flows

Los nombres siguen los recorridos del PRD (UJ-1 a UJ-7).

### UJ-1 — Mariana reserva su primera Cita desde el teléfono

1. Llega al sitio desde Google y toca "Agendar cita".
2. Tipo de consulta: "Primera consulta" ya está elegida; toca "Ver horarios disponibles".
3. Día y hora: ve los días desde mañana con sus horarios libres; toca jueves y luego 4:30 pm. El resumen al pie se actualiza.
4. Toca "Continuar".
5. Tus datos: escribe nombre, celular, correo y motivo; marca el Consentimiento.
6. Toca "Confirmar cita".
7. **Clímax:** "Tu cita está confirmada", con la fecha y hora en grande, el lugar, "Agregar a mi calendario" y "Guarda este enlace". Todo en menos de un minuto.

Falla: alguien tomó el horario un instante antes → en la misma pantalla ve el aviso y los horarios restantes; elige otro y vuelve a sus datos intactos para confirmar.

### UJ-2 — Mariana reprograma, y otro día cancela tarde

1. Abre el Enlace de gestión desde su correo: "Tu cita", estado "Agendada".
2. Toca "Reprogramar", elige otro día y hora, confirma.
3. **Clímax 1:** "Cita reprogramada" con la nueva fecha.
4. Semanas después, a una hora de otra Cita, abre el enlace: ya no hay "Reprogramar".
5. Toca "Cancelar cita": panel ámbar "Faltan menos de 2 horas" y la sugerencia de cancelar y volver a agendar.
6. Toca "Sí, cancelar mi cita".
7. **Clímax 2:** confirmación amable de la cancelación tardía, con "Agendar otra cita".

### UJ-3 — Gerardo registra un día de cirugía

1. En administración abre el Calendario de atención y toca el martes.
2. Ve los Tramos de la Agenda base (3:30–6:00) y los cambia a 3:30–5:00, con motivo "cirugía".
3. El sistema lista la Cita de las 5:30 que queda fuera y pregunta qué hacer con ella; si no tuviera correo, lo señala.
4. Elige cancelarla y guarda.
5. **Clímax:** el martes aparece marcado con su motivo, en unos instantes la Cita queda Cancelada por el Consultorio y el Paciente recibe la disculpa con el enlace para elegir otro horario. El miércoles sigue normal sin que nadie haga nada.

### UJ-4 — Gerardo captura una Cita pactada por WhatsApp

1. En la Agenda toca "Agendar cita".
2. Captura teléfono y nombre; el sistema indica que el Paciente ya existe.
3. Elige día y hora; puede salirse de la Rejilla.
4. **Clímax:** la Cita aparece en la Agenda como cualquier otra.

Falla: el horario traslapa otra Cita → mensaje con la regla incumplida.

### UJ-6 — Selina atiende un sábado

Para un Paciente concreto:

1. En la Agenda, Gerardo toca "Agendar cita" y elige el sábado a las 10:15.
2. El sistema avisa que está fuera de horario y pide la nota.
3. Escribe "lo pidió Selina, no puede entre semana" y guarda.
4. **Clímax:** la Cita aparece en la Agenda del sábado con su nota; el público sigue sin ver ese sábado.

Abierto al público:

1. En el Calendario de atención toca el sábado y le da Tramos 10:00–1:00, motivo "sábado abierto".
2. **Clímax:** ese sábado aparece con Horarios libres en la reserva; el siguiente sigue cerrado.

### UJ-7 — Selina cierra un sábado pero conserva una cita

1. En el Calendario de atención, Gerardo toca el sábado y le quita todos los Tramos, con motivo "asunto personal".
2. El sistema lista las cuatro Citas que quedan fuera.
3. Toca "cancelar todas" y cambia la de las 12:30 a "conservar".
4. Guarda.
5. **Clímax:** el sábado aparece como "no se atiende"; tres Citas pasan a Canceladas por el Consultorio y sus Pacientes reciben la disculpa; la de las 12:30 sigue en la Agenda y su Paciente no recibe nada.

### UJ-5 — Selina lee su correo de las 2:00 pm

1. Recibe "Tus citas de hoy".
2. **Clímax:** ve hora, Paciente y motivo de cada Cita de la tarde sin abrir nada más.

## Cobertura de mockups

| Con mockup | Solo texto |
|---|---|
| Sitio Inicio (escritorio y celular) | Aviso de privacidad |
| Reserva móvil: 3 pasos y confirmación | Cita reprogramada, Cita cancelada, Enlace no válido |
| Reserva escritorio: 3 pantallas | Cancelación normal (sin panel ámbar) |
| Tu cita; Cancelación tardía | Confirmada sin correo; horario tomado |
| | Toda la administración, incluido el Calendario de atención |
| | Todos los correos |

## Preguntas abiertas

Ninguna impide pasar a arquitectura; se avanza con los supuestos indexados abajo. Responsable: Gerardo. Revisar antes de construir la superficie afectada.

1. ¿"Tipo de consulta" (diseño) o "Tipo de visita" (PRD)? Hoy la interfaz dice consulta y el Glosario dice visita.
2. ¿El sitio queda como una sola página con secciones, o FR-27 pide páginas separadas?
3. ¿Qué vino manda: el de los mockups (`#6B2A45`) o el de la papelería (`#6D2D45`)?
4. El calendario de escritorio está dibujado con cinco columnas fijas de lunes a viernes. Debe poder mostrar cualquier día con Tramos: ¿siete columnas siempre, o solo los días que tengan atención en el mes?
5. ¿Qué pantallas de administración necesitan mockup antes de construirse?
6. ¿Se oscurece el borde de los campos para llegar a 3:1?
7. En el sitio, la cédula profesional dice "(UANL)" y la formación dice "Universidad Autónoma de San Luis Potosí": ¿cuál es el dato correcto?

## Índice de supuestos

- Foundation — el Administrador trabaja en escritorio.
- IA — el sitio es una página con secciones; navegación de administración de cuatro entradas.
- Component Patterns — "Continuar" deshabilitado sin hora; validación al salir del campo.
- State Patterns (Paciente) — sin horarios en la ventana; textos de error; tope semanal; variante sin correo; cancelación normal; enlace no válido.
- State Patterns (Administrador) — todos los tratamientos.
- Accessibility — ampliación al 200 %.
- Responsive — administración mínima en teléfono.
- Mensajes por correo — estructura común.
