# Reconciliación de entradas contra el spine de arquitectura

Fecha: 2026-10-04
Spine revisado: `../architecture-appointment-booking.md` (AD-1 a AD-22, estado *draft*) y su `.memlog.md`.

Entradas, en orden de autoridad:

1. PRD — `../../prd-appointment-booking/prd-appointment-booking.md`
2. Decisión de disponibilidad — `../../brainstorm-agenda-horario-configuracion/brainstorm-agenda-horario-configuracion.md`
3. UX — `../../ux-appointment-booking/EXPERIENCE.md`
4. Plan de solución original — `docs/Appointment Booking System — Solution Plan.md`

## Cómo leer este reporte

- Solo se listan cosas que **no aterrizaron**: no están en un AD, ni en *Deferred*, ni en *Open Questions*.
- No se reportan los reemplazos intencionales (.NET 11, DataAnnotations, sin Serilog, Aspire, Agenda base/custom, cita manual fuera de horario, sin tabla Settings, sin tabla Location) ni el detalle táctico que el spine deja a propósito en el plan (campos, algoritmo, costos, `BufferMin`, `InsurerId`, `Price`).
- Cada hallazgo trae: fuente, qué falta, y destino recomendado (**AD** nuevo o ajuste de un AD, **Deferred**, **Open Question**, o **Convención**).
- Severidad: **Alta** = dos partes construidas por separado quedarían incompatibles o se rompe un requisito; **Media** = ambigüedad que alguien resolverá por su cuenta; **Baja** = conviene dejarlo escrito.

Total: **47 hallazgos** (10 de severidad alta, 22 media, 15 baja).

| Entrada | Hallazgos |
| --- | --- |
| 1. PRD | 14 (P-1 a P-14) |
| 2. Decisión de disponibilidad | 7 (B-1 a B-7) |
| 3. UX | 10 (U-1 a U-10) |
| 4. Plan original | 9 (L-1 a L-9) |
| 5. Requisitos cualitativos | 4 (Q-1 a Q-4) |
| 6. Contradicciones spine ↔ PRD | 3 (C-1 a C-3) |

---

## 1. PRD

### P-1 — El Enlace de gestión debe viajar en correos, pero solo existe su hash · Alta · AD

- **Fuente:** FR-5 (pantalla y evento de calendario con el enlace), FR-21 (confirmación con Enlace de gestión y archivo de calendario), FR-22 y FR-23 (los correos de UX llevan el botón "Ver o cambiar mi cita"); EXPERIENCE.md, tabla *Mensajes por correo*.
- **Qué falta:** AD-19 guarda solo el hash SHA-256 del token y AD-3 dice que los eventos llevan "identificadores y tiempos". Con esas dos reglas, Notificaciones no tiene forma de construir el enlace: ni en la confirmación (el token en claro solo existe dentro del handler de Agenda), ni en el Recordatorio (se envía un día antes, mucho después de creado), ni en el aviso de reprogramación. El plan original tiene el mismo hueco; el spine lo heredó. Las salidas posibles se excluyen entre sí y cada una toca módulos distintos: (a) el token en claro viaja en el evento, y entonces queda en claro en las tablas del outbox de Wolverine, lo que anula "solo se guarda el hash"; (b) el token se deriva de forma determinista (HMAC del `CitaId` con un secreto del servidor) y Agenda lo entrega por contrato a quien lo pida; (c) se guarda cifrado y reversible. También falta decir quién conoce la URL pública base para armar el enlace.
- **Destino:** ampliar AD-19 con la regla de cómo obtiene el enlace cada consumidor (pantalla de confirmación, `.ics`, cada correo) y qué pasa con el outbox.

### P-2 — El Consentimiento no tiene dueño ni forma · Alta · AD

- **Fuente:** FR-3 ("se guarda la fecha y hora del Consentimiento"), FR-15 (supuesto: el Administrador registra el Consentimiento dado por canal externo), §6 *Datos de salud*, Glosario *Consentimiento*; plan, tabla de datos (`Patient.ConsentAt`) y *Selina's decisions* (ConsentAt desde el MVP).
- **Qué falta:** el spine no menciona el Consentimiento en ningún AD. Queda sin decidir: (1) qué módulo lo guarda (el plan lo ponía en Pacientes; el Motivo de consulta, que es lo que lo exige, vive en la Cita de Agenda); (2) si es uno por Paciente o uno por Cita — un Paciente subsecuente vuelve a marcar la casilla en cada reserva, y con un solo `ConsentAt` se pierde el primero o el último; (3) si se guarda el canal (en línea / externo registrado por el Administrador) y la versión del aviso aceptada; (4) si entra en la bitácora. Es el único registro con valor legal del piloto.
- **Destino:** AD nuevo ("Consentimiento: dueño, granularidad y canal").

### P-3 — El Motivo de consulta no tiene reglas de manejo como dato sensible · Alta · AD

- **Fuente:** §0 ("la privacidad no se aligera"), §6 *Datos de salud* y *Datos mínimos*, FR-25 (el motivo viaja por correo a Selina), pregunta abierta 6 del PRD.
- **Qué falta:** AD-3 prohíbe nombre, teléfono y correo en los eventos, pero no nombra el Motivo de consulta ni la nota de la cita manual. Ningún AD dice: qué módulo es dueño del motivo (se infiere Agenda); que no puede ir en eventos, ni en el outbox, ni en registros o trazas. AD-21 activa OpenTelemetry con ServiceDefaults: la instrumentación HTTP registra la ruta y la cadena de consulta, y los comandos fallidos pueden volcar su contenido. Sin regla, el token del Enlace de gestión (va en la URL) y los datos del formulario pueden terminar en Application Insights.
- **Destino:** AD nuevo ("Datos sensibles: dónde viven y dónde nunca aparecen"), que cubra eventos, outbox, telemetría y URL. La pregunta 6 del PRD (¿el motivo viaja en la Agenda del día?) debe copiarse a *Open Questions* del spine porque decide si Notificaciones necesita leer el motivo.

### P-4 — Reservar toca dos módulos: frontera transaccional y actualización de contacto · Alta · AD

- **Fuente:** FR-3, FR-6; plan, *Cómo se comunican* ("Agenda dispara el registro, Pacientes lo ejecuta") y *Eventos de dominio* ("un correo corregido llega también a los recordatorios pendientes").
- **Qué falta:** la convención dice "un comando, una transacción", pero `AgendarCita` escribe en `pacientes` (por `IPacientesApi.BuscarOCrear`, otro DbContext) y en `agenda`. No se dice si ambos comparten transacción y conexión. Si no la comparten y la Cita falla por `23P01`, queda un Paciente creado sin Cita (y, según P-2, quizá con Consentimiento). Tampoco se dice qué hace `BuscarOCrear` cuando el Paciente ya existe y llega con otro correo, o sin correo: ¿actualiza, conserva, borra? De eso depende a dónde llegan los recordatorios ya programados y la marca "sin correo".
- **Destino:** AD nuevo o ampliación de AD-3 (transacción entre módulos en llamadas síncronas) más una regla de actualización de contacto en AD-18.

### P-5 — Autorización de comandos y origen del actor · Media · ajuste de AD-22 y AD-16

- **Fuente:** FR-11, FR-20 (autor: Paciente, Administrador o sistema), §6 *Acceso*.
- **Qué falta:** AD-22 dice "todo comando de administración exige usuario autenticado", pero los comandos entran en proceso por `ISender`, no por HTTP. No se dice dónde se comprueba (¿atributo de página, middleware de Wolverine, marcador en el comando?) ni cómo distingue el backend un comando de administración de uno público. AD-16 exige el actor en la bitácora, pero no dice de dónde sale: si viaja como campo del comando, una página pública podría enviar `Administrador`. Tampoco se dice que los comandos del Paciente identifican la Cita solo por el token, nunca por `CitaId`.
- **Destino:** ajustar AD-22 (punto único de autorización; el actor se deriva del contexto autenticado, nunca del comando) y AD-19 (los comandos públicos reciben el token).

### P-6 — Recordatorio: cómo se ajusta, se elimina y qué pasa al cambiar la hora configurada · Media · ajuste de AD-20 / Open Question

- **Fuente:** FR-22 ("si la Cita se reprograma o se cancela, el Recordatorio se ajusta o se elimina"; "si al agendar esa hora ya pasó, se omite"), FR-19 (hora del Recordatorio y de la Agenda del día configurables).
- **Qué falta:** AD-20 dice que son mensajes programados durables, pero no fija la estrategia: cancelar el mensaje programado, o dejarlo dispararse y que al dispararse pregunte a Agenda si la Cita sigue Agendada a esa hora. Son dos diseños distintos y solo el segundo es robusto ante reintentos. Tampoco se dice qué pasa con los recordatorios ya programados cuando el Administrador cambia la hora en Configuración, ni cómo se re-programa la Agenda del día (mensaje recurrente) cuando cambia su hora.
- **Destino:** una línea en AD-20 ("el mensaje programado revalida contra Agenda al dispararse") y una *Open Question* sobre el cambio de hora.

### P-7 — Destinatarios y URL pública no tienen dueño · Media · ajuste de AD-14

- **Fuente:** FR-24 (alerta al Administrador), FR-25 (correo a Selina); EXPERIENCE.md, correo "Cancelación tardía" con botón "Abrir la Agenda".
- **Qué falta:** AD-14 da a Notificaciones solo las dos horas. Falta decir dónde viven el correo de Selina, el correo del Administrador para alertas, el remitente y la URL pública base del sitio (necesaria para todos los enlaces de los correos). AD-15 pone los datos del consultorio en Contenido; estos datos no están en ninguna de las dos listas.
- **Destino:** ampliar AD-14 (o AD-15) con la lista completa.

### P-8 — Entrega al menos una vez: idempotencia y fallo definitivo · Media · Convención / ajuste de AD-20

- **Fuente:** FR-26 ("los envíos fallidos se reintentan"), §5 *Disponibilidad* (alerta de errores al Administrador).
- **Qué falta:** con outbox y reintentos, un handler de Notificaciones puede ejecutarse dos veces. No hay convención de idempotencia (el Paciente recibiría dos confirmaciones o dos disculpas). Tampoco se dice qué pasa cuando los reintentos se agotan: ¿el Administrador se entera? Un aviso de "Cancelada por el Consultorio" que nunca sale es justo el caso que el PRD marca como el más sensible.
- **Destino:** fila nueva en *Consistency Conventions* (handlers de eventos idempotentes) y una línea en AD-20 sobre el fallo definitivo.

### P-9 — Contenido: ¿base de datos o archivos del despliegue? · Media · Open Question

- **Fuente:** FR-27 (nombre, cédula profesional, especialidad, dirección, mapa, WhatsApp), FR-30 (supuesto: el contenido se actualiza por despliegue); plan, *Practice website* ("Markdown files or a small table").
- **Qué falta:** AD-2 le da a Contenido un esquema `contenido` y el diagrama de despliegue lo lista en PostgreSQL, pero FR-30 dice que en el piloto el contenido se edita en un despliegue, y la semilla estructural muestra `Modules/Contenido/` sin capas. No queda claro si `IContenidoApi` lee de una tabla, de configuración o de archivos. Además AD-15 lista nombre, dirección, mapa y WhatsApp, y omite cédula profesional y especialidad (FR-27), que también aparecen en el pie del sitio. El memlog (línea 16) listaba solo tres esquemas; el spine añadió el cuarto sin registrar la decisión.
- **Destino:** *Open Question* ("¿de dónde lee `IContenidoApi` en el piloto?") y corregir AD-2 o AD-15 según la respuesta.

### P-10 — Respaldo, cifrado y alerta de errores no aparecen ni como diferidos · Media · Deferred

- **Fuente:** §5 *Disponibilidad* (alerta de errores y respaldo diario), §6 *Protección* (conexiones cifradas, cifrado en reposo, respaldo diario); plan, *Security and privacy* y *Deployment → Operations*.
- **Qué falta:** *Deferred* dice "aprovisionamiento de Azure y despliegue automático", sin nombrar estos requisitos. Como son requisitos de privacidad del PRD y no opciones, deben quedar nombrados como condición para recibir al primer Paciente, o se pierden cuando se retome el aprovisionamiento.
- **Destino:** ampliar el primer punto de *Deferred* con la lista explícita: HTTPS obligatorio, cifrado en reposo, respaldo diario, alerta de errores, secretos (ver L-5).

### P-11 — Tablas de Identity y alta del primer Administrador · Baja · Convención + Deferred

- **Fuente:** FR-11 (sin registro público, verificación en dos pasos).
- **Qué falta:** AD-2 enumera cuatro esquemas y el diagrama añade `wolverine`; las tablas de ASP.NET Core Identity no tienen esquema ni DbContext asignado. Sin registro público, tampoco se dice cómo nace la cuenta del Administrador (semilla, comando de arranque).
- **Destino:** una fila en AD-2 o AD-22 (esquema `identidad`, DbContext en el host) y un punto en *Deferred* para el alta inicial.

### P-12 — No indexable: administración y Enlaces de gestión · Baja · Convención

- **Fuente:** FR-29 (tercera consecuencia).
- **Qué falta:** es una regla transversal de la interfaz que dos páginas pueden resolver distinto (meta `noindex`, cabecera, `robots.txt`). El spine no la menciona. Para el Enlace de gestión conviene también una política de *referrer* para que el token no salga hacia el mapa o WhatsApp.
- **Destino:** fila en *Consistency Conventions*.

### P-13 — Las métricas del piloto necesitan datos que nadie guarda · Baja · Deferred (y ver B-4)

- **Fuente:** §9, SM-2 (proporción en línea vs. capturadas por el Administrador), SM-4 (mediana de tiempo desde abrir "Agendar cita" hasta la confirmación).
- **Qué falta:** SM-2 exige que el origen de la Cita se guarde (ver B-4). SM-4 exige conocer el inicio del recorrido, y AD-11 dice que el servidor no guarda nada entre pasos; solo se puede medir con telemetría o con una marca de tiempo que viaje en el formulario. Nada lo prevé.
- **Destino:** *Deferred* ("instrumentación de las métricas del piloto").

### P-14 — Límite de frecuencia sobre las páginas del Enlace de gestión · Baja · ajuste de AD-22

- **Fuente:** FR-8 ("el enlace no es adivinable"), FR-7.
- **Qué falta:** AD-22 pone límite de frecuencia y anti-bots solo en "las páginas de reserva". Las páginas del Enlace de gestión (ver, reprogramar, cancelar) quedan fuera, y son las que reciben intentos de adivinar tokens.
- **Destino:** ampliar AD-22 a todas las páginas públicas que reciben un token.

---

## 2. Decisión de disponibilidad

### B-1 — Cambio de Agenda base: la lista se ve **antes** de guardar, no como resultado · Media · ajuste de AD-6

- **Fuente:** regla 3 ("se muestran antes de guardar"); PRD FR-12 ("el Administrador ve cuáles son antes de guardar"); EXPERIENCE.md, estado *Cambio de Agenda base con Citas fuera*.
- **Qué falta:** AD-6 dice que para la base "el comando devuelve las citas que quedan fuera y las fechas con custom", es decir, después de aplicado. Para la custom sí prevé "una consulta previa que las lista". Las tres entradas piden la vista previa también para la base (para las Citas; para las fechas con custom basta al guardar).
- **Destino:** corregir AD-6: consulta previa en ambos casos.

### B-2 — Custom creada o editada después sobre una Cita del consultorio fuera de horario · Alta · Open Question

- **Fuente:** regla 5 ("una custom que deja fuera citas existentes las cancela… aplica igual si la fecha era de horario normal o un día abierto por excepción") y regla 9 ("ignora base y custom"); PRD FR-15 ("una Cita manual registrada sobre una fecha ya cerrada se conserva; no la cancela la Agenda custom que ya existía").
- **Qué falta:** AD-7 protege la Cita del consultorio solo de "una custom ya existente". No se decide qué pasa cuando la custom se crea o se **edita** después: una Cita manual a las 7:00 pm de un martes (ya fuera de la base) y luego una custom que cierra ese martes; o el sábado de UJ-6 con Cita manual a las 10:15 y luego una custom 11:00–13:00. Por AD-6 se cancelaría; por el espíritu de la regla 9, no. Dos lecturas válidas, dos implementaciones.
- **Destino:** *Open Question* para Gerardo; al cerrarla, una línea en AD-6 ("la custom cancela solo Citas de origen en línea" o "cancela toda Cita fuera de sus tramos").

### B-3 — Carrera entre guardar una custom y una reserva en línea · Media · ajuste de AD-6

- **Fuente:** regla 5 y regla 10; AD-6 *Prevents* ("que un día cerrado conserve citas en silencio").
- **Qué falta:** la reserva en línea valida contra los tramos que leyó antes; si en ese instante se guarda una custom que cierra el día, ninguna restricción de base lo impide y queda una Cita activa en un día cerrado, sin aviso: exactamente lo que AD-6 dice prevenir. El caso inverso también: entre la consulta previa y la confirmación entra una Cita nueva y el comando cancela una que el Administrador no vio.
- **Destino:** una línea en AD-6: el comando recalcula las afectadas dentro de su transacción y, si difieren de las mostradas, no aplica y devuelve la lista nueva; y se decide cómo se serializa contra `AgendarCita` (bloqueo por fecha o verificación al confirmar).

### B-4 — Origen y nota de la Cita no están declarados como datos guardados · Media · ajuste de AD-7

- **Fuente:** sección *Tablas* ("`citas` — … más **origen** (en línea / consultorio) y **nota**").
- **Qué falta:** AD-7 distingue dos caminos de creación, pero no dice que el origen se persiste. De ese dato dependen: FR-14 (distinguir las manuales fuera de horario), SM-2, la respuesta a B-2, y el supuesto de AD-13 ("cuentan citas de cualquier origen").
- **Destino:** una frase en AD-7: la Cita guarda su origen y, si aplica, su nota.

### B-5 — Nota obligatoria: ¿siempre o solo fuera de horario? · Baja · Open Question (confirmar)

- **Fuente:** regla 9 ("solo la registra el administrador, con nota obligatoria") frente a la sección *Tablas* ("obligatoria en la cita manual fuera de horario").
- **Qué falta:** la propia decisión dice las dos cosas. El spine (AD-7) y el PRD (FR-15) eligieron "solo fuera de los tramos vigentes". Es la lectura razonable, pero no quedó registrada como elección.
- **Destino:** anotar en el memlog que se tomó la lectura del PRD; nada más.

### B-6 — "A lo más una custom por fecha": quién lo garantiza y qué hace la aplicación múltiple · Baja · ajuste de AD-5

- **Fuente:** *El modelo* ("a lo más una por fecha"); PRD FR-13 ("puede aplicarse la misma Agenda custom a varias fechas en una sola acción").
- **Qué falta:** AD-5 enuncia la regla sin decir quién la garantiza (restricción única sobre la fecha, igual que AD-8 lo hace con el traslape). Tampoco se dice qué pasa cuando la aplicación a varias fechas cae sobre una fecha que ya tiene custom: ¿reemplaza, rechaza todo, salta esa fecha? Ni si las cancelaciones de varias fechas van en una sola transacción.
- **Destino:** una frase en AD-5 (la unicidad la garantiza la base de datos) y otra en AD-6 (aplicación múltiple: todo o nada, reemplaza).

### B-7 — Reprogramar por el Paciente una Cita creada por el Consultorio · Baja · Open Question

- **Fuente:** regla 9; sección *Arquitectura* ("la cita del consultorio no los valida").
- **Qué falta:** AD-7 dice que el camino en línea aplica "también al reprogramar". Una Cita manual del sábado a las 10:15 tiene Enlace de gestión (FR-21, supuesto). Si el Paciente la reprograma, pasa a reglas en línea; no se dice si conserva origen "consultorio" y nota, ni si puede quedarse en un horario fuera de los tramos.
- **Destino:** *Open Question* menor; se resuelve junto con B-2.

---

## 3. UX (EXPERIENCE.md)

### U-1 — Horario tomado al confirmar: regresar con los datos conservados · Alta · ajuste de AD-11

- **Fuente:** *State Patterns → Paciente*, "Horario tomado al confirmar"; UJ-1, *Falla*; *Interaction Primitives* ("se regresa… lo elegido se conserva").
- **Qué falta:** AD-11 dice que "lo elegido viaja en la cadena de consulta y en el formulario" y que el servidor no guarda nada entre pasos. Para volver a *Día y hora* conservando nombre, teléfono, correo y Motivo de consulta, esos datos tienen que viajar de ida y vuelta. Si alguien los pone en la cadena de consulta, un dato de salud queda en la URL, en el historial del navegador y en las trazas (ver P-3). La única salida compatible es que el paso de regreso y el avance siguiente sean envíos de formulario con campos ocultos; eso obliga a que "Continuar" en *Día y hora* sea un `POST` cuando hay datos previos. El spine no lo dice y la flecha "Regresar" de UX tiene el mismo problema.
- **Destino:** ampliar AD-11: la selección (tipo, fecha, hora) puede ir en la cadena de consulta; los datos personales viajan solo en el cuerpo del formulario, nunca en la URL.

### U-2 — Doble envío de "Confirmar cita" · Alta · AD / Convención

- **Fuente:** *State Patterns → Paciente*, "Enviando" ("el botón se deshabilita…; evita el doble envío").
- **Qué falta:** en SSR estático deshabilitar el botón requiere script y no es garantía. Si el segundo envío llega, choca con la restricción de AD-8 contra la Cita que el propio Paciente acaba de crear, y AD-8 lo traduce a "ese horario se acaba de ocupar": el Paciente que sí reservó ve un mensaje de que perdió su horario. Además, por AD-19 ya no se puede volver a mostrar su enlace. Hace falta una regla de idempotencia (clave de idempotencia en el formulario, o reconocer mismo Paciente + mismo rango).
- **Destino:** AD nuevo o línea en AD-8 ("un reintento del mismo envío devuelve la misma confirmación").

### U-3 — Una Cita nueva aparece en la Agenda sin recargar · Alta · AD

- **Fuente:** *State Patterns → Administrador*, "Cita nueva en línea"; PRD FR-14 (primera consecuencia).
- **Qué falta:** el spine no tiene mecanismo. AD-10 prohíbe a la interfaz usar Wolverine o el dominio directamente y AD-1 solo le deja ver `*.Contracts`. Para que un circuito de Interactive Server se entere de un `CitaAgendada` hace falta una pieza que no está nombrada: un handler en el host que reenvíe a un servicio de notificación de la interfaz, o sondeo periódico por `ISender`. Son dos diseños con consecuencias distintas (el primero necesita una abstracción nueva en algún `Contracts`).
- **Destino:** AD nuevo ("cómo se entera la Agenda del Administrador de un cambio").

### U-4 — "Sin correo" y nombre del Paciente: composición de lecturas entre módulos · Alta · AD

- **Fuente:** *State Patterns → Administrador*, "Sin correo" y "Agenda custom que deja Citas fuera" (lista con las que no tienen correo); tarjeta de Cita con Paciente; PRD FR-13, FR-14, FR-18, FR-25.
- **Qué falta:** la Agenda guarda `PatientId`; el nombre y si tiene correo viven en Pacientes, y AD-2 prohíbe joins. Casi todas las pantallas de administración y la Agenda del día necesitan los dos datos juntos. El spine no dice quién compone: ¿la interfaz hace dos consultas, o el handler de Agenda llama a `IPacientesApi`? Tampoco prevé una consulta por lote (`ObtenerContactos(ids)`); sin ella, la vista semanal hace una llamada por Cita. Para FR-18 (lista de Pacientes con historial) el diagrama de referencias no permite que Pacientes vea `Agenda.Contracts`, así que la composición solo puede ocurrir en la interfaz o en Agenda; conviene decirlo.
- **Destino:** AD nuevo ("composición de lecturas entre módulos: dónde ocurre y con qué contrato por lote").

### U-5 — La pantalla de confirmación promete o no el Recordatorio · Media · ajuste de AD-20

- **Fuente:** *State Patterns → Paciente*, "Confirmada, con correo", "Confirmada, sin correo" y "Recordatorio omitido".
- **Qué falta:** si habrá Recordatorio depende de la hora configurada (dato de Notificaciones, AD-14) y de la regla "si esa hora ya pasó, se omite" (FR-22). Si la interfaz calcula eso por su cuenta y Notificaciones lo calcula al programar, hay dos implementaciones de la misma regla. Hace falta una consulta en `Notificaciones.Contracts` ("¿se enviará Recordatorio para esta Cita?").
- **Destino:** una línea en AD-20.

### U-6 — Resultados con código, para que cada caso tenga su mensaje · Media · Convención

- **Fuente:** *State Patterns → Paciente*: "Horario tomado", "Tope semanal alcanzado", "Menos de 2 horas antes", "Enlace no válido", "Campo inválido"; *Voice and Tone*.
- **Qué falta:** el spine nombra `Resultado.Invalido` y un resultado "de conflicto". UX necesita distinguir al menos: inválido por campo, horario ocupado (con libres restantes), tope semanal, fuera del corte para reprogramar, fuera de la ventana, enlace no válido, y en administración "traslapa" frente a "en el pasado". Sin una lista cerrada de códigos, cada handler inventará la suya y la interfaz no podrá mapear mensajes.
- **Destino:** fila en *Consistency Conventions* (familias de `Resultado` y códigos de regla de negocio).

### U-7 — Paciente reconocido en "Agendar cita" de administración · Baja · ajuste de AD-3

- **Fuente:** *State Patterns → Administrador*, "Paciente reconocido" ("indica que ya existe y muestra su última Cita"); UJ-4 paso 2.
- **Qué falta:** el contrato de Pacientes que el spine y el memlog nombran es `BuscarOCrear`, `ObtenerContacto` y los ids por teléfono. Esta pantalla necesita buscar **sin crear** mientras se captura, y luego la última Cita desde Agenda. Es superficie de contrato que hoy no existe.
- **Destino:** mencionar la consulta de solo lectura en AD-3 o resolverlo dentro de U-4.

### U-8 — El Enlace de gestión de una Cita cancelada · Baja · ajuste de AD-19

- **Fuente:** *State Patterns → Paciente*, "Cancelada por el Consultorio" (superficie *Tu cita*, desde el correo) y "Cita pasada o enlace no válido".
- **Qué falta:** AD-19 dice que el enlace "deja de valer al pasar la hora de la Cita". No dice que sigue valiendo para una Cita Cancelada hasta esa hora (UX lo necesita para mostrar la disculpa), ni que la respuesta para enlace vencido, inexistente o mal formado es la misma (para no revelar cuáles tokens existieron).
- **Destino:** dos frases en AD-19.

### U-9 — Vocabulario visible frente al Glosario · Baja · Open Question

- **Fuente:** *Voice and Tone* ("la palabra visible es consulta"); *Preguntas abiertas* 1.
- **Qué falta:** la convención del spine dice "el Glosario del PRD manda" y que ciertos términos "no existen en código ni en pantallas". UX usa "Tipo de consulta" en pantalla y el Glosario dice "Tipo de visita". Falta decir que el Glosario manda en el código y UX manda en el texto visible, o cerrar la pregunta.
- **Destino:** aclarar la fila *Vocabulario* o copiar la pregunta a *Open Questions*.

### U-10 — Cuánto script admiten las páginas públicas · Baja · ajuste de AD-11

- **Fuente:** *Component Patterns*: "Copiar → Copiado", "valida al salir del campo", "Continuar deshabilitado sin hora", "elegir otro día borra la hora elegida"; estado "Enviando".
- **Qué falta:** AD-11 fija SSR estático y el memlog descarta un selector propio en JavaScript, pero estos comportamientos necesitan algo de script. Sin regla, una página los resuelve con navegación y otra con un componente interactivo.
- **Destino:** una frase en AD-11: mejora progresiva; todo el recorrido funciona sin script y el script solo añade comodidad.

---

## 4. Plan de solución original (decisiones no reemplazadas que se cayeron)

### L-1 — Migraciones: cuántas, quién las aplica y qué SQL propio llevan · Alta · AD / Convención

- **Fuente:** *Architecture* ("Database: EF Core migrations"), *Pipeline de CI* paso 6 ("aplicando las migraciones de EF Core"), *Deployment → CI/CD*.
- **Qué falta:** el spine solo dice que Infraestructura contiene "migraciones". Con cuatro DbContext de módulo, Identity y las tablas de Wolverine sobre una sola base, hay que decidir: un historial de migraciones por esquema; quién las aplica en local (¿el AppHost, al arrancar?) y en producción; y dónde viven los objetos que EF no genera solo — la extensión `btree_gist`, la restricción de exclusión de AD-8 y la collation de AD-18. Es la primera cosa que dos historias paralelas harán de forma distinta.
- **Destino:** AD nuevo o fila en *Consistency Conventions*; la parte de producción, a *Deferred*.

### L-2 — Emparejamiento de Pacientes: collation, índice único y búsqueda · Media · ajuste de AD-18

- **Fuente:** *Pacientes por dentro* (collation ICU no determinista, índice único sobre teléfono + nombre normalizado, aviso de que `LIKE` no funciona con esa collation).
- **Qué falta:** AD-18 conserva la regla ("comparado sin distinguir mayúsculas ni acentos") pero no dónde se compara. Si un desarrollador normaliza en C# y otro confía en la collation, habrá dos definiciones de "mismo Paciente". Sin índice único, dos reservas simultáneas del mismo Paciente nuevo crean un duplicado. Y la búsqueda por nombre de FR-18 necesita una columna con collation determinista. Además el servidor PostgreSQL debe tener soporte ICU, que no aparece en *Stack*.
- **Destino:** dos frases en AD-18 y una nota en *Stack*.

### L-3 — Integración continua como puerta de las reglas · Media · Deferred explícito

- **Fuente:** *Pipeline de CI* (compilar, unitarias, bUnit, arquitectura, integración en cada push y PR).
- **Qué falta:** *Deferred* habla de "despliegue automático", no de la integración continua. AD-1 y AD-10 se apoyan en que "las pruebas de arquitectura fallan"; si nada las ejecuta en cada cambio, la regla no se cumple sola. Hay que decidirla o diferirla con nombre.
- **Destino:** punto propio en *Deferred*, o una fila en *Consistency Conventions*.

### L-4 — Archivo de calendario (.ics): quién lo genera y cómo se sirve · Media · AD (junto con P-1)

- **Fuente:** *Booking flow* paso 5 y *Notifications* (".ics calendar file"); PRD FR-5 y FR-21.
- **Qué falta:** el `.ics` se necesita en dos lugares: adjunto del correo (Notificaciones) y descarga desde la pantalla de confirmación (interfaz pública). Si cada uno lo genera, habrá dos formatos. Además la descarga es el único punto del piloto que necesita un endpoint HTTP de verdad, que AD-10 permite "solo si hace falta" sin decir cuál; y el archivo debe incluir el Enlace de gestión, así que hereda el problema de P-1.
- **Destino:** resolver dentro del AD de P-1: un solo generador, expuesto por contrato, y el endpoint de descarga autorizado por token.

### L-5 — Seguridad operativa: secretos, HTTPS, salud, alertas · Media · Deferred explícito

- **Fuente:** *Deployment* (secretos en App Service o Key Vault, certificado TLS gestionado, endpoint de salud, alertas de Application Insights, respaldo diario); *Costs* (límite diario de ingesta).
- **Qué falta:** nada de esto está nombrado; quedó implícito en "aprovisionamiento de Azure". También falta el manejo de secretos en local con Aspire (cadena de conexión, clave del anti-bots, credenciales del correo). El memlog (línea 58) dice que producción exporta a Application Insights; AD-21 no lo recoge.
- **Destino:** ampliar *Deferred* (se junta con P-10) y añadir a AD-21 el destino de producción.

### L-6 — Proveedor de correo y correo en local · Media · Stack + Open Question

- **Fuente:** *Technology choices* (Azure Communication Services Email), *Notifications*.
- **Qué falta:** el plan eligió Azure Communication Services; el spine dice "proveedor de correo" y no lo pone en *Stack*, sin registrar si la elección sigue en pie. Tampoco dice qué recibe los correos en local bajo Aspire (un contenedor de correo de prueba). Y la *Open Question* 1 solo pregunta por la residencia de la base de datos: el correo (que lleva nombre y, según FR-25, Motivo de consulta) y la telemetría también salen de México.
- **Destino:** fila en *Stack* o punto en *Deferred*; ampliar la *Open Question* 1 a correo y telemetría.

### L-7 — Diagrama de referencias incompleto frente a "el host compone todo" · Media · corregir AD-1 / diagrama

- **Fuente:** *Capas y dependencias* (`Consultorio.Web` referencia todo; `Infrastructure` referencia `Features` y `Domain`).
- **Qué falta:** el spine dice "toda referencia que no aparece está prohibida y la verifican las pruebas de arquitectura". En el diagrama no existen `Pacientes.Infrastructure`, `Notificaciones.Infrastructure` ni la implementación de Contenido, y el host solo apunta a `Agenda.Infrastructure`, `Pacientes.Features` y `Notificaciones.Features`. Tampoco aparecen las flechas de `UI`, `Pacientes.Contracts`, `Notificaciones.Contracts` y `Contenido.Contracts` hacia `BuildingBlocks.Contracts` (donde vive `ISender`). Leído al pie de la letra, el diagrama prohíbe que el host componga tres de los cuatro módulos.
- **Destino:** completar el diagrama; no es un AD nuevo.

### L-8 — Estrategia de pruebas recortada · Baja · Deferred

- **Fuente:** *Pruebas unitarias*, *Pruebas de UI/UX*, *Pipeline de CI* (qué cubre integración: handlers de Wolverine, outbox, restricción de exclusión, collation; Playwright solo en local con recorridos móviles y accesibilidad).
- **Qué falta:** la fila *Pruebas* y el punto diferido del arnés cubren lo esencial. Se perdieron sin registro: las pruebas de extremo a extremo con Playwright, y la lista de lo que la integración **debe** cubrir (la restricción de exclusión y la collation son reglas que solo una base real demuestra).
- **Destino:** añadir al punto existente de *Deferred*.

### L-9 — Dos niveles de eventos y transporte de Wolverine · Baja · Convención

- **Fuente:** *Eventos de dominio (modelo)* (el agregado levanta eventos de dominio; el handler los traduce a eventos de integración en `Contracts`); *Transporte de Wolverine* (sin broker, colas locales durables, `InvokeAsync` para comandos).
- **Qué falta:** AD-3 nombra los eventos de integración y el kernel lista `IDomainEvent`, pero la regla de traducción (los eventos de dominio no salen de `Agenda.Domain`) no está. Tampoco la decisión "sin broker; colas locales durables", que es la que hace cierto FR-26 tras un reinicio.
- **Destino:** una frase en AD-3 y otra en AD-20.

---

## 5. Requisitos cualitativos que la estructura de AD perdió

### Q-1 — Tono: quién es dueño del texto que lee el Paciente · Media · Convención

- **Fuente:** PRD §5 *Tono*; FR-23; EXPERIENCE.md *Voice and Tone*.
- **Qué falta:** el tono es requisito ("la mención de una Cancelación tardía nunca regaña"). El texto vive en tres lugares posibles: mensajes de error del dominio, DataAnnotations en `Contracts` (AD-12) y plantillas de correo en Notificaciones. Sin regla, el dominio devolverá frases técnicas que la interfaz mostrará tal cual.
- **Destino:** fila en *Consistency Conventions*: el dominio y los handlers devuelven códigos (ver U-6); el texto visible lo ponen la interfaz y las plantillas de Notificaciones; los mensajes de DataAnnotations siguen la guía de voz.

### Q-2 — "La reserva nunca espera al correo" quedó solo como *Prevents* · Media · ajuste de AD-20

- **Fuente:** PRD FR-26; plan, *Architecture* ("the patient never waits on email").
- **Qué falta:** AD-20 lo menciona en *Prevents*, pero la *Rule* no lo afirma: no dice que el comando de reserva termina sin llamar a Notificaciones ni al proveedor, ni que la pantalla de confirmación no depende de ningún dato de Notificaciones salvo la consulta de U-5 (que debe ser local y rápida).
- **Destino:** una frase en la *Rule* de AD-20.

### Q-3 — Postura de privacidad como criterio, no solo como controles · Media · AD (junto con P-3)

- **Fuente:** PRD §0 ("la privacidad no se aligera"), §6 *Datos mínimos* y *Acceso*.
- **Qué falta:** el spine trata la privacidad en tres piezas sueltas (AD-3, AD-19, AD-22) y una *Open Question*. No hay una regla que diga "datos mínimos": qué datos personales guarda cada módulo y que ningún otro lugar (bitácora, outbox, telemetría, correos al Administrador) los copia. La bitácora de AD-16, por ejemplo, no dice si guarda valores anteriores con nombre o motivo.
- **Destino:** el mismo AD de P-3.

### Q-4 — El criterio de desempate del producto · Baja · preámbulo del spine

- **Fuente:** PRD §1 ("ante cualquier duda, gana la opción que le quite trabajo a Selina"); plan, *Visión del dominio*.
- **Qué falta:** es el criterio con el que se resolvieron varias decisiones (cancelar por custom, respetar por base). No está en el spine, y las preguntas abiertas de este reporte (B-2, B-7) se resolverían con él.
- **Destino:** una línea en el preámbulo o en *Design Paradigm*.

---

## 6. Contradicciones entre el spine y el PRD

### C-1 — Tope semanal aplicado al reprogramar · Media · corregir AD-7

- **Fuente:** AD-7 ("En línea (`Cita.Agendar`, también al reprogramar): … la ventana de reserva y el tope semanal") frente a PRD FR-9 ("el nuevo horario cumple las mismas reglas que una reserva nueva (FR-2, FR-4)", sin citar FR-7).
- **Problema:** un Paciente con tres Citas en la semana que mueve una de ellas dentro de la misma semana chocaría con el tope, porque la Cita que mueve ya cuenta. El PRD no pide el tope al reprogramar. AD-7 tampoco menciona el corte de 2 horas, que es la regla propia de reprogramar.
- **Destino:** corregir AD-7: al reprogramar valen tramos, rejilla, ventana y corte; el tope no se aplica (o excluye la propia Cita).

### C-2 — "Administración en Interactive Server" incluye el inicio de sesión · Media · corregir AD-11

- **Fuente:** AD-11 (*Binds* FR-11 a FR-20) frente a PRD FR-11 y EXPERIENCE.md, superficie *Iniciar sesión*.
- **Problema:** el inicio de sesión con ASP.NET Core Identity escribe la cookie de autenticación en la respuesta HTTP; no puede hacerse dentro de un circuito interactivo. Las páginas de inicio de sesión y de segundo factor tienen que ser SSR estático. Tal como está escrito, AD-11 manda lo contrario.
- **Destino:** excepción explícita en AD-11.

### C-3 — Esquema `contenido` frente a contenido que se actualiza por despliegue · Baja · ver P-9

- **Fuente:** AD-2 y diagrama de despliegue (esquema `contenido`) frente a PRD FR-30 (supuesto: se edita en un despliegue).
- **Problema:** no es incompatible, pero el spine afirma una tabla que el PRD no pide y la semilla estructural no muestra.
- **Destino:** se resuelve con la *Open Question* de P-9.

Ver también B-1 (vista previa del cambio de Agenda base), que es una diferencia del spine con el PRD FR-12 además de con la decisión de disponibilidad.

---

## Lo que sí aterrizó (para no revisarlo otra vez)

- Las once reglas de la decisión de disponibilidad están en AD-5, AD-6 y AD-7, salvo los matices B-1 a B-7.
- No doble reserva (FR-4), estados (FR-10, FR-16), ventana, hora de Ciudad de México, tope semanal, propiedad de la configuración y de la bitácora: cubiertos.
- Fuera de alcance del PRD (§8.2): WhatsApp, expediente, Asistente, fusión de duplicados, retención y ARCO, varias sedes están en *Deferred*. Aseguradoras y anticipos no están, pero no tienen consecuencia arquitectónica en el piloto.
- La pregunta abierta 9 del PRD (hora de la Agenda del día con Citas por la mañana) está como *Open Question* 3 del spine.

## Resumen de destinos recomendados

| Destino | Hallazgos |
| --- | --- |
| AD nuevo | P-1 (+L-4), P-2, P-3 (+Q-3), P-4, U-2, U-3, U-4, L-1 |
| Ajuste de un AD existente | P-5, P-6, P-7, P-14, B-1, B-3, B-4, B-6, U-1, U-5, U-7, U-8, U-10, L-2, L-7, L-9, Q-2, C-1, C-2 |
| Convención | P-8, P-12, U-6, Q-1 |
| Deferred | P-10, P-11, P-13, L-3, L-5, L-8 |
| Open Question | P-9 (+C-3), B-2, B-5, B-7, U-9, L-6 |
| Preámbulo | Q-4 |
