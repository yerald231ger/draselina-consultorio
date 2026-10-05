---
title: 'Esqueleto de la solución con pruebas de arquitectura'
type: 'feature' # feature | bugfix | refactor | chore
ticket: '2' # the entry id from the ticket tree, or the story file's stem when the entry has no id; empty outside it
created: '2026-10-05'
status: done
baseline_revision: '3269b71c1ca4cbede88284e73a167cab7abfdfb3' # the commit the work starts from, set before any code change
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
warnings: ['oversized'] # optional: machine-readable warnings for orchestration, e.g. oversized, multiple-goals
deferred: [] # append-only machine-readable deferred review findings; each item carries summary/evidence and optional location/severity
---

<intent-contract>

## Intent

**Problem:** No existe la solución. Sin los proyectos, el Kernel y los contratos base, ninguna historia puede escribir código, y la tabla de referencias de AD-1 es solo texto que nada hace cumplir.

**Approach:** Crear la solución sobre .NET 11 con todos los proyectos del Structural Seed como cascarones que compilan, el contenido del Kernel y de `BuildingBlocks.Contracts`, y pruebas de arquitectura que fallan cuando una referencia se sale de la tabla.

## Boundaries & Constraints

**Always:**
- Proyectos, carpetas y referencias como el Structural Seed y la tabla de "Invariants & Rules" de la arquitectura. Raíz de nombres `DraSelina.Consultorio`.
- El Kernel contiene solo ids tipados, `IClock`, `Resultado` y `Actor` (E-17). No referencia nada.
- Ids tipados sobre Guid v7: `CitaId`, `PatientId`, `TipoDeVisitaId`; dos ids de tipo distinto no son intercambiables.
- `Resultado` tiene cuatro formas: éxito, inválido (errores por nombre de propiedad), no autorizado y fallo esperado con código. El Kernel no enumera códigos de negocio: cada módulo define su conjunto cerrado en `Contracts`.
- `Actor` distingue exactamente `Paciente`, `Administrador` y `Sistema` (E-3).
- `BuildingBlocks.Contracts`: `ICommand<T>`, `IQuery<T>`, `ISender` e `IRequiereAdministrador`, solo como interfaces.
- `Kernel`, `BuildingBlocks.Contracts`, todo `*.Contracts` y todo `*.Domain` no llevan ninguna referencia a paquetes ni a frameworks (T-28, AD-12).
- Paquetes solo de la tabla Stack. El SDK de .NET 11 se instala local al repositorio, sin tocar el sistema.

**Never:**
- Cablear Wolverine, EF Core, DbContext, migraciones, Blazor, telemetría o el AppHost: son las entradas 3 a 7. Aquí los proyectos de host son cascarones.
- Implementar `ISender`, el middleware de validación o el servicio de actor.
- Agregados, comandos de negocio, pantallas, o cualquier concepto de negocio en el Kernel.
- Agregar una librería de pruebas de arquitectura: no está en el Stack.
- Tocar `spikes/`, los documentos de planeación o los archivos de tickets. Sin commits.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Solución entregada | Los proyectos reales de `src/` | Cero violaciones | No se espera error |
| Paquete prohibido | Un `Domain` con referencia a EF Core | Violación que nombra el proyecto y la referencia | La prueba falla |
| Módulo ajeno | `Agenda.Domain` referencia `Pacientes.Contracts` | Violación | La prueba falla |
| Interfaz al dominio | `Consultorio.UI` referencia un `Domain` | Violación | La prueba falla |
| Contrato ajeno permitido | `Agenda.Features` referencia `Pacientes.Contracts` | Sin violación | No se espera error |
| Proyecto fuera de la tabla | Un proyecto nuevo en `src/` que la tabla no nombra | Violación: proyecto desconocido | La prueba falla |
| Resultado inválido | Errores de dos campos | Forma inválida con cada error bajo el nombre de su propiedad | Sin excepción |

</intent-contract>

## Code Map

No hay código de producto: solo `spikes/arranque/` (desechable, no se toca ni se copia su estructura) y los documentos.

- `global.json` -- SDK `11.0.100-rc.1.26425.128` local en `.dotnet/` (ya ignorada por `.gitignore`); permitir la versión definitiva de noviembre
- `dotnet.sh` -- envoltura que exporta `DOTNET_ROOT` y `PATH` hacia `.dotnet/`. Lección de la entrada 1: `global.json` redirige el SDK pero no el runtime, y sin esto las pruebas no arrancan
- `Directory.Build.props`, `Directory.Packages.props`, `DraSelina.Consultorio.slnx` -- destino `net11.0`, versiones centralizadas, solución
- `src/Consultorio.{AppHost,ServiceDefaults,MigrationService,Web,UI}/` -- cascarones
- `src/BuildingBlocks/{Kernel,Contracts}/` -- el contenido real de esta historia
- `src/Modules/Agenda|Pacientes/{Contracts,Domain,Features,Infrastructure}/`, `src/Modules/Notificaciones/{Contracts,Features,Infrastructure}/`, `src/Modules/Contenido/{Contracts,Contenido}/` -- cascarones con sus referencias
- `tests/DraSelina.Consultorio.Kernel.Tests/`, `tests/DraSelina.Consultorio.ArchitectureTests/` -- xunit.v3 4.0.1

Cada `.csproj` se llama `DraSelina.Consultorio.<...>.csproj` dentro de la carpeta del Seed.

## Tasks & Acceptance

**Execution:**
- [x] `global.json`, `dotnet.sh` -- SDK local y envoltura -- el sistema solo tiene .NET 10
- [x] `Directory.Build.props`, `Directory.Packages.props`, `DraSelina.Consultorio.slnx` -- configuración común y solución con todos los proyectos
- [x] `src/BuildingBlocks/Kernel/` -- ids tipados, `IClock`, `Resultado`, `Actor` -- P3
- [x] `src/BuildingBlocks/Contracts/` -- `ICommand<T>`, `IQuery<T>`, `ISender`, `IRequiereAdministrador` -- P3
- [x] `src/Modules/**` y `src/Consultorio.*/` -- un proyecto por fila del Seed, con las referencias que la tabla permite y que su capa necesita -- P3
- [x] `tests/DraSelina.Consultorio.ArchitectureTests/` -- la tabla como datos, un verificador que lee los `.csproj` de `src/`, y una prueba por fila de la matriz -- AD-1
- [x] `tests/DraSelina.Consultorio.Kernel.Tests/` -- ids (versión 7, distintos entre sí), las cuatro formas de `Resultado`, `Actor`
- [x] `README.md` -- cómo compilar y probar con `./dotnet.sh`

**Acceptance Criteria:**
- Given el repositorio recién clonado con el SDK local instalado, when se corre `./dotnet.sh build`, then compila con 0 errores y 0 advertencias propias.
- Given la solución entregada, when se corre `./dotnet.sh test`, then pasan todas las pruebas.
- Given la solución entregada, when se agrega a `Agenda.Domain` una referencia al paquete de EF Core y se corre `./dotnet.sh test`, then las pruebas de arquitectura fallan nombrando ese proyecto y esa referencia; al quitarla vuelven a pasar.
- Given la solución entregada, when se listan los proyectos de `src/`, then coinciden uno a uno con el Structural Seed.

## Implementation Notes

**Verificación corrida el 2026-10-05** (macOS arm64, tras borrar todo `bin/` y `obj/`):

- `./dotnet.sh --version` → `11.0.100-rc.1.26425.128`. El SDK se instaló en `.dotnet/` con `dotnet-install.sh --version 11.0.100-rc.1.26425.128 --install-dir .dotnet`, la forma que el registro de la entrada 1 dejó sin correr.
- `./dotnet.sh build` → `Build succeeded. 0 Warning(s) 0 Error(s)`.
- `./dotnet.sh test` → `total: 32, failed: 0, succeeded: 32` (18 del Kernel, 14 de arquitectura).
- Chequeo manual: se agregó `<PackageReference Include="Microsoft.EntityFrameworkCore" VersionOverride="10.0.12" />` a `Agenda.Domain` y `./dotnet.sh test` salió con código 2:

  ```text
  failed DraSelina.Consultorio.ArchitectureTests.SolucionEntregadaTests.Los_proyectos_reales_de_src_no_tienen_violaciones
    Assert.Empty() Failure: Collection was not empty
    Collection: [Agenda.Domain -> Microsoft.EntityFrameworkCore: paquete prohibido: Kernel, contratos y dominio no llevan paquetes (T-28, AD-12)]
  total: 32  failed: 1  succeeded: 31
  ```

  Al quitar la referencia volvieron a pasar las 32. Hace falta `VersionOverride` (o una `PackageVersion`) porque las versiones son centrales: sin versión, la restauración falla antes de llegar a las pruebas.

**Cómo quedó construido:**

- **Corredor de pruebas.** `global.json` fija `test.runner = Microsoft.Testing.Platform`: xunit.v3 4.0.1 lo trae y así no hacen falta `Microsoft.NET.Test.Sdk` ni `xunit.runner.visualstudio`, que no están en el Stack. xunit.v3 4.0.1 no agrega el `using Xunit` global; lo declara cada proyecto de pruebas.
- **`global.json`** usa `rollForward: latestPatch` con `allowPrerelease`, para aceptar la 11.0.100 definitiva. `[ASSUMPTION]` Esa regla acepta la definitiva partiendo de la candidata; no se puede comprobar hasta noviembre.
- **Versiones centrales** se activan en `Directory.Build.props` y no en `Directory.Packages.props`, para que `spikes/arranque` (que tiene su propio `Directory.Build.props`) no quede bajo la regla. Comprobado: en el prototipo `ManagePackageVersionsCentrally` evalúa vacío.
- **Los 20 proyectos de `src/` son bibliotecas vacías sobre `Microsoft.NET.Sdk`**, también los de host. La entrada 3 les cambia el SDK (Web, AppHost de Aspire, Worker) al cablearlos.
- **El verificador** lee de cada `.csproj` el SDK y los elementos `ProjectReference`, `PackageReference`, `FrameworkReference` y `Reference`. En Kernel, contratos y dominio también es violación un SDK distinto de `Microsoft.NET.Sdk` (el SDK web trae ASP.NET sin declarar ningún `FrameworkReference`). Una segunda prueba compara las rutas de `src/` con las del Seed, una a una.

**Supuestos tomados** (los decide Gerardo):

- `[ASSUMPTION]` `ServiceDefaults` no referencia nada y solo `Web` lo referencia. Que `MigrationService` pueda referenciarlo lo decide la entrada 3; hoy la tabla de pruebas lo marcaría como violación.
- `[ASSUMPTION]` `AppHost` puede referenciar `Web` y `MigrationService` (del diagrama; la tabla escrita no tiene esa fila).
- `[ASSUMPTION]` "Todos los `*.Contracts`" de `Consultorio.UI` incluye `BuildingBlocks.Contracts` (ahí vive `ISender`). "`Contracts` de otros módulos" de `Features` no lo incluye: le llega de forma transitiva. La regla mira solo referencias directas.
- `[ASSUMPTION]` `Features` de Agenda y de Notificaciones ya referencian los `Contracts` ajenos que dibuja el Structural Seed (Agenda → Pacientes, Contenido; Notificaciones → Agenda, Pacientes, Contenido).
- `[ASSUMPTION]` `Actor` es un `enum` de tres valores que empieza en 1, para que el valor por omisión no sea un actor. El modelo de datos tiene una columna `actor_id` (id del usuario administrador) que este `Actor` no puede llevar.
- `[ASSUMPTION]` El código de un fallo esperado es un `string`; el conjunto cerrado de cada módulo serían constantes en sus `Contracts`.
- `[ASSUMPTION]` `Resultado` (sin valor) y `Resultado<T>` (con valor). `ISender` devuelve `Task<Resultado<T>>` y sus dos métodos se llaman `SendAsync`. Un comando que no devuelve nada no tiene forma decidida: `ICommand<T>` siempre pide un `T`.
- `[ASSUMPTION]` `IClock` expone `DateTimeOffset UtcNow`.
- `[ASSUMPTION]` `XxxId.Nuevo()` usa `Guid.CreateVersion7()`, que toma la hora del sistema para la parte temporal del id. No es una hora de negocio, pero roza la letra de AD-17 ("el dominio nunca lee la hora del sistema").
- `[ASSUMPTION]` `TreatWarningsAsErrors` activo en toda la solución, para sostener el criterio de 0 advertencias.

## Plan Change Log

## Review Triage Log

### 2026-10-05 — Review pass
- verdicts: 4 findings — high 0, medium 1, low 2, false 1, maybe-false 0
- findings:
  - `[medium]` `[patch]` Las pruebas de arquitectura solo leían cada `.csproj`: un paquete declarado en un archivo compartido de MSBuild (`Directory.Build.props`, que aplica a los 20 proyectos) llegaría a Kernel, contratos y dominio con las pruebas en verde — se agregó `Ningun_archivo_compartido_de_MSBuild_declara_referencias`; comprobado agregando EF Core a `Directory.Build.props` (falla 1 de 34, nombra el archivo) y revirtiendo.
  - `[low]` `[patch]` `LectorDeProyectos.Archivos` saltaba todo `.csproj` bajo una carpeta `bin` u `obj` — se quitó el filtro; ahora un proyecto ahí sale como desconocido.
  - `[false]` `[reject]` La fila de `Web` permite `Web → MigrationService` y `Web → AppHost` — la tabla de la arquitectura dice literalmente "`Consultorio.Web` | todo (compone)" y "la tabla es la regla"; el código cumple la regla escrita. Queda como pregunta para Gerardo si la fila debe precisarse.
  - `[low]` `[patch]` `Resultado.Errores` exponía un `Dictionary` modificable por conversión, con una instancia vacía compartida — ahora `ReadOnlyDictionary.Empty` y `.AsReadOnly()`; el tipo público no cambia.

## Design Notes

- **La tabla es "puede referenciar".** La prueba exige que las referencias reales sean un subconjunto de lo permitido y que todo proyecto de `src/` esté en la tabla. `tests/` queda fuera de la regla.
- **Por qué leer los `.csproj`.** La regla de AD-1 es sobre referencias entre proyectos, y el `verify` pide atrapar también un paquete. Leer `ProjectReference`, `PackageReference` y `FrameworkReference` cubre ambos sin una librería nueva. El verificador recibe la lista de proyectos como dato, para poder probarlo con casos sintéticos sin ensuciar `src/`.
- **Filas que la tabla no nombra.** Del diagrama: `AppHost` → `Web`, `MigrationService`. `ServiceDefaults` no aparece en la tabla: aquí no referencia nada y solo `Web` ("todo") lo referencia. `[ASSUMPTION]` Que `MigrationService` pueda referenciarlo lo decide la entrada 3; anotarlo en Implementation Notes.
- `[ASSUMPTION]` `Actor` lleva solo su tipo, sin identificador de usuario: hay una sola cuenta de Administrador.
- `[ASSUMPTION]` `ICommand<T>` e `IQuery<T>` son marcadores cuyo `T` es el valor que el `Resultado` trae en el éxito.

## Verification

**Commands:**
- `./dotnet.sh --version` -- expected: `11.0.100-rc.1.26425.128`
- `./dotnet.sh build` -- expected: 0 errores
- `./dotnet.sh test` -- expected: todas las pruebas pasan, incluidas las de cada fila de la matriz

**Manual checks (if no CLI):**
- Agregar el paquete de EF Core a `Agenda.Domain`, correr las pruebas, ver la falla, y revertir; dejar la salida en Implementation Notes.

## Auto Run Result

- Status: built
- Resumen: solución `DraSelina.Consultorio.slnx` sobre .NET 11 con los 20 proyectos del Structural Seed (18 cascarones con sus referencias), el Kernel (`CitaId`, `PatientId`, `TipoDeVisitaId`, `IClock`, `Actor`, `Resultado` y `Resultado<T>` con cuatro formas), `BuildingBlocks.Contracts` (`ICommand<T>`, `IQuery<T>`, `ISender`, `IRequiereAdministrador`) y pruebas de arquitectura que leen los `.csproj` y los archivos compartidos de MSBuild contra la tabla de AD-1.
- Archivos:
  - `global.json`, `dotnet.sh`, `README.md` — SDK 11 local en `.dotnet/`, envoltura con el runtime local, instrucciones
  - `Directory.Build.props`, `Directory.Packages.props`, `DraSelina.Consultorio.slnx` — configuración común (advertencias como errores, versiones centralizadas) y solución
  - `src/BuildingBlocks/Kernel/`, `src/BuildingBlocks/Contracts/` — el contenido real
  - `src/Consultorio.*/`, `src/Modules/**` — cascarones con las referencias de la tabla
  - `tests/DraSelina.Consultorio.ArchitectureTests/` — tabla como datos, lector, verificador y 16 pruebas
  - `tests/DraSelina.Consultorio.Kernel.Tests/` — 18 pruebas
- Revisión (lente `quick`): 4 hallazgos; 3 parches aplicados (1 medium, 2 low), 0 diferidos, 1 rechazado como `false` (la fila "todo" de `Web`, que el código cumple tal como está escrita).
- Revisión de seguimiento: `false`. Parches por veredicto: high 0, medium 1, low 2.
- Verificación (corrida por el orquestador después de los parches): `./dotnet.sh --version` → 11.0.100-rc.1.26425.128; `./dotnet.sh build` → 0 advertencias, 0 errores; `./dotnet.sh test` → 34 de 34. Comprobación manual: EF Core en `Agenda.Domain` → falla nombrando `Agenda.Domain -> Microsoft.EntityFrameworkCore: paquete prohibido`; EF Core en `Directory.Build.props` → falla nombrando el archivo; al revertir, 34 de 34.
- Riesgos y pendientes para Gerardo:
  - `[ASSUMPTION]` `Actor` es un enum de tres valores sin identificador de usuario; el modelo de datos tiene `actor_id` en la bitácora, que este `Actor` no puede llevar.
  - `[ASSUMPTION]` No hay forma definida para un comando que no devuelve valor: `ICommand<T>` siempre exige `T`.
  - `[ASSUMPTION]` Los códigos de fallo son cadenas; cada módulo definiría sus constantes en `Contracts`.
  - `[ASSUMPTION]` `XxxId.Nuevo()` usa `Guid.CreateVersion7()`, que lee el reloj del sistema; no es una hora de negocio, pero roza la redacción de AD-17.
  - Filas que la tabla escrita no tiene: `AppHost` → `Web`, `MigrationService` (del diagrama); `ServiceDefaults` no referencia nada y solo `Web` lo referencia. Si la entrada 3 necesita `MigrationService` → `ServiceDefaults`, la prueba lo marcará hasta que cambie la tabla.
  - La fila "todo" de `Web` permite `Web → MigrationService` y `Web → AppHost`.
  - Fuera de Kernel, contratos y dominio no se verifica qué paquetes se usan.
  - La prueba de archivos compartidos también lee los `.props`/`.targets` generados bajo `obj/`; hoy pasan, pero un paquete futuro podría generar ahí un archivo que la dispare.
  - Los proyectos de host son bibliotecas simples; la entrada 3 cambiará su SDK al cablearlos.
  - Las Implementation Notes dicen 32 pruebas y describen el verificador antes de los parches; el estado final es el de esta sección.

### Cambio posterior a la corrida (2026-10-05, decisión de Gerardo)

Lo construido en la corrida (20 proyectos, uno por capa) se rehízo fuera del flujo por decisión de Gerardo: todo el código de la aplicación vive en `DraSelina.Consultorio.Web`, con módulos y capas por carpeta y namespace; las carpetas de proyecto llevan el nombre completo `DraSelina.Consultorio.*`. Las secciones anteriores de este plan describen la forma vieja.

- Quedan cuatro proyectos en `src/`: `AppHost`, `ServiceDefaults`, `MigrationService` y `Web`. `Kernel`, `BuildingBlocks.Contracts`, los módulos y `UI` son carpetas de `Web`.
- Las pruebas de arquitectura se reescribieron (`Reglas.cs`, `Lector.cs`, `Verificador.cs`): verifican las dependencias entre namespaces por las directivas `using` de cada archivo según su carpeta, y las referencias entre los cuatro proyectos por los `.csproj`.
- Verificación: `./dotnet.sh build` → 0 advertencias, 0 errores; `./dotnet.sh test` → 38 de 38 (18 Kernel, 20 arquitectura). Un archivo en `Modules/Agenda/Domain/` con `using DraSelina.Consultorio.BuildingBlocks.Contracts;` hace fallar las pruebas nombrando el archivo y la referencia; al quitarlo vuelven a pasar.
- La arquitectura, el documento de dominio, el C4, la spec y la épica se actualizaron antes que el código.
