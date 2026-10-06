---
title: 'Un caso de uso recorre ISender, transacción y outbox'
type: 'feature' # feature | bugfix | refactor | chore
ticket: '5' # the entry id from the ticket tree, or the story file's stem when the entry has no id; empty outside it
created: '2026-10-06'
status: 'built' # draft | ready-for-dev | in-progress | in-review | built | done | blocked | dropped
baseline_revision: '95d40a6e4b730ea783a6d15e5df3a88f16595ffb' # the commit the work starts from, set before any code change
route: 'full' # oneshot | full — set by step-02
route_source: 'auto' # pinned | auto — set with route by step-02
risk: 'high' # low | medium | high — set by step-02; a CI process can read it to decide how much review the change gets
review: 'quick' # none | quick | thorough — set by step-04
review_source: 'pinned' # pinned | auto — set with review by step-04
lenses_ran: ['quick'] # ids of the lenses launched, set by step-04
review_loop_iteration: 0 # incremented by step-04 before each review loopback
followup_review_recommended: true # set by step-04 at Finalize — true if the LLM decided another review pass is worthwhile
context:
  - '{project-root}/_bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md'
  - '{project-root}/README.md'
warnings: ['oversized'] # optional: machine-readable warnings for orchestration, e.g. oversized, multiple-goals
deferred: [] # append-only machine-readable deferred review findings; each item carries summary/evidence and optional location/severity
---

<intent-contract>

## Intent

**Problem:** `ISender` existe como interfaz pero nadie la implementa, y la web no arranca Wolverine: hoy no hay forma de despachar un caso de uso ni garantía de que sea atómico. Cada historia de negocio tendría que decidirlo por su cuenta.

**Approach:** El host implementa `ISender` sobre Wolverine y fija, una sola vez y con una política propia, que todo comando corre en la transacción del módulo de su handler: el host la abre, el handler guarda, y al terminar el host confirma el cambio y sus mensajes juntos si el `Resultado` es exitoso, o deshace todo si no. Se demuestra con tipos de prueba que viven solo en el proyecto de pruebas de integración y corren contra la web en memoria. Decisiones de Gerardo del 2026-10-06 (Notes de la épica).

## Boundaries & Constraints

**Always:**
- `ISender` es el único camino: las pruebas despachan por `ISender` resuelto del contenedor de la web en memoria, nunca por `IMessageBus`.
- Un handler por comando o consulta, que devuelve `Resultado<T>`. Un fallo esperado es `Resultado.Fallo(codigo)` con un código de un conjunto cerrado (constantes junto al comando); nunca una excepción ni texto libre.
- **Dueño de la transacción:** el `DbContext` del módulo donde vive el handler. El host lo fija una sola vez, como datos (namespace del handler → contexto: `Agenda.Features` → `AgendaDbContext`, y así Pacientes y Notificaciones). Ningún handler ni comando declara nada: sin atributos de transacción.
- **Alcance:** la política cubre los handlers de comandos (`ICommand<T>`) y de eventos. Una consulta (`IQuery<T>`) corre sin transacción y nadie guarda por ella.
- **Quién guarda:** el handler guarda de forma explícita (`SaveChanges` dentro del handler, directo o por su puerto). El host nunca llama a `SaveChanges`: solo abre la transacción y la cierra.
- **Cierre:** si el handler de un comando devuelve un `Resultado` exitoso, el host confirma el cambio y los mensajes publicados, juntos. Si devuelve uno no exitoso (fallo, inválido, no autorizado) o lanza, deshace todo: ni cambio ni mensajes. Quien llamó recibe el `Resultado` tal cual, o la excepción. Un handler de evento confirma salvo excepción.
- Un handler cuyas dependencias alcanzan dos `DbContext` (su puerto y la API de otro módulo) no impide arrancar: su transacción es la de su módulo; lo que el otro contexto haga queda fuera de ella.
- Los mensajes publicados dentro de un handler van a colas locales durables: se guardan en el esquema `wolverine` en la misma transacción que el cambio.
- Contra una base sin migrar (sin el esquema `wolverine`) la web se niega a arrancar, con un error que nombra lo que falta, y no crea nada (AD-25).
- Los tipos de prueba viven en `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/` y sus tablas en el esquema `pruebas`. El arnés los da de alta en la web en memoria como se da de alta un módulo: su contexto, sus handlers y su renglón namespace → contexto.
- `[ASSUMPTION]` Un handler de comando que vive fuera de todo módulo dado de alta hace fallar el arranque con un error que lo nombra; no corre en silencio sin transacción.
- Paquetes solo de la tabla Stack: `WolverineFx.EntityFrameworkCore` y `WolverineFx.RuntimeCompilation`, 6.45.0.
- Las pruebas de arquitectura y del Kernel siguen pasando; build con 0 advertencias.

**Never:**
- `Policies.AutoApplyTransactions`, ni atributos `[Transactional]` o `[NonTransactional]` en handlers.
- Que el host llame a `SaveChanges` para que algo funcione. Si Wolverine no guarda el mensaje del outbox sin eso, o no deja deshacer ante un `Resultado` no exitoso, se detiene y se reporta lo observado; no se cambia la regla.
- Un modo de arranque del arnés que apague la mensajería para esquivar el arranque contra una base vacía.
- Validación de forma (entrada 6); actor, `IRequiereAdministrador` o autorización (entrada 7). Interceptores de EF Core para eventos (pregunta abierta de la épica).
- Comandos, consultas, eventos, handlers o tablas de prueba en `src/`. Ninguna página ni componente nuevo.
- Tablas o migraciones nuevas en `agenda`, `pacientes` o `notificaciones`. Si integrar sus `DbContext` con Wolverine cambia su modelo, se detiene y se reporta.
- Cambiar las firmas de `ISender`, `ICommand<T>`, `IQuery<T>` o `Resultado`.
- Tocar `spikes/`, la arquitectura, la épica o `tickets.toml`. Sin commits.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Comando con éxito | Comando de prueba con un valor propio, por `ISender` | `Resultado` exitoso con su valor; la fila está en `pruebas` y el mensaje del evento está guardado en el esquema `wolverine` | — |
| Fallo antes de cambiar | El mismo comando en la condición que su handler rechaza de entrada | `Forma` `Fallo` con el código del conjunto cerrado; ninguna excepción; ni fila ni mensaje | — |
| Fallo después de cambiar | Comando cuyo handler guarda, publica y después devuelve `Resultado.Fallo` | `Forma` `Fallo` con su código; ninguna excepción; ni fila ni mensaje | — |
| Fallo inesperado | Comando cuyo handler guarda, publica y después lanza | La excepción sale por `ISender`; ni fila ni mensaje | La prueba lo afirma |
| El handler no guarda | Comando cuyo handler agrega una entidad, no llama a `SaveChanges` y devuelve éxito | `Resultado` exitoso; la fila no está: el host no guardó por él | — |
| Dos contextos | Comando cuyo handler usa un puerto sobre `PruebasDbContext` y un servicio que usa un `DbContext` de producción | La web arranca; `Resultado` exitoso; el cambio y el mensaje quedan confirmados juntos | — |
| El mensaje llega | Comando de prueba cuyo evento tiene un handler de prueba | El handler del evento corre después de confirmada la transacción y deja su rastro guardado en `pruebas` | — |
| Consulta | Consulta de prueba por `ISender` sobre una fila ya guardada | `Resultado` exitoso con el dato leído; el handler no vio ninguna transacción abierta | — |
| Consulta que modifica | Consulta cuyo handler agrega una entidad y no guarda | `Resultado` exitoso; la fila no está | — |
| Sin handler | Un comando sin handler, por `ISender` | Excepción que nombra el tipo del comando; no un `Resultado` | La prueba lo afirma |
| Base sin migrar | La web real contra una base vacía creada aparte | No arranca; el error nombra el almacenamiento que falta; la base sigue sin los esquemas `agenda`, `pacientes`, `notificaciones`, `wolverine` y sin ningún historial de migraciones | La prueba lo afirma |

</intent-contract>

## Code Map

- `story-un-caso-de-uso-recorre-isender-transaccion-y-outbox-plan.patch` (junto a este plan) -- el primer intento, revertido; se aplica limpio sobre la base. **Reutilizar:** `SenderDeWolverine.cs` (`IMessageBus.InvokeAsync<Resultado<T>>`), los paquetes, `Policies.UseDurableLocalQueues`, `optionsLifetime: ServiceLifetime.Singleton` en los tres `Registro.cs` (sin eso Wolverine rechaza el handler), el alta del ensamblado de pruebas con `ConfigureWolverine` + `Discovery.IncludeAssembly` en `Arnes.cs`, `Consultas.MensajesGuardadosAsync`, los tipos `Anotar`/`LeerAnotacion`/`Anotacion` y la forma de `CasoDeUsoTests`. **No reutilizar:** `AutoApplyTransactions`, `ArrancarWebSinMensajeria`, el cambio a `WebNoMigraTests`, y las frases del README "todo handler que recibe un `DbContext`" y "se confirman juntos, o ninguno" tal como estaban
- `src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/ISender.cs`, `ICommand.cs`, `IQuery.cs` -- marcadores genéricos; `SendAsync<T>` devuelve `Task<Resultado<T>>`. No cambiar
- `src/DraSelina.Consultorio.Web/Kernel/Resultado.cs` -- clase base no genérica `Resultado` con `EsExito`; `Resultado<T>` deriva de ella. La política puede mirar `EsExito` sin conocer `T`. No cambiar
- `src/DraSelina.Consultorio.Web/Program.cs` -- hoy registra los tres contextos y Razor; no llama a `UseWolverine`. Aquí se compone Wolverine, `ISender` y la política
- `src/DraSelina.Consultorio.Web/AlmacenamientoDeWolverine.cs` -- `Configurar(opciones, cadena)`: esquema `wolverine`, `AutoCreate.None`. Lo comparten la web y el `MigrationService`
- `src/DraSelina.Consultorio.Web/Modules/*/Infrastructure/` -- `Registro.cs` (`AddAgenda`…) y cada `DbContext` con su constante `Esquema`; los usa también el `MigrationService`
- `src/DraSelina.Consultorio.Web/DraSelina.Consultorio.Web.csproj` -- `<Using Remove="Microsoft.*" />`: todo `using` se escribe
- `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs` -- `ArrancarWeb()`/`ArrancarWeb(cadena)`; subclase privada `Web`. `CrearBaseVaciaAsync()` da la base sin migrar
- `tests/DraSelina.Consultorio.IntegrationTests/WebNoMigraTests.cs` -- hoy espera GET `/` con 200 sobre una base vacía; pasa a la fila "Base sin migrar". Sus afirmaciones de que nada se creó se conservan
- `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/PruebasDbContext.cs` -- esquema `pruebas`; el arnés crea sus tablas con `CreateTablesAsync`
- `tests/DraSelina.Consultorio.ArchitectureTests/` -- los archivos en la raíz de `Web` son host y pueden usar todo
- Comportamiento de Wolverine 6.45 ya observado: con dos `DbContext` en el árbol de un handler, `AutoApplyTransactions` impide arrancar; Wolverine retiene los mensajes salientes hasta que el handler termina sin excepción, aun sin transacción; el sobre se escribe en la transacción del `DbContext` del handler; con almacenamiento durable y sin esquema `wolverine` el host falla al arrancar (`The Wolverine message storage ... is missing`)

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Packages.props` y `src/DraSelina.Consultorio.Web/DraSelina.Consultorio.Web.csproj` -- agregar `WolverineFx.EntityFrameworkCore` y `WolverineFx.RuntimeCompilation` 6.45.0 -- Stack
- [x] `src/DraSelina.Consultorio.Web/Modules/*/Infrastructure/Registro.cs` -- opciones del contexto como singleton -- lo exige Wolverine
- [x] `src/DraSelina.Consultorio.Web/` (raíz, host) -- `ISender` sobre Wolverine, registrado en el contenedor -- P6, AD-10
- [x] `src/DraSelina.Consultorio.Web/` (raíz, host) -- la política propia: tabla namespace → contexto, transacción por comando y por handler de evento, cierre según el `Resultado`, consultas fuera, sin `SaveChanges` del host -- decisiones 1, 2 y 4
- [x] `src/DraSelina.Consultorio.Web/Program.cs` y `AlmacenamientoDeWolverine.cs` -- la web arranca Wolverine con almacenamiento durable, colas locales durables y la política; no crea nada al arrancar -- decisiones 3 y 5
- [x] `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/` -- comandos, consultas, evento, handlers, puerto, entidades y códigos de prueba que piden las filas de la matriz; `DbSet` nuevos en `PruebasDbContext` -- lo pide la entrada
- [x] `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs` -- da de alta los tipos de prueba en la web en memoria como un módulo -- sin esto no corren
- [x] `tests/DraSelina.Consultorio.IntegrationTests/` (pruebas) -- una prueba por fila de la matriz; `WebNoMigraTests` pasa a la fila "Base sin migrar" -- P6, AD-25
- [x] `README.md` -- sección "Casos de uso": forma de un comando, una consulta y su handler; qué garantiza el host (dueño de la transacción, quién guarda, cierre según el `Resultado`, consultas sin transacción); que un handler de módulo llega a la base por su puerto (AD-4); cómo se da de alta un módulo en la política; cómo se escribe un comando de prueba. Y la web contra una base sin migrar -- la épica pide que la épica 2 no decida esto

**Acceptance Criteria:**
- Given Docker en ejecución y ningún AppHost arriba, when se corre `./dotnet.sh test`, then pasan todas las pruebas de la solución.
- Given la política quitada del host, when se corren las pruebas de integración, then fallan al menos "Fallo después de cambiar" y "Fallo inesperado".
- Given `src/`, when se buscan los tipos de prueba y los atributos `[Transactional]`, then no hay ninguno.
- Given el AppHost levantado, when se corre `scripts/verificar-arranque.sh`, then sale con 0.

## Implementation Notes

**Primer intento (2026-10-06), revertido tras la revisión; queda como registro:**

- **Política.** `Program.cs`: `UseEntityFrameworkCoreTransactions`, `Policies.AutoApplyTransactions` y `Policies.UseDurableLocalQueues`, sobre `AlmacenamientoDeWolverine.Configurar`. Aplica a todo handler que recibe un `DbContext`: comandos, handlers de evento y también consultas (una consulta corre dentro de una transacción y el middleware llama a `SaveChangesAsync` sin nada que guardar).
- **`ISender`.** `SenderDeWolverine` (raíz de `Web`, `internal`, registrado por ámbito): `IMessageBus.InvokeAsync<Resultado<T>>`. Sin handler, Wolverine lanza `IndeterminateRoutesException`, que nombra el tipo.
- **Los contextos de módulo no se integran con Wolverine.** No se usó `AddDbContextWithWolverineIntegration`: el modelo de `agenda`, `pacientes` y `notificaciones` queda igual y no hay migración. Lo único que cambió en los tres `Registro.cs` es `optionsLifetime: ServiceLifetime.Singleton`; sin eso Wolverine rechaza el handler (`InvalidServiceLocationException`, `ServiceLocationPolicy.NotAllowed`). No se comprobó si la integración habría cambiado el modelo.
- **La web real ya no arranca contra una base sin migrar.** Con almacenamiento durable y `AutoCreate.None`, Wolverine comprueba al arrancar que su almacenamiento existe y, si no, el host falla (`The Wolverine message storage ... is missing`). `ResourceMigrationFailureMode.ContinueOnFailures` no basta: después falla al leer `wolverine.wolverine_nodes`, también en modo `Solo`. Para que `WebNoMigraTests` siga pasando sin tocar sus afirmaciones, el arnés tiene `ArrancarWebSinMensajeria(cadena)` (`DurabilityMode.MediatorOnly` y agente de durabilidad apagado) y esa prueba cambió solo su línea de arranque. Se agregó una prueba que fija lo que hace la web real: no arranca y no crea el esquema `wolverine`. Queda a decisión de Gerardo si ese es el comportamiento que quiere.
- **Varias webs en memoria.** Corren como nodos en modo por defecto (`Balanced`) sobre la misma base; no estorbó y no se ajustó nada.
- **Pendiente de Design Notes.** No se observó qué hace Wolverine cuando un handler cambia datos y devuelve un `Resultado` no exitoso.
- **Verificación.** `./dotnet.sh build`: 0 errores, 0 advertencias. `./dotnet.sh test`: 58 pruebas pasan (51 anteriores, 6 de la matriz, 1 de la web real contra base vacía). Con `AutoApplyTransactions` comentado falla solo "Fallo inesperado" (la fila queda guardada); restaurado. `scripts/verificar-arranque.sh` contra el AppHost: sale con 0.

**Segundo intento:**

- **Política propia.** `UnaTransaccionPorModulo` (un `IHandlerPolicy`, raíz de `Web`) agrega a cada cadena dos pasos de código generado: abrir antes del handler y cerrar después. Comando: `AbrirAsync → Handle → CerrarAsync(resultado)`. Evento: `AbrirAsync → Handle → ConfirmarAsync`. Consulta: nada. No se usó `AutoApplyTransactions`, `UseEntityFrameworkCoreTransactions`, atributos ni `AddDbContextWithWolverineIntegration`; el modelo de los tres contextos no cambia y no hay migraciones.
- **Tabla namespace → contexto.** `AddTransaccionDeModulo<TContexto>(namespace)` registra un `ModuloConTransaccion`; `Program.cs` da de alta `Agenda.Features`, `Pacientes.Features` y `Notificaciones.Features` (cadenas literales: esos namespaces aún no tienen tipos). El namespace se compara exacto, no por prefijo.
- **`TransaccionDeModulo`.** Abre con `BeginTransactionAsync` sobre el contexto del módulo y enlista en el outbox un `EfCoreEnvelopeTransaction` de Wolverine. Confirmar es su `CommitAsync` (que además marca como atendido el mensaje entrante, en la misma transacción); deshacer es `RollbackAsync` más `ClearAllAsync` de los mensajes retenidos; si el handler lanza, el `await using` deshace. El host no llama a `SaveChanges`: funcionó sin eso, porque los contextos no llevan el modelo de Wolverine y los sobres se escriben con SQL en la transacción.
- **El host se niega a arrancar** cuando: falta el esquema `wolverine`; un handler de comando o de evento vive fuera de todo módulo dado de alta; un comando tiene más de un handler o su handler no devuelve exactamente `Resultado<T>` con la `T` de su `ICommand<T>`; un evento tiene handlers de más de un módulo en la misma cadena.
- **Tipos de prueba.** Llegan a la base por un puerto (`IAnotaciones`), como un módulo (AD-4). `Pruebas/Registro.cs` (`AddPruebas`) los da de alta: contexto, puerto y renglón. `WebNoMigraTests` usa la web real.
- **`TransaccionDeModulo` y `ModuloConTransaccion` son públicas** porque el código que Wolverine genera vive en otro ensamblado y el arnés las usa.
- **Orden del middleware.** La política se agrega con `Middleware.Add`: validación (entrada 6) y autorización (entrada 7) tendrán que registrarse de modo que corran antes de abrir la transacción.

## Plan Change Log

### 2026-10-06 — Intent reescrito tras el bloqueo
- Lo que lo provocó: la revisión del primer intento encontró tres huecos de intent (ver Review Triage Log) y el plan quedó bloqueado. Gerardo los decidió el mismo día; están como cinco líneas `Decision:` en las Notes de la épica y en la arquitectura (Consistency Conventions, AD-25).
- Qué cambió: el `<intent-contract>` (dueño de la transacción por módulo, consultas fuera, el handler guarda y el host cierra según el `Resultado`, colas locales durables ya decididas, la web se niega a arrancar sin migrar), el Code Map, las tareas, los criterios y las Design Notes.
- Estado malo que evita: `AutoApplyTransactions` (la web no arranca con dos contextos; guarda por el handler; confirma aunque el `Resultado` sea fallo; mete a las consultas en transacción) y la prueba de AD-25 vaciada con un arranque sin mensajería.
- KEEP: lo que el Code Map marca como reutilizable del parche.

## Review Triage Log

### 2026-10-06 — Review pass
- verdicts: 6 findings — high 1, medium 3, low 2, false 0, maybe-false 0
- intento guardado como parche: [story-un-caso-de-uso-recorre-isender-transaccion-y-outbox-plan.patch](story-un-caso-de-uso-recorre-isender-transaccion-y-outbox-plan.patch) (se aplica limpio sobre `95d40a6` con `git apply`); el código se revirtió a la base.
- findings:
  - `[high]` `[intent_gap]` `Program.cs`, `AutoApplyTransactions`: si las dependencias de un handler alcanzan dos tipos de `DbContext`, la web no arranca (`Cannot determine the DbContext type ... multiple DbContext types detected`) — el revisor lo reprodujo con una sonda (`DosContextosHandler`: un puerto sobre `PruebasDbContext` más un servicio que recibe `PacientesDbContext`); el orquestador leyó la sonda. La arquitectura exige esa forma (AD-13 y AD-28: un handler de Agenda llama a `IPacientesApi`; AD-20). Lo que Wolverine ofrece (`[Transactional(typeof(...))]` o `[NonTransactional]` en el handler) contradice "sin que el handler ni el comando la pidan". El intent no dice quién es dueño de la transacción cuando hay dos contextos: decisión de Gerardo.
  - `[low]` `[intent_gap]` README y comentario de `Program.cs` dicen "todo handler que recibe un `DbContext`", pero la política cubre también al handler que solo recibe un puerto (sonda: transacción abierta antes de la primera escritura), y el ejemplo del README guarda directo por el contexto, que AD-4 prohíbe en un módulo — cierto; es redacción y se corregiría con un parche, pero queda sin efecto porque el código se revirtió. Comparte ruta con el primero: el alcance de la política es lo que falta decidir.
  - `[medium]` `[intent_gap]` Las consultas corren dentro de una transacción y el middleware llama a `SaveChangesAsync` al terminar (sonda: `CurrentTransaction` no nulo en una consulta; un `Add` sin guardar quedó guardado) — contra "nunca `SaveChanges` en una consulta". El intent dice "a todo comando" y no dice qué recibe una consulta; excluirlas cambia la política, misma decisión que el primero.
  - `[medium]` `[intent_gap]` `WebNoMigraTests` ya no ejercita la web real: arranca con `ArrancarWebSinMensajeria` (Wolverine sin almacenamiento), así que su afirmación sobre el esquema `wolverine` no puede fallar — cierto. Causa de fondo: con almacenamiento durable y `AutoCreate.None` la web real se niega a arrancar sin el esquema `wolverine`. El intent pedía a la vez Wolverine durable y que esa prueba (GET `/` con 200 sobre base vacía) pasara: no se pueden cumplir las dos. Decisión de Gerardo: ¿la web debe negarse a arrancar sobre una base sin migrar?
  - `[medium]` `[intent_gap]` Un handler que guarda, publica y después devuelve `Resultado.Fallo` deja confirmados el cambio y el mensaje (sonda del revisor: `Forma = Fallo`, 1 fila, 1 mensaje): solo una excepción deshace — cierto y ahora observado. El intent lo dejó fuera a propósito ("Never: decidir qué pasa..."); con el dato en la mano es la misma decisión pendiente, y AD-6 y AD-8 dependen de ella.
  - `[low]` `[reject]` `CasoDeUsoTests`, "Fallo inesperado": la afirmación "ni mensaje" pasa aunque se quite la política, porque Wolverine retiene los mensajes salientes hasta que el handler termina bien — cierto; la afirmación sobre la fila sí depende de la política y es la que hace fallar la prueba (comprobado por el orquestador y por el revisor). Hacer que la mitad del mensaje dependa de la política pediría otro comando de prueba para un caso que la otra afirmación ya cubre.

### 2026-10-06 — Review pass (segundo intento)
- verdicts: 6 findings — high 0, medium 2, low 4, false 0, maybe-false 0
- findings:
  - `[medium]` `[patch]` `UnaTransaccionPorModulo.cs`, comprobación del retorno del handler de un comando: aceptaba cualquier `Resultado`, así que un handler de `ICommand<Guid>` que devuelve `Resultado<int>` arrancaba, confirmaba su cambio y después `ISender` lanzaba (sonda del revisor: excepción y la fila guardada) — el arranque exige ahora exactamente `Resultado<T>` con la `T` del `ICommand<T>` y, si no, se niega con un error que nombra handler, comando, tipo esperado y tipo devuelto. README ajustado.
  - `[medium]` `[patch]` `UnaTransaccionPorModulo.cs`, `if (contextos is [null]) return;`: un handler de evento fuera de todo módulo corría sin transacción y en silencio, contra "la política cubre handlers de comandos y de eventos" — ahora el host se niega a arrancar, con el mismo error que para un comando; la comprobación va antes que la de "más de un módulo". `[ASSUMPTION]` extiende a eventos el supuesto que el intent escribe para comandos; el revisor comprobó que ninguna prueba necesitaba el camino silencioso.
  - `[low]` `[reject]` Un evento con handlers de dos módulos en la misma cadena impide arrancar (Wolverine combina por defecto los handlers de un mensaje) — cierto por lectura, sin sonda. Falla con un mensaje claro ante un caso que hoy no existe: ningún evento de la arquitectura tiene dos módulos suscritos. Cómo separarlos (`MultipleHandlerBehavior.Separated` u otra forma) es decisión de Gerardo cuando ocurra. El texto engañoso del caso "uno dentro, uno fuera" quedó resuelto por el parche anterior.
  - `[low]` `[reject]` `ConfirmarAsync`: si un contexto de módulo llevara el modelo de Wolverine, `EfCoreEnvelopeTransaction.CommitAsync` llamaría a `SaveChangesAsync` y el host guardaría por el handler — cierto según el código de Wolverine 6.45, pero hoy ningún contexto lo lleva y el intent lo prohíbe; una comprobación al arrancar vigilaría un estado que no se ha visto. Queda como riesgo.
  - `[low]` `[patch]` `TransaccionDeModulo.cs`, comentarios: decían que confirmar solo confirma el cambio y los mensajes; también marca como atendido el mensaje entrante en la misma transacción — comentarios corregidos, sin cambio de comportamiento. La dependencia de `Wolverine.EntityFrameworkCore.Internals` no tiene arreglo aquí: queda como riesgo.
  - `[low]` `[reject]` No hay prueba de que un handler de evento que lanza deshaga su transacción — el revisor comprobó con una sonda que el comportamiento es correcto (no quedó fila). La matriz no pide esa fila, y fijarla pide una prueba de espera negativa, propensa a fallar por tiempos.

## Design Notes

- **Cómo ver el mensaje en el outbox sin carreras.** Un mensaje programado para dentro de una hora se queda como fila en el esquema `wolverine`: la prueba lo encuentra después del éxito y comprueba que no existe después de un fallo. La fila "El mensaje llega" usa un evento inmediato y espera el rastro de su handler con un límite de tiempo.
- **Que cada afirmación dependa de la política.** Wolverine retiene los mensajes salientes hasta que el handler termina sin excepción, con o sin política: en "Fallo inesperado" la afirmación que prueba la transacción es la de la fila. En "Fallo después de cambiar" no hay excepción, así que ahí las dos afirmaciones dependen de la política.
- **Ejemplo del cierre**, visto desde quien llama:

  ```csharp
  var resultado = await sender.SendAsync(comando);   // la página o una Minimal API
  // Éxito: fila y mensaje confirmados. Fallo: nada quedó. Nunca hay Commit ni SaveChanges aquí.
  ```
- **Varias webs en memoria contra la misma base.** Cada prueba arranca su web y cada una es un nodo de Wolverine; en el primer intento no estorbó. Si estorba, se ajusta en el arnés.

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 errores, 0 advertencias
- `./dotnet.sh test` -- expected: todas las pruebas pasan, con Docker arriba y sin AppHost
- `./dotnet.sh run --project src/DraSelina.Consultorio.AppHost` y, en otra terminal, `scripts/verificar-arranque.sh` -- expected: sale con 0

**Manual checks (if no CLI):**
- Quitar temporalmente la política del host y confirmar que fallan "Fallo después de cambiar" y "Fallo inesperado"; restaurarla.

## Auto Run Result — primer intento (2026-10-06, bloqueado)

- Status: blocked
- Blocking condition: intent gap
- Qué pasó: el plan se implementó y pasó la verificación (build con 0 advertencias; `./dotnet.sh test` 58 de 58; `scripts/verificar-arranque.sh` con 0; sin `AutoApplyTransactions` falla solo "Fallo inesperado"). La revisión (lente `quick`, 6 hallazgos) encontró tres cosas que el intent no decide. El código se revirtió a `95d40a6` y el intento quedó en [story-un-caso-de-uso-recorre-isender-transaccion-y-outbox-plan.patch](story-un-caso-de-uso-recorre-isender-transaccion-y-outbox-plan.patch): `git apply` lo restaura completo.
- Preguntas sin contestar, para Gerardo:
  1. **Dueño de la transacción y alcance de la política.** Con `AutoApplyTransactions`, un handler cuyas dependencias alcanzan dos `DbContext` impide que la web arranque. Ejemplo: `ConfirmarReserva` en Agenda usa su puerto (`AgendaDbContext`) y llama a `IPacientesApi.BuscarOCrear`, cuya implementación usa `PacientesDbContext` (AD-13, AD-28). ¿Quién es dueño de la transacción: el contexto del módulo del handler, por convención que fija el host, o lo declara cada handler con un atributo? ¿Y las consultas quedan fuera de la política (hoy reciben transacción y `SaveChangesAsync`)?
  2. **Web sobre base sin migrar.** Con almacenamiento durable y sin permiso de crear tablas, la web real se niega a arrancar si falta el esquema `wolverine` ("The Wolverine message storage ... is missing"); no crea nada. ¿Es el comportamiento querido? Si sí, `WebNoMigraTests` cambia de "arranca y no crea nada" a "no arranca y no crea nada".
  3. **Fallo después de cambiar.** Observado: un handler que guarda, publica y devuelve `Resultado.Fallo` deja confirmados la fila y el mensaje; solo una excepción deshace. ¿Un `Resultado` no exitoso debe deshacer la transacción? AD-6 ("devuelve conflicto y no cambia nada") y AD-8 (choque `23P01` traducido a conflicto) dependen de esto.
- Hallazgos de la revisión: 5 con ruta `intent_gap` (1 high, 3 medium, 1 low), 1 rechazado (low: la afirmación "ni mensaje" de "Fallo inesperado" no depende de la política; la de la fila sí). Sin parches ni diferidos. Detalle en Review Triage Log.
- Lo que el intento sí dejó comprobado (está en el parche): `ISender` sobre `IMessageBus.InvokeAsync<Resultado<T>>`; el mensaje se escribe en `wolverine` dentro de la transacción del handler; sin `UseDurableLocalQueues` no hay fila de outbox; los `DbContext` necesitan `optionsLifetime: ServiceLifetime.Singleton`; el arnés agrega el ensamblado de pruebas con `ConfigureWolverine`.
- `[ASSUMPTION]` que sigue en pie para cuando se retome: los mensajes van a colas locales durables.
- Sin commits. `spikes/`, arquitectura, épica y `tickets.toml` sin tocar.

## Auto Run Result

- Status: built
- Resumen: el host implementa `ISender` sobre Wolverine y fija con una política propia que todo comando y todo handler de evento corre en la transacción del contexto de su módulo. El host abre la transacción, el handler guarda, y al terminar el host confirma el cambio y sus mensajes juntos si el `Resultado` es exitoso, o deshace todo si no lo es o si hubo excepción. Las consultas corren sin transacción. Los mensajes van a colas locales durables. Sobre una base sin migrar la web se niega a arrancar y no crea nada. Todo se demuestra con tipos de prueba que viven solo en `tests/`.
- Archivos:
  - `Directory.Packages.props`, `src/DraSelina.Consultorio.Web/DraSelina.Consultorio.Web.csproj` — `WolverineFx.EntityFrameworkCore` y `WolverineFx.RuntimeCompilation` 6.45.0
  - `src/DraSelina.Consultorio.Web/SenderDeWolverine.cs` — `ISender` sobre `IMessageBus.InvokeAsync<Resultado<T>>`
  - `src/DraSelina.Consultorio.Web/UnaTransaccionPorModulo.cs` — la política y sus comprobaciones al arrancar
  - `src/DraSelina.Consultorio.Web/TransaccionDeModulo.cs` — la tabla namespace → contexto y la transacción que usa el código generado
  - `src/DraSelina.Consultorio.Web/Program.cs`, `AlmacenamientoDeWolverine.cs` — composición de Wolverine, la tabla de tres renglones, colas locales durables
  - `src/DraSelina.Consultorio.Web/Modules/*/Infrastructure/Registro.cs` — opciones del contexto como singleton
  - `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/` — comandos, consultas, evento, handlers, puerto, entidades, códigos y `AddPruebas`
  - `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs`, `Consultas.cs` — alta de los tipos de prueba; lectura del esquema `wolverine`
  - `tests/DraSelina.Consultorio.IntegrationTests/CasoDeUsoTests.cs`, `WebNoMigraTests.cs` — una prueba por fila de la matriz
  - `README.md` — sección "Casos de uso"
- Revisión (lente `quick`): 6 hallazgos; 3 parches (2 medium, 1 low), 0 diferidos, 3 rechazados (3 low): evento con handlers de dos módulos (falla claro ante un caso que hoy no existe; decisión futura), `SaveChanges` del host si un contexto se integrara con Wolverine (estado no visto), y la prueba faltante del handler de evento que lanza (comportamiento comprobado por sonda; la prueba sería de espera negativa).
- Revisión de seguimiento: `true`. Parches por veredicto: high 0, medium 2, low 1. Riesgo sin verificar: las dos ramas de rechazo al arrancar que agregaron los parches (retorno que no es `Resultado<T>` exacto; handler de evento fuera de módulo) no tienen prueba propia ni las vio un revisor.
- Verificación (corrida por el orquestador después de los parches): `./dotnet.sh build` → 0 advertencias, 0 errores; `./dotnet.sh test` → 62 de 62 (18 Kernel, 24 arquitectura, 20 integración); `scripts/verificar-arranque.sh` contra el AppHost → 0, cuatro filas pasan. Antes de los parches, con la política quitada de `Program.cs` fallaron 4 pruebas: "Fallo después de cambiar", "Fallo inesperado", "Dos contextos" y "comando fuera de módulo"; restaurada. Las 11 filas de la matriz tienen prueba que corrió y pasó.
- Para Gerardo:
  - `[ASSUMPTION]` Un handler de comando o de evento fuera de todo módulo dado de alta impide arrancar.
  - El namespace de la tabla se compara exacto: un handler en `Agenda.Features.Reservas` quedaría "fuera de módulo" y la web no arrancaría. Si los casos de uso van a vivir en subcarpetas con su propio namespace, hay que cambiarlo a prefijo. La arquitectura dice "un namespace plano por capa", así que hoy coincide.
  - Un evento con handlers de dos módulos impide arrancar; habrá que decidir cómo separarlos cuando ocurra.
  - La política depende de `Wolverine.EntityFrameworkCore.Internals.EfCoreEnvelopeTransaction`: pública, pero de un namespace `Internals`; puede cambiar al subir de versión de Wolverine. Las pruebas de integración lo detectarían.
  - Si algún día un contexto de módulo se integra con Wolverine (`AddDbContextWithWolverineIntegration`), el host empezaría a llamar a `SaveChanges` al confirmar.
  - El README fija como convención que los eventos se publican con `IMessageBus` dentro del handler y que Wolverine encuentra el handler por nombre (`*Handler`, método `Handle`).
  - Las entradas 6 y 7 deben registrar su middleware de modo que corra antes de abrir la transacción.
  - Pregunta abierta en la épica: interceptores de EF Core para eventos y permanencia de Wolverine, al terminar todas las épicas.
