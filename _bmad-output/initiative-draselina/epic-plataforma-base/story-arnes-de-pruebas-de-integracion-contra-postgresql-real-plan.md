---
title: 'Arnés de pruebas de integración contra PostgreSQL real'
type: 'feature' # feature | bugfix | refactor | chore
ticket: '4' # the entry id from the ticket tree, or the story file's stem when the entry has no id; empty outside it
created: '2026-10-06'
status: done
baseline_revision: 'a6d6a9c64f01a084f6eff72a8f8ee649bb439ed3' # the commit the work starts from, set before any code change
route: 'full' # oneshot | full — set by step-02
route_source: 'auto' # pinned | auto — set with route by step-02
risk: 'medium' # low | medium | high — set by step-02; a CI process can read it to decide how much review the change gets
review: 'quick' # none | quick | thorough — set by step-04
review_source: 'pinned' # pinned | auto — set with review by step-04
lenses_ran: ['quick'] # ids of the lenses launched, set by step-04
review_loop_iteration: 0 # incremented by step-04 before each review loopback
followup_review_recommended: false # set by step-04 at Finalize — true if the LLM decided another review pass is worthwhile
context:
  - '{project-root}/_bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md'
  - '{project-root}/README.md'
warnings: ['oversized'] # optional: machine-readable warnings for orchestration, e.g. oversized, multiple-goals
deferred: [] # append-only machine-readable deferred review findings; each item carries summary/evidence and optional location/severity
---

<intent-contract>

## Intent

**Problem:** No hay forma automática de probar algo contra PostgreSQL real: hoy el historial de migraciones por esquema solo lo comprueba `scripts/verificar-arranque.sh`, a mano y con el AppHost arriba. Las entradas 5 a 7 necesitan una base migrada y un lugar para sus tipos de prueba.

**Approach:** Un proyecto de pruebas de integración con un arnés que, una vez por corrida, levanta PostgreSQL en un contenedor, lo migra ejecutando el `MigrationService` real y crea un esquema propio para los tipos de prueba. Sobre él, pruebas que confirman el historial por esquema y que la web no migra al arrancar. La elección de herramienta se registra como Decision en la épica.

## Boundaries & Constraints

**Always:**
- La base de cada corrida se migra ejecutando el punto de entrada real del `MigrationService` (su `Program`), no una copia de sus pasos. Si sale con código distinto de cero, el arnés falla y muestra su salida.
- PostgreSQL real, misma versión mayor que el AppHost (imagen `postgres:18.3`). Una base por corrida, compartida por todas las pruebas de integración.
- Los tipos de prueba viven en el proyecto de pruebas y sus tablas en el esquema `pruebas`, que crea el arnés después de migrar. Ningún esquema de producción (`agenda`, `pacientes`, `notificaciones`, `wolverine`) recibe tablas de prueba.
- El arnés entrega a las pruebas la cadena de conexión de la base migrada y una forma de arrancar la web en memoria contra la base que la prueba indique.
- Versiones de la tabla Stack: `Testcontainers.PostgreSql` 4.15.0, `xunit.v3` 4.0.1. `[ASSUMPTION]` `Microsoft.AspNetCore.Mvc.Testing` cuenta como parte de ASP.NET Core 11.
- Sin Docker en ejecución, las pruebas de integración fallan con un mensaje que lo dice; no se omiten.
- Las pruebas de arquitectura y del Kernel siguen pasando; build con 0 advertencias.

**Never:**
- `ISender`, handlers, middleware o `UseWolverine` en la web (entradas 5 a 7); el paquete `WolverineFx.EntityFrameworkCore`.
- Migrar desde el arnés con `MigrateAsync` propio, `EnsureCreated` sobre los contextos de producción, o SQL que imite al `MigrationService`.
- Cambiar el comportamiento del `MigrationService`, del AppHost o de la web. Si hace falta hacer alcanzable un punto de entrada, es el único cambio permitido en `src/`.
- Tocar `spikes/`, la arquitectura, `tickets.toml` o `scripts/verificar-arranque.sh`. De la épica solo se tocan sus `## Notes`. Sin commits.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Historial por esquema | Base de la corrida, recién migrada | `__EFMigrationsHistory` existe en `agenda`, `pacientes` y `notificaciones`, cada uno con al menos una migración; ninguno en `public` | — |
| Almacenamiento de Wolverine | Base de la corrida | El esquema `wolverine` existe con tablas | — |
| La web no migra | Una base vacía, creada aparte en el mismo servidor; la web arranca contra ella y atiende `GET /` | Responde 200 y la base sigue sin los esquemas `agenda`, `pacientes`, `notificaciones`, `wolverine` y sin ninguna tabla `__EFMigrationsHistory` | — |
| Esquema de pruebas | Un tipo de prueba mínimo con su `DbContext` en el esquema `pruebas` | Se guarda una fila y se lee de vuelta; su tabla está en `pruebas` y en ningún otro esquema | — |
| Migración fallida | El `MigrationService` ejecutado con una cadena de conexión que no sirve | El arnés lanza, con el código de salida y la salida del proceso en el mensaje | La prueba lo afirma |

</intent-contract>

## Code Map

- `src/DraSelina.Consultorio.MigrationService/Program.cs` -- instrucciones de nivel superior; lee `ConnectionStrings:consultorio` (variable `ConnectionStrings__consultorio`), migra los tres contextos, crea el almacenamiento de Wolverine, devuelve 0 o 1. Es "el mismo camino". No cambiar su comportamiento
- `src/DraSelina.Consultorio.Web/Program.cs` -- instrucciones de nivel superior; `BaseDeDatos.CadenaDeConexion` lanza si falta la cadena. Hoy no llama a `UseWolverine`
- `src/DraSelina.Consultorio.Web/BaseDeDatos.cs` -- `BaseDeDatos.Nombre = "consultorio"`: nombre de la cadena de conexión
- `src/DraSelina.Consultorio.Web/AlmacenamientoDeWolverine.cs` -- `Esquema = "wolverine"`
- `src/DraSelina.Consultorio.Web/Modules/*/Infrastructure/*DbContext.cs` -- constante `Esquema` de cada módulo; reutilizarlas en las afirmaciones
- `tests/DraSelina.Consultorio.Kernel.Tests/*.csproj` -- forma de un proyecto de pruebas aquí: `OutputType` `Exe`, `xunit.v3`, `<Using Include="Xunit" />`, Microsoft.Testing.Platform (`global.json`)
- `tests/DraSelina.Consultorio.ArchitectureTests/Reglas.cs` -- la tabla de referencias cubre solo `src/`; un proyecto en `tests/` no entra en ella
- `DraSelina.Consultorio.slnx` -- carpeta `/tests/`: aquí se agrega el proyecto nuevo
- `Directory.Packages.props` -- versiones centrales; `Directory.Build.props` -- advertencias como errores
- `scripts/verificar-arranque.sh` -- las consultas SQL de las filas 2 y 3 son el modelo de las afirmaciones
- `dotnet.sh` -- todo se corre con esta envoltura; exporta `DOTNET_ROOT`, que hereda un proceso hijo
- `_bmad-output/initiative-draselina/epic-plataforma-base/epic-plataforma-base.md` -- `## Notes`: la línea "Open question: Testcontainers o el soporte de pruebas de Aspire…"

Dos `Program` de nivel superior (Web y MigrationService) chocan si un mismo proyecto referencia los dos ensamblados: resolverlo sin renombrar nada en `src/` (por ejemplo, ejecutar el `MigrationService` como proceso, o un alias de referencia).

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Packages.props` -- agregar `Testcontainers.PostgreSql` 4.15.0 y `Microsoft.AspNetCore.Mvc.Testing` (la versión que corresponde al runtime 11 instalado) -- Stack
- [x] `tests/DraSelina.Consultorio.IntegrationTests/DraSelina.Consultorio.IntegrationTests.csproj` y `DraSelina.Consultorio.slnx` -- proyecto nuevo con la forma de `Kernel.Tests`, agregado a la solución -- P5
- [x] `tests/DraSelina.Consultorio.IntegrationTests/` (arnés) -- una vez por corrida: contenedor de PostgreSQL, base migrada ejecutando el `MigrationService`, esquema `pruebas` creado; expone la cadena de conexión, la creación de una base vacía aparte y el arranque de la web en memoria contra una cadena dada; apaga el contenedor al terminar -- P5
- [x] `tests/DraSelina.Consultorio.IntegrationTests/` (tipo de prueba) -- una entidad mínima y su `DbContext` en el esquema `pruebas`, cuyas tablas crea el arnés -- lugar donde las entradas 5 a 7 pondrán los suyos
- [x] `tests/DraSelina.Consultorio.IntegrationTests/` (pruebas) -- una prueba por cada fila de la matriz -- P4, P5
- [x] `README.md` -- sección de pruebas: `./dotnet.sh test` ahora necesita Docker; qué da el arnés y cómo escribir una prueba de integración con él
- [x] `_bmad-output/initiative-draselina/epic-plataforma-base/epic-plataforma-base.md` -- en `## Notes`: marcar resuelta la Open question y agregar una línea `Decision:` con la elección (Testcontainers), su razón y la marca `[ASSUMPTION]` de que la tomó la entrada 4 y espera el visto bueno de Gerardo -- lo pide la entrada

**Acceptance Criteria:**
- Given Docker en ejecución y ningún AppHost arriba, when se corre `./dotnet.sh test`, then pasan todas las pruebas de la solución, incluidas las de integración, sin ningún otro comando.
- Given una corrida terminada, when se listan los contenedores de Docker, then no queda ninguno creado por la corrida.
- Given la épica, when se leen sus `## Notes`, then la elección entre Testcontainers y el soporte de pruebas de Aspire está como línea `Decision:` con su razón.
- Given el README, when alguien quiere escribir una prueba de integración nueva, then encuentra ahí cómo obtener la base migrada y dónde poner un tipo de prueba.

## Implementation Notes

- El `MigrationService` se ejecuta como proceso (`dotnet <dll>` con `ConnectionStrings__consultorio`), no se referencia su ensamblado: corre su `Program` real y no choca con el `Program` de Web. El `.csproj` de pruebas lo referencia con `ReferenceOutputAssembly="false"` y deja la ruta de su `.dll` como metadato del ensamblado.
- El arnés es un `AssemblyFixture` de xUnit v3: uno por corrida, y cada clase de prueba lo recibe por su constructor.
- Las tablas del esquema `pruebas` se crean con `IRelationalDatabaseCreator.CreateTablesAsync` sobre `PruebasDbContext`, sin migraciones.
- `Microsoft.AspNetCore.Mvc.Testing` quedó en 11.0.0-rc.1.26425.128, la versión del runtime de `.dotnet/`.
- Nada cambió en `src/`: el `Program` de Web ya es alcanzable desde el proyecto de pruebas.
- Sorpresa: el contenedor de limpieza de Testcontainers (`testcontainers-ryuk-*`) sigue listado unos 10 segundos después de la corrida y se borra solo.

## Plan Change Log

## Review Triage Log

### 2026-10-06 — Review pass
- verdicts: 5 findings — high 0, medium 0, low 4, false 0, maybe-false 1
- findings:
  - `[low]` `[patch]` Justo al terminar la corrida, `docker ps -a --filter label=org.testcontainers=true` todavía lista `testcontainers-ryuk-*` (unos 10 s), y el README decía solo "borra el contenedor" — confirmado por el orquestador (a los 3 s estaba, a los 20 s no). El README ahora dice que el contenedor de PostgreSQL se borra al terminar y que el de limpieza desaparece solo unos segundos después.
  - `[low]` `[patch]` `Arnes.cs`, `InitializeAsync`: cualquier fallo al construir o arrancar el contenedor se reportaba como "arranca Docker", aunque Docker estuviera arriba (imagen sin poder bajarse, espera agotada, puerto ocupado) — el mensaje ahora nombra Docker como lo primero a comprobar sin afirmarlo como causa; conserva el detalle y la excepción interna.
  - `[maybe-false]` `[reject]` "Sin Docker las pruebas fallan con un mensaje que lo dice; no se omiten" no se ejecutó nunca y el README lo afirma — no se pudo decidir sin detener Docker, que tiene otros contenedores en uso (`ollama`, `pgvector`). Lo zanjaría una corrida de `./dotnet.sh test` con Docker detenido. Por lectura, construir y arrancar están dentro del `try`, y el revisor observó con otro fallo forzado que una excepción del arnés hace fallar todas las pruebas, no omitirlas. Si fuera cierto sería `low` (el texto de un mensaje); queda declarado como sin verificar.
  - `[low]` `[reject]` `MigracionFallidaTests` afirma sobre `MigrationService.EjecutarAsync` y no sobre `Arnes.InitializeAsync`; la rama del límite de 3 minutos no tiene prueba — `InitializeAsync` llama a `EjecutarAsync` sin `catch`, así que la excepción sale tal cual. Probarlo por el arnés entero exigiría un segundo arnés contra un servidor que falle, y la rama del límite una migración colgada: complejidad nueva para un caso que no se encuentra en el uso diario.
  - `[low]` `[patch]` `Arnes.cs`, `ArrancarWeb(string)`: `new WebApplicationFactory<Program>().WithWebHostBuilder(...)` devolvía la fábrica derivada y dejaba la raíz sin desechar, una por llamada — ahora devuelve una sola fábrica (subclase privada `Web` que aplica la cadena en `ConfigureWebHost`); la firma pública no cambió.

## Design Notes

- **Testcontainers y no el soporte de pruebas de Aspire** `[ASSUMPTION]`. La épica deja la elección a esta historia. Testcontainers ya está en la tabla Stack (4.15.0) y es lo que traía el plan. El soporte de pruebas de Aspire arrancaría el AppHost entero con su orquestador en cada corrida, y la entrada 3 mostró que leer el código de salida de `migraciones` desde fuera exige `kubectl`. Con Testcontainers el mismo camino se conserva ejecutando el `MigrationService` contra el contenedor.
- **Por qué una base vacía para "la web no migra".** Contra la base ya migrada no habría nada pendiente y la prueba pasaría aunque la web migrara. Contra una base vacía, cualquier creación de esquema o tabla al arrancar queda a la vista. La prueba pasa hoy sin esfuerzo porque la web no cablea Wolverine; existe para que la entrada 5 no pueda romper AD-25 sin que falle.
- `[ASSUMPTION]` El esquema de los tipos de prueba se llama `pruebas`.
- `[ASSUMPTION]` Una base por corrida, compartida: las pruebas no dependen del orden ni de datos de otra prueba; cada una crea lo que necesita con valores propios.

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 errores, 0 advertencias
- `./dotnet.sh test` -- expected: todas las pruebas pasan, con Docker arriba y sin AppHost
- `docker ps -a --filter label=org.testcontainers=true` -- expected: vacío después de la corrida

**Manual checks (if no CLI):**
- Con Docker detenido, las pruebas de integración fallan con un mensaje que nombra a Docker (solo si detener Docker no interrumpe otro trabajo; si no se comprueba, se declara).

## Auto Run Result

- Status: built
- Resumen: proyecto nuevo `tests/DraSelina.Consultorio.IntegrationTests` con un arnés que, una vez por corrida, levanta `postgres:18.3` con Testcontainers, migra la base ejecutando el `MigrationService` real como proceso y crea el esquema `pruebas` para los tipos de prueba. Nueve pruebas confirman el historial de migraciones por esquema (y ninguno en `public`), el almacenamiento de Wolverine, que la web arranca contra una base vacía sin crear nada, que un tipo de prueba vive solo en `pruebas`, y que una migración fallida lanza con el código de salida y la salida del proceso. La elección de Testcontainers quedó como `Decision:` en las Notes de la épica.
- Archivos:
  - `Directory.Packages.props` — `Testcontainers.PostgreSql` 4.15.0 y `Microsoft.AspNetCore.Mvc.Testing` 11.0.0-rc.1.26425.128
  - `DraSelina.Consultorio.slnx` — el proyecto nuevo en `/tests/`
  - `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs` — el arnés: contenedor, migración, esquema `pruebas`, base vacía aparte, web en memoria
  - `tests/DraSelina.Consultorio.IntegrationTests/MigrationService.cs` — ejecuta el `MigrationService` como proceso y lanza `MigracionFallidaException` si no sale con 0
  - `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/{PruebasDbContext,Apunte}.cs` — el tipo de prueba mínimo en el esquema `pruebas`
  - `tests/DraSelina.Consultorio.IntegrationTests/Consultas.cs` — consultas al catálogo para las afirmaciones
  - `tests/DraSelina.Consultorio.IntegrationTests/{BaseMigrada,WebNoMigra,EsquemaDePruebas,MigracionFallida}Tests.cs` — una prueba por fila de la matriz
  - `README.md` — sección "Pruebas": Docker, qué da el arnés, cómo escribir una prueba de integración
  - `epic-plataforma-base.md` — Notes: Open question resuelta y línea `Decision:` con marca `[ASSUMPTION]`
- Revisión (lente `quick`): 5 hallazgos; 3 parches aplicados (3 low), 0 diferidos, 2 rechazados: el comportamiento sin Docker (`maybe-false` en la revisión; comprobado después, ver abajo) y la prueba de migración fallida que no pasa por el arnés entero (`low`, el arreglo agrega complejidad para un caso raro).
- Revisión de seguimiento: `false`. Parches por veredicto: high 0, medium 0, low 3.
- Verificación (corrida por el orquestador después de los parches): `./dotnet.sh build` → 0 advertencias, 0 errores; `./dotnet.sh test` → 51 de 51 (18 Kernel, 24 arquitectura, 9 integración), con Docker arriba y sin AppHost. `docker ps -a --filter label=org.testcontainers=true` → vacío a los 20 s de terminar la corrida (a los 3 s todavía lista el contenedor de limpieza). Cada fila de la matriz tiene una prueba que corrió y pasó.
- Docker detenido (comprobado el 2026-10-06, después de la revisión, con permiso de Gerardo para detenerlo): `./dotnet.sh test --project tests/DraSelina.Consultorio.IntegrationTests` → 9 de 9 fallan, 0 omitidas, código de salida 2, con el mensaje "No se pudo levantar PostgreSQL en un contenedor. Las pruebas de integración necesitan Docker: lo primero es comprobar que esté en ejecución…" seguido del detalle de Testcontainers. Esto zanja el hallazgo `maybe-false` de la revisión: el comportamiento es el que dice el README. Con Docker arriba de nuevo, `./dotnet.sh test` → 51 de 51.
- Riesgos y pendientes para Gerardo:
  - `[ASSUMPTION]` Testcontainers y no el soporte de pruebas de Aspire. Razón de más peso, que no está en la línea de la épica: la entrada 5 pide un comando de prueba que vive solo en el proyecto de pruebas, y eso necesita la web en memoria dentro del proceso de pruebas.
  - `[ASSUMPTION]` `Microsoft.AspNetCore.Mvc.Testing` cuenta como parte de ASP.NET Core 11; no está en la tabla Stack.
  - `[ASSUMPTION]` Esquema `pruebas`; una base por corrida, compartida por todas las pruebas.
  - La línea de Deferred de la arquitectura sobre el arnés sigue diciendo que está por decidirse; no se tocó.
  - `./dotnet.sh test` ahora necesita Docker en ejecución.
  - La prueba de "la web no migra" pasa hoy sin esfuerzo porque la web no cablea Wolverine; no se ha visto fallar contra una web que migre. Su valor empieza en la entrada 5.
  - El arnés solo se ejercitó por `./dotnet.sh test`; fuera de la envoltura busca `dotnet` junto al runtime y después en el PATH.
- Decisiones de Gerardo después de la corrida (2026-10-06):
  - Testcontainers aprobado; la línea `Decision:` de la épica ya no lleva `[ASSUMPTION]`.
  - Arquitectura: se quitó de Deferred la línea del arnés y la fila Pruebas de Consistency Conventions nombra Testcontainers y el `MigrationService`.
  - `Microsoft.AspNetCore.Mvc.Testing` agregado a la tabla Stack de la arquitectura (solo en pruebas, misma versión que ASP.NET Core); deja de ser supuesto.
  - Esquema `pruebas` y una base compartida por corrida aprobados; quedaron como `Decision:` en las Notes de la épica.
