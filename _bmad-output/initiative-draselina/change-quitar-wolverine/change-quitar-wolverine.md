---
type: change-proposal
title: 'Quitar Wolverine'
parent: initiative-draselina
created: '2026-10-06'
status: aprobada
scope: major
trigger: 'Entrada 5 de epic-plataforma-base'
---

# Propuesta de cambio — Quitar Wolverine

## 1. Resumen del problema

**Qué pasó.** Al construir la entrada 5 de la épica Plataforma base ("Un caso de uso recorre ISender, transacción y outbox") quedó a la vista que Wolverine pide más código para rodearlo del que ahorra. Gerardo decidió quitarlo el 2026-10-06 y reemplazarlo por el patrón que ya usa en su proyecto Cypma.Cop: handlers inyectados directamente, decoradores para lo transversal y servicios en segundo plano para lo programado.

**Tipo de problema.** Enfoque que no rindió, descubierto durante la implementación.

**Evidencia.**

- No arranca sin el paquete `WolverineFx.RuntimeCompilation`, que compila código por cada handler al arrancar (entrada 1).
- Su validación de DataAnnotations lanza excepción en vez de entregar `Resultado` (entrada 1).
- Su política de transacción de fábrica (`AutoApplyTransactions`) impide arrancar cuando un handler alcanza dos `DbContext`, guarda por el handler y confirma aunque el `Resultado` sea un fallo (entrada 5, primer intento, revisión).
- La política propia que la reemplazó genera código y depende de `Wolverine.EntityFrameworkCore.Internals.EfCoreEnvelopeTransaction` (entrada 5, segundo intento).
- Junta los handlers de un mismo evento en una sola cadena, lo que choca con "una transacción, un módulo".
- Cuando algo falla, el error está en código generado que no está en el repositorio.
- El addendum del PRD registra que el plan original era "Hangfire y outbox propio", reemplazados por Wolverine. Este cambio vuelve, en parte, a esa idea.

## 2. Análisis de impacto

### Checklist

| Punto | Estado | Hallazgo |
| --- | --- | --- |
| 1.1 Historia que lo reveló | Hecho | Entrada 5 de Plataforma base |
| 1.2 Problema | Hecho | Enfoque que no rindió |
| 1.3 Evidencia | Hecho | Lista de arriba |
| 2.1 Épica actual | Requiere acción | Plataforma base se puede terminar, con cambios en P6, P8, "Done when" 3 y Notes |
| 2.2 Cambios a nivel épica | Requiere acción | El outbox pasa a ser trabajo propio: requisito nuevo P11 y entrada nueva |
| 2.3 Épicas restantes | Requiere acción | Notificaciones: redacción del alcance. Reserva, Gestión por el paciente y Administración: sin cambio de texto; cambia el cómo |
| 2.4 Épicas obsoletas o nuevas | No aplica | Ninguna |
| 2.5 Orden de épicas | No aplica | Sin cambio |
| 3.1 PRD | Hecho | Sin conflicto con objetivos ni alcance; solo el addendum |
| 3.2 Arquitectura | Requiere acción | AD-2, AD-3, AD-10, AD-12, AD-14, AD-20, AD-22, AD-25, tabla de capas, Consistency Conventions, Stack, diagrama, árbol, Open Questions; C4; dominio Parte I (T-24, T-28) y Parte II |
| 3.3 UX | No aplica | Sin impacto |
| 3.4 Otros | Requiere acción | Spec, README, `scripts/verificar-arranque.sh`, `Directory.Packages.props` |
| 4.1 Ajuste directo | Viable | Esfuerzo medio, riesgo medio |
| 4.2 Revertir | Viable | Esfuerzo medio, riesgo bajo |
| 4.3 Revisar alcance del piloto | No aplica | Ninguna capacidad cambia |
| 4.4 Camino elegido | Hecho | Híbrido, aprobado por Gerardo |

### Impacto técnico

- **Código que se revierte:** el commit `b2e3c10` (entrada 5 sobre Wolverine).
- **Código que sale después:** el paso del `MigrationService` que crea el esquema `wolverine`, `AlmacenamientoDeWolverine.cs`, `ISender.cs`, la prueba y la fila de `scripts/verificar-arranque.sh` que comprueban ese esquema, y los paquetes `WolverineFx` y `WolverineFx.Postgresql`.
- **Código nuevo:** interfaces de handler, registro por módulo, puerto de transacción, decoradores con Scrutor, tabla de outbox y servicio de entrega.
- **Entradas 1 a 4:** se conservan. De la 3 y la 4 solo sale lo relativo al esquema `wolverine`.

## 3. Camino recomendado

**Híbrido de revertir y ajuste directo** (aprobado por Gerardo, 2026-10-06).

1. Revertir `b2e3c10` con un commit de reversión, para no dejar código de Wolverine mezclado.
2. Aplicar los cambios de documentos de la sección 4: primero los documentos, después el código.
3. Avanzar con las entradas reescritas, en la secuencia 5 → 11 → 6 → 7.

**Razón.** Se parte de un estado probado (la entrada 4), sin restos que limpiar a mano. De lo revertido se rescatan la matriz de casos y la forma de los tipos de prueba, que quedan en el historial y en el plan de la entrada 5.

**Esfuerzo:** medio. **Riesgo:** medio; se concentra en el outbox propio, que es código nuevo sin referencia en Cypma.Cop. **Alcance del piloto:** sin cambio.

### Decisiones de Gerardo que fijan el reemplazo (2026-10-06)

| # | Decisión |
| --- | --- |
| 1 | Sin `ISender` ni mediador: la interfaz pide directamente `ICommandHandler<TComando, T>` o `IQueryHandler<TConsulta, T>`. |
| 2 | Cada handler de comando abre y cierra su propia transacción. Reemplaza "el host cierra según el `Resultado`". |
| 3 | Lo transversal va en decoradores registrados con Scrutor. |
| 4 | Outbox propio: una tabla en el esquema de cada módulo que publica; un servicio del host entrega y reintenta. |
| 5 | Recordatorio y Agenda del día: filas de Notificaciones más un servicio en segundo plano con temporizador. |
| 6 | El `MigrationService` se queda. Sobre una base sin migrar la web arranca y no crea nada. |
| 7 | La entrada 5 se parte en dos; secuencia 5 → 11 → 6 → 7. |

## 4. Propuestas de cambio detalladas

Las catorce fueron revisadas una por una y aprobadas por Gerardo el 2026-10-06.

### Arquitectura (`architecture-appointment-booking.md`)

**Propuesta 1 — AD-10**

Antes:

> ### AD-10 — La interfaz habla con el backend por `ISender` `[ADOPTED]`
>
> - **Prevents:** páginas que usan dominio, DbContext o Wolverine directamente; interfaz que arma datos de varios módulos.
> - **Rule:** todo caso de uso es un `ICommand<T>` o `IQuery<T>` en `*.Contracts` con exactamente un handler, y devuelve `Resultado`. La interfaz no compone datos de dos módulos: el handler de la consulta los reúne pidiendo a la API del otro módulo, en lote (`IPacientesApi.ObtenerContactos(ids)`). Sin MVC; HTTP solo con Minimal APIs y solo donde un formulario no alcanza (descarga del archivo de calendario).

Después:

> ### AD-10 — La interfaz habla con el backend por el handler de cada caso de uso
>
> - **Prevents:** páginas que usan dominio o DbContext directamente; interfaz que arma datos de varios módulos; una librería de despacho entre la página y el caso de uso.
> - **Rule:** todo caso de uso es un `ICommand<T>` o `IQuery<T>` en `*.Contracts` con exactamente un handler, que implementa `ICommandHandler<TComando, T>` o `IQueryHandler<TConsulta, T>` y devuelve `Resultado<T>`. Esas dos interfaces viven en `BuildingBlocks.Contracts`. La interfaz pide al contenedor el handler del caso de uso que necesita, por su interfaz, y nunca conoce la clase que lo implementa. Cada módulo registra sus handlers. No hay `ISender`, mediador ni librería de despacho. La interfaz no compone datos de dos módulos: el handler de la consulta los reúne pidiendo a la API del otro módulo, en lote (`IPacientesApi.ObtenerContactos(ids)`). Sin MVC; HTTP solo con Minimal APIs y solo donde un formulario no alcanza (descarga del archivo de calendario).

Razón: decisión 1. Sale `[ADOPTED]` porque la regla ya no es la del plan de solución. El nombre del método del handler lo fija la entrada 5.

**Propuesta 2 — AD-3** (los dos primeros puntos de la regla)

Antes:

> - Entre módulos, síncrono solo por las interfaces `I<Módulo>Api` de `Contracts`. `ISender` es exclusivo de la interfaz.
> - Asíncrono por eventos de integración publicados por el outbox. Agenda nunca llama a Notificaciones.

Después:

> - Entre módulos, síncrono solo por las interfaces `I<Módulo>Api` de `Contracts`. Los handlers de casos de uso son exclusivos de la interfaz: un módulo nunca pide el handler de otro.
> - Asíncrono por eventos de integración publicados por el outbox del módulo que publica: una tabla en su propio esquema, donde el evento se guarda en la misma transacción que el cambio. Un servicio del host en segundo plano entrega cada evento a sus handlers y reintenta los que fallan; un evento puede entregarse más de una vez. Agenda nunca llama a Notificaciones.

Razón: decisiones 1 y 4.

**Propuesta 3 — Consistency Conventions, fila Transacciones**

Antes:

> | Transacciones | Un comando, una transacción, un módulo: cambio, bitácora y outbox juntos. La transacción es la del módulo del handler y la abre el host. El handler guarda de forma explícita; el host confirma si el `Resultado` es exitoso y deshace todo si no. |

Después:

> | Transacciones | Un comando, una transacción, un módulo: cambio, bitácora y outbox juntos. El handler del comando abre la transacción de su módulo por un puerto, guarda de forma explícita y la confirma. Toda salida con un `Resultado` no exitoso deshace la transacción antes de devolver; una excepción también la deshace. Quien llama al handler nunca abre, confirma ni guarda. |

Razón: decisión 2. "Por un puerto" porque AD-4 y la tabla de dependencias impiden que un handler reciba el `DbContext`. La fila Lecturas no cambia.

**Propuesta 4 — AD-12 y AD-22**

AD-12, antes:

> El backend valida siempre, antes del handler, con un middleware propio de Wolverine que aplica DataAnnotations a todo comando, y el fallo se entrega como `Resultado` inválido con errores por campo.

AD-12, después:

> El backend valida siempre, antes del handler, con un decorador que envuelve a todo handler de comando y aplica DataAnnotations; el fallo se entrega como `Resultado` inválido con errores por campo y el handler no se ejecuta.

AD-22, antes:

> - […] Un único middleware de Wolverine rechaza los que lleguen sin usuario autenticado. […]
> - […] `Sistema` en mensajes programados. […]

AD-22, después:

> - […] Un único decorador, que envuelve a todo handler de comando y de consulta, rechaza con `Resultado` no autorizado los que lleguen sin usuario autenticado. […]
> - […] `Sistema` en los trabajos programados (Recordatorio, Agenda del día). […]

Razón: decisión 3. El orden entre los dos decoradores lo fijan las entradas 6 y 7.

**Propuesta 5 — AD-20**

Antes:

> Recordatorio y Agenda del día son mensajes programados durables de Wolverine. La Agenda del día pregunta a Agenda qué hay ese día; nunca lee sus tablas.

Después:

> El Recordatorio es una fila en una tabla de Notificaciones, con la hora en que debe enviarse. Un servicio en segundo plano de Notificaciones revisa a intervalos, envía los que ya tocan y deja pendientes los que fallan para la siguiente vuelta. `[ASSUMPTION: cada minuto]` Ese mismo servicio envía la Agenda del día a la hora configurada, una sola vez por fecha. La Agenda del día pregunta a Agenda qué hay ese día; nunca lee sus tablas.

Razón: decisión 5.

**Propuesta 6 — AD-2 y AD-25**

AD-2, antes: "El host es dueño de `identidad`; Wolverine, de `wolverine`."

AD-2, después: "El host es dueño de `identidad`. El outbox de un módulo que publica eventos es una tabla de su propio esquema."

AD-25, antes: "La aplicación web nunca migra al arrancar. Sobre una base sin migrar, se niega a arrancar."

AD-25, después: "La aplicación web nunca migra ni crea nada al arrancar: sobre una base sin migrar arranca igual y la deja como estaba."

Razón: decisiones 4 y 6. El servicio de entrega del host lee el outbox de cada módulo a través del contexto de ese módulo.

**Propuesta 7 — Lugares restantes**

| Lugar | Antes | Después |
| --- | --- | --- |
| Tabla de capas, Contratos base | `ICommand<T>`, `IQuery<T>`, `ISender`, marcadores de autorización | `ICommand<T>`, `IQuery<T>`, `ICommandHandler<TComando, T>`, `IQueryHandler<TConsulta, T>`, marcadores de autorización |
| Tabla de capas, Host | Composición, identidad, Wolverine | Composición, identidad, decoradores de los handlers y entrega de eventos del outbox |
| AD-14 | La pantalla de Configuración compone ambas por `ISender`. | La pantalla de Configuración compone ambas pidiendo el handler de cada consulta. |
| Stack | `WolverineFx, .EntityFrameworkCore, .Postgresql, .RuntimeCompilation` — 6.45.x | Se quita. Se agrega `Scrutor` — 6.1.x `[ASSUMPTION: la versión que usa Cypma.Cop; se confirma que corre sobre .NET 11 al agregarla]` |
| Diagrama del Structural Seed | esquemas agenda, pacientes, notificaciones, identidad, wolverine | esquemas agenda, pacientes, notificaciones, identidad |
| Árbol de carpetas | `# ICommand, IQuery, ISender, marcadores` | `# ICommand, IQuery, sus handlers, marcadores` |
| Open Questions 1 y 2 | Resueltas el 2026-10-05 con Wolverine | Se conservan y se agrega a cada una: "Wolverine se quitó el 2026-10-06; ver AD-10 y AD-12." |
| Encabezado | `updated: '2026-10-05'` | `updated: '2026-10-06'` |

### Dominio y modelo de datos (`ddd-and-data-model-appointment-booking.md`)

**Propuesta 8 — Parte I (invariante)**

T-24, antes: "Reacciona a los tres eventos de Agenda y a sus propios mensajes programados. Pide lo que necesita por contrato."

T-24, después: "Reacciona a los tres eventos de Agenda y envía, cuando les llega su hora, sus propios envíos programados (Recordatorio y Agenda del día). Pide lo que necesita por contrato."

T-28, antes: "[…] No conoce EF Core, Wolverine, ASP.NET ni otros contextos. […]"

T-28, después: "[…] No conoce EF Core, ASP.NET ni otros contextos. […]"

Se agrega una entrada del 2026-10-06 al "Historial de cambios a las invariantes del diseño". E-13 no cambia.

**Propuesta 9 — Parte II**

- **9a.** Convenciones físicas, fila Esquemas: sale `wolverine`.
- **9b.** "Esquemas que no se diseñan aquí": sale la línea de `wolverine`.
- **9c.** `notificaciones.mensaje`, columna `evento_id`: "Nulo en mensajes programados" pasa a "Nulo en los envíos programados".
- **9d.** `notificaciones.recordatorio_programado`: sale la columna `version`, también del diagrama entidad-relación. Descripción nueva:

  > El recordatorio pendiente de una cita (AD-20). El servicio en segundo plano de Notificaciones envía los que tienen `programado_para` ya cumplido. Mover el recordatorio es actualizar `programado_para`; eliminarlo es borrar la fila. `[ASSUMPTION]` Al enviarlo se borra la fila; lo enviado queda en `notificaciones.mensaje`.

- **9e.** Tabla nueva `agenda.outbox` `[ASSUMPTION: tabla completa]`, agregada también a "Supuestos de la Parte II":

  > Los eventos de integración que Agenda publica (AD-3). Cada fila se guarda en la misma transacción que el cambio que la origina.

  | Columna | Tipo | Nulo | Notas |
  | --- | --- | --- | --- |
  | `id` | `uuid` | no | PK. Es el `EventoId` del evento |
  | `tipo` | `text` | no | `CitaAgendada`, `CitaReprogramada`, `CitaCancelada` |
  | `contenido` | `jsonb` | no | El evento serializado. Sin datos personales (AD-23) |
  | `ocurrido_en` | `timestamptz` | no | |
  | `entregado_en` | `timestamptz` | sí | Nulo mientras está pendiente |
  | `intentos` | `integer` | no | Entregas fallidas hasta ahora |

Pendiente para la entrada 11: tope de intentos y qué pasa con un evento que falla siempre.

### Diagramas C4 (`c4-appointment-booking.md`)

**Propuesta 10**

| Diagrama | Antes | Después |
| --- | --- | --- |
| Contenedores, base de datos | Esquemas agenda, pacientes, notificaciones, identidad y wolverine | Esquemas agenda, pacientes, notificaciones e identidad |
| Componentes, Host | "ASP.NET Core Identity, Wolverine"; "…despacho de comandos" | "ASP.NET Core Identity"; "Sesión con segundo factor, actor, límite de frecuencia, decoradores de los handlers, entrega de eventos" |
| Componentes, Eventos de cita | "Outbox de Wolverine" | "Tabla `agenda.outbox`" |
| Componentes, UI pública y UI administración → Host | "Envía comandos y consultas por" `ISender` | "Pide el handler de cada caso de uso a" `ICommandHandler`, `IQueryHandler` |
| Componentes, Notificaciones → eventos | "Se suscribe a" | "Recibe, entregados por el host, los eventos de" |
| Componentes de Agenda, Host externo | "Wolverine"; "Despacha comandos y consultas ya validados" | "Decoradores"; "Valida y autoriza antes de cada handler" |
| Flujo de reserva, Host | "Wolverine"; "Anti-bots, validación, despacho" | "ASP.NET Core"; "Anti-bots y validación" |
| Flujo de reserva, paso 2 | "2. Envía AgendarCita" por `ISender` | "2. Envía AgendarCita" a su `ICommandHandler` |

Las flechas conservan su lugar; solo cambian las etiquetas.

### Épica Plataforma base (`epic-plataforma-base.md`)

**Propuesta 11 — Requisitos y "Done when"**

- **P6**, después: "Todo caso de uso es `ICommand<T>` o `IQuery<T>` con un handler (`ICommandHandler` o `IQueryHandler`) que la interfaz pide directamente y que devuelve `Resultado` con códigos cerrados; el handler de un comando abre y cierra su propia transacción. (AD-10, Consistency Conventions)"
- **P8**, después: "Un único servicio entrega el actor y un único decorador rechaza lo marcado `IRequiereAdministrador` sin usuario autenticado. (AD-22)"
- **P11**, nuevo: "Outbox propio: un módulo guarda sus eventos de integración en una tabla de su esquema, dentro de la transacción del cambio, y un servicio del host en segundo plano los entrega a sus handlers y reintenta los que fallan. (AD-3, AD-2)"
- **"Done when" 3**, después: "Un comando de prueba, pedido por su `ICommandHandler`, se valida, cambia la base y deja su evento en el outbox en una sola transacción, y el evento llega después a su handler; con datos inválidos devuelve `Resultado` inválido con errores por campo, sin excepción."

P1 y P7 no cambian.

**Propuesta 12 — Notes**

- **12a.** Se antepone "Reemplazada (2026-10-06, se quita Wolverine):" a siete líneas: `WolverineFx.RuntimeCompilation` en el Stack; validación con middleware propio de Wolverine; y las cinco del 2026-10-06 por la mañana (transacción fijada por el host; alcance de la política; la web se niega a arrancar; el host cierra según el `Resultado`; colas locales durables).
- **12b.** Secuencia de entradas: "porque ambas tocan la configuración de middlewares de Wolverine en el host" pasa a "porque ambas registran decoradores de los handlers en el host".
- **12c.** La pregunta abierta sobre Wolverine pasa a: "Resuelto (2026-10-06): Wolverine se quita; ver las Decision de abajo. Sigue abierto, para el final, el uso de interceptores de EF Core para publicar eventos desde el agregado."
- **12d.** Ocho líneas `Decision:` nuevas, con fecha 2026-10-06 y autoría de Gerardo: se quita Wolverine y por qué; sin `ISender` ni mediador; transacción dentro del handler por un puerto y consultas sin transacción; decoradores con Scrutor; outbox propio; Recordatorio y Agenda del día con tabla y temporizador; el `MigrationService` se queda y la web arranca sobre una base sin migrar; la entrada 5 sobre Wolverine se revierte y se rehace.

### Entradas (`epic-plataforma-base/tickets.toml`)

**Propuesta 13** — se aplican con `bmad-ticket`.

| Entrada | Campo | Después |
| --- | --- | --- |
| 5 | Título | Un caso de uso recorre su handler, su transacción y Resultado |
| 5 | Descripción | Quita Wolverine de la solución. `ICommandHandler` e `IQueryHandler` propias, registradas por módulo y pedidas directamente por quien llama, con un handler por comando o consulta que devuelve Resultado con códigos cerrados; el handler de un comando abre y cierra su transacción por un puerto. Demostrado con un comando y una consulta de prueba que viven solo en el proyecto de pruebas. |
| 5 | Verify | Una prueba de integración pide el handler de un comando de prueba y comprueba que su cambio queda confirmado; que un fallo esperado, antes o después de cambiar, vuelve como Resultado con código, sin excepción y sin dejar nada; y que la web arranca sobre una base vacía sin crear nada. |
| 5 | Cubre / después de / riesgo | P6 / 4 / alto |
| 11 (nueva) | Título | Outbox propio y entrega de eventos |
| 11 | Descripción | La tabla de outbox que un módulo agrega a su contexto, el puerto para publicar un evento dentro de la transacción del handler, y el servicio del host en segundo plano que entrega cada evento a sus handlers, reintenta los que fallan y no pierde eventos si la web se cae. Demostrado con un evento de prueba en el esquema `pruebas`. |
| 11 | Verify | Una prueba de integración comprueba que el evento y el cambio se confirman juntos o ninguno, que el evento llega a su handler después de confirmar, que una entrega fallida se reintenta, y que un evento que quedó pendiente se entrega al arrancar de nuevo la web. |
| 11 | Cubre / después de / riesgo | P11 / 5 / alto; sin revisión humana obligatoria |
| 6 | Descripción | Un decorador valida con DataAnnotations todo comando antes de su handler y entrega los errores por campo como Resultado inválido. |
| 7 | Descripción | "un único middleware de Wolverine rechaza" pasa a "un único decorador rechaza"; "Sistema en mensajes programados" pasa a "Sistema en los trabajos programados". |
| 10 | Después de | Se agrega la 11 |

Secuencia: **5 → 11 → 6 → 7**. La entrada 5 carga también el retiro de lo que quedó de Wolverine en las entradas 3 y 4. La entrada 2 no se toca; `ISender.cs` lo borra la 5.

### Documentos menores

**Propuesta 14**

- **14a. Épica Notificaciones, alcance:** "mensajes programados" pasa a "los envíos programados (Recordatorio y Agenda del día, con su servicio en segundo plano)".
- **14b. Spec:** "Stack fijo: .NET 11, Blazor Web App, PostgreSQL, Aspire 13, DataAnnotations, OpenTelemetry (versiones en la arquitectura, sección Stack). Sin librería de mensajería ni de despacho: handlers propios, outbox propio y servicios en segundo plano." Y bajo "Resueltas por la primera historia" se agrega: "Wolverine se quitó el 2026-10-06 (épica Plataforma base, Notes); de esa prueba sigue vigente la elección de .NET 11."
- **14c. Addendum del PRD:** fila "Mensajería y tareas" pasa a "Outbox propio en PostgreSQL y servicios en segundo plano, sin broker ni librería de mensajería". En alternativas descartadas, "Hangfire y outbox propio (reemplazados por Wolverine)." pasa a "Hangfire. Wolverine (se adoptó y se quitó el 2026-10-06: pedía más código para rodearlo del que ahorraba)."

### Lo que no se toca

- La iniciativa: su única mención es histórica.
- El plan de solución en `docs/` y las revisiones de la arquitectura: históricos.
- Los planes de las entradas 1 a 4 y el plan y el parche de la entrada 5 sobre Wolverine: registro de lo construido.
- El README: vuelve a su estado de la entrada 4 al revertir, y la nueva entrada 5 lo actualiza.

## 5. Traspaso para la implementación

**Clasificación: mayor.** Cambia decisiones de arquitectura (ocho AD), dos reglas de la Parte I del dominio y el desglose de una épica. Las decisiones ya las tomó Gerardo, que hace de responsable de producto y de arquitecto; no queda escalamiento pendiente.

**Orden de ejecución.**

1. Revertir `b2e3c10` con un commit de reversión.
2. Aplicar las propuestas 1 a 12 y 14 a los documentos, y registrar cada cambio en el `.memlog.md` de su carpeta (arquitectura, épica Plataforma base, épica Notificaciones, spec, PRD).
3. Aplicar la propuesta 13 a `tickets.toml` con `bmad-ticket`.
4. Cerrar el plan de la entrada 5 sobre Wolverine como registro y construir la nueva entrada 5.
5. Seguir con 11, 6 y 7.

**Quién.** Los pasos 1 a 3 los ejecuta el agente con el visto bueno de Gerardo sobre esta propuesta. Los pasos 4 y 5 son corridas de `bmad-build-auto`, una por entrada.

**Criterios de éxito.**

- Ningún documento normativo (arquitectura, C4, dominio, épicas, spec) nombra Wolverine o `ISender` como regla vigente.
- `./dotnet.sh build` con 0 advertencias y `./dotnet.sh test` en verde después de revertir, y de nuevo al terminar cada entrada.
- La solución no referencia ningún paquete `WolverineFx.*` al terminar la entrada 5.
- El punto 3 de "Done when" de la épica se cumple al terminar la entrada 6: un comando de prueba se valida, cambia la base, deja su evento en el outbox y el evento llega a su handler.

**Riesgos y pendientes.**

- El outbox propio es código nuevo sin referencia en Cypma.Cop. La entrada 11 debe decidir el tope de intentos, qué pasa con un evento que falla siempre, y cómo se evita que dos vueltas del servicio entreguen el mismo evento a la vez.
- `[ASSUMPTION]` Versión de Scrutor 6.1.x sin verificar sobre .NET 11.
- `[ASSUMPTION]` Intervalo de un minuto del servicio de Notificaciones; el recordatorio se borra al enviarse.
- `[ASSUMPTION]` Columnas de `agenda.outbox`.
- La regla "toda salida con fallo deshace antes de devolver" no la hace cumplir el host: queda a la revisión de cada historia.
- Sigue abierta la pregunta sobre interceptores de EF Core para publicar eventos desde el agregado.

## 6. Ejecución (2026-10-06)

- Aprobada por Gerardo el 2026-10-06.
- Paso 1: el commit `b2e3c10` se revirtió en `4468d65`. Sobre ese estado: build con 0 advertencias y 51 de 51 pruebas; el código quedó idéntico al de la entrada 4.
- Paso 2: propuestas 1 a 12 y 14 aplicadas a los documentos, con su entrada en el memlog de cada carpeta. A la épica Notificaciones se le creó su memlog, que no tenía.
- Paso 3: propuesta 13 aplicada a `epic-plataforma-base/tickets.toml`. La entrada 5 vuelve a estado `planned` y la 11 es nueva.
- El plan de la entrada 5 sobre Wolverine y su parche se movieron a esta carpeta como registro: [story-un-caso-de-uso-recorre-isender-transaccion-y-outbox-plan.md](story-un-caso-de-uso-recorre-isender-transaccion-y-outbox-plan.md). Su campo `ticket` quedó vacío para que no marque como retirada a la entrada 5 reescrita.
- Pendiente: construir la entrada 5 reescrita, y después la 11, la 6 y la 7.
