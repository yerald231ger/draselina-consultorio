---
title: 'Un caso de uso recorre su handler, su transacción y Resultado'
type: 'feature' # feature | bugfix | refactor | chore
ticket: '5' # the entry id from the ticket tree, or the story file's stem when the entry has no id; empty outside it
created: '2026-10-07'
status: done
baseline_revision: '9693f7017af0b2e6aab48887b51bf4d3f0244776' # the commit the work starts from, set before any code change
route: 'full' # oneshot | full — set by step-02
route_source: 'auto' # pinned | auto — set with route by step-02
risk: 'high' # low | medium | high — set by step-02; a CI process can read it to decide how much review the change gets
review: 'quick' # none | quick | thorough — set by step-04
review_source: 'pinned' # pinned | auto — set with review by step-04
lenses_ran: ['quick'] # ids of the lenses launched, set by step-04
review_loop_iteration: 0 # incremented by step-04 before each review loopback
followup_review_recommended: false # set by step-04 at Finalize — true if the LLM decided another review pass is worthwhile
context:
  - '{project-root}/_bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md'
  - '{project-root}/README.md'
warnings: ['oversized'] # optional: machine-readable warnings for orchestration, e.g. oversized, multiple-goals
deferred:
  - summary: >-
      Dos comandos del mismo módulo en curso a la vez dentro del mismo ámbito del contenedor chocan: el segundo AbrirAsync lanza InvalidOperationException, y si el primero termina sin confirmar, su limpieza del contexto borra también lo que el otro tenía en seguimiento.
    evidence: |-
      El contexto, el puerto de transacción y los handlers son uno por ámbito (Registro.cs de cada módulo), y en administración (Interactive Server) el ámbito dura lo que el circuito. Por lectura es cierto; no se pudo decidir si ocurre en la práctica porque todavía no hay páginas de administración. Lo zanjaría una página Interactive Server que dispare dos comandos del mismo módulo cuyos await se crucen. La salida habitual es un ámbito por comando o una fábrica de contextos, y eso toca cómo las páginas piden sus handlers (AD-10): decisión de Gerardo antes de la primera pantalla de administración.
      Registrado el 2026-10-16 (entrada 10) en la pregunta abierta de la épica sobre cuánto vive el contexto en las pantallas de administración; se decide ahí.
    location: >-
      src/DraSelina.Consultorio.Web/Modules/*/Infrastructure/TransaccionDe*.cs (AbrirAsync)
    severity: medium (unverified)
---

<intent-contract>

## Intent

**Problem:** La arquitectura ya no usa Wolverine ni `ISender` (decisión de Gerardo, 2026-10-06), pero el código todavía los trae: el `MigrationService` crea el esquema `wolverine`, la solución referencia sus paquetes y `ISender` sigue en los contratos base. Y no existe aún la forma propia de un caso de uso que la reemplaza.

**Approach:** Retirar Wolverine del código y entregar la forma de un caso de uso según AD-10: interfaces `ICommandHandler` e `IQueryHandler` propias, que quien llama pide directamente al contenedor; cada módulo registra sus handlers; el handler de un comando abre, guarda y confirma su transacción por un puerto de su módulo. Se demuestra con un comando y una consulta de prueba que viven solo en el proyecto de pruebas de integración.

## Boundaries & Constraints

**Always:**
- Las interfaces de handler viven en `BuildingBlocks.Contracts`, sin paquetes: `ICommandHandler<TComando, T>` (con `TComando : ICommand<T>`) e `IQueryHandler<TConsulta, T>` (con `TConsulta : IQuery<T>`), un método asíncrono que recibe el mensaje y un `CancellationToken` y devuelve `Resultado<T>`. `[ASSUMPTION]` El método se llama `HandleAsync`, como en Cypma.Cop.
- Quien llama pide el handler al contenedor por su interfaz. Las pruebas lo piden al contenedor de la web en memoria, dentro de un ámbito; nunca crean la clase del handler.
- Cada módulo registra sus handlers en su `Registro` de `Infrastructure`. El host no descubre handlers por reflexión ni por nombre.
- Un fallo esperado es `Resultado.Fallo(codigo)` con un código de un conjunto cerrado (constantes junto al comando); nunca excepción ni texto libre.
- El handler de un comando abre la transacción de su módulo por un puerto, guarda de forma explícita y la confirma. Toda salida con un `Resultado` no exitoso deshace antes de devolver. Lo que no se confirma no queda: si el handler lanza o termina sin confirmar, la transacción se deshace al soltarla. Quien llama nunca abre, confirma ni guarda.
- El handler llega a la base solo por puertos (AD-4): no recibe un `DbContext`.
- Una consulta corre sin transacción.
- `[ASSUMPTION]` El puerto de transacción es de cada módulo y lo declara el propio módulo en su namespace `Features`, junto con el tipo de la transacción abierta; lo implementa su `Infrastructure` sobre su `DbContext`. No hay tipo compartido entre módulos: así no cambia ninguna tabla de la arquitectura. Esta entrada lo entrega para Agenda, Pacientes y Notificaciones, y para los tipos de prueba.
- Sobre una base sin migrar la web arranca, atiende `GET /` y no crea nada (AD-25). El `MigrationService` sigue migrando los tres módulos y ya no crea el esquema `wolverine`.
- Los tipos de prueba viven en `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/` y sus tablas en el esquema `pruebas`; el arnés los da de alta en la web en memoria como se da de alta un módulo.
- Las pruebas de arquitectura y del Kernel siguen pasando; build con 0 advertencias.

**Never:**
- Ningún paquete `WolverineFx.*`, `JasperFx.*` ni librería de despacho o mediador. `ISender` deja de existir.
- Outbox, eventos de integración o servicios en segundo plano (entrada 11). Decoradores, Scrutor o validación (entrada 6). Actor o autorización (entrada 7).
- Comandos, consultas, handlers o tablas de prueba en `src/`. Ninguna página ni componente nuevo.
- Tablas o migraciones nuevas en `agenda`, `pacientes` o `notificaciones`.
- Cambiar `ICommand<T>`, `IQuery<T>`, `IRequiereAdministrador` o `Resultado`.
- Una comprobación al arrancar de "un comando, un handler": no está decidida; se reporta lo que hace el contenedor tal cual.
- Tocar `spikes/`, la arquitectura, las épicas, `tickets.toml` o `change-quitar-wolverine/`. Sin commits.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Comando con éxito | Comando de prueba con un valor propio, pedido su handler al contenedor | `Resultado` exitoso con su valor; la fila está en `pruebas` | — |
| Fallo antes de cambiar | El mismo comando en la condición que su handler rechaza de entrada | `Forma` `Fallo` con el código del conjunto cerrado; ninguna excepción; no hay fila | — |
| Fallo después de cambiar | Comando cuyo handler guarda y después descubre el fallo | `Forma` `Fallo` con su código; ninguna excepción; no hay fila | — |
| Fallo inesperado | Comando cuyo handler guarda y después lanza | La excepción sale a quien llamó; no hay fila | La prueba lo afirma |
| Sin confirmar | Comando cuyo handler guarda, no confirma y devuelve éxito | `Resultado` exitoso; no hay fila | — |
| Consulta | Consulta de prueba sobre una fila ya guardada | `Resultado` exitoso con el dato leído; el handler no vio ninguna transacción abierta | — |
| Sin handler | Se pide al contenedor el handler de un comando que no tiene | Excepción que nombra el tipo del comando | La prueba lo afirma |
| Transacción de cada módulo | Por el puerto de Agenda, de Pacientes y de Notificaciones: abrir y confirmar; abrir y deshacer | Ambas terminan sin error contra la base migrada | — |
| Base sin migrar | La web real contra una base vacía creada aparte; `GET /` | Responde 200; la base sigue sin los esquemas `agenda`, `pacientes`, `notificaciones` y sin ningún historial de migraciones | — |
| Base migrada | La base de la corrida, migrada por el `MigrationService` | No existe el esquema `wolverine`; los tres historiales de migraciones siguen en su esquema | — |

</intent-contract>

## Code Map

- `src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/` -- `ICommand.cs`, `IQuery.cs` (marcadores genéricos, no cambiar), `IRequiereAdministrador.cs` (no cambiar), `ISender.cs` (se borra). Aquí van las dos interfaces de handler. Capa pura: solo `System.*` y `Kernel`
- `src/DraSelina.Consultorio.Web/Kernel/Resultado.cs` -- `Resultado.Exito(valor)`, `Resultado.Fallo(codigo)`, `.Como<T>()`, conversión implícita de `T` a `Resultado<T>`. No cambiar
- `src/DraSelina.Consultorio.Web/Modules/{Agenda,Pacientes,Notificaciones}/Infrastructure/` -- `*DbContext.cs` (constante `Esquema`, método `Configurar`) y `Registro.cs` (`AddAgenda(cadena)`…, hoy solo `AddDbContext`). El registro de handlers y la implementación del puerto de transacción van aquí
- `src/DraSelina.Consultorio.Web/Modules/*/Features/` -- hoy solo `.gitkeep`; namespace `DraSelina.Consultorio.<Módulo>.Features`. `Features` no puede usar `Infrastructure`; `Infrastructure` sí puede usar `Features`
- `src/DraSelina.Consultorio.Web/AlmacenamientoDeWolverine.cs` -- se borra
- `src/DraSelina.Consultorio.Web/DraSelina.Consultorio.Web.csproj` y `Directory.Packages.props` -- salen `WolverineFx` y `WolverineFx.Postgresql`. El `.csproj` tiene `<Using Remove="Microsoft.*" />`: todo `using` se escribe
- `src/DraSelina.Consultorio.Web/Program.cs` -- no usa Wolverine; no necesita cambios salvo que el registro de módulos cambie de firma
- `src/DraSelina.Consultorio.MigrationService/Program.cs` -- quitar `using Wolverine`, el `UseWolverine` y el bloque que crea el almacenamiento; el resto (tres `MigrateAsync`, códigos de salida 0 y 1, registros) no cambia. Su `.csproj` y `src/DraSelina.Consultorio.AppHost/AppHost.cs` lo nombran en un comentario
- `scripts/verificar-arranque.sh` -- fila "Almacenamiento de Wolverine" (líneas 95 a 101) y el comentario de cabecera: se quita la fila; quedan tres
- `tests/DraSelina.Consultorio.ArchitectureTests/Reglas.cs` -- las reglas por carpeta; un archivo nuevo en `Features` o `Infrastructure` debe llevar el namespace de su capa. `VerificadorTests.cs` usa `using Wolverine;` como texto de ejemplo dentro de casos de prueba: es un dato, puede cambiarse por otro paquete o quedarse
- `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs` -- `ArrancarWeb()`/`ArrancarWeb(cadena)` con la subclase privada `Web`; `CrearBaseVaciaAsync()`; `CrearContextoDePruebas()`. Aquí se dan de alta los tipos de prueba en la web en memoria
- `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/` -- `PruebasDbContext.cs` (esquema `pruebas`, tablas creadas por el arnés con `CreateTablesAsync`) y `Apunte.cs`
- `tests/DraSelina.Consultorio.IntegrationTests/BaseMigradaTests.cs` -- la prueba `El_almacenamiento_de_Wolverine_esta_en_su_esquema` pasa a la fila "Base migrada"
- `tests/DraSelina.Consultorio.IntegrationTests/WebNoMigraTests.cs` -- ya es la prueba original (200 y nada creado); solo deja de nombrar `AlmacenamientoDeWolverine.Esquema`
- `README.md` -- nombra el esquema `wolverine` en "El arnés de integración", "Arrancar" y "Verificar el arranque" (cuatro filas)
- `git show b2e3c10` -- el intento sobre Wolverine, revertido. Sirve de referencia para la forma de los tipos de prueba (`Anotar` con un desenlace por caso, `LeerAnotacion`, el puerto `IAnotaciones`, `Pruebas/Registro.cs` con `AddPruebas`) y de `CasoDeUsoTests`. No copiar nada que nombre Wolverine, `ISender` o `IMessageBus`

## Tasks & Acceptance

**Execution:**
- [x] `src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/` -- agregar `ICommandHandler` e `IQueryHandler`; borrar `ISender.cs` -- AD-10
- [x] `Directory.Packages.props`, `src/DraSelina.Consultorio.Web/DraSelina.Consultorio.Web.csproj`, `src/DraSelina.Consultorio.Web/AlmacenamientoDeWolverine.cs` -- quitar los paquetes y el archivo -- se quita Wolverine
- [x] `src/DraSelina.Consultorio.MigrationService/Program.cs`, su `.csproj`, `src/DraSelina.Consultorio.AppHost/AppHost.cs` -- el `MigrationService` deja de crear el esquema `wolverine`; comentarios al día -- AD-2, AD-25
- [x] `src/DraSelina.Consultorio.Web/Modules/{Agenda,Pacientes,Notificaciones}/{Features,Infrastructure}/` -- puerto de transacción de cada módulo, su implementación sobre su contexto y su registro -- Consistency Conventions, Transacciones
- [x] `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/` y `Arnes.cs` -- comandos, consulta, handlers, puertos, entidad, códigos y registro de prueba, dados de alta en la web en memoria -- lo pide la entrada
- [x] `tests/DraSelina.Consultorio.IntegrationTests/` (pruebas) -- una prueba por fila de la matriz; `BaseMigradaTests` y `WebNoMigraTests` sin Wolverine -- P6, AD-25
- [x] `scripts/verificar-arranque.sh` -- quitar la fila de Wolverine -- ya no hay ese esquema
- [x] `README.md` -- quitar Wolverine de arnés, arranque y verificación; sección nueva "Casos de uso": forma del comando, la consulta y su handler, cómo se pide un handler, cómo registra un módulo los suyos, la regla de la transacción con un ejemplo (abrir, guardar, deshacer en el fallo, confirmar), y cómo se escribe un comando de prueba -- la épica pide que la épica 2 no decida esto

**Acceptance Criteria:**
- Given Docker en ejecución y ningún AppHost arriba, when se corre `./dotnet.sh test`, then pasan todas las pruebas de la solución.
- Given el repositorio, when se busca `Wolverine`, `JasperFx` o `ISender` en `src/`, `tests/`, `scripts/`, `Directory.Packages.props` y `README.md`, then no aparece, salvo como texto de ejemplo en `VerificadorTests.cs` si se dejó.
- Given `src/`, when se buscan los tipos de prueba, then no hay ninguno.
- Given el AppHost levantado, when se corre `scripts/verificar-arranque.sh`, then sale con 0 y la base no tiene el esquema `wolverine`.

## Implementation Notes

- **Interfaces.** `ICommandHandler<in TComando, T>` e `IQueryHandler<in TConsulta, T>` en `BuildingBlocks/Contracts/`, con un solo método `HandleAsync(mensaje, CancellationToken)` que devuelve `Task<Resultado<T>>`. `ISender.cs` borrado.
- **Puerto de transacción por módulo.** `Features/ITransaccionDe<Módulo>.cs` declara `ITransaccionDe<Módulo>` (`AbrirAsync`) e `ITransaccionAbiertaDe<Módulo>` (`ConfirmarAsync`, `DeshacerAsync`, `IAsyncDisposable`). El módulo va en el nombre para que un archivo que vea dos módulos no tenga nombres iguales. `Infrastructure/TransaccionDe<Módulo>.cs` lo implementa sobre el contexto del módulo y `Registro.cs` lo registra; la firma de `Add<Módulo>(cadena)` no cambió.
- **Deshacer también limpia el contexto** (no estaba en el plan). Al deshacer, o al soltar sin confirmar, se llama a `db.ChangeTracker.Clear()`, en un `finally`. Sin eso, en un ámbito que dura lo que el circuito, el siguiente comando guardaría lo que uno no confirmado había agregado. Lo cubre `Un_comando_que_no_confirma_no_estorba_al_siguiente_del_mismo_ambito`; el constructor comprobó que falla si se quita la línea.
- **El "fallo antes de cambiar"** abre la transacción primero y la deshace al devolver el código: la regla "toda salida con fallo deshace" queda literal.
- **Retiro de Wolverine.** Salen los dos paquetes, `AlmacenamientoDeWolverine.cs`, el `UseWolverine` y la creación del almacenamiento en el `MigrationService`, la fila del script de arranque (quedan tres) y los textos de ejemplo `using Wolverine;` de `VerificadorTests.cs`.
- **Sin comprobación de "un comando, un handler".** Pedir un handler no registrado lanza la excepción del contenedor, que nombra el tipo; dos registros para el mismo comando no se detectan (gana el último).
- Quedaron fuera los `.gitkeep` de `Features` de los tres módulos, que ya tienen un archivo real.

## Plan Change Log

## Review Triage Log

### 2026-10-07 — Review pass
- verdicts: 8 findings — high 0, medium 1, low 6, false 0, maybe-false 1
- findings:
  - `[low]` `[patch]` `TransaccionDe*.cs`, `Abierta.DisposeAsync`: el contexto solo se limpiaba si soltar la transacción no lanzaba; con la conexión rota lo no confirmado seguía en seguimiento y lo guardaría el siguiente comando del ámbito — la limpieza pasó a un `finally`, en los tres módulos y en el de prueba.
  - `[maybe-false]` `[defer]` `TransaccionDe*.cs`, `AbrirAsync`: dos comandos del mismo módulo en curso a la vez en el mismo ámbito chocan (el segundo `BeginTransactionAsync` lanza, y la limpieza de uno borra el seguimiento del otro) — cierto por lectura; no se pudo decidir si ocurre, porque aún no hay páginas de administración. Lo zanjaría una página Interactive Server con dos comandos que se crucen. Si fuera cierto sería `medium`. Diferido en el frontmatter, para decidir antes de la primera pantalla de administración.
  - `[low]` `[patch]` `Abierta` solo recuerda si se confirmó: confirmar después de deshacer, o llamar dos veces, lanza la excepción del proveedor, y los comentarios no decían que son excluyentes — falla de forma visible ante un mal uso; se corrigieron los comentarios del puerto para decirlo. Sin cambio de comportamiento.
  - `[medium]` `[patch]` README, "Casos de uso": el ejemplo que la épica 2 va a copiar mezclaba tipos de prueba con tipos de producción, mostraba un `AnotarHandler(ITransaccionDeAgenda, IAnotaciones)` que no existe y una forma contraria a AD-4 (entidad con setters públicos, puerto junto al handler) — reescrito: todos los ejemplos son el código real de `tests/.../Pruebas/`, rotulado, y cada parte termina con una nota "En un módulo" que dice qué cambia (comando y códigos en `Contracts`, handler en `Features`, `ITransaccionDe<Módulo>`, puerto de escritura del agregado en `Domain`).
  - `[low]` `[reject]` `CasoDeUsoTests`, consulta "sin transacción": la afirmación solo ve transacciones abiertas en ese mismo `PruebasDbContext` — cierto, pero en esta solución una transacción solo se abre por el puerto, sobre ese contexto; no hay otro camino que la prueba deje sin ver.
  - `[low]` `[patch]` `BaseMigradaTests`: afirmaba la lista exacta de esquemas de la base que comparten todas las pruebas, así que dependía del orden — ahora es `La_base_migrada_no_tiene_el_esquema_wolverine` y afirma solo eso, que es lo que pide la matriz.
  - `[low]` `[patch]` `.gitkeep` sobrantes en `Features` de los tres módulos — borrados.
  - `[low]` `[patch]` Comentario del `.csproj` del `MigrationService` mal cortado — reacomodado.

## Design Notes

- **La transacción, vista desde un handler.** El objetivo es que se lea de corrido, como en Cypma.Cop pero por un puerto:

  ```csharp
  await using var tx = await transaccion.AbrirAsync(ct);
  anotaciones.Agregar(nueva);
  await anotaciones.GuardarAsync(ct);
  if (/* fallo descubierto al guardar */)
  {
      await tx.DeshacerAsync(ct);
      return Resultado.Fallo(Codigos.Rechazada).Como<Guid>();
  }
  await tx.ConfirmarAsync(ct);
  return nueva.Id;
  ```
- **Por qué una transacción explícita que se suelta con `await using`.** En administración (Interactive Server) el ámbito del contenedor dura lo que el circuito: si la transacción solo se deshiciera al terminar el ámbito, un handler que lanza la dejaría abierta. Soltarla al salir del handler la deshace de inmediato.
- **Por qué el puerto se repite en cada módulo.** Un tipo compartido tendría que vivir en `Kernel` o en `BuildingBlocks.Contracts`, y eso cambia una tabla de la arquitectura, que solo cambia Gerardo. Son pocas líneas por módulo; si después se comparte, es un cambio mecánico.
- **"Sin handler".** No se agrega nada: se afirma lo que el contenedor ya hace al pedirle un servicio que no está registrado.

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 errores, 0 advertencias
- `./dotnet.sh test` -- expected: todas las pruebas pasan, con Docker arriba y sin AppHost
- `grep -rnE 'Wolverine|JasperFx|ISender' src tests scripts Directory.Packages.props README.md --include='*' | grep -v '/bin/\|/obj/'` -- expected: vacío, o solo el texto de ejemplo de `VerificadorTests.cs`
- `./dotnet.sh run --project src/DraSelina.Consultorio.AppHost` y, en otra terminal, `scripts/verificar-arranque.sh` -- expected: sale con 0

## Auto Run Result

- Status: built
- Resumen: Wolverine e `ISender` salieron del código. Un caso de uso es ahora un `ICommand<T>` o `IQuery<T>` con un handler que implementa `ICommandHandler` o `IQueryHandler` y que quien llama pide directamente al contenedor; cada módulo registra los suyos. El handler de un comando abre la transacción de su módulo por un puerto, guarda de forma explícita y la confirma; toda salida con fallo deshace, y lo que no se confirma no queda. Agenda, Pacientes y Notificaciones ya tienen su puerto de transacción. El `MigrationService` ya no crea el esquema `wolverine` y la web arranca sobre una base sin migrar sin crear nada.
- Archivos:
  - `src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/ICommandHandler.cs`, `IQueryHandler.cs` — las interfaces de handler; `ISender.cs` borrado
  - `src/DraSelina.Consultorio.Web/Modules/{Agenda,Pacientes,Notificaciones}/Features/ITransaccionDe*.cs` — el puerto de transacción de cada módulo
  - `src/DraSelina.Consultorio.Web/Modules/{Agenda,Pacientes,Notificaciones}/Infrastructure/TransaccionDe*.cs`, `Registro.cs` — su implementación y su registro
  - `src/DraSelina.Consultorio.Web/AlmacenamientoDeWolverine.cs` (borrado), `DraSelina.Consultorio.Web.csproj`, `Directory.Packages.props` — sin Wolverine
  - `src/DraSelina.Consultorio.MigrationService/Program.cs`, su `.csproj`, `src/DraSelina.Consultorio.AppHost/AppHost.cs`, `src/DraSelina.Consultorio.Web/Program.cs` — sin el esquema `wolverine`; comentarios al día
  - `scripts/verificar-arranque.sh` — tres filas
  - `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/` — `Anotar`, `LeerAnotacion`, `Anotacion`, `IAnotaciones`, `ITransaccionDePruebas`, `Registro` (`AddPruebas`)
  - `tests/DraSelina.Consultorio.IntegrationTests/CasoDeUsoTests.cs`, `TransaccionDeModuloTests.cs`, `BaseMigradaTests.cs`, `WebNoMigraTests.cs`, `Arnes.cs`, `Consultas.cs` — una prueba por fila de la matriz
  - `tests/DraSelina.Consultorio.ArchitectureTests/VerificadorTests.cs` — los textos de ejemplo ya no nombran Wolverine
  - `README.md` — sin Wolverine; sección "Casos de uso"
- Revisión (lente `quick`): 8 hallazgos; 6 parches (1 medium, 5 low), 1 diferido (`maybe-false`, sería medium: dos comandos a la vez en el mismo ámbito), 1 rechazado (low: la prueba de consulta sin transacción solo ve transacciones de su contexto, que es el único camino por el que se abren).
- Revisión de seguimiento: `false`. Parches por veredicto: high 0, medium 1, low 5.
- Verificación (corrida por el orquestador después de los parches): `./dotnet.sh build` → 0 advertencias, 0 errores; `./dotnet.sh test` → 65 de 65 (18 Kernel, 24 arquitectura, 23 integración); la búsqueda de `Wolverine|JasperFx|ISender` en `src`, `tests`, `scripts`, `Directory.Packages.props` y `README.md` → vacía; `scripts/verificar-arranque.sh` contra el AppHost → 0, tres filas pasan. Las diez filas de la matriz tienen prueba que corrió y pasó.
- Para Gerardo:
  - `[ASSUMPTION]` El método del handler se llama `HandleAsync`.
  - `[ASSUMPTION]` El puerto de transacción se repite en cada módulo (`ITransaccionDe<Módulo>`, `ITransaccionAbiertaDe<Módulo>`), sin tipo compartido, para no tocar las tablas de la arquitectura. Las tres implementaciones son copias de unas 45 líneas.
  - Deshacer limpia el contexto del módulo (`ChangeTracker.Clear()`): lo agregó el constructor y no estaba en el plan.
  - Diferido: dos comandos del mismo módulo a la vez en el mismo ámbito. Conviene decidir cuánto vive el contexto en administración antes de la primera pantalla Interactive Server.
  - No hay comprobación al arrancar de "un comando, un handler".
  - La regla "toda salida con fallo deshace antes de devolver" depende de cada handler; solo la red de seguridad (lo no confirmado no queda) es automática.
- Sin commits: se pregunta a Gerardo antes de confirmar.
- Decisiones de Gerardo después de la corrida (2026-10-10), registradas en las Notes de la épica:
  - `HandleAsync` aprobado; deja de ser supuesto.
  - El puerto de transacción se queda repetido por módulo; deja de ser supuesto.
  - La limpieza del contexto al deshacer se queda.
  - El diferido (dos comandos a la vez en el mismo ámbito) se decide con la primera pantalla de administración; quedó como Open question en la épica.
  - "Un comando, un handler" se hará cumplir con una prueba de integración, en la entrada 6; sin comprobación al arrancar.
