---
title: 'Outbox propio y entrega de eventos'
type: 'feature' # feature | bugfix | refactor | chore
ticket: '11' # the entry id from the ticket tree, or the story file's stem when the entry has no id; empty outside it
created: '2026-10-13'
status: 'built' # draft | ready-for-dev | in-progress | in-review | built | done | blocked | dropped
baseline_revision: 'f7921bb9df09d3c6098146aab002049a60a0f4d7' # the commit the work starts from, set before any code change
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
deferred:
  - summary: >-
      Un handler de evento que nunca termina detiene toda la entrega: el servicio entrega de uno en uno, sin límite de tiempo por handler, y mientras uno está colgado ningún otro evento de ningún outbox se entrega; la fila sigue apartada y nunca suma intentos ni llega a Fallido.
    evidence: |-
      EntregaDeEventos.DarUnaVueltaAsync recorre los outbox en un solo bucle secuencial y el único token que recibe un handler es el de apagado. Cierto por lectura; no se sabe si algún handler llegará a colgarse, porque todavía no existe ninguno de negocio (el primero enviará correo). Lo zanjaría un handler de prueba que no regrese. La salida es un límite de tiempo por handler: cuánto dura y qué pasa al vencerse (¿cuenta como intento fallido?) es decisión de Gerardo, antes del primer handler que llame a un servicio externo.
    location: >-
      src/DraSelina.Consultorio.Web/EntregaDeEventos.cs (DarUnaVueltaAsync, EntregarAsync)
    severity: medium (unverified)
---

<intent-contract>

## Intent

**Problem:** Los módulos se hablan de forma asíncrona por eventos de integración (AD-3), y sin Wolverine no hay nada que los guarde con el cambio ni que los entregue después. Sin eso, una reserva tendría que esperar al correo, o el correo se perdería si la web se cae.

**Approach:** Un outbox propio. El módulo que publica guarda cada evento en una tabla `outbox` de su esquema, dentro de la transacción del handler. Un servicio del host en segundo plano entrega cada evento pendiente a sus handlers, reintenta los que fallan con espera creciente y respeta el orden por clave. Se entrega para Agenda (tabla, puerto y registro) y se demuestra con eventos de prueba en el esquema `pruebas`.

## Boundaries & Constraints

**Always:**
- **Guardar.** El handler de un comando publica por un puerto de su módulo, dentro de su transacción abierta: el evento y el cambio se confirman juntos, o ninguno. Publicar pide el evento, su `EventoId`, su clave de orden y cuándo ocurrió.
- **La tabla** se llama `outbox`, vive en el esquema del módulo que publica y tiene las columnas del modelo de datos: `id` (el `EventoId`), `tipo`, `contenido` (`jsonb`), `ocurrido_en`, `clave_de_orden`, `estado` (`Pendiente`, `Entregado`, `Fallido`), `intentos`, `proximo_intento_en`, `entregado_en`. Agenda la recibe por una migración suya.
- **Entregar.** Un servicio del host en segundo plano da vueltas y entrega cada evento pendiente a todos los handlers registrados para su tipo; al terminar todos sin error, el evento queda `Entregado` con su `entregado_en`. Un handler de evento implementa `IEventHandler<TEvento>` y lo registra su módulo.
- **Reintentar.** Si un handler lanza, el evento sigue `Pendiente`, suma un intento y espera antes del siguiente: 1 min, 5 min, 15 min, 1 h, 4 h, 12 h. Al fallar el séptimo intento queda `Fallido`, se registra un error que nombra el evento y no se intenta más. El calendario vive en un solo lugar del código.
- **Orden por clave.** Un evento no se entrega mientras haya otro anterior (por `ocurrido_en`) con la misma `clave_de_orden` todavía `Pendiente`. Los de otras claves siguen. Si el anterior queda `Fallido`, los siguientes ya no esperan.
- **Dos instancias.** El servicio aparta los pendientes con `FOR UPDATE SKIP LOCKED`: dos webs sobre la misma base nunca entregan el mismo evento a la vez.
- **Sobrevive a una caída.** Un evento que quedó `Pendiente` se entrega cuando la web vuelve a arrancar.
- **El tiempo** se lee del reloj inyectado (`IClock`), nunca de la hora del sistema ni de `now()` de la base.
- **El servicio nunca tumba la web.** Si una vuelta falla (por ejemplo, la tabla no existe porque la base está sin migrar), registra el error y sigue; la web arranca y atiende igual (AD-25).
- El host lee y actualiza el outbox de un módulo a través del contexto de ese módulo. El host conoce los outbox porque `Program.cs` los da de alta; un módulo no usa tipos del host.
- `[ASSUMPTION]` `IEventHandler<TEvento>` vive en `BuildingBlocks.Contracts`, sin paquetes, junto a las interfaces de handler: es la única capa que ven tanto el módulo que consume como el host.
- `[ASSUMPTION]` El servicio da una vuelta cada 5 segundos. Los eventos entregados se quedan en la tabla.
- `[ASSUMPTION]` Un evento sin ningún handler registrado para su tipo queda `Entregado` y se registra un aviso: quien publica no sabe quién escucha (E-14).
- Los tipos de prueba viven en `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/` y sus tablas en el esquema `pruebas`. Las pruebas no dependen del orden ni de lo que otra dejó, aunque corran en paralelo sobre la misma base.
- Las pruebas de arquitectura y del Kernel siguen pasando; build con 0 advertencias.

**Never:**
- Paquetes nuevos. Nada en `Kernel` (E-17). Un tipo compartido entre módulos para el outbox o su fila: cada módulo declara lo suyo, como con la transacción.
- Eventos de negocio (`CitaAgendada` y los demás), handlers de Notificaciones, recordatorios o envío de correo: llegan con sus épicas.
- Decoradores, Scrutor o validación (entrada 6); actor o autorización (entrada 7).
- Tablas nuevas en `pacientes` o `notificaciones`. En `agenda`, solo `outbox`.
- Eventos, handlers o tablas de prueba en `src/`.
- Pruebas que esperen minutos reales: el tiempo se mueve con el reloj.
- Tocar `spikes/`, la arquitectura, el modelo de datos, las épicas, `tickets.toml` o `change-quitar-wolverine/`. Sin commits.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Juntos | Comando de prueba que guarda una fila, publica un evento y confirma | La fila existe y el evento está en `pruebas.outbox` con `id` igual a su `EventoId` | — |
| Ninguno | El mismo comando deshace, o lanza, después de publicar | No hay fila ni evento | — |
| El evento llega | Evento publicado con un handler de prueba | El handler corre después de confirmada la transacción y deja su rastro; el evento queda `Entregado` con `entregado_en` | — |
| Varios handlers | Evento con dos handlers registrados | Los dos lo reciben; queda `Entregado` | — |
| Sin handler | Evento de un tipo que nadie escucha | Queda `Entregado`; se registra un aviso | — |
| Reintento | Handler que falla la primera vez | Tras la falla: `Pendiente`, `intentos` 1, `proximo_intento_en` un minuto después. No se entrega antes de ese minuto. Pasado el minuto se entrega y queda `Entregado` | La prueba lo afirma |
| Tope | Handler que falla siempre, avanzando el reloj | Los intentos ocurren con las esperas del calendario; al fallar el séptimo queda `Fallido`, con un error registrado, y no se vuelve a intentar | La prueba lo afirma |
| Orden por clave | Dos eventos con la misma clave; el primero falla una vez. Un tercero con otra clave | Mientras el primero está `Pendiente` el segundo no se entrega y el tercero sí. Después llegan el primero y el segundo, en ese orden | — |
| Fallido no bloquea | Dos eventos con la misma clave; el primero queda `Fallido` | El segundo se entrega | — |
| Dos instancias | Dos webs sobre la misma base dan una vuelta a la vez sobre el mismo evento pendiente | Su handler corre una sola vez | — |
| Reinicio | Un evento quedó `Pendiente`; arranca una web nueva | El evento se entrega | — |
| Outbox de Agenda | Por el puerto de Agenda, dentro de su transacción: publicar y confirmar; publicar y deshacer | Confirmado: la fila está en `agenda.outbox` con sus columnas. Deshecho: no está | — |
| Base migrada | La base de la corrida | Existe `agenda.outbox` con las nueve columnas del modelo de datos | — |
| Base sin migrar | La web real contra una base vacía; `GET /` | Responde 200 y la base sigue sin esquemas ni historial, aunque el servicio no pueda leer ningún outbox | — |

</intent-contract>

## Code Map

- `src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/` -- `ICommandHandler.cs`, `IQueryHandler.cs`: modelo para `IEventHandler`. Capa pura: solo `System.*` y `Kernel`
- `src/DraSelina.Consultorio.Web/Kernel/IClock.cs` -- `DateTimeOffset UtcNow`. Nadie lo implementa ni lo registra todavía: el host necesita su implementación sobre la hora del sistema
- `src/DraSelina.Consultorio.Web/Program.cs` -- composición: `AddAgenda/AddPacientes/AddNotificaciones(cadena)`. Aquí se registran el reloj, el servicio de entrega y el alta de cada outbox. `<Using Remove="Microsoft.*" />` en el `.csproj`: todo `using` se escribe
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Features/ITransaccionDeAgenda.cs` e `Infrastructure/TransaccionDeAgenda.cs` -- el patrón puerto en `Features`, implementación en `Infrastructure` sobre `AgendaDbContext`, registro en `Registro.cs`. El puerto de outbox de Agenda sigue el mismo patrón. `Features` no puede usar `Infrastructure`
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/AgendaDbContext.cs` -- `Esquema = "agenda"`, `Configurar(opciones, cadena)`, hoy sin tablas. `Migrations/` tiene `Inicial` vacía. Migración nueva con `./dotnet.sh ef migrations add … --project src/DraSelina.Consultorio.Web --context AgendaDbContext --output-dir Modules/Agenda/Infrastructure/Migrations --namespace DraSelina.Consultorio.Agenda.Infrastructure.Migrations` (README, "Crear una migración")
- `_bmad-output/initiative-draselina/architecture-appointment-booking/ddd-and-data-model-appointment-booking.md`, sección `agenda.outbox` -- tipos y nombres exactos de las columnas. Solo lectura
- `src/DraSelina.Consultorio.MigrationService/Program.cs` -- aplica las migraciones de Agenda; no necesita cambios
- `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs` -- `ArrancarWeb()`/`ArrancarWeb(cadena)`, `CrearBaseVaciaAsync()`, `CrearContextoDePruebas()`; `MigrationService.EjecutarAsync(cadena)` migra otra base; las tablas de `pruebas` se crean con `CreateTablesAsync` después de migrar
- `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/` -- `Anotar.cs` (comando con `Desenlace`), `Anotaciones.cs` (puerto), `TransaccionDePruebas.cs`, `Registro.cs` (`AddPruebas`), `PruebasDbContext.cs`. Aquí van el outbox de prueba, los eventos y sus handlers
- `tests/DraSelina.Consultorio.IntegrationTests/CasoDeUsoTests.cs`, `TransaccionDeModuloTests.cs`, `BaseMigradaTests.cs`, `WebNoMigraTests.cs` -- forma de las pruebas existentes; `WebNoMigraTests` debe seguir pasando con el servicio de entrega corriendo
- `tests/DraSelina.Consultorio.ArchitectureTests/Reglas.cs` -- un archivo nuevo debe llevar el namespace de la capa de su carpeta; los de la raíz de `Web` son host
- `README.md` -- secciones "Casos de uso" y "El arnés de integración"

## Tasks & Acceptance

**Execution:**
- [x] `src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/IEventHandler.cs` -- la interfaz del handler de un evento -- AD-3
- [x] `src/DraSelina.Consultorio.Web/Modules/Agenda/{Features,Infrastructure}/` -- puerto de outbox de Agenda, su fila y su mapeo en `AgendaDbContext`, su implementación, su registro y la migración que crea `agenda.outbox` -- AD-2, P11
- [x] `src/DraSelina.Consultorio.Web/` (raíz, host) -- reloj del sistema; alta de un outbox por contexto; la entrega (tomar pendientes, orden por clave, entregar, reintentar según el calendario, marcar) y el servicio en segundo plano que la repite -- P11
- [x] `src/DraSelina.Consultorio.Web/Program.cs` -- registrar el reloj, el outbox de Agenda y el servicio -- composición
- [x] `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/` y `Arnes.cs` -- outbox de prueba en `pruebas`, eventos y handlers de prueba, un reloj que la prueba mueve, y lo que haga falta para que una prueba dé una vuelta de entrega cuando quiera -- lo pide la entrada
- [x] `tests/DraSelina.Consultorio.IntegrationTests/` (pruebas) -- una prueba por fila de la matriz -- P11
- [x] `README.md` -- sección "Eventos entre módulos": cómo publica un handler, cómo se escribe y registra un handler de evento (y que debe tolerar recibirlo más de una vez), cómo se da de alta el outbox de un módulo, el calendario de reintentos, el orden por clave, qué significa `Fallido` y cómo se ve en la tabla -- la épica pide que la épica 2 no decida esto

**Acceptance Criteria:**
- Given Docker en ejecución y ningún AppHost arriba, when se corre `./dotnet.sh test` tres veces seguidas, then pasan todas las pruebas las tres veces.
- Given la suite de integración, when se mide, then ninguna prueba del outbox espera un minuto real ni más.
- Given `src/`, when se buscan los tipos de prueba, then no hay ninguno.
- Given el AppHost levantado, when se corre `scripts/verificar-arranque.sh`, then sale con 0 y `agenda.outbox` existe en la base.

## Implementation Notes

- **Publicar.** `IOutboxDeAgenda.PublicarAsync(evento, eventoId, claveDeOrden, ocurridoEn, ct)` en `Agenda.Features`; `OutboxDeAgenda` en `Infrastructure` inserta la fila con SQL directo sobre la transacción abierta de `AgendaDbContext`, no con `SaveChanges`, para no guardar de paso lo que el handler tenga sin guardar. Lanza si no hay transacción abierta. `tipo` es el nombre corto del tipo real del evento (`evento.GetType().Name`) y `contenido` su JSON.
- **Tabla.** Migración `20261013160526_CrearOutbox` crea `agenda.outbox` con las nueve columnas del modelo de datos; `FilaDeOutbox` y su mapeo en `AgendaDbContext`. Sin índices: el modelo no lista ninguno.
- **Entrega (host).** `Outbox.cs` (`AddEntregaDeEventos`, `AddOutbox<TContexto>`, `CatalogoDeEventos`), `EntregaDeEventos.cs` (una vuelta), `ServicioDeEntregaDeEventos.cs` (una vuelta al arrancar y otra cada 5 s), `CalendarioDeReintentos.cs`, `RelojDelSistema.cs`. El host trabaja con SQL sobre la conexión y la transacción del contexto del módulo; ningún módulo usa tipos del host.
- **Una transacción por evento.** Aparta una fila con `FOR UPDATE … SKIP LOCKED` (la consulta aplica el orden por clave), llama a los handlers y la marca. Cada handler corre en su propio ámbito del contenedor. Si un `HandleAsync` lanza, los demás handlers reciben el evento igual, y en el reintento lo reciben todos otra vez.
- **Marcar no se cancela.** Las primeras corridas mostraron una entrega doble real: una web que se apagaba a media entrega deshacía la marca y otra web entregaba de nuevo. Tras marcar con `CancellationToken.None`, 0 de 8 corridas la repitieron, contra 5 de 7 antes.
- **Catálogo de eventos.** Sale de los `IEventHandler<>` registrados. Dos tipos de evento con el mismo nombre entre los que tienen handler impiden arrancar la web; un evento sin handler queda `Entregado` con un aviso.
- **Pruebas.** `EscenarioDeEntrega` da a una prueba su propia base migrada, una web sin entrega automática y su propio reloj; la prueba da las vueltas. `RegistrosDePrueba` captura el registro para afirmar el error del tope y el aviso de "sin handler".
- **`IClock`** tiene ahora su implementación en el host (`RelojDelSistema`), registrada en `Program.cs`.

## Plan Change Log

## Review Triage Log

### 2026-10-13 — Review pass
- verdicts: 8 findings — high 0, medium 2, low 5, false 0, maybe-false 1
- findings:
  - `[medium]` `[patch]` `OutboxDeAgenda.PublicarAsync<TEvento>` (y su copia de prueba): `tipo` y el JSON salían del argumento de tipo estático; publicar un evento guardado en una variable de tipo base u `object` dejaba `tipo = "Object"`, nadie lo escuchaba y quedaba `Entregado`: evento perdido en silencio — ahora usa `evento.GetType()`; prueba nueva `Un_evento_publicado_desde_una_variable_de_tipo_object_llega_a_su_handler`.
  - `[low]` `[reject]` `CatalogoDeEventos`: la comprobación de nombre único solo ve tipos con handler; dos eventos del mismo nombre corto, uno sin handlers, se confundirían al deserializar — cierto por lectura, pero pide que dos módulos nombren igual un evento, algo que el glosario y la revisión de cada historia evitan; el arreglo cambia qué se guarda en `tipo`. El README lo deja como regla del desarrollador.
  - `[low]` `[patch]` `CatalogoDeEventos`: dos tipos de evento con el mismo nombre entre los que tienen handler impiden arrancar la web, y el README no lo decía — es un error de programación que falla de forma visible al arrancar; no es una vuelta que falla. README corregido para decirlo.
  - `[low]` `[reject]` `Despacho<TEvento>.LlamarAsync`: `GetServices(...).ElementAt(indice)` construye todos los handlers del evento en cada ámbito; si uno no se puede construir, ninguno recibe el evento — es un error de configuración que se nota al primer evento y acaba en `Fallido`; resolver cada handler por separado pide rehacer el catálogo. El README decía de más ("si uno lanza, los demás lo reciben igual"): corregido para limitarlo a cuando lanza `HandleAsync`.
  - `[low]` `[reject]` `EntregarAsync`: una falla provocada por el apagado que no sea `OperationCanceledException` gasta un intento — no reproducido; cuesta a lo más una espera de un minuto en un evento.
  - `[maybe-false]` `[defer]` `DarUnaVueltaAsync`: un handler que nunca termina detiene toda la entrega y nunca suma intentos — cierto por lectura; no se sabe si ocurrirá, porque no hay handlers de negocio. Lo zanjaría un handler de prueba que no regrese. Si fuera cierto sería `medium`. Diferido: el límite de tiempo por handler es decisión de Gerardo.
  - `[medium]` `[patch]` `EntregaDeEventosTests`, las dos pruebas de "Dos instancias": la segunda web arrancaba dentro de su propia vuelta, así que la primera podía terminar sola y las afirmaciones pasaban sin solape — ahora las dos webs están construidas y arrancadas antes de que empiece cualquiera de las vueltas.
  - `[low]` `[patch]` Criterio de aceptación 4: el script de arranque no miraba `agenda.outbox` — se agregó la comprobación dentro de la fila "Historial por esquema"; siguen siendo tres filas. Corrida por el orquestador: `PASA agenda.outbox existe`.

## Design Notes

- **Cómo lee el host sin un tipo compartido.** Los nombres de tabla y columnas son fijos, así que el host puede trabajar con SQL sobre la conexión y la transacción del contexto del módulo. La consulta que aparta ya tiene que ser SQL (`FOR UPDATE SKIP LOCKED`). Es una sugerencia; lo que importa es que ningún módulo dependa del host y que no haya un tipo compartido entre módulos.
- **De `tipo` al tipo de .NET.** Los handlers registrados dicen qué tipos de evento existen; con eso el host arma el mapa para deserializar `contenido`.
- **Publicar, visto desde un handler:**

  ```csharp
  await using var tx = await transaccion.AbrirAsync(ct);
  anotaciones.Agregar(nueva);
  await anotaciones.GuardarAsync(ct);
  await outbox.PublicarAsync(new AnotacionHecha(nueva.Id), eventoId, claveDeOrden: nueva.Id, ocurridoEn, ct);
  await tx.ConfirmarAsync(ct);
  ```
- **El riesgo principal está en las pruebas.** Todas las webs en memoria de la corrida comparten `pruebas.outbox`, y cada una corre su servicio de entrega: una web puede entregar el evento que publicó la prueba de otra, y una web con el reloj adelantado puede reintentar antes de tiempo el evento de otra prueba. Las pruebas de reintento, tope y orden necesitan aislarse de eso: una base propia migrada (`CrearBaseVaciaAsync` más `MigrationService.EjecutarAsync`), o una web sin el servicio automático cuya vuelta la da la prueba, o ambas. Lo que una prueba afirma debe salir de la base, no de memoria del proceso.
- **La fila queda apartada mientras corren sus handlers.** Con un handler lento (enviar un correo) la transacción del outbox dura lo que dure la entrega. Para el piloto es aceptable; si se observa un problema, se reporta.

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 errores, 0 advertencias
- `./dotnet.sh test` (tres veces seguidas) -- expected: todas las pruebas pasan las tres veces
- `./dotnet.sh run --project src/DraSelina.Consultorio.AppHost` y, en otra terminal, `scripts/verificar-arranque.sh` -- expected: sale con 0

## Auto Run Result

- Status: built
- Resumen: outbox propio. El handler de un comando publica por el puerto de outbox de su módulo, dentro de su transacción, y el evento se confirma junto con el cambio o no queda. Un servicio del host da una vuelta al arrancar y cada 5 segundos: aparta cada evento pendiente con `FOR UPDATE SKIP LOCKED`, respeta el orden por clave, lo entrega a todos sus handlers (`IEventHandler<TEvento>`) y lo marca. Una entrega fallida se reintenta con el calendario de 7 intentos (1 min, 5 min, 15 min, 1 h, 4 h, 12 h) y después queda `Fallido` con un error registrado. Agenda ya tiene su tabla `agenda.outbox`, su puerto y su alta en el host.
- Archivos:
  - `src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/IEventHandler.cs` — la interfaz de quien escucha un evento
  - `src/DraSelina.Consultorio.Web/Modules/Agenda/Features/IOutboxDeAgenda.cs`, `Infrastructure/OutboxDeAgenda.cs`, `FilaDeOutbox.cs`, `AgendaDbContext.cs`, `Registro.cs`, `Migrations/20261013160526_CrearOutbox*` — el outbox de Agenda y su tabla
  - `src/DraSelina.Consultorio.Web/Outbox.cs`, `EntregaDeEventos.cs`, `ServicioDeEntregaDeEventos.cs`, `CalendarioDeReintentos.cs`, `RelojDelSistema.cs`, `Program.cs` — la entrega y su servicio en el host
  - `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/` — outbox, eventos y handlers de prueba, `RelojDePrueba`, `RegistrosDePrueba`; `AnotarHandler` publica `AnotacionHecha`
  - `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs`, `EscenarioDeEntrega.cs`, `Consultas.cs` — base propia migrada, web sin entrega automática, vuelta a pedido
  - `tests/DraSelina.Consultorio.IntegrationTests/OutboxTests.cs`, `EntregaDeEventosTests.cs`, `ReintentosYOrdenTests.cs`, `WebNoMigraTests.cs` — una prueba por fila de la matriz
  - `scripts/verificar-arranque.sh` — comprueba que `agenda.outbox` existe
  - `README.md` — sección "Eventos entre módulos"
- Revisión (lente `quick`): 8 hallazgos; 4 parches (2 medium, 2 low), 1 diferido (`maybe-false`, sería medium: un handler que nunca termina detiene la entrega), 3 rechazados (low): nombre repetido entre un evento con handlers y otro sin ellos (pide un descuido de nombres y el arreglo cambia `tipo`); un handler que no se puede construir deja sin evento a los demás (error de configuración visible; el arreglo rehace el catálogo); una falla por apagado que no sea cancelación gasta un intento (no reproducido, costo de un minuto).
- Revisión de seguimiento: `true`. Parches por veredicto: high 0, medium 2, low 2. Riesgo sin verificar: los dos parches de severidad media no los vio un revisor: que publicar use el tipo real del evento, y que las pruebas de "Dos instancias" ahora sí hagan correr las dos vueltas a la vez.
- Verificación (corrida por el orquestador después de los parches): `./dotnet.sh build` → 0 advertencias, 0 errores; `./dotnet.sh test` tres veces seguidas → 86 de 86 cada vez, unos 10 segundos por corrida; `scripts/verificar-arranque.sh` contra el AppHost → 0, tres filas, con `agenda.outbox existe`. Las catorce filas de la matriz tienen prueba que corrió y pasó. Ninguna prueba del outbox espera tiempo real: el reloj lo mueve la prueba.
- Para Gerardo:
  - `[ASSUMPTION]` `IEventHandler<TEvento>` vive en `BuildingBlocks.Contracts`. Si se aprueba, falta agregarlo a la fila "Contratos base" de la tabla de capas de la arquitectura.
  - `[ASSUMPTION]` Una vuelta cada 5 segundos; los eventos entregados se quedan en la tabla.
  - `[ASSUMPTION]` Un evento que nadie escucha queda `Entregado` con un aviso.
  - Diferido: límite de tiempo por handler de evento. Conviene decidirlo antes del primer handler que llame a un servicio externo (el correo).
  - `tipo` guarda el nombre corto de la clase del evento: los nombres de evento deben ser únicos en toda la aplicación, y renombrar una clase de evento deja sin handler a las filas ya guardadas con el nombre viejo.
  - Publicar inserta con SQL directo y exige una transacción abierta.
  - Todo handler de evento debe tolerar recibir el mismo evento más de una vez (AD-3 ya pide deduplicar por `EventoId`).
  - Sin índice en `agenda.outbox` y sin limpieza de entregados: la consulta de pendientes recorrerá una tabla que crece. No pesa con el volumen del piloto.
  - Sobre una base sin migrar el servicio registra un error cada 5 segundos.
  - Un id tipado (`CitaId`) dentro de un evento se serializaría como `{"Valor": …}` con la configuración de JSON por defecto; no hay eventos de negocio todavía, así que no está probado.
  - La fila queda apartada, con una transacción abierta, mientras corren sus handlers.
  - No hay pantalla ni alerta para los eventos `Fallido`: solo el registro de errores y la tabla.
- Sin commits: se pregunta a Gerardo antes de confirmar.
