---
title: 'El AppHost levanta PostgreSQL, las migraciones y la web, con telemetría'
type: 'feature' # feature | bugfix | refactor | chore
ticket: '3' # the entry id from the ticket tree, or the story file's stem when the entry has no id; empty outside it
created: '2026-10-05'
status: done
baseline_revision: '9e35f303dd8dba31fd18f84aedce81d26a92e8ff' # the commit the work starts from, set before any code change
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
  - '{project-root}/spikes/arranque/REGISTRO.md'
warnings: ['oversized'] # optional: machine-readable warnings for orchestration, e.g. oversized, multiple-goals
deferred: [] # append-only machine-readable deferred review findings; each item carries summary/evidence and optional location/severity
---

<intent-contract>

## Intent

**Problem:** Los cuatro proyectos son cascarones: nada arranca. Ninguna historia puede guardar un dato ni pintar una página hasta que exista la base, un único camino de migraciones y un host web con telemetría.

**Approach:** Un solo comando sobre el AppHost levanta PostgreSQL, corre el `MigrationService` hasta terminar y después arranca la web. El `MigrationService` aplica las migraciones de tres `DbContext` vacíos (uno por módulo con datos) y crea el almacenamiento de Wolverine. La web queda como Blazor Web App mínima con registros y trazas por ServiceDefaults.

## Boundaries & Constraints

**Always:**
- Orden de arranque: PostgreSQL → `MigrationService` hasta terminar bien → web. Si las migraciones fallan, el `MigrationService` sale con código distinto de cero y la web no arranca.
- Un `DbContext` por módulo con datos (Agenda, Pacientes, Notificaciones), en su carpeta `Infrastructure`, con su esquema (`agenda`, `pacientes`, `notificaciones`) y su historial de migraciones dentro de ese mismo esquema (AD-2, AD-25).
- El almacenamiento de Wolverine vive en el esquema `wolverine` y lo crea solo el `MigrationService`.
- La web nunca crea ni migra nada al arrancar: ni EF Core ni Wolverine (AD-25).
- Telemetría solo por `Microsoft.Extensions.Logging` y OpenTelemetry mediante ServiceDefaults, en la web y en el `MigrationService` (AD-21).
- Cada archivo en la carpeta de su capa y con el namespace de su capa; las pruebas de arquitectura siguen pasando. Páginas y componentes Blazor en `UI/`, namespace `DraSelina.Consultorio.UI`; `Program.cs` y la composición en la raíz de `Web`.
- Ningún namespace fuera de `System.*` llega a las capas como `using` implícito o global: al pasar `Web` al SDK web, sus `using` implícitos de `Microsoft.*` se quitan, y una prueba lo verifica.
- Versiones de la tabla Stack. Los paquetes de la plantilla ServiceDefaults de Aspire 13.6.0 y las herramientas de EF Core cuentan como parte de Aspire y de EF Core.
- Contraseñas y cadenas de conexión las genera y entrega Aspire; ningún secreto en el repositorio.

**Never:**
- ASP.NET Core Identity ni el esquema `identidad` (épica 5).
- Tablas de negocio, entidades, datos iniciales, `ISender`, handlers o middleware de Wolverine (entradas 5 a 7).
- Arnés de pruebas de integración (entrada 4), Bootstrap, tokens de diseño o layout público (entrada 8).
- `EnsureCreated`, o copiar la estructura de `spikes/arranque/`.
- Tocar `spikes/`, los documentos de planeación o los archivos de tickets. Sin commits.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Arranque completo | Un comando sobre el AppHost, con Docker arriba y base vacía | PostgreSQL en ejecución, `MigrationService` terminado con código 0, web en ejecución; `GET /` responde 200 con HTML | Si algo no llega a ese estado, la verificación falla |
| Historial por esquema | Después del arranque | Existe una tabla de historial de migraciones en `agenda`, otra en `pacientes` y otra en `notificaciones`; ninguna en `public` | — |
| Almacenamiento de Wolverine | Después del arranque | El esquema `wolverine` existe con sus tablas | — |
| Salud | Después del arranque | El punto de salud de la web responde 200 | — |
| Using implícito | El proyecto `Web` sobre el SDK web | Ningún `using` implícito o global fuera de `System.*` | La prueba de arquitectura falla |
| Namespace de un componente | Un `.razor` en `UI/` con `@namespace` de otra capa | Violación | La prueba de arquitectura falla |

</intent-contract>

## Code Map

- `src/DraSelina.Consultorio.AppHost/` -- cascarón sobre `Microsoft.NET.Sdk`; pasa a `Aspire.AppHost.Sdk/13.6.0`. Ya referencia `Web` y `MigrationService`
- `src/DraSelina.Consultorio.ServiceDefaults/` -- cascarón vacío; recibe la plantilla ServiceDefaults
- `src/DraSelina.Consultorio.MigrationService/` -- cascarón que ya referencia `Web`; pasa a ser un ejecutable que termina
- `src/DraSelina.Consultorio.Web/` -- biblioteca; pasa al SDK web. Hoy solo tiene `Kernel/`, `BuildingBlocks/Contracts/` y carpetas de capa con `.gitkeep` (se borra el `.gitkeep` de la carpeta que reciba archivos)
- `src/DraSelina.Consultorio.Web/Modules/{Agenda,Pacientes,Notificaciones}/Infrastructure/` -- aquí van `DbContext` y migraciones
- `src/DraSelina.Consultorio.Web/UI/` -- aquí van `App.razor`, `Routes.razor`, `_Imports.razor` y una página mínima
- `tests/DraSelina.Consultorio.ArchitectureTests/{Reglas,Lector,Verificador}.cs` -- reglas por carpeta y `using`. `Lector` hoy no lee `@namespace` de un `.razor`. La tabla ya permite `MigrationService` → `Web`, `ServiceDefaults`
- `tests/DraSelina.Consultorio.Kernel.Tests/` -- referencia a `Web`; debe seguir pasando
- `Directory.Build.props` -- advertencias como errores: `ASPIRE010` del AppHost hay que resolverla o silenciarla con su razón
- `Directory.Packages.props` -- hoy solo `xunit.v3`
- `dotnet.sh` -- todo se corre con esta envoltura

De la entrada 1 (`spikes/arranque/REGISTRO.md`): Wolverine 6 necesita `WolverineFx.RuntimeCompilation`; no hay certificado de desarrollo de confianza, así que el AppHost corre por HTTP con `ASPIRE_ALLOW_UNSECURED_TRANSPORT`; Aspire 13.6.0 usa la imagen `postgres:18.3`; la CLI `aspire` no está instalada.

## Tasks & Acceptance

**Execution:**
- [ ] `Directory.Packages.props` -- versiones de Aspire, WolverineFx, EF Core, Npgsql y OpenTelemetry
- [ ] `src/DraSelina.Consultorio.ServiceDefaults/` -- OpenTelemetry (registros, métricas, trazas), salud y descubrimiento, como la plantilla -- P9
- [ ] `src/DraSelina.Consultorio.Web/Modules/*/Infrastructure/` -- un `DbContext` vacío por módulo con su esquema, su historial en el esquema y su migración inicial -- P4
- [ ] `src/DraSelina.Consultorio.Web/` -- SDK web sin `using` implícitos de `Microsoft.*`; `Program.cs` con ServiceDefaults, los tres `DbContext` y Blazor; la definición del almacenamiento de Wolverine en un solo lugar que la web y el `MigrationService` compartan
- [ ] `src/DraSelina.Consultorio.Web/UI/` -- `App.razor`, `Routes.razor`, `_Imports.razor` con `@namespace DraSelina.Consultorio.UI`, y una página de inicio mínima en SSR estático
- [ ] `src/DraSelina.Consultorio.MigrationService/` -- aplica las tres migraciones, crea el almacenamiento de Wolverine, registra cada paso y termina -- P4
- [ ] `src/DraSelina.Consultorio.AppHost/` -- PostgreSQL con la base `consultorio`, `MigrationService` esperando a la base, web esperando a que el `MigrationService` termine
- [ ] `tests/DraSelina.Consultorio.ArchitectureTests/` -- leer `@namespace` de los `.razor`; prueba de que no hay `using` implícitos ni globales fuera de `System.*`; una prueba por cada una de las dos filas de arquitectura de la matriz
- [ ] `scripts/verificar-arranque.sh` -- con el AppHost arriba, ejecuta las cuatro primeras filas de la matriz y sale distinto de cero si alguna falla
- [ ] `README.md` -- cómo arrancar, cómo verificar y cómo crear una migración nueva

**Acceptance Criteria:**
- Given Docker en ejecución y ninguna base previa, when se corre `./dotnet.sh run --project src/DraSelina.Consultorio.AppHost`, then PostgreSQL queda en ejecución, el `MigrationService` termina con código 0 y la web responde 200 en `/`, sin ningún otro comando.
- Given la aplicación arriba, when se consulta PostgreSQL, then hay un historial de migraciones en cada uno de los esquemas `agenda`, `pacientes` y `notificaciones`, y el esquema `wolverine` tiene sus tablas.
- Given la aplicación arriba, when se abre el panel de Aspire y se pide una página a la web, then el panel muestra los tres recursos en su estado y una traza de esa petición.
- Given la solución entregada, when se corre `./dotnet.sh test`, then pasan todas las pruebas, incluidas las de arquitectura.

## Implementation Notes

## Plan Change Log

## Review Triage Log

### 2026-10-05 — Review pass
- verdicts: 6 findings — high 0, medium 0, low 4, false 1, maybe-false 1
- findings:
  - `[maybe-false]` `[reject]` `UI/Pages/Inicio.razor` compila en `DraSelina.Consultorio.UI.Pages` y las migraciones en `<Módulo>.Infrastructure.Migrations`, contra "un namespace plano por capa" — lo resuelve la lectura de Gerardo de esa frase: si admite sub-namespaces dentro de la capa (como exigen las carpetas por caso de uso) no hay defecto; si es estricta, sería `low`. Las pruebas de arquitectura los aceptan a propósito. Queda como pregunta para Gerardo.
  - `[low]` `[patch]` `MigrationService/Program.cs`: sin cadena de conexión el proceso moría con excepción no manejada (134) sin pasar por el registro — ahora la falta se lanza dentro del `try`: registro crítico y código 1.
  - `[low]` `[patch]` La prueba de `using` implícitos leía `obj/` de Web sin que el proyecto de pruebas obligara a compilar Web — `ProjectReference` a Web con `ReferenceOutputAssembly="false"`; el ensamblado de Web no llega a las pruebas.
  - `[false]` `[reject]` `WolverineFx.RuntimeCompilation` no está referenciado — nada arranca el bus en esta historia (el `MigrationService` construye el host sin arrancarlo y la web no llama a `UseWolverine`), y cablearlo está fuera del alcance por la intención (entradas 5 a 7). Anotado para la entrada 5.
  - `[low]` `[reject]` `scripts/verificar-arranque.sh` depende de `kubectl` y de los nombres internos del orquestador — declarado en el README y funciona en el equipo; no hay otra forma directa de leer el código de salida de `migraciones`, y reemplazarlo agrega complejidad.
  - `[low]` `[patch]` El recurso `migraciones` corría sin `DOTNET_ENVIRONMENT` (como `Production`) — `Properties/launchSettings.json` con `Development`; comprobado bajo el AppHost que el recurso recibe la variable.

## Design Notes

- **Por qué quitar los `using` implícitos de `Microsoft.*`.** Las pruebas de arquitectura ven directivas `using`. El SDK web agrega `using` globales de ASP.NET a todo el proyecto: con ellos, un archivo de `Modules/Agenda/Domain/` podría usar `HttpContext` sin escribir ningún `using` y la prueba no lo vería.
- **El almacenamiento de Wolverine en un solo lugar.** La entrada 5 cableará Wolverine en la web con la misma base y el mismo esquema. Si el `MigrationService` y la web lo configuran por separado, pueden divergir.
- `[ASSUMPTION]` La base se llama `consultorio` y no lleva volumen: cada arranque parte de una base vacía. Persistirla entre arranques se decide cuando haya datos que conservar.
- `[ASSUMPTION]` `MigrationService` referencia a `ServiceDefaults`; la tabla de la arquitectura lo permite marcado como supuesto.

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 errores, 0 advertencias
- `./dotnet.sh test` -- expected: todas las pruebas pasan
- `./dotnet.sh run --project src/DraSelina.Consultorio.AppHost` -- expected: los tres recursos llegan a su estado
- `scripts/verificar-arranque.sh` -- expected: las cuatro filas pasan, salida 0

**Manual checks (if no CLI):**
- En el panel de Aspire: PostgreSQL en ejecución, `MigrationService` terminado, web en ejecución, y una traza de `GET /` visible.

## Auto Run Result

- Status: built
- Resumen: `./dotnet.sh run --project src/DraSelina.Consultorio.AppHost` levanta PostgreSQL 18.3 (base `consultorio`), corre el `MigrationService` (migraciones de Agenda, Pacientes y Notificaciones, cada una con su historial en su esquema, más el almacenamiento de Wolverine en `wolverine`) y, si termina con código 0, arranca la web: Blazor Web App mínima en SSR estático, con registros, métricas, trazas y salud por ServiceDefaults. La web no crea ni migra nada.
- Archivos:
  - `Directory.Packages.props`, `dotnet-tools.json` — versiones del Stack; `dotnet-ef` 10.0.12 como herramienta local
  - `src/DraSelina.Consultorio.AppHost/` — recursos `postgres`, `migraciones`, `web`; perfil HTTP (panel en 15080)
  - `src/DraSelina.Consultorio.ServiceDefaults/` — plantilla de Aspire 13.6.0
  - `src/DraSelina.Consultorio.MigrationService/` — ejecutable que migra, crea el almacenamiento de Wolverine y termina con 0 o 1
  - `src/DraSelina.Consultorio.Web/` — SDK web sin `using` implícitos de `Microsoft.*`; `Program.cs`, `BaseDeDatos.cs`, `AlmacenamientoDeWolverine.cs`; tres `DbContext` con su migración `Inicial` en `Modules/*/Infrastructure/`; `UI/` con `App.razor`, `Routes.razor`, `_Imports.razor` y `Pages/Inicio.razor`
  - `tests/DraSelina.Consultorio.ArchitectureTests/` — `@namespace` de los `.razor`, `using` implícitos, cuatro pruebas nuevas
  - `scripts/verificar-arranque.sh`, `README.md`
- Revisión (lente `quick`): 6 hallazgos; 3 parches aplicados (3 low), 0 diferidos, 3 rechazados: los sub-namespaces (`maybe-false`, pregunta para Gerardo), `WolverineFx.RuntimeCompilation` (`false`, es de la entrada 5) y la dependencia de `kubectl` del script (`low`, declarada y sin alternativa simple).
- Revisión de seguimiento: `false`. Parches por veredicto: high 0, medium 0, low 3.
- Verificación (corrida por el orquestador después de los parches): `./dotnet.sh build` → 0 advertencias, 0 errores; `./dotnet.sh test` → 42 de 42; AppHost arriba y `scripts/verificar-arranque.sh` → las cuatro filas pasan (recursos en su estado, `GET /` 200, historial en `agenda`, `pacientes` y `notificaciones` y ninguno en `public`, `wolverine` con 8 tablas, `/health` 200). `migraciones` recibe `DOTNET_ENVIRONMENT=Development`.
- Sin verificar: la traza de `GET /` a la vista en el panel de Aspire. El constructor la vio por la API del panel; el orquestador obtuvo 401 en esa API y no abrió el panel. Queda para Gerardo: abrir la dirección con token que imprime la consola.
- Riesgos y pendientes para Gerardo:
  - ¿"Un namespace plano por capa" admite sub-namespaces (`UI.Pages`, `Infrastructure.Migrations`, `Features.<CasoDeUso>`)?
  - `[ASSUMPTION]` Puertos 5080 (web) y 15080 (panel); recursos `postgres`, `migraciones`, `web`; base `consultorio` sin volumen.
  - `[ASSUMPTION]` `MigrationService` → `ServiceDefaults`.
  - El `MigrationService` no registra los comandos SQL de EF Core, para no mostrar como error la consulta al historial sobre una base vacía.
  - La entrada 5 debe agregar `WolverineFx.RuntimeCompilation` y `WolverineFx.EntityFrameworkCore` al cablear el bus.
  - Sin integración cliente de Aspire para Npgsql: `/health` no comprueba la base y no hay trazas de Npgsql.
  - AD-23: en local Aspire desactiva el enmascarado de la cadena de consulta en las trazas; revisarlo cuando exista el enlace de gestión.
  - Al detener el AppHost con una señal, el proceso de la web puede quedar vivo; con Ctrl+C en la terminal no se observó.
