# Revisión adversarial — Architecture Spine, Sistema de citas

- **Documento revisado:** `../architecture-appointment-booking.md` (AD-1 a AD-22, estado `draft`)
- **Contexto leído:** `.memlog.md`, PRD `prd-appointment-booking`, decisión `brainstorm-agenda-horario-configuracion`
- **Fecha:** 2026-10-04
- **Método:** para cada hueco se construyen dos unidades un nivel abajo (dos historias, dos módulos, dos handlers) que cumplen todos los AD al pie de la letra y aun así no encajan. No se modificó ningún documento fuente.

## Veredicto

El spine cierra bien los límites entre módulos y el modelo de disponibilidad, pero **no es todavía un contrato suficiente para construir historias por separado**: deja sin fijar la forma de los eventos, la mecánica de reprogramar, el recorrido del token de gestión hasta el correo, y los puntos donde se cruzan transacciones y concurrencia. Hay 2 huecos críticos y 10 altos; casi todos se cierran con una o dos frases añadidas a un AD existente.

## Resumen

| # | Hueco | Severidad | AD afectados |
| --- | --- | --- | --- |
| H1 | El token de gestión no puede llegar al correo de confirmación | Crítica | AD-3, AD-19, AD-20 |
| H2 | Reprogramar: ¿misma Cita movida o Cita nueva? | Crítica | AD-7, AD-9, AD-19 |
| H3 | Custom posterior contra Cita del consultorio; qué es "quedar fuera" | Alta | AD-6, AD-7 |
| H4 | Carrera entre reservar y guardar una custom; lista previa desactualizada | Alta | AD-6, AD-8 |
| H5 | `BuscarOCrear` de Pacientes dentro de la transacción de Agenda | Alta | AD-2, AD-3, AD-18, convenciones |
| H6 | La forma de los eventos queda a criterio de cada constructor | Alta | AD-3, AD-9, AD-14 |
| H7 | Dónde se atrapa `23P01` y quién calcula los libres restantes | Alta | AD-8, convenciones |
| H8 | Forma del rango de la Cita: fin guardado o derivado, extremos abiertos o cerrados | Alta | AD-8, AD-14 |
| H9 | Ids tipados e `IClock` en un kernel que los Contracts no pueden referenciar; diagrama incompleto | Alta | AD-1, AD-18, AD-17 |
| H10 | Punto de control de acceso y origen del actor; consultas internas sin usuario | Alta | AD-10, AD-16, AD-22 |
| H11 | Dueño del Recordatorio al reprogramar o cancelar | Alta | AD-20, AD-14 |
| H12 | Cascada de cancelación de una custom: síncrona o por evento | Alta | AD-6, AD-16, convenciones |
| H13 | Rejilla implementada dos veces; ancla y duraciones distintas; tope en el agregado o en el handler | Media | AD-5, AD-7, AD-13 |
| H14 | Idempotencia y orden de los envíos de correo | Media | AD-20 |
| H15 | Dueño de correo, Consentimiento y Motivo de consulta | Media | AD-2, AD-18 |
| H16 | Anti-bots y límite de frecuencia: nadie o los dos verifican | Media | AD-11, AD-22 |
| H17 | Doble envío del formulario de confirmación | Media | AD-8, AD-11, AD-19 |
| H18 | Custom sobre hoy cancela citas que ya ocurrieron | Media | AD-6, AD-9 |
| H19 | Semana, "ese día", "ya pasó" y día de la semana sin zona ni forma fijas | Media | AD-13, AD-17, AD-20, AD-9 |
| H20 | Actualización perdida entre dos cambios a la misma Cita | Media | AD-4, AD-8 |
| H21 | Quién compone lecturas que cruzan Agenda y Pacientes | Media | AD-2, AD-3, AD-10 |
| H22 | Reprogramar cuenta la propia Cita y tapa su propio horario; origen tras reprogramar | Media | AD-5, AD-7, AD-13 |
| H23 | Mecanismo y forma de la bitácora | Media | AD-16 |
| H24 | Corte de reprogramación y umbral tardío calculados fuera de Agenda | Media | AD-14 |
| H25 | Cambio de base: vista previa o resultado del comando | Baja | AD-6 |
| H26 | Cómo se entera la agenda del Administrador de una Cita nueva | Baja | AD-10, AD-3 |
| H27 | Dueño de los correos de Selina y del Administrador | Baja | AD-14, AD-15 |
| H28 | Normalización de teléfono y nombre en dos lugares | Baja | AD-12, AD-18 |
| H29 | Estructura de Contenido y origen de sus datos | Baja | AD-2, AD-15 |

---

## Críticos

### H1 — El token de gestión no puede llegar al correo de confirmación

- **Unidades:** historia "Reservar en línea" (Agenda, `AgendarCita`) contra historia "Correo de confirmación" (Notificaciones, handler de `CitaAgendada`).
- **Cómo cumplen y chocan:** Agenda genera 32 bytes, guarda solo el hash (AD-19) y publica `CitaAgendada` con "identificadores y tiempos" (AD-3). Notificaciones debe incluir el Enlace de gestión en el correo (FR-21) y solo puede pedir datos a Agenda por contrato (AD-20). El token en claro ya no existe en ningún lado: Agenda no puede devolverlo y el evento no lo lleva. Si el constructor de Agenda lo mete en el evento, queda escrito en las tablas de sobres de Wolverine (y en la cola de mensajes muertos), que es justo el "token recuperable" que AD-19 quiere impedir. Los dos cumplen; el correo sale sin enlace o el secreto queda en la base.
- **Severidad:** crítica. FR-21 es inalcanzable tal como está escrito.
- **AD propuesto (ajuste a AD-19 y AD-3):** "El token no se guarda ni viaja: se **deriva** como `HMAC-SHA256(clave del host, CitaId)`, con la clave fuera de la base de datos. Agenda guarda el hash SHA-256 del token y expone `ObtenerEnlaceDeGestion(CitaId)` solo en su API interna. Ningún evento lleva el token." Alternativa si se quiere conservar la aleatoriedad pura: "El token en claro viaja una única vez en `CitaAgendada`, como excepción expresa a AD-3; ningún otro mensaje lo lleva y los sobres atendidos se borran". La primera es más pequeña y además cierra H17.

### H2 — Reprogramar: ¿la misma Cita movida o una Cita nueva?

- **Unidades:** historia "Reprogramar por el Paciente" contra historias "Enlace de gestión", "Historial del Paciente" y handler de `CitaReprogramada` en Notificaciones.
- **Cómo cumplen y chocan:** AD-7 dice "En línea (`Cita.Agendar`, también al reprogramar)". Un constructor lo lee literal: reprogramar llama a la fábrica `Cita.Agendar`, nace una Cita con otro `CitaId` y la anterior se cancela. Otro lee AD-9 ("Reprogramar no es un estado") y FR-9 ("la Cita sigue Agendada; solo cambia de horario") y muta la misma fila. Consecuencias del primero: la Cita vieja necesita un motivo de cancelación que no existe (paciente, tarde, consultorio), el historial muestra una cancelación falsa, el Enlace de gestión del correo original deja de servir y el tope semanal cuenta distinto. `CitaReprogramada` sale como `{CitaIdAnterior, CitaIdNueva}` en un caso y `{CitaId, InicioAnterior, InicioNuevo}` en el otro. Además, si alguien guardó una columna de vencimiento del token al crear la Cita, reprogramar la deja apuntando a la hora vieja.
- **Severidad:** crítica. Afecta identidad de la Cita, forma del evento, token y recordatorio.
- **AD propuesto (ajuste a AD-7 y AD-9):** "Reprogramar es `cita.Reprogramar(nuevoInicio, …)` sobre la **misma** Cita: conserva `CitaId`, token, origen y bitácora, y aplica las reglas del camino en línea. No crea ni cancela ninguna Cita. El vencimiento del token nunca se guarda: se compara `Inicio` actual contra `now`."

---

## Altos

### H3 — Custom posterior contra Cita del consultorio; qué significa "quedar fuera"

- **Unidades:** historia "Dar Agenda custom a una fecha" (`GuardarAgendaCustom`) contra historia "Agendar manualmente" (`AgendarPorConsultorio`).
- **Cómo cumplen y chocan:** AD-6: "Guardar una custom que deja citas activas fuera las cancela". AD-7: "una custom **ya existente** no la cancela". Caso: sábado sin tramos en la base; Gerardo registra a mano una Cita a las 10:15 (UJ-6). Días después abre ese sábado con custom 11:00–13:00. El constructor de la custom cancela todo lo activo que no cabe en los tramos nuevos, incluida la Cita de las 10:15 (AD-6 no distingue origen y la custom no "existía ya"). El constructor de la Cita manual asume que una Cita del consultorio es inmune a base y custom. Lo mismo pasa al **editar** una custom que ya existía. Además "fuera" no está definido: ¿una Cita que traslapa parcialmente un tramo?, ¿una que cruza dos tramos contiguos (15:30–16:30 y 16:30–18:00, Cita 16:15–16:45)?
- **Severidad:** alta. Una Cita pactada personalmente por Selina se cancela con disculpa automática.
- **AD propuesto (ajuste a AD-6):** "Una Cita **queda fuera** cuando no cabe completa dentro de un solo tramo vigente. Guardar o editar una custom cancela solo las Citas de origen *en línea*, futuras, que quedan fuera. Las Citas de origen *consultorio* nunca se cancelan por una custom, sea anterior o posterior; la consulta previa las lista aparte como 'se conservan'."

### H4 — Carrera entre reservar y guardar una custom; lista previa desactualizada

- **Unidades:** handler `AgendarCita` (paciente a media reserva) contra handler `GuardarAgendaCustom`.
- **Cómo cumplen y chocan:** T1 (reserva) lee los tramos vigentes de la base y valida. T2 (custom) busca las citas activas de la fecha, no ve la de T1 porque no está confirmada, cierra el día y confirma. T1 confirma. Resultado: Cita Agendada en un día cerrado, sin cancelar y sin aviso, exactamente lo que AD-6 dice prevenir. La restricción de exclusión (AD-8) no protege porque no hay traslape entre citas. Segundo choque: la consulta previa listó una Cita; antes de confirmar entra otra; el comando cancela dos y el Administrador vio una (o cancela solo la que vio y deja la otra viva).
- **Severidad:** alta.
- **AD propuesto (nuevo, junto a AD-6):** "Todo comando que crea o mueve una Cita y todo comando que guarda o borra una custom toman, al inicio de su transacción, un candado consultivo de PostgreSQL (`pg_advisory_xact_lock`) por **fecha local** afectada, y leen los tramos vigentes después de tomarlo. `GuardarAgendaCustom` recibe la lista de `CitaId` que el Administrador confirmó; si las citas que quedarían fuera no son exactamente esas, devuelve conflicto con la lista nueva y no cambia nada."

### H5 — `BuscarOCrear` de Pacientes dentro de la transacción de Agenda

- **Unidades:** historia "Reconocer al Paciente" (Pacientes, `IPacientesApi.BuscarOCrear`) contra historia "Reservar en línea" (Agenda, `AgendarCita`).
- **Cómo cumplen y chocan:** Pacientes tiene su DbContext (AD-2) y su constructor aplica "un comando, una transacción": `BuscarOCrear` guarda y confirma por su cuenta. Agenda lo llama dentro de `AgendarCita`, cuya transacción falla después con `23P01`: queda un Paciente sin Cita (con Consentimiento registrado, si vive ahí). Otro constructor hace lo contrario: comparte conexión y transacción entre los dos DbContext, lo que acopla los esquemas que AD-2 separa. Además, dos reservas simultáneas con el mismo teléfono y nombre crean dos Pacientes si no hay índice único; AD-18 no dice si existe ni sobre qué columnas. Tampoco se dice si `IPacientesApi` es una interfaz directa o pasa por `ISender` (AD-10 exige "exactamente un handler" por caso de uso).
- **Severidad:** alta.
- **AD propuesto (ajuste a AD-3 y AD-18):** "`IPacientesApi` e `IContenidoApi` son interfaces llamadas directamente, no pasan por `ISender`. `BuscarOCrear` confirma en su **propia** transacción, antes de que Agenda abra la suya, y es idempotente: índice único sobre (teléfono E.164, nombre normalizado); ante violación, relee y devuelve el existente. Un Paciente sin Citas es un estado válido y no se limpia."

### H6 — La forma de los eventos queda a criterio de cada constructor

- **Unidades:** historias de Agenda que publican (`AgendarCita`, `Reprogramar`, `Cancelar`, `GuardarAgendaCustom`) contra handlers de Notificaciones (FR-21 a FR-24).
- **Cómo cumplen y chocan:** AD-3 solo dice "identificadores y tiempos". Agenda publica `CitaCancelada {CitaId, PatientId, Inicio}`. Notificaciones necesita saber si fue paciente, tarde o consultorio para elegir plantilla y disparar la alerta (FR-23, FR-24). Como el motivo no es "identificador ni tiempo", el constructor de Notificaciones lo recalcula: `Inicio − canceladaEn < 2 h`, con su propia constante o comparando con `<=` en vez de `<`. La Cita queda guardada como *paciente* y la alerta dice *tarde*, o al revés, y basta cambiar el umbral en Configuración (AD-14, dueño Agenda) para que diverjan. Igual con `CitaReprogramada` (¿trae el inicio anterior?) y con `CitaAgendada` (¿trae origen, para decidir si se confirma una Cita manual?).
- **Severidad:** alta.
- **AD propuesto (ajuste a AD-3):** "Todo evento de integración lleva `EventoId` (Guid v7), `OcurridoEn` (UTC), `CitaId`, `PatientId` y `Actor`. Además: `CitaAgendada {Inicio, Fin, Origen}`; `CitaReprogramada {InicioAnterior, InicioNuevo, FinNuevo}`; `CitaCancelada {Inicio, Motivo}` con `Motivo` ∈ paciente, tarde, consultorio. Los consumidores nunca recalculan una clasificación que Agenda ya decidió. Los enumerados de dominio pueden viajar; los datos de contacto y de salud no."

### H7 — Dónde se atrapa `23P01` y quién calcula los libres restantes

- **Unidades:** infraestructura transaccional de Wolverine (historia "esqueleto del host") contra handler `AgendarCita`.
- **Cómo cumplen y chocan:** con "un comando, una transacción: cambio, bitácora y outbox juntos", el constructor del host activa el middleware transaccional de Wolverine: `SaveChanges` y el commit ocurren **después** de que el handler devuelve. El constructor del handler cumple AD-8 con un `try/catch` dentro del handler que nunca se ejecuta: la excepción sale del middleware como error, justo lo que AD-8 prohíbe. Y aunque se atrape, AD-8 pide que el conflicto "incluya los libres restantes": la transacción ya está abortada en PostgreSQL y no admite más lecturas, así que hay que abrir otra, lo que rompe "un comando, una transacción" o deja a la interfaz y al handler calculando lo mismo.
- **Severidad:** alta. Es la regla de UJ-1.
- **AD propuesto (ajuste a AD-8):** "El puerto de escritura de Cita ejecuta `SaveChanges` de forma explícita dentro del handler y traduce `23P01` a `Resultado.Conflicto`. El conflicto **no** lleva los libres: la página vuelve a pedir `ConsultarDisponibilidad` por `ISender` y los muestra. Ningún middleware confirma cambios que el handler no haya vaciado ya."

### H8 — Forma del rango de la Cita

- **Unidades:** migración de la restricción de exclusión (historia "Cita firme") contra `CalculadoraDeDisponibilidad` y contra "Configurar Tipos de visita".
- **Cómo cumplen y chocan:** (a) Un constructor guarda `Inicio` y `TipoDeVisitaId` y deriva el fin de la duración del tipo; la restricción necesita un rango en la fila y no puede leer otra tabla, y al cambiar la duración del tipo (AD-14) las citas existentes cambiarían de largo y podrían traslaparse. (b) La migración usa `tstzrange(inicio, fin, '[]')`; la calculadora ofrece 16:00 y 16:30 como contiguas: la segunda reserva choca con `23P01` aunque la calculadora la ofreció. (c) La Cita manual "fuera de la rejilla" no dice si su duración es la del tipo o libre.
- **Severidad:** alta.
- **AD propuesto (ajuste a AD-8):** "La Cita guarda `Inicio` y `Fin` en UTC; `Fin` se fija al crear o reprogramar con la duración vigente del Tipo de visita y no cambia si el tipo cambia después. El rango es semiabierto `[Inicio, Fin)`, igual en la restricción, en la calculadora y en el dominio: dos Citas contiguas no se traslapan. La Cita del consultorio usa la duración de su Tipo de visita."

### H9 — Ids tipados e `IClock` inalcanzables desde Contracts; diagrama de referencias incompleto

- **Unidades:** historia "pruebas de arquitectura" contra cualquier historia que defina un comando, DTO o evento.
- **Cómo cumplen y chocan:** AD-18 pone `CitaId` y `PatientId` en el kernel (`BuildingBlocks.Domain`). AD-1: "`Domain` solo referencia `BuildingBlocks.Domain`" y el diagrama solo permite `Agenda.Contracts → BuildingBlocks.Contracts`. Los eventos y comandos viven en Contracts y "llevan identificadores": un constructor usa `Guid` crudo en Contracts (se pierde la protección de AD-18 justo en la frontera), otro añade la referencia prohibida, otro duplica el tipo. Lo mismo con `IClock`: Notificaciones no tiene dominio ni flecha al kernel, y lo necesita para programar el Recordatorio. Además el diagrama, que "prohíbe toda referencia que no aparece", no tiene `Pacientes.Infrastructure`, `Notificaciones.Infrastructure`, ninguna implementación de Contenido, ni `Pacientes.Contracts → BuildingBlocks.Contracts`, ni `Web → Notificaciones/Pacientes Infrastructure`. Quien escriba las pruebas desde el diagrama hará fallar código legítimo.
- **Severidad:** alta. Bloquea desde la primera historia.
- **AD propuesto (ajuste a AD-1 y AD-18):** "Los ids tipados, `IClock` y `ZonaDelConsultorio` viven en `BuildingBlocks.Contracts`, que todos los proyectos pueden referenciar; `BuildingBlocks.Domain` referencia a `BuildingBlocks.Contracts`. Los Contracts usan siempre el id tipado, nunca `Guid`." Y completar el diagrama con todas las flechas reales (Infrastructure de cada módulo, `*.Contracts → BuildingBlocks.Contracts`, `Features → BuildingBlocks`).

### H10 — Punto de control de acceso y origen del actor

- **Unidades:** historia "Cancelar por el Paciente" (página pública del enlace) contra historia "Cancelar por el Consultorio" (administración); y la tarea programada "Agenda del día" contra la consulta de Agenda que la alimenta.
- **Cómo cumplen y chocan:** AD-22 dice "todo comando de administración exige usuario autenticado" sin decir dónde. Un constructor lo pone en la página (`[Authorize]`); otro espera un middleware de Wolverine. Con el control solo en la página, la página pública (que referencia los mismos Contracts) puede enviar `CancelarCita(CitaId)` de administración y nada lo detiene. Con un middleware que lee `HttpContext.User`, falla dentro del circuito de Interactive Server, donde no hay `HttpContext`. El actor de la bitácora (AD-16) sale del tipo de comando para unos y de la identidad ambiente para otros: una cancelación del Paciente queda registrada como *Administrador* o como *Sistema*. Por último, la Agenda del día pide a Agenda "qué hay ese día" (datos de pacientes y motivo): si esa consulta es de administración, la tarea en segundo plano no tiene usuario y es rechazada; si no lo es, queda accesible desde la interfaz pública.
- **Severidad:** alta (seguridad y bitácora).
- **AD propuesto (nuevo):** "Todo comando o consulta declara su **audiencia** con una interfaz marcadora: `IPublico`, `IDePaciente` (lleva el token; el handler localiza la Cita por el hash del token, nunca por `CitaId`), `IDeAdministrador`. Un middleware de Wolverine rechaza `IDeAdministrador` si `IActorActual` (puerto en `BuildingBlocks.Contracts`, implementado en el host a partir del `AuthenticationState`) no es un usuario autenticado. El actor de la bitácora se deriva de la audiencia: Paciente, Administrador; `Sistema` solo para handlers de mensajes sin usuario. Lo que un módulo pide a otro va por `I<Módulo>Api`, que no es despachable por `ISender` ni visible para la interfaz."

### H11 — Dueño del Recordatorio al reprogramar o cancelar

- **Unidades:** handler de `CitaAgendada` (programa el Recordatorio) contra handlers de `CitaReprogramada` y `CitaCancelada` en Notificaciones.
- **Cómo cumplen y chocan:** AD-20 dice que el Recordatorio es un mensaje programado durable, y FR-22 que "se ajusta o se elimina". Wolverine no ofrece cancelar un mensaje programado por clave de negocio. Un constructor programa y confía en que el handler de cancelación lo borre; ese otro constructor confía en que el Recordatorio revalide al dispararse. Resultado: recordatorio de una Cita cancelada, o dos recordatorios tras reprogramar (el viejo y el nuevo). Cambiar la hora del Recordatorio en Configuración (AD-14) tampoco mueve los ya programados.
- **Severidad:** alta. Un recordatorio de una Cita cancelada por el Consultorio es el peor correo posible.
- **AD propuesto (ajuste a AD-20):** "Los mensajes programados nunca se borran ni se mueven. `EnviarRecordatorio` lleva `CitaId` e `InicioEsperado`; al dispararse pregunta a Agenda y envía solo si la Cita sigue Agendada y su `Inicio` es el esperado; si no, se descarta en silencio. `CitaReprogramada` programa uno nuevo. Si la hora de envío ya pasó al programar, no se programa. El cambio de hora en Configuración aplica solo a los que se programen después."

### H12 — Cascada de cancelación de una custom: síncrona o por evento

- **Unidades:** handler `GuardarAgendaCustom` contra un posible handler `CancelarCitasFueraDeAgenda`; y la variante "aplicar a varias fechas".
- **Cómo cumplen y chocan:** un constructor guarda la custom, carga cada Cita afectada, la cancela, escribe N entradas de bitácora y N eventos en una sola transacción (cumple "un comando, una transacción" y AD-4). Otro guarda la custom, levanta un evento de dominio y un segundo handler cancela después, cada Cita en su transacción (también cumple). En el segundo hay una ventana con el día cerrado y Citas activas "en silencio", y si falla a medias queda inconsistente. Para varias fechas en una acción (FR-13), uno hace un comando por fecha y otro un comando con N fechas: atomicidad distinta ante un fallo. Y el actor de esas cancelaciones es *Administrador* para uno y *Sistema* para otro.
- **Severidad:** alta.
- **AD propuesto (ajuste a AD-6):** "Guardar una custom y cancelar las Citas que deja fuera ocurren en la **misma** transacción del mismo comando, con una entrada de bitácora y un `CitaCancelada` por Cita, todas con actor *Administrador*. Aplicar a varias fechas es un solo comando con todas las fechas: o se aplican todas o ninguna."

---

## Medios

### H13 — Rejilla implementada dos veces; ancla y duraciones distintas

- **Unidades:** `CalculadoraDeDisponibilidad` (AD-5, "única implementación del cálculo") contra `Cita.Agendar` (AD-7, valida "la rejilla").
- **Choque:** son dos implementaciones de la misma rejilla. Con un tramo 15:45–18:00, una ancla los pasos al inicio del tramo (15:45, 16:15…) y la otra a la hora en punto (16:00, 16:30…). Con tipos de 30 y 45 min, una usa paso = duración del tipo elegido y la otra paso fijo de 30. Una re-ancla después de una Cita manual de 10:15–10:45 (ofrece 10:45) y la otra no: la calculadora ofrece un horario que el agregado rechaza. Aparte, AD-7 pone el tope semanal como validación de la Cita y AD-13 dice que Agenda "cuenta": el agregado no puede contar.
- **AD propuesto (ajuste a AD-5 y AD-7):** "La rejilla es un único objeto de dominio, `Rejilla`, usado por la calculadora y por `Cita.Agendar`: horas de inicio = inicio del tramo + k × duración del Tipo de visita elegido, mientras la Cita quepa completa en el tramo. No se re-ancla por Citas existentes ni entre tramos. El tope semanal lo verifica el handler antes de llamar al agregado; el agregado no lo conoce."

### H14 — Idempotencia y orden de los envíos

- **Unidades:** handler de Notificaciones contra el canal de correo (`INotificationChannel`) y los reintentos de Wolverine.
- **Choque:** entrega "al menos una vez": el correo sale, el handler falla antes de confirmar, se reintenta y sale otra vez. Sin `EventoId` no hay clave natural (una Cita puede reprogramarse varias veces). Tampoco hay orden garantizado: el aviso de reprogramación puede salir después del de cancelación.
- **AD propuesto (ajuste a AD-20):** "Notificaciones registra cada envío en su esquema con clave única (`EventoId`, tipo de mensaje, destinatario) y consulta esa clave antes de enviar. Un aviso de reprogramación o un recordatorio verifica contra Agenda que la Cita sigue Agendada antes de salir."

### H15 — Dueño de correo, Consentimiento y Motivo de consulta

- **Unidades:** historia "Reconocer al Paciente" (Pacientes) contra historia "Reservar" (Agenda) y contra "Agenda del día" (Notificaciones).
- **Choque:** ningún AD dice dónde viven el Consentimiento (¿por Paciente, la primera vez, o por Cita?), el Motivo de consulta (dato de salud) ni qué pasa cuando un Paciente conocido reserva con otro correo o sin correo. Un constructor guarda el Consentimiento en Paciente y otro en Cita; uno sobrescribe el correo en `BuscarOCrear` y otro lo conserva, con lo que "sin correo" cambia de significado y el correo de confirmación va a la dirección vieja.
- **AD propuesto (nuevo):** "Pacientes guarda nombre, teléfono y correo; `BuscarOCrear` actualiza el correo solo si llega uno no vacío. Agenda guarda en la Cita el Motivo de consulta y la fecha y hora del Consentimiento de esa reserva. El Motivo solo sale de Agenda por su API interna hacia la Agenda del día y hacia consultas de administración."

### H16 — Anti-bots y límite de frecuencia: nadie o los dos verifican

- **Unidades:** página pública de confirmación (UI) contra handler `AgendarCita`.
- **Choque:** AD-22 dice que "las páginas llevan" la verificación; AD-12 dice que la entrada se valida en el middleware del comando. El constructor de la página pone el widget y pasa el resultado en el comando, esperando que el backend lo verifique; el del handler asume que el host ya lo verificó. Nadie llama al proveedor. Con SSR sin estado (AD-11) tampoco está dicho en qué paso se exige, ni si el flujo de reprogramación (que reutiliza las mismas páginas) lo lleva.
- **AD propuesto (ajuste a AD-22):** "La verificación anti-bots y el límite de frecuencia se aplican en el host, sobre el POST que confirma una reserva o una reprogramación, **antes** de `ISender.Send`. Los comandos no llevan campos de anti-bots. Las páginas de solo lectura llevan únicamente límite de frecuencia."

### H17 — Doble envío del formulario de confirmación

- **Unidades:** página de confirmación (SSR estático) contra `AgendarCita` y la restricción de exclusión.
- **Choque:** el Paciente toca dos veces o la red reintenta. El segundo POST choca con su propia Cita (`23P01`) y, según AD-8, ve "ese horario ya no está, elige otro", cuando en realidad ya reservó. Como el token solo existe en la primera respuesta (AD-19), si esa respuesta se pierde no hay forma de mostrarle su enlace. Si el Paciente elige otro horario, queda con dos Citas.
- **AD propuesto (ajuste a AD-11):** "El formulario de confirmación lleva una `ClaveDeIntento` (Guid generado al pintar el paso) que se guarda en la Cita con índice único. Un POST repetido con la misma clave devuelve el mismo resultado de éxito. Tras confirmar, la respuesta es una redirección a la página del Enlace de gestión." (Requiere el token derivable de H1.)

### H18 — Custom sobre hoy cancela citas que ya ocurrieron

- **Unidades:** `GuardarAgendaCustom` contra el estado derivado *Atendida* (AD-9).
- **Choque:** a las 5:10 pm Gerardo recorta hoy a 3:30–5:00. Las Citas de 3:30 y 4:00 siguen guardadas como `Agendada` (Atendida se deriva). Un constructor filtra "citas activas" como "no canceladas de esa fecha" y no las toca porque caben; pero al cerrar el día completo (cero tramos) las cancela todas, incluidas las ya atendidas, y les manda la disculpa.
- **AD propuesto (ajuste a AD-6):** "Una custom solo puede guardarse para hoy o fechas futuras y solo cancela Citas cuyo `Inicio` es posterior a `now`."

### H19 — Semana, "ese día", "ya pasó" y día de la semana

- **Unidades:** tope semanal (Agenda) contra Agenda del día (Notificaciones) contra vistas de agenda e historial (UI).
- **Choque:** AD-13 no dice la zona de la "semana de calendario" (está en el memlog, no en el spine): una Cita del domingo 7 pm local es lunes en UTC. "La Agenda del día pregunta qué hay ese día" no dice si el parámetro es una fecha local o un rango UTC. *Atendida* es "hora ya pasada": inicio para uno, fin para otro, y se deriva por separado en la agenda, el historial y el enlace. El día de la semana de la base se guarda como `DayOfWeek` de .NET (domingo = 0) o ISO (lunes = 1). Cada consumidor convierte a hora local por su cuenta.
- **AD propuesto (ajuste a AD-17 y AD-9):** "Toda consulta 'por día' o 'por semana' recibe `DateOnly` **local** y la convierte el módulo dueño. La semana va de lunes 00:00 a domingo 24:00 hora local. El día de la semana se guarda en ISO (1 = lunes … 7 = domingo). Los DTO entregan instantes UTC y la conversión para mostrar usa una única función `ZonaDelConsultorio`. *Atendida* = `Agendada` con `Fin` ≤ `now`, y la calcula Agenda en el DTO (`EstadoMostrado`); nadie más la deriva. El enlace vence cuando `Inicio` ≤ `now`."

### H20 — Actualización perdida entre dos cambios a la misma Cita

- **Unidades:** `ReprogramarCita` (Paciente) contra `CancelarCita` o la cascada de una custom (Administrador).
- **Choque:** ambos cargan la Cita `Agendada`; uno cancela y confirma; el otro guarda solo la columna de horario. Queda una Cita `Cancelada` con horario nuevo, con correo de cancelación y correo de reprogramación. Ningún AD pide control de concurrencia en el agregado.
- **AD propuesto (ajuste a AD-4):** "Los agregados llevan concurrencia optimista (`xmin` en PostgreSQL). El conflicto se traduce a `Resultado.Conflicto`, nunca a excepción ni a reintento automático."

### H21 — Quién compone lecturas que cruzan Agenda y Pacientes

- **Unidades:** "Ver la agenda" (FR-14: hora, Paciente, "sin correo") contra "Lista de Pacientes con historial" (FR-18).
- **Choque:** sin joins entre esquemas (AD-2). Para la agenda, un constructor compone en el handler de Agenda llamando a `IPacientesApi` (referencia permitida) y otro devuelve solo `PatientId` y espera que la página haga dos consultas. Para el historial, Pacientes no puede referenciar Agenda, así que la composición solo puede estar en la interfaz. Los DTO resultantes no coinciden y la marca "sin correo" se calcula en dos sitios.
- **AD propuesto (ajuste a AD-3):** "Las lecturas de Agenda que muestran datos del Paciente las compone el handler de Agenda con una llamada por lote `IPacientesApi.ObtenerResumenes(ids)` (nombre, tiene correo). El historial de un Paciente lo compone la página con dos consultas: la de Pacientes y `Agenda.HistorialDeCitas(PatientId)`."

### H22 — Reprogramar cuenta la propia Cita y tapa su propio horario

- **Unidades:** `ReprogramarCita` contra `CalculadoraDeDisponibilidad` y el tope semanal.
- **Choque:** AD-7 valida el tope también al reprogramar: con tres Citas en la semana, el Paciente no puede mover ninguna dentro de la misma semana porque la propia cuenta. La calculadora recibe "citas activas", incluida la que se mueve, así que un constructor oculta el horario actual y los vecinos que traslapa, y otro los muestra. Además, cuando un Paciente reprograma por su enlace una Cita de origen *consultorio*, no está dicho si conserva origen y nota (y con ello la inmunidad de H3).
- **AD propuesto (ajuste a AD-7):** "Al reprogramar, la propia Cita se excluye de las citas activas de la calculadora y del conteo del tope. El origen no cambia al reprogramar; una Cita reprogramada por el Paciente se valida con las reglas en línea sea cual sea su origen."

### H23 — Mecanismo y forma de la bitácora

- **Unidades:** cualquier par de historias de Agenda que escriben bitácora.
- **Choque:** AD-16 fija el dueño y la transacción, no el mecanismo. Un constructor escribe la entrada en el handler; otro añade un interceptor de `SaveChanges` que la genera de los eventos de dominio: entradas dobles. El diagrama ER solo relaciona `BITACORA` con `CITA`, pero AD-16 incluye base y custom; la tabla que cree la primera historia no sirve a la segunda.
- **AD propuesto (ajuste a AD-16):** "Una sola tabla `agenda.bitacora` con: instante UTC, actor, tipo de entidad (Cita, AgendaBase, AgendaCustom), id o fecha de la entidad, acción y detalle en JSON. La entrada la escribe únicamente el puerto de escritura del agregado a partir de los eventos de dominio levantados; los handlers no escriben bitácora."

### H24 — Corte de reprogramación y umbral tardío calculados fuera de Agenda

- **Unidades:** página del Enlace de gestión (UI) contra `Cita.Reprogramar` y `Cita.Cancelar`.
- **Choque:** la página decide si muestra "Reprogramar" o "Cancelar y volver a agendar" (FR-9). Si calcula con su propio "2 horas" o con el umbral tardío en lugar del corte de reprogramación (son dos ajustes distintos en AD-14, hoy con el mismo valor), ofrece un botón que el agregado rechaza.
- **AD propuesto (ajuste a AD-14):** "Las reglas de Agenda nunca se evalúan fuera de Agenda. La consulta del enlace devuelve `PuedeReprogramar` y `CancelarSeriaTardia` ya calculados."

---

## Bajos

### H25 — Cambio de base: vista previa o resultado

- **Unidades:** pantalla de Configuración contra `GuardarAgendaBase`.
- **Choque:** FR-12 pide ver las Citas afectadas "antes de guardar"; AD-6 dice "el comando devuelve". La interfaz espera una consulta previa que el backend no construyó.
- **AD propuesto:** "Base y custom tienen ambas una consulta previa con la misma forma de respuesta; el comando de la base devuelve lo mismo como constancia."

### H26 — Cómo se entera la agenda del Administrador de una Cita nueva

- **Unidades:** vista de agenda (Interactive Server) contra `AgendarCita`.
- **Choque:** FR-14 pide que aparezca "sin recargar". La interfaz no puede usar Wolverine (AD-10) y Agenda no publica nada hacia la interfaz (AD-3). Uno hace sondeo; otro espera un aviso en proceso.
- **AD propuesto:** "La vista de agenda se refresca por sondeo con `ISender` a intervalo fijo; no hay canal de avisos hacia la interfaz en el piloto."

### H27 — Dueño de los correos de Selina y del Administrador

- **Unidades:** Agenda del día y alerta de Cancelación tardía contra Configuración.
- **Choque:** el destinatario puede salir de Identity (host), de Contenido o de la configuración de Notificaciones.
- **AD propuesto (ajuste a AD-14):** "Notificaciones guarda en su configuración el correo de Selina y el del Administrador para alertas."

### H28 — Normalización de teléfono y nombre en dos lugares

- **Unidades:** `AgendarCita` (tope por teléfono) contra `BuscarOCrear`.
- **Choque:** si Agenda normaliza a E.164 para preguntar por el tope y Pacientes normaliza para reconocer, dos normalizadores pueden diferir en un prefijo o un acento.
- **AD propuesto (ajuste a AD-18):** "Solo Pacientes normaliza teléfono y nombre. `IPacientesApi` recibe el texto tal como lo escribió la persona."

### H29 — Estructura de Contenido y origen de sus datos

- **Unidades:** historia del sitio contra plantillas de Notificaciones.
- **Choque:** AD-2 da a Contenido un esquema, el árbol de carpetas no le da proyectos y FR-30 dice que el contenido cambia por despliegue. Uno crea tabla y pantalla; otro, un archivo.
- **AD propuesto (ajuste a AD-15):** "En el piloto Contenido no tiene tablas: `IContenidoApi` sirve valores de configuración del despliegue. El esquema `contenido` se crea cuando exista edición desde administración."

---

## Contradicciones textuales detectadas

1. **AD-7 contra AD-9 y FR-9:** "`Cita.Agendar`, también al reprogramar" sugiere una Cita nueva; "reprogramar no es un estado" y "la Cita sigue Agendada" sugieren la misma (H2).
2. **AD-6 contra AD-7:** "cancela las citas activas que quedan fuera" no excluye las del consultorio; AD-7 solo las protege de una custom "ya existente" (H3).
3. **AD-19 contra FR-21 y AD-3:** el token no se guarda ni viaja, pero el correo debe llevarlo (H1).
4. **AD-18 contra AD-1 y el diagrama:** los ids viven donde los Contracts no pueden referenciar (H9).
5. **AD-8 contra la convención de transacciones:** un conflicto "que incluye los libres" exige leer después de una transacción abortada (H7).
6. **AD-5 contra AD-7:** "única implementación del cálculo" y, a la vez, la Cita valida la rejilla por su cuenta (H13).
7. **AD-7 contra AD-13:** el tope aparece como validación de la Cita y como conteo del handler (H13).
8. **AD-2 contra el memlog y el árbol de carpetas:** el esquema `contenido` existe en el AD y no en la decisión original ni en la estructura (H29).

## Orden sugerido para cerrar

1. H1, H2, H9 antes de escribir la primera historia: fijan identidad, token y referencias.
2. H6, H8, H7, H5 antes de la historia de reserva en línea.
3. H3, H4, H12, H18 antes de la historia de Agenda custom.
4. H10 antes de cualquier pantalla de administración o del Enlace de gestión.
5. H11, H14 antes de la primera historia de Notificaciones.
6. El resto puede resolverse al redactar la historia que lo toca, siempre que quede escrito en el spine y no en la historia.
