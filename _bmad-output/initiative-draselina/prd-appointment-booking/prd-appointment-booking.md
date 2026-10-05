---
title: "Sistema de citas — Dra. Selina Medina"
status: final
created: 2026-10-04
updated: 2026-10-05
---

# PRD: Sistema de citas — Dra. Selina Medina

*Título de trabajo — por confirmar.*

## 0. Propósito del documento

Este PRD define **qué** debe hacer la versión piloto del sistema de citas del Consultorio de la Dra. Selina Medina (Ortopedia y Traumatología). Lo leen Gerardo (único desarrollador y Administrador) y los flujos posteriores de UX, arquitectura y tickets.

Se construye sobre `docs/Appointment Booking System — Solution Plan.md` (3 oct 2026), que ya contiene las decisiones acordadas con Selina y el diseño técnico. Este documento no repite el **cómo**: el modelo de dominio, la estructura de módulos, la tecnología, el hospedaje y los costos viven en ese plan y se indexan en `addendum.md`.

Estructura: el vocabulario se fija en el Glosario (§3) y se usa literalmente; las funciones se agrupan en §4 con requisitos numerados de forma global (FR-1 a FR-30); lo inferido sin confirmación lleva la etiqueta `[ASSUMPTION]` y se indexa en §11.

**Nivel de rigor:** piloto primero. Se lanza con pocos pacientes reales, se observa y después se endurece. La privacidad no se aligera: el Motivo de consulta es un dato de salud desde la primera Cita.

## 1. Visión

Hoy toda la agenda de la Dra. Selina vive en WhatsApp: cada Cita, cada cambio y cada cancelación pasa por ella. El sistema le quita esa carga: los Pacientes reservan, reprograman y cancelan por sí mismos desde su teléfono, sin cuenta y sin instalar nada, y las excepciones las atiende el Administrador desde una sola pantalla.

Selina no usa el sistema. Solo recibe, cada día de atención, un correo con sus Citas de hoy. Su agenda se llena y se corrige sin que ella intervenga.

**Criterio de diseño:** ante cualquier duda, gana la opción que le quite trabajo a Selina.

## 2. Usuarios

### 2.1 Trabajos por resolver

- **Dra. Selina:** "Quiero atender Pacientes, no administrar mi agenda." Quiere saber cada día a quién verá, sin abrir ningún sistema.
- **Paciente:** "Quiero apartar un horario seguro en menos de un minuto, desde el teléfono, sin esperar a que alguien me conteste." Y poder cambiarlo o cancelarlo sin dar explicaciones.
- **Administrador (Gerardo):** "Quiero resolver las excepciones (días fuera de lo normal, ausencias, Citas pactadas por WhatsApp) en un solo lugar y enterarme de inmediato cuando algo deja una silla vacía."

### 2.2 No usuarios (piloto)

- **Selina como usuaria:** no tiene cuenta ni pantalla; solo recibe correo.
- **Asistente:** rol futuro con el mismo acceso que el Administrador; no se construye.
- **Aseguradoras** (GNP, AXA, Chubb) y sus reglas.

### 2.3 Recorridos clave

Los nombres de Pacientes son ilustrativos `[ASSUMPTION: los recorridos se redactaron a partir del plan; no provienen de sesiones reales narradas por Gerardo]`.

- **UJ-1. Mariana reserva su primera Cita desde el teléfono.** Mariana, con dolor de rodilla desde hace semanas, llega al sitio del Consultorio desde Google. Sin cuenta, toca "Agendar cita", elige el Tipo de visita, ve los Horarios libres desde mañana, elige jueves 4:30 pm, escribe nombre, teléfono, correo y Motivo de consulta, y acepta el aviso de privacidad. En menos de un minuto ve su Cita confirmada en pantalla con el Enlace de gestión y la opción de agregarla a su calendario; después le llega el correo de confirmación. **Caso límite:** otra persona toma el mismo horario un instante antes; Mariana ve los Horarios libres restantes, no un error.
- **UJ-2. Mariana reprograma, y otro día cancela tarde.** Desde el Enlace de gestión de su correo, Mariana mueve su Cita al viernes, tres días antes. Semanas después, a una hora de otra Cita, ya no puede reprogramar: el enlace le ofrece cancelar y volver a agendar. Cancela; la confirmación reconoce con amabilidad que fue con menos de 2 horas de anticipación y Gerardo recibe la alerta de inmediato.
- **UJ-3. Gerardo registra un día de cirugía.** Selina avisa que el martes opera a las 5:00. Gerardo, con sesión iniciada, toca ese martes en el calendario de atención y le da Agenda custom: 3:30 a 5:00, motivo "cirugía". El sistema le muestra que una Cita de las 5:30 queda fuera y le pregunta qué hacer con ella; Gerardo elige cancelarla, y el Paciente recibe una disculpa con un enlace para elegir otro horario. Si esa Cita no tuviera correo aparecería marcada "sin correo" y Gerardo avisaría a mano. El miércoles vuelve solo a la Agenda base.
- **UJ-4. Gerardo captura una Cita pactada por WhatsApp.** Un Paciente de siempre escribe a Selina por WhatsApp. Gerardo abre "Agendar cita" en administración, captura nombre, teléfono y horario, y el sistema reconoce al Paciente existente. La Cita queda en la agenda como cualquier otra.
- **UJ-6. Selina atiende un sábado.** Dos casos. Selina le dice a un Paciente "te puedo atender el sábado": Gerardo registra la Cita a mano para ese sábado a las 10:15, con la nota "lo pidió Selina, no puede entre semana"; nadie más ve ese sábado como disponible. Otra semana Selina dice "abramos este sábado, a ver si cae algún paciente": Gerardo le da Agenda custom a ese sábado, 10:00 a 1:00, y ese día aparece con Horarios libres para el público; el sábado siguiente sigue cerrado.
- **UJ-7. Selina cierra un sábado pero conserva una cita.** Selina tiene un asunto personal: "ciérrame el sábado y cancela todas las consultas, excepto la de las 12:30, que es importante". Gerardo le da a ese sábado una Agenda custom sin tramos. El sistema lista las cuatro Citas que quedan fuera; Gerardo marca tres para cancelar y conserva la de las 12:30. Los tres Pacientes reciben la disculpa con el enlace para reagendar; el de las 12:30 no recibe nada y su Cita sigue en pie. El sábado ya no ofrece Horarios libres al público.
- **UJ-5. Selina lee su correo de las 2:00 pm.** Sin abrir nada más, Selina ve "Tus citas de hoy": hora, Paciente y Motivo de consulta de cada Cita de la tarde.

## 3. Glosario

Los términos se usan exactamente así en todo el documento y en los flujos posteriores.

- **Consultorio** — La práctica de la Dra. Selina Medina; una sola ubicación en el piloto.
- **Paciente** — Persona que tiene o tuvo una Cita. Se identifica por teléfono y nombre; no tiene cuenta.
- **Administrador** — Único usuario con sesión; en el piloto, Gerardo.
- **Cita** — Espacio reservado en la agenda para un Paciente. Estados: Agendada, No se presentó, Cancelada. Atendida se deriva (ver abajo).
- **Consulta** — El acto médico que ocurre cuando el Paciente llega a su Cita. El sistema no registra su contenido.
- **Tipo de visita** — Clase de Cita que el Paciente elige al reservar; define la duración (30 min en el piloto).
- **Motivo de consulta** — Texto breve que el Paciente escribe al reservar. Es un dato de salud.
- **Agenda base** — La semana tipo de Selina, sin fechas: por cada día de la semana, cero o más tramos de atención. Un día sin tramos no se atiende. Se repite sola (hoy: lunes a viernes, 3:30–6:00 pm).
- **Agenda custom** — El horario propio de una fecha concreta, que reemplaza por completo a la Agenda base ese día: cero o más tramos que no se enciman, más un motivo. Cero tramos significa que ese día no se atiende. A lo más una por fecha.
- **Tramo** — Periodo continuo de atención dentro de un día, con hora de inicio y de fin.
- **Horario libre** — Hora de inicio en la que hoy se puede reservar: dentro de un Tramo vigente para esa fecha (el de la Agenda custom si existe; si no, el de la Agenda base), sin Cita Agendada y dentro de la Ventana de reserva. Se calcula al momento; nunca se guarda.
- **Ventana de reserva** — Periodo en el que un Paciente puede reservar: desde mañana (día calendario) hasta 2 meses adelante.
- **Atendida** — Cita Agendada cuya hora ya pasó. Se calcula, no se guarda.
- **No se presentó** — Estado final que marca el Administrador cuando el Paciente no llegó.
- **Cancelación tardía** — Cancelación hecha por el Paciente con menos de 2 horas de anticipación.
- **Cancelada por el Consultorio** — Cancelación originada por el Administrador, sea directa o porque eligió cancelarla al guardar o borrar una Agenda custom que la dejó fuera.
- **Enlace de gestión** — Enlace único y privado de una Cita, con el que el Paciente la ve, la reprograma o la cancela sin cuenta.
- **Rejilla** — Horas de inicio que se ofrecen al Paciente dentro de cada Tramo, en pasos iguales a la duración del Tipo de visita (30 min en el piloto).
- **Recordatorio** — Mensaje informativo al Paciente el día anterior a su Cita. No pide confirmación.
- **Agenda del día** — Correo "Tus citas de hoy" que recibe Selina.
- **Consentimiento** — Aceptación expresa del aviso de privacidad, con fecha y hora.

## 4. Funciones

### 4.1 Reserva en línea

**Descripción:** Cualquier persona, sea Paciente nuevo o subsecuente, reserva una Cita desde el sitio público, sin cuenta. Una Cita reservada en línea queda firme al instante: no existe aprobación ni estado "solicitada". Realiza UJ-1.

#### FR-1: Elegir Tipo de visita

El Paciente puede elegir un Tipo de visita entre los marcados como reservables en línea.

**Consecuencias (verificables):**
- Todos los Tipos de visita duran 30 min en el piloto; la duración es configurable por tipo.
- No se muestran precios.
- Los tipos no reservables en línea no aparecen.
- El piloto arranca con dos Tipos de visita públicos, según el diseño de Gerardo: "Primera consulta" y "Consulta de seguimiento".

#### FR-2: Ver Horarios libres

El Paciente puede ver únicamente los Horarios libres dentro de la Ventana de reserva.

**Consecuencias (verificables):**
- Los horarios ofrecidos se calculan al momento: los Tramos de la Agenda custom de la fecha si existe (FR-13); si no, los de la Agenda base (FR-12). Nunca salen de valores fijos ni de registros creados por adelantado. Con la Agenda base actual, cada día de atención ofrece como máximo 3:30, 4:00, 4:30, 5:00 y 5:30 pm.
- Toda Cita reservada en línea cabe completa dentro de un Tramo.
- No aparecen horarios de hoy, de fechas sin Tramos, ni posteriores a 2 meses.
- No aparecen horarios que traslapen una Cita Agendada, incluidas las Citas manuales fuera de horario (FR-15).
- Las horas se muestran en hora de Ciudad de México.

#### FR-3: Reservar con datos de contacto y Consentimiento

El Paciente puede reservar un Horario libre dando nombre, teléfono, correo (opcional) y Motivo de consulta, y aceptando el aviso de privacidad.

**Consecuencias (verificables):**
- Nombre, teléfono, Motivo de consulta y Consentimiento son obligatorios; el correo no.
- Un teléfono imposible para México se rechaza antes de reservar.
- Sin Consentimiento no hay reserva; se guarda la fecha y hora del Consentimiento.
- La reserva completa toma menos de 1 minuto a un Paciente en su teléfono.

#### FR-4: Cita firme, sin doble reserva

El sistema garantiza que dos Citas Agendadas nunca se traslapen.

**Consecuencias (verificables):**
- Si dos personas intentan el mismo horario a la vez, exactamente una obtiene la Cita.
- A la otra se le muestran los Horarios libres restantes, no un mensaje de error.
- La garantía cubre también las Citas creadas por el Administrador fuera de la rejilla de 30 min.

#### FR-5: Confirmación en pantalla

Tras reservar, el Paciente ve su Cita completa en pantalla. Realiza UJ-1.

**Consecuencias (verificables):**
- La pantalla muestra fecha, hora, dirección, el Enlace de gestión con el aviso "guarda este enlace" y la opción de agregar la Cita a su calendario.
- El evento de calendario incluye el Enlace de gestión.
- Esto ocurre aunque el Paciente no haya dado correo.

#### FR-6: Reconocer al Paciente

El sistema reconoce a un Paciente existente por teléfono y nombre, o lo registra como nuevo.

**Consecuencias (verificables):**
- Mismo teléfono y mismo nombre (sin distinguir mayúsculas, acentos ni espacios de más) = mismo Paciente.
- Mismo teléfono con otro nombre = Paciente distinto (una madre que agenda para sus hijos).
- Límite aceptado: un error de dedo en el nombre crea un duplicado.

**Fuera de alcance:** fusionar Pacientes duplicados (después del piloto).

#### FR-7: Protección contra abuso

El sistema limita las reservas automatizadas o excesivas.

**Consecuencias (verificables):**
- Las páginas de reserva tienen límite de frecuencia y una verificación anti-bots.
- Un mismo teléfono no puede superar un tope semanal de Citas. `[ASSUMPTION: tope de 3 Citas por teléfono por semana, configurable; el plan no fija el valor]`

### 4.2 Gestión de la Cita por el Paciente

**Descripción:** Con su Enlace de gestión, el Paciente ve, reprograma o cancela esa única Cita, sin cuenta. Cancelar siempre es posible; reprogramar tiene un corte para que una Cancelación tardía no se disfrace de reprogramación. Realiza UJ-2.

#### FR-8: Ver la Cita con el Enlace de gestión

El Paciente puede ver su Cita con el Enlace de gestión.

**Consecuencias (verificables):**
- El enlace da acceso solo a esa Cita.
- El enlace deja de funcionar después de la hora de la Cita.
- El enlace no es adivinable.

#### FR-9: Reprogramar

El Paciente puede reprogramar su Cita hasta 2 horas antes de su inicio.

**Consecuencias (verificables):**
- El nuevo horario cumple las mismas reglas que una reserva nueva (FR-2, FR-4).
- La Cita sigue Agendada; solo cambia de horario.
- Dentro de las 2 horas previas, el enlace ofrece cancelar y volver a agendar, no reprogramar.

#### FR-10: Cancelar

El Paciente puede cancelar su Cita en cualquier momento antes de su inicio.

**Consecuencias (verificables):**
- Nunca se bloquea una cancelación, ni dentro de las 2 horas previas.
- Una cancelación con menos de 2 horas de anticipación queda marcada como Cancelación tardía.
- El horario cancelado vuelve a ser Horario libre de inmediato (si cae dentro de la Ventana de reserva).

### 4.3 Agenda del Administrador

**Descripción:** El Administrador opera las excepciones desde un área con sesión. Realiza UJ-3 y UJ-4.

#### FR-11: Acceso del Administrador

Solo el Administrador puede entrar al área de administración.

**Consecuencias (verificables):**
- Requiere verificación en dos pasos.
- No existe registro público de cuentas.

#### FR-12: Configurar la Agenda base

El Administrador puede configurar, desde administración y sin intervención de desarrollo, la semana tipo: qué días se atiende y en qué Tramos.

**Consecuencias (verificables):**
- Cada día de la semana, incluidos sábado y domingo, admite cero o más Tramos (por ejemplo 10:00–13:00 y 15:30–18:00).
- Los Tramos de un mismo día no se enciman; en cada uno el fin es posterior al inicio y cabe al menos una Cita del Tipo de visita más corto.
- Se captura una vez y se repite sola; no se renueva ni se habilita por periodos.
- Un mismo horario puede aplicarse a varios días en una sola acción.
- El cambio aplica de inmediato a todas las fechas futuras sin Agenda custom; no existe fecha de inicio futura.
- Las Citas Agendadas que queden fuera de la nueva Agenda base se respetan: el Administrador ve cuáles son antes de guardar y decide qué hacer con cada una.
- Las fechas futuras con Agenda custom no heredan el cambio; el Administrador ve cuáles son al guardar.
- Valor inicial: lunes a viernes, 3:30–6:00 pm.

#### FR-13: Dar Agenda custom a una fecha

El Administrador puede dar a una fecha concreta un horario propio que reemplaza a la Agenda base ese día, para recortarlo, cerrarlo o abrir un día que normalmente no se atiende. Realiza UJ-3, UJ-6 y UJ-7.

**Consecuencias (verificables):**
- Una fecha tiene a lo más una Agenda custom, con cero o más Tramos que no se enciman y un motivo. Cero Tramos: ese día no se atiende. En cada Tramo cabe al menos una Cita del Tipo de visita más corto, igual que en la Agenda base.
- Puede registrarse con cualquier anticipación (vacaciones de diciembre capturadas en octubre).
- Puede aplicarse la misma Agenda custom a varias fechas en una sola acción.
- Editarla cambia el día; borrarla lo devuelve a la Agenda base.
- Borrarla también puede dejar Citas fuera (por ejemplo, al quitar la Agenda custom que abría un sábado): el sistema pregunta qué hacer con cada una, igual que al guardarla.
- No se repite: una fecha abierta por excepción no abre las siguientes.
- Si guardarla o borrarla deja fuera Citas Agendadas, el Administrador ve la lista antes de confirmar, con las que no tienen correo señaladas, y elige para cada una si se cancela o se conserva. Puede cancelarlas todas, ninguna, o todas menos una.
- Ninguna Cita se cancela sin esa elección explícita. Las elegidas quedan Canceladas por el Consultorio poco después de guardar, registradas a nombre del Administrador, y cada Paciente recibe el aviso de FR-23. Las conservadas no cambian y su Paciente no recibe ningún aviso. `[ASSUMPTION: las Citas manuales (FR-15) vienen marcadas "conservar"; las reservadas en línea no vienen preseleccionadas]`
- El sistema nunca aparta al Paciente afectado un horario nuevo; él lo elige.

**Fuera de alcance:** reglas recurrentes para excepciones (por ejemplo "el primer sábado de cada mes").

#### FR-14: Ver la agenda

El Administrador puede ver la agenda por día y por semana.

**Consecuencias (verificables):**
- Una Cita reservada en línea aparece sin recargar manualmente.
- Cada Cita muestra hora, Paciente, Motivo de consulta y estado (incluida Atendida, derivada).
- Las Citas de Pacientes sin correo se marcan "sin correo".
- Las fechas con Agenda custom se distinguen a la vista y muestran su motivo.
- Las Citas manuales fuera de horario se distinguen y muestran su nota.

#### FR-15: Agendar una Cita manualmente

El Administrador puede crear una Cita para un Paciente que la pidió por teléfono o WhatsApp, o que Selina aceptó atender fuera de horario. Realiza UJ-4 y UJ-6.

**Consecuencias (verificables):**
- Puede agendar fuera de la Rejilla, fuera de la Ventana de reserva (incluido hoy), fuera de los Tramos vigentes y en una fecha marcada como no se atiende.
- Cuando la Cita cae fuera de los Tramos vigentes, una nota con el motivo es obligatoria y el sistema avisa que está fuera de horario.
- Nunca puede traslapar otra Cita ni agendar en el pasado.
- Una Cita manual no abre disponibilidad: el público no ve Horarios libres por ella.
- Una Cita manual no se cancela por una Agenda custom salvo que el Administrador lo elija expresamente (FR-13).
- Se aplica FR-6 para reconocer al Paciente.
- `[ASSUMPTION: al capturar la Cita, el Administrador registra que el Paciente dio su Consentimiento por el canal externo]`

#### FR-16: Marcar No se presentó

El Administrador puede marcar una Cita como No se presentó.

**Consecuencias (verificables):**
- Puede hacerlo en cualquier momento después de la hora de la Cita.
- Es un estado final.

#### FR-17: Cancelar por el Consultorio

El Administrador puede cancelar una Cita individual.

**Consecuencias (verificables):**
- La Cita queda Cancelada por el Consultorio y el Paciente recibe el aviso de FR-23.

#### FR-18: Lista de Pacientes

El Administrador puede consultar una lista de Pacientes con su historial de Citas.

**Consecuencias (verificables):**
- Puede buscar por nombre.
- El historial distingue Atendida, No se presentó, Cancelada y Cancelación tardía, para identificar a quien cancela tarde repetidamente.

#### FR-19: Configurar las reglas

El Administrador puede cambiar las reglas de la agenda sin intervención de desarrollo.

**Consecuencias (verificables):**
- Son configurables, además de la Agenda base (FR-12) y la Agenda custom (FR-13): anticipación mínima, Ventana de reserva, umbral de Cancelación tardía (un solo valor, hoy 2 horas, que también es el corte para reprogramar), tope semanal por teléfono, hora del Recordatorio y hora de la Agenda del día.
- Son configurables los Tipos de visita: nombre, duración y si son reservables en línea.

#### FR-20: Bitácora de cambios

El sistema registra quién cambió cada Cita y cuándo.

**Consecuencias (verificables):**
- Cada reserva, reprogramación, cancelación y marca de No se presentó queda registrada con su autor (Paciente, Administrador o sistema).
- Cada cambio a la Agenda base y a una Agenda custom queda registrado con su autor y fecha.

### 4.4 Notificaciones

**Descripción:** En el piloto el único canal es el correo, y solo para Pacientes que lo dieron. Los mensajes informan; ninguno pide confirmar. Realiza UJ-1, UJ-2, UJ-3 y UJ-5.

#### FR-21: Confirmación de Cita

El Paciente con correo recibe una confirmación al quedar Agendada su Cita.

**Consecuencias (verificables):**
- Incluye fecha, hora, dirección, enlace al mapa, archivo de calendario y Enlace de gestión.
- `[ASSUMPTION: las Citas creadas por el Administrador (FR-15) envían la misma confirmación]`

#### FR-22: Recordatorio

El Paciente con correo recibe un Recordatorio el día anterior a su Cita, a las 9:00 am.

**Consecuencias (verificables):**
- Solo informa; la Cita sigue firme sin respuesta.
- Si al agendar esa hora ya pasó, se omite.
- Si la Cita se reprograma o se cancela, el Recordatorio se ajusta o se elimina.

#### FR-23: Aviso de cambios

El Paciente con correo recibe un aviso cuando su Cita se reprograma o se cancela.

**Consecuencias (verificables):**
- Reprogramada: nueva fecha y hora.
- Cancelada por el Paciente: confirmación; si fue Cancelación tardía, lo menciona con amabilidad.
- Cancelada por el Consultorio: disculpa sincera en nombre del Consultorio y enlace para elegir cualquier Horario libre.

#### FR-24: Alerta de Cancelación tardía

El Administrador recibe una alerta inmediata ante una Cancelación tardía.

**Consecuencias (verificables):**
- La alerta sale en cuanto ocurre la cancelación, sin esperar a un proceso periódico, e indica Paciente y hora de la Cita.
- `[ASSUMPTION: la alerta llega por correo al Administrador]`

#### FR-25: Agenda del día

Selina recibe la Agenda del día a las 2:00 pm de cada fecha que tenga Tramos o al menos una Cita Agendada. `[ASSUMPTION: incluye sábados abiertos por Agenda custom y días con solo una Cita manual; si la primera Cita del día es antes de las 2:00 pm, el correo llega tarde para ella]`

**Consecuencias (verificables):**
- Lista hora, Paciente y Motivo de consulta de cada Cita Agendada de ese día.
- `[ASSUMPTION: si no hay Citas, el correo se envía igual diciendo que no hay Citas hoy, para que la ausencia de correo nunca sea ambigua]`

#### FR-26: La reserva nunca espera al correo

El sistema entrega las notificaciones sin afectar la reserva.

**Consecuencias (verificables):**
- Una falla o lentitud del proveedor de correo no impide ni retrasa una reserva.
- Los envíos fallidos se reintentan.

### 4.5 Sitio del Consultorio

**Descripción:** La página de inicio del sistema es el sitio del Consultorio: mismo dominio, con "Agendar cita" siempre a la mano. `[ASSUMPTION: el sitio entra en el piloto porque es la puerta de entrada a la reserva; añade alrededor de una semana]`

#### FR-27: Páginas del Consultorio

El visitante puede consultar: Inicio, Sobre mí, Padecimientos y tratamientos, Ubicación y horarios, y Aviso de privacidad.

**Consecuencias (verificables):**
- Ubicación y horarios muestra dirección, mapa, los días y horas de la Agenda base y contacto de WhatsApp.
- Se muestran nombre, cédula profesional y especialidad de la doctora.
- No hay testimonios ni fotos de antes/después.

#### FR-28: Agendar desde cualquier página

El visitante puede iniciar la reserva desde cualquier página con el botón "Agendar cita".

**Consecuencias (verificables):**
- El botón es visible sin desplazarse en la pantalla de un teléfono, en todas las páginas de FR-27.
- Un toque lleva al primer paso de la reserva (FR-1).

#### FR-29: Ser encontrable

El sitio puede ser encontrado en buscadores por quien busca la especialidad en la ciudad.

**Consecuencias (verificables):**
- Cada página pública tiene un título propio en español que incluye especialidad y ciudad.
- Las páginas públicas son legibles por buscadores sin ejecutar la interfaz interactiva.
- Las páginas de administración y los Enlaces de gestión no son indexables.

#### FR-30: Contenido actualizable

Gerardo puede actualizar los textos del sitio sin rediseñarlo. `[ASSUMPTION: en el piloto basta editarlos como parte de un despliegue; la edición desde administración queda para después]`

## 5. Requisitos transversales

- **Móvil primero y en español.** Todo el recorrido del Paciente funciona en la pantalla de un teléfono, sin instalar nada.
- **Hora de Ciudad de México.** Toda hora mostrada o enviada está en America/Mexico_City.
- **Tono.** Los mensajes al Paciente son amables y directos; la disculpa por una Cancelada por el Consultorio es sincera, y la mención de una Cancelación tardía nunca regaña.
- **Disponibilidad.** `[ASSUMPTION: sin compromiso formal de disponibilidad en el piloto; basta alerta de errores al Administrador y respaldo diario de datos]`

## 6. Privacidad y cumplimiento

- **Datos de salud.** El Motivo de consulta es dato sensible bajo la ley mexicana de protección de datos personales: aviso de privacidad publicado, casilla de Consentimiento expreso y registro de su fecha desde la primera Cita del piloto.
- **Datos mínimos.** El sistema guarda solo datos de agenda; ningún diagnóstico ni nota clínica. Así evita las reglas del expediente clínico electrónico (NOM-024) hasta esa fase.
- **Acceso.** Solo el Administrador ve datos de Pacientes; el Enlace de gestión expone una sola Cita.
- **Protección.** Conexiones cifradas, datos cifrados en reposo y respaldo diario.
- **Publicidad médica.** Confirmar los requisitos vigentes de COFEPRIS antes de publicar el sitio.
- **Revisión legal.** Un abogado revisa el texto del aviso de privacidad antes de recibir al primer Paciente.

## 7. No objetivos

- No es un expediente clínico: no guarda diagnósticos, notas ni tratamientos.
- No cobra: sin anticipos, pagos ni precios visibles.
- No pide al Paciente confirmar su Cita.
- No aprueba Citas: toda reserva en línea es firme.
- No da cuenta ni pantalla a Selina.
- No reemplaza WhatsApp como canal de conversación; solo saca de ahí la agenda.

## 8. Alcance del piloto

### 8.1 Dentro

- Reserva en línea (§4.1) y gestión por el Paciente (§4.2).
- Agenda del Administrador (§4.3), incluida la captura de las Citas ya pactadas por WhatsApp antes de abrir.
- Notificaciones por correo y Agenda del día (§4.4).
- Sitio del Consultorio (§4.5).
- Aviso de privacidad y Consentimiento (§6).

### 8.2 Fuera

- **Recordatorios por WhatsApp** — fase 2; requiere cuenta de negocio de Meta y plantillas aprobadas. `[NOTE FOR PM]` Los Pacientes viven en WhatsApp y los que no dan correo no reciben ningún aviso: es la carencia más sensible del piloto.
- **Reglas de aseguradoras** — fase posterior.
- **Anticipos o Citas prepagadas** — fase posterior.
- **Expediente clínico** — después del piloto, tras evaluar construir contra integrar un sistema certificado.
- **Rol de Asistente** — previsto, no construido.
- **Fusionar Pacientes duplicados** — después del piloto.
- **Política de retención y derechos ARCO** — después del piloto.
- **Confirmación confiable de datos de contacto** (teléfono o correo mal escritos) — fase 2; en el piloto se mitiga con FR-5.
- **Varias ubicaciones** — una sola en el piloto.

## 9. Métricas de éxito

Todas las metas son `[ASSUMPTION]`: el plan no define cómo se evalúa el piloto.

**Definición del piloto** `[ASSUMPTION: 4 semanas con todos los Pacientes que pidan Cita en ese periodo; al terminar se decide continuar, ajustar o volver a WhatsApp]`

**Primarias**
- **SM-1: Intervenciones de Selina en la agenda** — veces por semana que Selina tiene que agendar, mover o cancelar una Cita. Meta: 0 en las últimas 2 semanas del piloto. Valida FR-3, FR-9, FR-10, FR-25.
- **SM-2: Citas reservadas por el propio Paciente** — proporción de Citas creadas en línea frente a las capturadas por el Administrador. Meta: 70 % o más en las últimas 2 semanas. Valida FR-1 a FR-5.

**Secundarias**
- **SM-3: Dobles reservas** — meta: 0. Valida FR-4.
- **SM-4: Tiempo de reserva** — mediana menor a 1 minuto desde abrir "Agendar cita" hasta la confirmación. Valida FR-3.

**Contramétricas (no optimizar)**
- **SM-C1: Minutos semanales del Administrador en la agenda** — si SM-1 baja solo porque el trabajo pasó de Selina a Gerardo, el sistema no resolvió el problema. Contrapesa SM-1.
- **SM-C2: Tasa de No se presentó y de Cancelación tardía** — no debe subir respecto a la época de WhatsApp; reservar fácil no debe volver desechables las Citas. Contrapesa SM-2.
- **SM-C3: Pacientes que siguen pidiendo Cita por WhatsApp** — no se les rechaza para inflar SM-2; el Administrador las captura (FR-15).

## 10. Preguntas abiertas

Ninguna impide pasar a UX o arquitectura; el PRD avanza con los supuestos de §11. Responsable de todas: Gerardo.

**Resolver antes de recibir al primer Paciente**

1. ¿Qué Tipos de visita existen además de la Consulta general, y cuáles son reservables en línea? El plan sugiere dejar posquirúrgicos y trauma urgente como "llámanos".
2. ¿Cuánto dura el piloto, con qué Pacientes y qué resultado decide continuar?
3. ¿Cómo se avisa a los Pacientes sin correo cuando el Consultorio cancela, más allá de la marca "sin correo"?
4. ¿El sitio del Consultorio entra completo en el piloto, o basta Inicio, Ubicación y Aviso de privacidad mientras Selina entrega el contenido pendiente?
5. ¿Quién es el abogado que revisa el aviso de privacidad y para cuándo? Es condición de lanzamiento (§6).
6. ¿El Motivo de consulta debe viajar por correo en la Agenda del día, o basta hora y Paciente?
7. ¿Qué límite de frecuencia y qué tope semanal por teléfono se usan en FR-7?

**Resolver al cerrar el piloto**

8. ¿Hay línea base de la época de WhatsApp (Citas por semana, ausencias) para comparar SM-C2?
9. ¿A qué hora debe salir la Agenda del día cuando hay Citas por la mañana (FR-25)?

## 11. Índice de supuestos

- §2.3 — Los recorridos se redactaron desde el plan, no desde sesiones reales.
- FR-7 — Tope de 3 Citas por teléfono por semana.
- FR-15 — El Administrador registra el Consentimiento dado por canal externo.
- FR-21 — Las Citas del Administrador envían confirmación.
- FR-24 — La alerta de Cancelación tardía llega por correo.
- FR-25 — La Agenda del día se envía aunque no haya Citas; se envía en toda fecha con Tramos o Citas, a las 2:00 pm.
- §4.5 — El sitio del Consultorio entra en el piloto.
- FR-30 — El contenido se actualiza por despliegue.
- §5 — Sin compromiso formal de disponibilidad.
- §9 — Duración del piloto y todas las metas.
