---
title: '¿Arrancan juntos .NET 11, Wolverine 6 y el AppHost de Aspire?'
type: 'chore' # feature | bugfix | refactor | chore
ticket: '1' # the entry id from the ticket tree, or the story file's stem when the entry has no id; empty outside it
created: '2026-10-05'
status: done
baseline_revision: '965b16ab202933ec2879a3189fc452c4a7b59a78' # the commit the work starts from, set before any code change
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

**Problem:** La arquitectura eligió .NET 11 sin poder confirmar que Wolverine 6 y el AppHost de Aspire 13 arrancan sobre él (Open Questions 1), ni que el fallo de DataAnnotations llega como `Resultado` inválido (2), ni que un `EditForm` se liga a un comando de `Contracts` (3). Las otras nueve entradas de la épica esperan estas respuestas.

**Approach:** Un prototipo desechable, fuera de `src/`, que el AppHost levanta completo: Blazor Web App, un handler de Wolverine que escribe en PostgreSQL por EF Core, y un formulario ligado a un comando con DataAnnotations. Se corre, se observa y se deja un registro con las tres respuestas y el runtime elegido.

## Boundaries & Constraints

**Always:**
- Primero .NET 11. Solo si falla por una causa que la configuración no arregla, se repite todo en .NET 10 y se elige .NET 10.
- Versiones de la sección Stack de la arquitectura (Aspire 13.6.0, WolverineFx 6.45.x, EF Core 10.0.12, Npgsql EF 10.0.3). Si una no restaura, usar la más cercana y anotarla.
- El SDK de .NET 11 no está instalado (hay 10.0.102): instalarlo **local** al prototipo, con `global.json`, sin tocar la instalación del sistema.
- Cada respuesta se apoya en una corrida real: comando, salida y versión exactos en el registro. Lo inferido se marca `[ASSUMPTION]`.
- El comando vive en un proyecto de contratos sin referencias a ASP.NET (AD-12).

**Never:**
- Crear la solución real (`src/`, `tests/`) ni los proyectos del Structural Seed; eso es la entrada 2.
- Escribir en el archivo de la épica, en `tickets.toml` o en los documentos de planeación. Las líneas `Decision` se dejan **propuestas** en el registro; las asienta Gerardo (entrada `hitl`).
- Contestar "sí" por documentación o por compilar: sin corrida, la respuesta es "no confirmado".
- Secretos en el repositorio; commits.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Arranque | `dotnet run` del AppHost con Docker arriba | PostgreSQL y la web en ejecución; `GET /` responde 200 | Si falla, registrar el error textual y probar .NET 10 |
| Comando válido | Formulario con texto válido | Una fila nueva en PostgreSQL, escrita por el handler | No se espera error |
| Comando inválido | Formulario con el campo requerido vacío | Error junto al campo, con el nombre de la propiedad; ninguna fila nueva; el handler no corre | Sin excepción no manejada en el registro de la web |
| Sin soporte nativo | El middleware de Wolverine lanza excepción en vez de devolver resultado | Se prueba un middleware propio mínimo y se registra cuál de los dos funciona | Si ninguno, la respuesta 2 es "no" con la evidencia |

</intent-contract>

## Code Map

El repositorio no tiene código todavía: solo `_bmad/`, `_bmad-output/` y `docs/`. Todo lo de esta entrada es nuevo y vive en `spikes/arranque/`.

- `spikes/arranque/global.json` -- fija el SDK y apunta a la instalación local
- `spikes/arranque/Arranque.AppHost/` -- Aspire: PostgreSQL y la web, que espera a la base
- `spikes/arranque/Arranque.Contracts/` -- comando con DataAnnotations y un `Resultado` mínimo; sin ASP.NET
- `spikes/arranque/Arranque.Web/` -- Blazor Web App: Wolverine, DbContext, handler y la página con `EditForm` en SSR estático
- `spikes/arranque/REGISTRO.md` -- registro de la corrida y líneas `Decision` propuestas
- `.gitignore` -- ya ignora `bin/`, `obj/`, `.aspire/`; falta la carpeta del SDK local
- `_bmad-output/initiative-draselina/epic-plataforma-base/epic-plataforma-base.md` -- solo lectura

## Tasks & Acceptance

**Execution:**
- [x] `.gitignore` -- agregar la carpeta del SDK local -- el SDK no se versiona
- [x] `spikes/arranque/global.json` -- instalar el SDK de .NET 11 local y fijarlo -- el sistema solo tiene .NET 10
- [x] `spikes/arranque/Arranque.Contracts/` -- comando `RegistrarNota` con `[Required]` y `[StringLength]`, y `Resultado` con forma inválida por campo -- pregunta 2 y 3
- [x] `spikes/arranque/Arranque.Web/` -- Blazor Web App con Wolverine, EF Core sobre Npgsql, handler que guarda la nota y página con `EditForm` ligado al comando -- preguntas 1, 2 y 3
- [x] `spikes/arranque/Arranque.AppHost/` -- recurso PostgreSQL y la web con `WaitFor` -- pregunta 1
- [x] `spikes/arranque/verificar.sh` -- script que, con el AppHost arriba, ejecuta cada fila de la matriz (arranque, válido, inválido y, si aplicó, el middleware propio) y sale distinto de cero si alguna falla -- la evidencia se puede repetir
- [x] `spikes/arranque/REGISTRO.md` -- comandos, versiones resueltas, extractos de salida, las tres respuestas, el runtime elegido y las líneas `Decision` propuestas -- es el entregable de la entrada

**Acceptance Criteria:**
- Given Docker en ejecución, when se corre el AppHost del prototipo, then PostgreSQL y la web quedan en ejecución y `GET /` responde 200 sobre el runtime que el registro declara elegido.
- Given la aplicación arriba, when se envía el formulario con datos válidos, then una consulta directa a PostgreSQL muestra la fila nueva.
- Given la aplicación arriba, when se envía el formulario con el campo requerido vacío, then la respuesta muestra el error de ese campo, la tabla no cambia y el registro de la web no tiene excepción no manejada.
- Given que .NET 11 falló en cualquiera de los tres, when se cambia el destino a .NET 10, then los mismos tres criterios se corren de nuevo y el registro guarda ambas corridas.
- Given el prototipo terminado, when se abre `REGISTRO.md`, then cada una de las tres preguntas tiene respuesta (sí, no, o sí con condición), su evidencia, y hay una línea `Decision` propuesta por respuesta más la del runtime.

## Implementation Notes

- Corrió sobre .NET 11 (SDK 11.0.100-rc.1.26425.128, local en `spikes/arranque/.dotnet/`). No hubo corrida en .NET 10: el único fallo de arranque se arregló con configuración.
- Paquete fuera de la tabla Stack: `WolverineFx.RuntimeCompilation` 6.45.0. Sin él Wolverine 6 no arranca (`TypeLoadMode.Dynamic … no IAssemblyGenerator (Roslyn) is registered`).
- Pregunta 2: el middleware nativo lanza `Wolverine.DataAnnotationsValidation.Internals.ValidationException`; `ValidacionPropia` entrega `Resultado` inválido. `verificar.sh` sale 2 en modo `nativo` y 0 en modo `propio`.
- Archivos no previstos en el Code Map: `arrancar.sh` (exporta `DOTNET_ROOT`/`PATH` hacia el SDK local; `global.json` solo redirige el SDK, no el runtime), `Directory.Build.props` (destino en una propiedad), `Arranque.slnx`.
- Los comandos de Verification que usan la ruta `spikes/arranque` desde la raíz no funcionan tal cual: `global.json` solo se encuentra desde dentro de `spikes/arranque/`.
- Se comprobaron además tres formas del comando (clase con `set`, `record` con `init`, `record` posicional): las tres ligan.
- No se tocó el frontmatter (`status`) ni `Auto Run Result`, que sigue describiendo el bloqueo anterior.

## Plan Change Log

## Review Triage Log

### 2026-10-05 — Review pass
- verdicts: 11 findings — high 0, medium 1, low 10, false 0, maybe-false 0
- findings:
  - `[low]` `[patch]` `arrancar.sh` y `AppHost.cs` arrancaban por defecto en modo `nativo`, con lo que `verificar.sh` falla — confirmado en la corrida propia (exit 2 en `nativo`); el valor por defecto pasó a `propio` en ambos y `nativo` queda como argumento explícito.
  - `[low]` `[patch]` `global.json`: el comando de instalación del `errorMessage` apuntaba a `spikes/arranque/.dotnet` estando ya dentro de esa carpeta — corregido a `.dotnet`.
  - `[low]` `[patch]` `REGISTRO.md`: el comando de instalación documentado no fijaba versión y dejaría de coincidir con `global.json` — ahora muestra `--version 11.0.100-rc.1.26425.128` y dice cuál se usó el 2026-10-05.
  - `[medium]` `[patch]` `REGISTRO.md`: la afirmación sobre `IFailureAction<T>` no llevaba `[ASSUMPTION]` y la Decision de la pregunta 2 afirmaba más de lo corrido, sobre una invariante (AD-12) — marcada `[ASSUMPTION]`; la Decision ahora afirma solo lo corrido y deja la salida del paquete como propuesta condicionada.
  - `[low]` `[patch]` `REGISTRO.md`, pregunta 3: "ligan y validan en las dos corridas" era falso para `nativo` — reescrito: ligan en las dos; el error por campo de B y C solo se vio en `propio`.
  - `[low]` `[patch]` `REGISTRO.md`, pregunta 3: nombraba `ISender`, que el prototipo no usa — queda solo `IMessageBus` como hecho e `ISender` con `[ASSUMPTION]`.
  - `[low]` `[patch]` `verificar.sh`: el código 2 significaba "sin contenedor" y "dos filas fallan" — la precondición sale ahora con 99; comprobado sin AppHost.
  - `[low]` `[reject]` `verificar.sh`: una lectura vacía de `/diag` o de `psql` se compara como "sin cambio" — real, pero improbable en un script desechable (la fila de arranque ya falla si la web no responde) y el arreglo agrega guardas.
  - `[low]` `[reject]` `verificar.sh`: el contenedor se elige solo por el prefijo `postgres-` — real solo con otro AppHost de Aspire corriendo a la vez; el arreglo agrega complejidad a un script desechable.
  - `[low]` `[reject]` `verificar.sh`: la fila "sin excepción no manejada" solo imprime y nunca falla — el criterio de aceptación 3 ya lo cubre con el delta por envío; convertirla en fila que falla exige una rama por modo.
  - `[low]` `[reject]` Plan: comandos de Verification y Auto Run Result desactualizados — su arreglo es editar el plan de esta corrida, que la revisión no hace; el Auto Run Result se reescribe en Finalize y los comandos reales están en Implementation Notes y en `REGISTRO.md`.

## Design Notes

- El prototipo crea su tabla con `EnsureCreated` al arrancar. Eso contradice AD-25 a propósito: aquí no hay `MigrationService`, que es la entrada 3. El registro debe decirlo para que nadie lo copie.
- La pregunta 3 tiene dos mitades que se contestan por separado: si el comando puede ser el `Model` del `EditForm` con `[SupplyParameterFromForm]` en SSR estático, y qué forma exige (clase con propiedades `set`, `record` con `init`, constructor sin parámetros).
- La pregunta 2 se contesta observando qué recibe quien invoca: un `Resultado` inválido con errores por nombre de propiedad, o una excepción de validación.

## Verification

**Commands:**
- `dotnet --version` dentro de `spikes/arranque/` -- expected: el SDK que el registro declara
- `dotnet build spikes/arranque` -- expected: 0 errores
- `dotnet run --project spikes/arranque/Arranque.AppHost` -- expected: PostgreSQL y la web en ejecución
- `spikes/arranque/verificar.sh` -- expected: todas las filas de la matriz pasan, salida 0
- `curl` a `GET /` y a los dos envíos del formulario -- expected: 200; fila nueva en el válido; error por campo y ninguna fila en el inválido
- `docker exec <contenedor> psql ... -c "select count(*) ..."` -- expected: el conteo sube solo con el envío válido

## Auto Run Result

- Status: built
- Resumen: prototipo desechable en `spikes/arranque/` que el AppHost de Aspire 13.6.0 levanta sobre .NET 11 (RC 1): Blazor Web App en SSR estático, handler de Wolverine 6.45.0 que escribe en PostgreSQL 18.3 por EF Core 10.0.12, y formulario ligado a un comando con DataAnnotations. Respuestas: (1) sí arrancan juntos, agregando `WolverineFx.RuntimeCompilation`; (2) el middleware de DataAnnotations de Wolverine, por defecto, lanza `ValidationException`; un middleware propio sí entrega `Resultado` inválido por campo; (3) el `EditForm` se liga directo al comando. Runtime elegido: .NET 11; no hubo corrida en .NET 10.
- Archivos:
  - `.gitignore` — ignora `.dotnet/`
  - `spikes/arranque/global.json`, `Directory.Build.props`, `Arranque.slnx` — SDK local fijado, destino en una propiedad, solución
  - `spikes/arranque/Arranque.Contracts/` — comando en tres formas y `Resultado` mínimo, sin ASP.NET
  - `spikes/arranque/Arranque.Web/` — host, Wolverine, DbContext, handlers, middleware propio, páginas y `/diag`
  - `spikes/arranque/Arranque.AppHost/` — PostgreSQL y la web
  - `spikes/arranque/arrancar.sh`, `verificar.sh` — arranque con el runtime local y filas de la matriz
  - `spikes/arranque/REGISTRO.md` — registro de la corrida y cuatro líneas `Decision` propuestas
- Revisión (lente `quick`): 11 hallazgos; 7 parches aplicados (1 medium, 6 low), 0 diferidos, 4 rechazados. Rechazados: tres endurecimientos de `verificar.sh` (lectura vacía tomada como "sin cambio", contenedor por prefijo, fila de errores que nunca falla), por improbables en un script desechable y porque su arreglo agrega guardas; y el plan desactualizado, porque su arreglo es editar este plan.
- Revisión de seguimiento: `false`. Parches por veredicto: high 0, medium 1, low 6.
- Verificación (corrida por el orquestador después de los parches, desde `spikes/arranque/`): `dotnet --version` → 11.0.100-rc.1.26425.128; `dotnet build` → 0 errores, 1 advertencia (ASPIRE010); `./arrancar.sh` + `./verificar.sh` → todas las filas pasan (modo `propio`); `./arrancar.sh nativo` + `./verificar.sh` → fallan las dos filas de comando inválido con `ValidationException`, que es la respuesta 2; `./verificar.sh` sin AppHost → 99. No quedan contenedores ni procesos.
- Riesgos y pendientes:
  - Las líneas `Decision` están propuestas en `spikes/arranque/REGISTRO.md`; no se escribieron en la épica. El `verify` de la entrada pide asentarlas ahí: las asienta Gerardo (entrada `hitl`).
  - La propuesta de la pregunta 2 cambia la redacción de AD-12 y la tabla Stack, que son invariantes del diseño; solo Gerardo las cambia.
  - `[ASSUMPTION]` `IFailureAction<T>` no puede devolver un valor: leído en el código fuente, no corrido.
  - Los comandos de Verification de este plan solo funcionan desde `spikes/arranque/` y con `arrancar.sh`.
  - Posible fuga contra AD-23: Wolverine registra el `ToString()` del mensaje cuando una invocación falla, y un `record` imprime sus propiedades.
  - Es una candidata; no se ejercitaron outbox con eventos, mensajes programados, ServiceDefaults, HTTPS, `TypeLoadMode.Static`, la validación en navegador de AD-12 ni el retroceso a .NET 10.
  - Sin commit: pendiente de la confirmación de Gerardo.
