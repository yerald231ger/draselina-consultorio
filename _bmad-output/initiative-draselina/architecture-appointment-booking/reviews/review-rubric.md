# Revisión del architecture spine — Sistema de citas, Dra. Selina Medina

- **Documento revisado:** `../architecture-appointment-booking.md` (status `draft`, 2026-10-04)
- **Consultados:** `../.memlog.md`, el PRD `prd-appointment-booking.md` y, para contrastar, `docs/Appointment Booking System — Solution Plan.md`
- **Tipo de revisión:** contra la lista de un buen spine (7 criterios). Solo lectura; no se modificó ningún documento fuente.
- **Límite de esta revisión:** las versiones no se volvieron a verificar en la web; el criterio 4 se juzga contra las entradas `version` del memlog.

## Veredicto

El spine es bueno en su núcleo: las decisiones de dominio (AD-5 a AD-9, AD-13 a AD-17) son concretas, verificables y cubren los puntos donde dos historias de Agenda chocarían. No está listo para alimentar historias todavía, por cuatro huecos que sí provocan divergencia: el Enlace de gestión no puede llegar al correo con las reglas actuales (AD-19 contra AD-3 y AD-20), el diagrama de dependencias es incompleto bajo su propia regla de "lo que no aparece está prohibido", la autorización y el actor no tienen punto de aplicación, y las migraciones, los datos iniciales y el almacén de Identity no están decididos ni diferidos. Ninguno exige rediseñar; todos se cierran con una o dos líneas por AD.

**Conteo:** 0 críticos, 4 altos, 10 medios, 9 bajos.

---

## Hallazgos de severidad alta

### A-1. El Enlace de gestión no puede llegar al correo con las reglas actuales

- **Ubicación:** AD-19, AD-3, AD-20 (criterios 2 y 7).
- **Problema:** AD-19 dice "se guarda solo su hash SHA-256". AD-3 dice que los eventos "llevan identificadores y tiempos". AD-20 dice que Notificaciones "reacciona a los eventos de Agenda" de forma asíncrona. Pero FR-21 exige que la confirmación por correo incluya el Enlace de gestión, FR-23 exige un enlace en el aviso de Cancelada por el Consultorio, y FR-5 lo exige en el evento de calendario. Cuando Notificaciones procesa `CitaAgendada`, el token en claro ya no existe en ningún lado: no está en la base (solo el hash) y no puede ir en el evento. Quien construya Agenda y quien construya Notificaciones resolverán esto cada uno a su manera: meter el token en el evento (queda en claro en la tabla del outbox, lo que rompe la intención de AD-19), guardarlo en claro, o generar uno nuevo por correo. El plan de solución arrastra la misma contradicción (paso 5 del flujo contra `ManageTokenHash`).
- **Arreglo sugerido:** decidir una de dos y escribirla en AD-19.
  - (a) Token derivado: `token = HMAC-SHA256(clave del servidor, CitaId)`. Agenda expone por contrato una consulta `ObtenerEnlaceDeGestion(CitaId)` que Notificaciones y la pantalla de confirmación usan. No se guarda nada recuperable y el evento sigue sin secretos. La clave entra a la lista de secretos (ver M-10).
  - (b) Excepción explícita: `CitaAgendada` y `CitaReprogramada` llevan el token en claro, y AD-3 lo nombra como la única excepción, con la regla de que las filas del outbox se purgan al entregarse.
  - En ambos casos, decir si el token sobrevive a una reprogramación (FR-9 implica que sí) y qué muestra el enlace de una Cita cancelada.

### A-2. El diagrama de dependencias es incompleto bajo su propia regla de mundo cerrado

- **Ubicación:** sección "Invariants & Rules", diagrama de dependencias; AD-1, AD-10, AD-18; árbol de carpetas (criterio 7).
- **Problema:** el texto dice "Toda referencia que no aparece está prohibida y la verifican las pruebas de arquitectura". Leído al pie de la letra, el sistema no compila:
  1. **Faltan proyectos enteros.** `Pacientes.Infrastructure` y `Notificaciones.Infrastructure` existen en el árbol y en la tabla de capas, pero no en el diagrama. El host referencia `Agenda.Infrastructure` pero `Pacientes.Features` y `Notificaciones.Features` directamente (`WEB --> PF`, `WEB --> NF`), así que nadie registra sus DbContext.
  2. **Contenido no tiene implementación.** Solo aparece `Contenido.Contracts`; el árbol dice `Modules/Contenido/` sin subproyectos; el host no lo referencia. Nadie implementa `IContenidoApi`.
  3. **Solo `Agenda.Contracts` puede ver `BuildingBlocks.Contracts`** (`AC --> BC`). AD-10 obliga a que todo caso de uso sea un `ICommand<T>` o `IQuery<T>` en `*.Contracts`, y esos tipos y `Resultado` viven en `BuildingBlocks/Contracts`. Pacientes, Notificaciones y Contenido no pueden declarar comandos.
  4. **Los ids tipados no son alcanzables desde Contracts.** AD-18 pone `CitaId` y `PatientId` "en el kernel compartido" (`BuildingBlocks/Domain`), pero ningún `*.Contracts` referencia `BuildingBlocks.Domain`. Los eventos de AD-3 y `IPacientesApi` necesitan esos ids. Un agente usará `Guid` crudo en contratos y otro añadirá la referencia; es justo la divergencia que AD-18 quiere evitar ("ids intercambiables").
  5. `IClock` está en `BuildingBlocks/Domain`, y `Notificaciones.Features` (sin dominio) lo necesita para programar mensajes; tampoco hay flecha.
- **Arreglo sugerido:** completar el diagrama: añadir `PI`, `NI` y los proyectos de Contenido; `WEB --> PI`, `WEB --> NI`, `WEB --> Contenido`; `PC`, `NC`, `CC --> BC`. Mover los ids tipados (y, si se quiere, `IClock`) a `BuildingBlocks.Contracts`, o declarar `*.Contracts --> BuildingBlocks.Domain`; elegir una y reflejarla en AD-18. Añadir una línea sobre si las referencias transitivas cuentan para las pruebas de arquitectura.

### A-3. La autorización y el actor no tienen punto de aplicación

- **Ubicación:** AD-22, AD-16, AD-10 (criterios 2 y 6, seguridad).
- **Problema:** AD-22 dice "Todo comando de administración exige usuario autenticado", sin decir dónde se comprueba ni cómo se distingue un comando de administración. Como la interfaz llama a `ISender` en proceso (AD-10), una página pública puede despachar cualquier comando; la protección puede quedar en la página (`[Authorize]`), en un middleware de Wolverine o en cada handler, y cada historia elegirá distinto. Además la regla habla solo de comandos: las **consultas** de administración (agenda del día, lista de Pacientes con historial, FR-14 y FR-18) exponen datos de salud y no están cubiertas, contra PRD §6 ("Solo el Administrador ve datos de Pacientes"). El mismo hueco afecta a AD-16: la bitácora necesita el actor (`Paciente`, `Administrador`, `Sistema`), y no se dice de dónde lo obtiene el handler ni qué actor llevan las cancelaciones en cascada de una custom.
- **Arreglo sugerido:** una regla en AD-22: los comandos y consultas de administración se marcan en `*.Contracts` (interfaz marcadora o atributo), un único middleware de Wolverine rechaza los marcados sin usuario autenticado con un `Resultado` de no autorizado, y las páginas de administración llevan además `[Authorize]`. Una regla en AD-16: el actor lo resuelve un único servicio de contexto en el host (administrador autenticado → `Administrador`; comando con Enlace de gestión o reserva pública → `Paciente`; mensajes programados → `Sistema`); las citas canceladas por una custom llevan actor `Administrador`.

### A-4. Migraciones, datos iniciales y almacén de Identity: ni decididos ni diferidos

- **Ubicación:** AD-2, AD-8, AD-22, "Structural Seed", "Deferred" (criterios 1, 3 y 6).
- **Problema:** hay cuatro DbContext de módulo, el esquema `wolverine`, la extensión `btree_gist` y una restricción de exclusión que EF no modela (SQL crudo en una migración). El spine no dice quién aplica las migraciones (arranque del host, un proyecto migrador lanzado por el AppHost, o el pipeline, como dice el plan), ni dónde vive el historial de migraciones de cada contexto, ni quién crea la extensión. La primera historia de cada módulo lo decidirá por su cuenta. Tampoco se dice:
  - **Datos iniciales:** Agenda base L–V 15:30–18:00 (FR-12), los dos Tipos de visita (FR-1), los valores por defecto de las reglas (FR-19) y la cuenta del Administrador (AD-22 prohíbe el registro público, así que alguien debe crearla).
  - **Almacén de Identity:** AD-22 pone Identity "en el host", pero el diagrama de contenedores lista los esquemas `agenda, pacientes, notificaciones, contenido, wolverine` y ninguno para Identity; AD-2 solo habla de módulos.
- **Arreglo sugerido:** un AD nuevo ("Esquema y datos iniciales"): cada DbContext con su tabla de historial en su propio esquema; un único mecanismo de aplicación (recomendado: un proyecto migrador que el AppHost ejecuta antes del host en local y el pipeline en producción; el host nunca migra al arrancar); la extensión y la restricción de exclusión van en la migración inicial de Agenda; los datos iniciales son migraciones o un sembrador idempotente del módulo dueño; el Administrador se crea por un comando de línea o sembrador con secreto, no por una página. Añadir el esquema `identidad` (propiedad del host) al diagrama de contenedores y a AD-2.

---

## Hallazgos de severidad media

### M-1. Dos mecanismos síncronos entre módulos, sin regla para elegir

- **Ubicación:** AD-3, AD-10, AD-15.
- **Problema:** AD-3 nombra interfaces (`IPacientesApi`, `IContenidoApi`) y, a la vez, "consultas de Agenda". AD-10 dice que todo caso de uso es `ICommand`/`IQuery` despachado por `ISender` con un handler. Notificaciones llamará a Pacientes por interfaz y a Agenda por `ISender`. Para la interfaz es peor: AD-15 dice que los datos del consultorio "se sirven por `IContenidoApi`", y AD-10 que la interfaz habla "por `ISender`". Tampoco se dice quién implementa las interfaces `I*Api` ni dónde se compone una vista que mezcla módulos (la agenda de FR-14 necesita Cita más nombre del Paciente; el historial de FR-18, lo contrario).
- **Arreglo sugerido:** una sola frase en AD-3: o todo lo síncrono es `IQuery`/`ICommand` por `ISender` (y `IPacientesApi` e `IContenidoApi` desaparecen), o las `I*Api` son solo para módulo-a-módulo, se implementan en `Features` del dueño y la interfaz nunca las inyecta. Añadir: "las vistas que mezclan módulos las compone el handler del módulo dueño de la pantalla llamando al otro por contrato, en lote; la interfaz no hace N consultas".

### M-2. El contrato de los eventos no fija su contenido ni la idempotencia

- **Ubicación:** AD-3, AD-20.
- **Problema:** los eventos son el único contrato entre Agenda y Notificaciones, y solo se dan sus nombres. FR-24 necesita saber si la cancelación fue tardía y FR-23 si fue por el Consultorio; eso no es "identificador ni tiempo". FR-23 (reprogramada) necesita la hora anterior o al menos la nueva. FR-21 depende de si las Citas del consultorio emiten `CitaAgendada` (el PRD lo asume). La entrega por outbox es al-menos-una-vez y no hay regla de idempotencia, así que un reintento duplica correos.
- **Arreglo sugerido:** listar los campos mínimos en AD-3: `CitaAgendada(CitaId, PatientId, InicioUtc, FinUtc, Origen)`, `CitaReprogramada(CitaId, PatientId, InicioAnteriorUtc, InicioUtc, FinUtc)`, `CitaCancelada(CitaId, PatientId, InicioUtc, Motivo)`. Cambiar la frase a "nunca datos de contacto ni Motivo de consulta". Añadir en AD-20: "los handlers de Notificaciones son idempotentes; registran cada envío en `notificaciones` con clave (CitaId, tipo, InicioUtc)".

### M-3. Recordatorio: no se fija cómo se ajusta o elimina, ni qué pasa al cambiar la hora configurada

- **Ubicación:** AD-20, AD-14; FR-22, FR-19.
- **Problema:** "mensajes programados durables de Wolverine" admite dos diseños incompatibles: un mensaje programado por Cita (que hay que localizar y anular al reprogramar o cancelar, y reprogramar en masa si el Administrador cambia la hora del Recordatorio), o un disparo diario que pregunta a Agenda qué Citas hay mañana. La historia de "confirmación" y la de "reprogramar" se construyen por separado y pueden asumir cada una un diseño.
- **Arreglo sugerido:** fijar uno. Recomendado: el Recordatorio se programa por Cita y, al dispararse, consulta a Agenda; si la Cita ya no está Agendada o su hora cambió, se descarta en silencio (nunca se anula un mensaje programado). `CitaReprogramada` programa uno nuevo. O bien: un único disparo diario, igual que la Agenda del día. Decir también cómo se vuelve a programar el disparo diario y qué pasa con lo ya programado al cambiar la hora en Configuración.

### M-4. AD-8 no dice dónde se traduce la violación `23P01`

- **Ubicación:** AD-8, convención "Transacciones".
- **Problema:** con el middleware transaccional de Wolverine el `SaveChanges` ocurre después de que el handler devuelve, así que el handler no puede capturar la violación ni devolver "un `Resultado` de conflicto que incluye los libres restantes". Además, calcular los libres tras el fallo exige una segunda lectura fuera de la transacción abortada. La regla es correcta pero no es aplicable tal como está escrita; AD-7 tiene dos caminos de creación más la reprogramación, y cada uno traducirá el error a su modo.
- **Arreglo sugerido:** "La violación `23P01` la traduce un único punto (el puerto de escritura de Cita o un middleware de Wolverine) a `Resultado.Conflicto`. El conflicto no lleva los libres; la página vuelve a pedir la disponibilidad con la consulta normal." Vale para `Agendar`, `AgendarPorConsultorio` y reprogramar.

### M-5. Reserva en línea: atomicidad entre Pacientes y Agenda, y dueño del Consentimiento

- **Ubicación:** AD-7, AD-13, AD-18, convención "Un comando, una transacción"; FR-3, FR-15.
- **Problema:** `Cita.Agendar` llama primero a `IPacientesApi.BuscarOCrear` (otro DbContext, otro esquema) y luego escribe la Cita. No se dice si ambas escrituras comparten transacción. Un agente intentará enlistar los dos contextos; otro dejará que Pacientes confirme por su cuenta. Si luego la Cita choca con `23P01`, queda un Paciente sin Cita. Además, ningún AD dice dónde se guarda el **Consentimiento** con su fecha y hora (FR-3, y el registrado por el Administrador en FR-15): el plan lo pone en `Patient.ConsentAt`, pero es un dato legal por reserva y el spine no lo adopta ni lo cambia. Tampoco se nombra al dueño del Motivo de consulta (se infiere Agenda por FR-25).
- **Arreglo sugerido:** en AD-7: "`BuscarOCrear` confirma en su propia transacción y es idempotente; un Paciente sin Cita es aceptable". En AD-18: índice único sobre (teléfono E.164, nombre normalizado) para que dos reservas simultáneas no creen duplicados, e incluir "espacios de más" en la normalización (FR-6 lo pide; el AD solo dice mayúsculas y acentos). Un renglón nuevo: "el Consentimiento (instante y origen: en línea o registrado por el Administrador) y el Motivo de consulta se guardan en la Cita".

### M-6. AD-6 y AD-7 se contradicen sobre las Citas del consultorio, y AD-6 se aparta de FR-12

- **Ubicación:** AD-6, AD-7; FR-12, FR-15.
- **Problema:** AD-7 dice "una custom ya existente no la cancela". AD-6 dice "Guardar una custom que deja citas activas fuera las cancela". Si el Administrador edita después esa misma custom (FR-13: "editarla cambia el día") o crea una custom sobre una fecha con una Cita manual fuera de horario, AD-6 al pie de la letra la cancela. No se dice si "quedar fuera" excluye las Citas de origen consultorio. Segundo punto: para la base, AD-6 dice "el comando devuelve las citas que quedan fuera", es decir, después de guardar; FR-12 dice que el Administrador "ve cuáles son antes de guardar". La custom sí tiene "consulta previa"; la base no. Tercero: entre la consulta previa y el guardado puede entrar una reserva nueva que se cancelaría sin haber sido mostrada.
- **Arreglo sugerido:** definir "queda fuera" una vez: "Cita Agendada futura de origen en línea que ya no cabe completa en un tramo vigente; las de origen consultorio nunca se cancelan por un cambio de agenda y se listan aparte". Dar a la base la misma consulta previa que a la custom (misma función de cálculo). El comando de guardar custom devuelve la lista de lo que realmente canceló.

### M-7. Contenido: esquema y DbContext contra "se actualiza por despliegue"

- **Ubicación:** AD-2, AD-15, diagrama de contenedores, árbol; FR-30.
- **Problema:** AD-2 da a Contenido un DbContext y el esquema `contenido`. FR-30 asume que los textos se editan como parte de un despliegue. AD-15 no dice si nombre, dirección, mapa y WhatsApp salen de la base, de configuración o de un archivo. El árbol muestra `Modules/Contenido/` sin capas y la tabla de capas no dice cuáles tiene ("no tienen capa de dominio" es lo único). Quien construya el sitio y quien construya las plantillas de correo necesitan saber de dónde sale el dato.
- **Arreglo sugerido:** decidir: en el piloto Contenido no tiene base de datos; los datos del consultorio son un archivo de configuración tipado dentro del módulo y los textos son páginas Razor; el esquema `contenido` se retira de AD-2 y del diagrama hasta que exista edición desde administración. O lo contrario, pero dicho. Listar las capas reales de Contenido en el árbol.

### M-8. "App Service o Container Apps" diferido sin fijar las restricciones que el diseño ya impone

- **Ubicación:** "Deferred", primera viñeta; AD-11, AD-20 (criterio 3).
- **Problema:** diferir el proveedor es razonable, pero el diseño ya depende de tres condiciones del hospedaje que no están escritas: (1) proceso siempre encendido, porque los mensajes programados de Wolverine (Recordatorio a las 9:00, Agenda del día a las 14:00) no se disparan en un contenedor escalado a cero; (2) una sola instancia o afinidad de sesión y WebSockets para Interactive Server (AD-11); (3) persistencia de las claves de Data Protection entre despliegues, o las cookies de Identity y los antiforgery se invalidan en cada liberación. Sin ellas, la decisión diferida puede tomarse mal.
- **Arreglo sugerido:** añadir a la viñeta: "Sea cual sea, una sola instancia siempre encendida, con WebSockets y claves de Data Protection persistidas fuera del contenedor".

### M-9. Datos de salud y tokens en la telemetría

- **Ubicación:** AD-21, AD-11, AD-19 (criterio 6, seguridad).
- **Problema:** AD-21 activa OpenTelemetry por ServiceDefaults; la instrumentación HTTP registra la ruta y la cadena de consulta. El Enlace de gestión lleva el token en la dirección y AD-11 hace viajar el estado de la reserva en la cadena de consulta. Los registros de Wolverine pueden incluir el cuerpo de los mensajes. No hay regla que impida que el token, el teléfono o el Motivo de consulta (dato sensible, PRD §6) acaben en los registros o en Application Insights.
- **Arreglo sugerido:** una línea en AD-21: "Nunca se registran nombre, teléfono, correo, Motivo de consulta ni el token; los registros llevan solo `CitaId` y `PatientId`. La ruta del Enlace de gestión se enmascara en las trazas y los datos de contacto viajan solo en el cuerpo del formulario, nunca en la cadena de consulta."

### M-10. El resto de la envolvente operativa está en silencio y no se sabe si se hereda del plan

- **Ubicación:** "Structural Seed · Entornos", "Deferred" (criterio 6).
- **Problema:** el spine no menciona secretos, respaldos, cifrado en reposo, alerta de errores al Administrador, integración continua ni el proveedor de correo. El PRD los exige (§5: "alerta de errores al Administrador y respaldo diario"; §6: "datos cifrados en reposo y respaldo diario"). El plan sí los cubre (Key Vault, respaldos automáticos, alertas de Application Insights, GitHub Actions, Azure Communication Services, Turnstile), pero el spine ya contradice partes de ese mismo bloque (Serilog fuera, App Service en duda), así que no queda claro qué sigue vigente. Las pruebas de arquitectura de AD-1 solo previenen algo si un pipeline las ejecuta. El memlog registra "production exports to Application Insights" y el spine lo perdió.
- **Arreglo sugerido:** una tabla corta "Envolvente operativa" con una fila por tema y su estado: secretos (local: parámetros de Aspire o user-secrets; producción: Key Vault; nunca en el repositorio) — decidido; integración continua (compilar, pruebas unitarias, de arquitectura e integración en cada push) — `[ADOPTED]`; respaldos diarios y cifrado en reposo — diferido al aprovisionamiento, con el requisito del PRD citado; alerta de errores por Application Insights — `[ADOPTED]`; proveedor de correo y anti-bots — `[ADOPTED]` o pregunta abierta. El proveedor de correo importa además por privacidad: la Agenda del día lleva Motivo de consulta (pregunta abierta 6 del PRD).

---

## Hallazgos de severidad baja

### B-1. Tecnología nombrada sin verificación o sin nombre

- **Ubicación:** "Stack" (criterio 4).
- **Problema:** con entrada `version` en el memlog: .NET 11, Wolverine 5.x, Aspire 13.x, Npgsql/EF Core, `btree_gist` en Azure. Sin entrada: Bootstrap 5.x, xUnit v3, bUnit, Testcontainers y la versión de PostgreSQL ("por fijar"). Sin nombre aunque un AD depende de ellas: la librería de pruebas de arquitectura (AD-1), `libphonenumber` (AD-12, solo en el memlog), el proveedor de correo y el anti-bots (AD-22). "Aspire 13.6" es una sola cifra, pero el memlog verificó paquetes en 13.4, 13.5 y 13.6. AD-12 descansa en una función de .NET 11 verificada contra notas de la vista previa 5; el spine no la marca como riesgo.
- **Arreglo sugerido:** nombrar las librerías que faltan; marcar las no verificadas como `[ASSUMPTION]` igual que EF Core; añadir a la pregunta abierta 2 la validación en navegador de formularios SSR y su salida si cambia (validar solo en servidor, que AD-11 ya tolera).

### B-2. Diagrama de contenedores y diagrama ER no coinciden con los AD

- **Ubicación:** "Structural Seed" (criterio 7).
- **Problema:** el diagrama de contenedores no tiene `UI --> NO`, pero el de dependencias tiene `UI --> NC` y AD-14 dice que Configuración compone la configuración de Notificaciones. En el ER, `BITACORA` cuelga solo de `CITA`, mientras AD-16 registra también cambios de base y de custom. El sistema anti-bots no aparece como sistema externo.
- **Arreglo sugerido:** añadir `UI --> NO`; dejar `BITACORA` como entidad suelta con referencia genérica al objeto cambiado; añadir el anti-bots al diagrama.

### B-3. El compañero C4 declarado no existe

- **Ubicación:** frontmatter, `companions: c4-appointment-booking.md`.
- **Problema:** el archivo no está en la carpeta (solo el spine y el memlog). El memlog lo lista como entregable.
- **Arreglo sugerido:** crearlo o quitar la referencia hasta que exista.

### B-4. Convención de nombres contradicha por el propio spine

- **Ubicación:** "Consistency Conventions · Naming", AD-18, AD-20, árbol.
- **Problema:** "Términos de negocio en español", pero el id es `PatientId` (junto a `CitaId`) y el puerto es `INotificationChannel` en el módulo `Notificaciones`. La raíz es `DraSelina.Consultorio`, la tabla de capas dice `DraSelina.Consultorio.<Módulo>.Contracts`, el árbol dice `Consultorio.Web` y `BuildingBlocks/Contracts`, y el diagrama `Agenda.Contracts`; no queda claro el nombre de proyecto de BuildingBlocks ni si lleva la raíz.
- **Arreglo sugerido:** `PacienteId` e `ICanalDeNotificacion`, o declarar ambos como excepciones adoptadas. Una línea: "carpeta corta, ensamblado y namespace con la raíz completa, incluido `DraSelina.Consultorio.BuildingBlocks.*`".

### B-5. `Resultado` no tiene un conjunto cerrado de variantes

- **Ubicación:** AD-10, AD-8, AD-12, convención "Resultados".
- **Problema:** aparecen `Resultado.Invalido` y "un `Resultado` de conflicto", pero no la lista. Cada módulo añadirá las suyas y la interfaz tendrá que tratar variantes distintas por módulo.
- **Arreglo sugerido:** cerrar la lista en la convención: `Ok`, `Invalido` (por campo), `Conflicto`, `NoEncontrado`, `NoAutorizado`, `ReglaDeNegocio` (con código estable).

### B-6. Consecuencias del PRD sin dueño arquitectónico

- **Ubicación:** "Capability → Architecture Map" (criterio 5).
- **Problema:** las FR-1 a FR-30 tienen fila, pero tres consecuencias no tienen regla: (1) FR-14, "aparece sin recargar manualmente": no se dice cómo llega una reserva nueva al circuito del Administrador; (2) FR-5 y FR-21: el archivo de calendario lo generan la pantalla y el correo, dos unidades distintas, sin un generador único; (3) FR-29: nada impide indexar administración y Enlaces de gestión. Además, la fila de reserva no cita AD-22 (límite de frecuencia de FR-7) y la de notificaciones cubre FR-26 sin decirlo.
- **Arreglo sugerido:** (1) "la agenda de administración se refresca por sondeo cada N segundos" o por notificación en proceso; elegir una; (2) un único generador de `.ics` expuesto por contrato de Agenda; (3) una línea en AD-22 o AD-11: `noindex` en administración y en la ruta del Enlace de gestión. Añadir AD-22 a la primera fila del mapa.

### B-7. La rejilla y la ventana se validan en dos sitios

- **Ubicación:** AD-5, AD-7.
- **Problema:** AD-5 declara a `CalculadoraDeDisponibilidad` "la única implementación del cálculo", y AD-7 hace que `Cita.Agendar` valide de nuevo "la rejilla, la ventana de reserva". Son dos implementaciones de la misma regla (por ejemplo, dónde empieza la rejilla dentro de un tramo) que pueden separarse.
- **Arreglo sugerido:** "`Cita.Agendar` valida comprobando que su inicio está entre los libres que devuelve la calculadora para esa fecha", o ambas delegan en el mismo objeto de valor `ReglasDeAgenda`.

### B-8. Supuestos del tope semanal sin cerrar en los bordes

- **Ubicación:** AD-13.
- **Problema:** no se dice si al reprogramar la propia Cita cuenta contra el tope, ni si "Agendadas" incluye las ya pasadas de esa semana (Atendidas derivadas). El PRD deja además abierto el valor (pregunta 7).
- **Arreglo sugerido:** "al reprogramar se excluye la propia Cita; cuentan las Agendadas de la semana, pasadas o futuras".

### B-9. El arnés de integración diferido puede bifurcarse si dos historias arrancan en paralelo

- **Ubicación:** "Deferred", penúltima viñeta (criterio 3).
- **Problema:** "lo decide la primera historia que lo necesite" funciona en serie; con agentes en paralelo, dos historias pueden ser "la primera". El memlog ya registra asperezas de las plantillas de prueba de Aspire con .NET 11.
- **Arreglo sugerido:** decidirlo ahora (Testcontainers, como el plan, que es lo verificado) o asignarlo a una historia de andamiaje que preceda a todas.

---

## Resumen por criterio

| # | Criterio | Estado | Hallazgos |
| --- | --- | --- | --- |
| 1 | Fija los puntos reales de divergencia | Parcial | A-1, A-3, A-4, M-1, M-2, M-3, M-5 |
| 2 | Cada regla es aplicable y previene lo que dice | Parcial | A-1, A-3, M-4, M-6, B-7 |
| 3 | Nada diferido permite divergir | Casi | M-8, B-9 |
| 4 | Tecnología verificada y vigente | Casi | B-1 |
| 5 | Cubre FR-1 a FR-30 | Sí, con huecos menores | M-5, M-7, B-6 |
| 6 | Ninguna dimensión en silencio | No | A-3, A-4, M-9, M-10 |
| 7 | Consistencia interna | No | A-2, M-6, M-7, B-2, B-3, B-4 |

**Lo que está bien y no debe tocarse:** AD-5 (modelo base/custom sin inventario de slots), AD-8 (la restricción es la ley), AD-9 (tres estados guardados), AD-13 (dueño del tope semanal), AD-14 a AD-16 (propiedad de configuración, datos del consultorio y bitácora), AD-17 (hora local para tramos, UTC para instantes). Son reglas concretas, con el motivo de la decisión trazable en el memlog.
