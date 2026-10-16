---
title: 'Actor único y rechazo de administración sin sesión'
type: 'feature'
ticket: '7'
created: '2026-10-15'
status: built
baseline_revision: '5a25db2d07f4574c2f9093281f98da92be40fd88'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md'
  - '{project-root}/README.md'
warnings: ['oversized']
deferred: []
---

<intent-contract>

## Intent

**Problem:** AD-22 pide que un solo servicio del host diga quién actúa (`Administrador`, `Paciente`, `Sistema`) y que un solo decorador, sobre todo handler de comando y de consulta, devuelva `Resultado` no autorizado a lo marcado `IRequiereAdministrador` sin usuario autenticado. Hoy existen `Actor` (Kernel) e `IRequiereAdministrador` (contratos base), pero nada los usa.

**Approach:** Un puerto `IActorActual` en el Kernel (como `IClock`), implementado en el host por un servicio con ámbito que lee el `ClaimsPrincipal` estándar de Blazor (`AuthenticationStateProvider`): autenticado → `Administrador`; sin autenticar → `Paciente`; un ámbito que un trabajo programado declaró como suyo → `Sistema`. Dos decoradores del host (comando y consulta) en `DecoradoresDeHandlers.Aplicar`, por fuera del validador, rechazan lo marcado cuando el actor no es `Administrador`.

## Boundaries & Constraints

**Always:**
- `[ASSUMPTION]` El puerto vive en `Kernel/IActorActual.cs`: `Task<Actor> ObtenerAsync()`. Async porque `AuthenticationStateProvider` lo es. Kernel queda puro (solo BCL).
- El servicio vive en la raíz de Web (`ActorDelHost.cs`, nombre libre), con ámbito, registrado como sí mismo y como `IActorActual` (la misma instancia por ámbito). Lee `AuthenticationStateProvider` del mismo ámbito, que es el de la petición SSR o el del circuito Interactive Server. No usa `IHttpContextAccessor` (no sirve en Interactive Server).
- `[ASSUMPTION]` `Sistema` es explícito: el trabajo programado, al crear su ámbito, llama a un método del servicio concreto (p. ej. `DeclararSistema()`); no está en `IActorActual`, así un handler no puede declararse `Sistema`. Hoy no hay trabajos programados: solo lo usa la prueba.
- `[ASSUMPTION]` Un ámbito que no viene de una petición ni de un circuito y no se declaró `Sistema` no tiene actor: el servicio no inventa uno; deja salir la excepción de `AuthenticationStateProvider` (o lanza `InvalidOperationException` con un mensaje claro). Es un error de programación, no un `Resultado`.
- "Usuario autenticado" es `Identity.IsAuthenticated == true`; equivale a `Administrador` porque no hay registro público (AD-22). El decorador usa el mismo servicio: rechaza si el actor no es `Administrador` (también `Sistema`).
- El decorador envuelve todo `ICommandHandler<,>` y todo `IQueryHandler<,>`; para un tipo no marcado delega sin pedir el actor (las pruebas existentes no tienen usuario y deben seguir pasando igual). Rechazo = `Resultado.NoAutorizado().Como<T>()`, sin llamar al handler ni validar.
- `[ASSUMPTION]` Orden: el rechazo va por fuera del validador (un no autorizado no aprende nada de la forma). En `Aplicar`, después de la línea de validación. Expone `Siguiente` público, como `ValidacionDeComandos` (decisión de Gerardo en la 1.6).
- Build 0 advertencias; todas las pruebas existentes pasan (99), ajustando solo la prueba "una sola envoltura" a la cadena nueva.

**Never:**
- Nada de ASP.NET Core Identity, inicio de sesión, `[Authorize]`, cookies ni esquemas de autenticación (épica 5). No agregar paquetes.
- No el actor `Paciente` por enlace de gestión (épica 3), ni límite de frecuencia/anti-bots.
- No declarar `Sistema` en la entrega de eventos: una política usa el actor del evento (AD-22).
- No tocar `Resultado`, `Actor`, `IRequiereAdministrador`, las interfaces de handler ni los documentos de arquitectura.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Marcado sin usuario | Comando marcado; estado de autenticación con principal anónimo | `Forma = NoAutorizado`; el handler no corre | Sin excepción |
| Marcado con usuario | Mismo comando; principal autenticado de prueba | El handler corre y recibe `Administrador` | — |
| Consulta marcada | Consulta marcada sin usuario / con usuario | `NoAutorizado` / pasa con `Administrador` | Sin excepción |
| Marcado e inválido sin usuario | Comando marcado con datos inválidos, anónimo | `NoAutorizado` (no `Invalido`) | — |
| Marcado en ámbito Sistema | Comando marcado, ámbito declarado `Sistema` | `NoAutorizado` | — |
| No marcado anónimo | Comando no marcado; principal anónimo | El handler recibe `Paciente` | — |
| No marcado Sistema | Comando no marcado; ámbito declarado `Sistema` | El handler recibe `Sistema` | — |
| No marcado sin estado | `Anotar` en un ámbito sin estado de autenticación | Pasa igual que hoy (no se pide actor) | — |
| Envolturas | Handler de cada comando / consulta del contenedor | Comando: rechazo → validador → real; consulta: rechazo → real; cada uno una vez | — |

</intent-contract>

## Code Map

- `src/DraSelina.Consultorio.Web/Kernel/Actor.cs`, `Kernel/IClock.cs` -- `Actor` (Paciente=1, Administrador=2, Sistema=3); `IClock` es el modelo del puerto que implementa el host (`RelojDelSistema.cs`). No cambiar `Actor`.
- `src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/IRequiereAdministrador.cs` -- marcador ya existente. No cambiar.
- `src/DraSelina.Consultorio.Web/Kernel/Resultado.cs` -- `Resultado.NoAutorizado()`, `.Como<T>()`. No cambiar.
- `src/DraSelina.Consultorio.Web/DecoradoresDeHandlers.cs` -- `Aplicar` con `TryDecorate`; el primero queda más cerca del handler. Agregar aquí los dos decoradores, después de la validación.
- `src/DraSelina.Consultorio.Web/ValidacionDeComandos.cs` -- modelo de estilo del decorador (`Siguiente` público, `<summary>` en español).
- `src/DraSelina.Consultorio.Web/Program.cs` -- registrar el servicio del actor (junto a `IClock`). `AddRazorComponents()` ya registra `AuthenticationStateProvider` como `ServerAuthenticationStateProvider` (`IHostEnvironmentAuthenticationStateProvider`) con ámbito; en SSR el renderizador le pone `HttpContext.User`. Verificarlo; si no lo registra, registrarlo explícitamente y anotarlo.
- `src/DraSelina.Consultorio.Web/DraSelina.Consultorio.Web.csproj` -- `<Using Remove="Microsoft.*" />`: escribir cada `using` (`Microsoft.AspNetCore.Components.Authorization`, `System.Security.Claims`).
- `tests/DraSelina.Consultorio.ArchitectureTests/Reglas.cs` -- Kernel puro; la raíz del host sin restricciones.
- `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/Registro.cs` -- `AddPruebas`: registrar aquí los tipos de prueba nuevos. `Inscribir.cs` es modelo de comando con contador singleton.
- `tests/DraSelina.Consultorio.IntegrationTests/ValidacionDeFormaTests.cs:110-137` -- "El validador envuelve una sola vez…": hoy exige que lo resuelto sea el validador; pasa a desenvolver el rechazo primero.
- `tests/DraSelina.Consultorio.IntegrationTests/CasosDeUso.cs`, `UnHandlerPorCasoDeUsoTests.cs`, `MensajesDeValidacionTests.cs` -- recorren todo caso de uso: los tipos nuevos necesitan su handler y, si llevan atributos de validación, `ErrorMessage`.
- `README.md` -- sección "Casos de uso" (tras "La validación de forma", línea ~203): agregar el actor y el rechazo.

## Tasks & Acceptance

**Execution:**
- [x] `src/DraSelina.Consultorio.Web/Kernel/IActorActual.cs` -- el puerto -- AD-22, un único servicio.
- [x] `src/DraSelina.Consultorio.Web/ActorDelHost.cs` -- la implementación con ámbito y `DeclararSistema()` -- AD-22.
- [x] `src/DraSelina.Consultorio.Web/RechazoSinAdministrador.cs` (nombre libre) -- decorador de comando y de consulta -- AD-22.
- [x] `src/DraSelina.Consultorio.Web/DecoradoresDeHandlers.cs`, `Program.cs` -- aplicar los decoradores y registrar el servicio.
- [x] `tests/.../Pruebas/` -- un comando marcado y una consulta marcada cuyos handlers devuelven el `Actor` recibido de `IActorActual` y cuentan sus llamadas; un comando no marcado que devuelve el actor; el comando marcado con un atributo de validación (con `ErrorMessage`) para la fila "marcado e inválido". Registrar en `AddPruebas`.
- [x] `tests/.../ActorYAdministracionTests.cs` -- una prueba por fila de la matriz, contra la web del arnés: el estado de autenticación se fija en el ámbito por `IHostEnvironmentAuthenticationStateProvider.SetAuthenticationState` (principal anónimo `new ClaimsPrincipal(new ClaimsIdentity())`, o autenticado con un tipo de autenticación de prueba); `Sistema` por el método del servicio.
- [x] `tests/.../ValidacionDeFormaTests.cs` -- la prueba de envolturas sigue la cadena nueva para todo comando y toda consulta.
- [x] `README.md` -- cómo se marca un caso de uso de administración, qué devuelve, cómo pide un handler su actor, quién declara `Sistema`, que una política usa el actor del evento, y qué pasa en un ámbito sin actor.

**Acceptance Criteria:**
- Given la web del arnés, when un comando marcado llega sin usuario, then vuelve `NoAutorizado` y el handler no corrió; con un usuario autenticado de prueba corre y recibe `Administrador`.
- Given la web del arnés, when un handler pide su actor, then recibe `Administrador`, `Paciente` o `Sistema` según el ámbito.
- Given la solución, when `./dotnet.sh build` y `./dotnet.sh test`, then 0 advertencias y todas pasan, incluidas las 99 que ya existían.
- Given el AppHost, when corre `scripts/verificar-arranque.sh`, then sale con 0.

## Implementation Notes

- `AddRazorComponents()` sí registra `AuthenticationStateProvider` con ámbito, y es `IHostEnvironmentAuthenticationStateProvider` (las pruebas lo resuelven y lo convierten); no hizo falta registrarlo.
- `[ASSUMPTION]` El analizador BL0013 (.NET 11) marca `GetAuthenticationStateAsync` sin suscribirse a `AuthenticationStateChanged`. Se suprime con `#pragma` solo en esa línea de `ActorDelHost`: el estado no se guarda, se pide en cada llamada.
- `ActorDelHost` envuelve la `InvalidOperationException` de `ServerAuthenticationStateProvider` ("called before SetAuthenticationState") en otra con mensaje claro, conservándola como interna.
- Además de la prueba de envolturas, cambió `El_handler_de_una_consulta_no_lo_envuelve_el_validador`: exigía que la consulta resolviera al handler real; ahora desenvuelve el rechazo primero. La de envolturas se renombró a `Cada_decorador_envuelve_una_sola_vez_al_handler_real_de_cada_comando_y_consulta`.
- Pruebas nuevas (12): una por fila de la matriz, más "marcado e inválido con usuario → Invalido", "ámbito sin estado ni Sistema lanza" y "mismo servicio por ámbito". Total 111.
- README: también se actualizó el bloque de `AddPruebas` con los registros nuevos.

## Plan Change Log

## Review Triage Log

### 2026-10-15 — Review pass
- verdicts: 7 findings — high 0, medium 2, low 5, false 0, maybe-false 0
- findings:
  - `[medium]` `[patch]` El README y `DeclararSistema` decían que el trabajo programado pide `ActorDelHost`, pero esos trabajos viven en Notificaciones (AD-20) y `Verificador.cs` prohíbe a un módulo usar `DraSelina.Consultorio.Web` — README y `<summary>` dicen ahora que lo llama quien crea el ámbito, desde el host, y que cómo obtiene `Sistema` el trabajo de un módulo se decide con el primer trabajo programado. Sin cambio de código.
  - `[medium]` `[patch]` El README prometía `Paciente` a toda petición sin usuario; `ServerAuthenticationStateProvider` solo recibe estado del renderizador de Blazor o del circuito, así que en un endpoint de Minimal API pedir el actor lanza — la fila dice "Página Blazor (SSR) o circuito sin usuario" y el README dice qué pasa en un endpoint y que se decide con el primero. Sin cambio de código.
  - `[low]` `[reject]` El `catch` de `InvalidOperationException` reescribe cualquier fallo del proveedor con el mismo mensaje — la original queda como interna y otro fallo es improbable hoy; distinguirlo exigiría comparar el texto del mensaje del framework.
  - `[low]` `[reject]` Cambió una segunda prueba existente, contra "ajustando solo la prueba 'una sola envoltura'" — el cambio es necesario (las consultas ahora tienen el rechazo por fuera) y está en Implementation Notes; el arreglo sería editar el plan.
  - `[low]` `[patch]` El README decía "al guardar una custom" y AD-22 dice "al guardar o borrar" — agregado "o borrar".
  - `[low]` `[reject]` `DeclararSistema` puede llamarse después de leer el actor y cambiarlo a mitad del ámbito — nadie lo llama fuera de las pruebas; impedirlo agrega un guardia y un estado.
  - `[low]` `[patch]` Comentario de `Program.cs` de ~155 caracteres — reacomodado a 120.

## Design Notes

- **Cómo pide su actor un handler** (lo que verá un módulo):

  ```csharp
  public sealed class CancelarCitaHandler(IActorActual actor, ...) : ICommandHandler<CancelarCita, Guid>
  {
      public async Task<Resultado<Guid>> HandleAsync(CancelarCita comando, CancellationToken ct)
      {
          var quien = await actor.ObtenerAsync(); // Administrador, Paciente o Sistema
          ...
  ```
- **Por qué `AuthenticationStateProvider`.** Es el mismo `ClaimsPrincipal` en SSR estático (el renderizador lo toma de `HttpContext.User`) y en Interactive Server (el del circuito), y la prueba lo fija por la API pública del framework, sin un doble propio.
- **Por qué `Sistema` explícito.** Desde un ámbito no se distingue con fiabilidad "petición anónima" de "trabajo en segundo plano"; si se dedujera, un trabajo que olvidara declararse quedaría como `Paciente` en la bitácora sin que nadie lo note.

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` (tres veces seguidas) -- expected: todas pasan cada vez.
- AppHost en segundo plano + `bash scripts/verificar-arranque.sh` -- expected: sale con 0; detener con `pkill -INT` y luego `-TERM` si sigue.

## Auto Run Result

- Resumen: un único servicio del host (`ActorDelHost`, por el puerto `IActorActual` del Kernel) dice quién actúa: `Administrador` con usuario autenticado, `Paciente` sin él (página Blazor o circuito), `Sistema` cuando quien creó el ámbito lo declaró. Dos decoradores (`RechazoSinAdministradorDeComandos`/`DeConsultas`), por fuera de la validación, devuelven `NoAutorizado` a todo caso de uso marcado `IRequiereAdministrador` cuyo actor no es `Administrador`, sin validar ni llamar al handler.
- Archivos:
  - `src/DraSelina.Consultorio.Web/Kernel/IActorActual.cs` — el puerto.
  - `src/DraSelina.Consultorio.Web/ActorDelHost.cs` — el servicio, con `DeclararSistema()`.
  - `src/DraSelina.Consultorio.Web/RechazoSinAdministrador.cs` — los dos decoradores.
  - `src/DraSelina.Consultorio.Web/DecoradoresDeHandlers.cs`, `Program.cs` — aplicarlos y registrar el servicio.
  - `tests/.../Pruebas/Administrar.cs`, `Pruebas/Registro.cs` — comando y consulta marcados, comando sin marca, contador.
  - `tests/.../ActorYAdministracionTests.cs` — 12 pruebas: una por fila de la matriz y tres más.
  - `tests/.../ValidacionDeFormaTests.cs` — la cadena de decoradores de cada comando y consulta.
  - `README.md` — sección "El actor y la administración".
- Revisión (lente `quick`): 7 hallazgos; 4 parchados (2 medium, 2 low; todos de README o comentarios), 3 low rechazados con su razón en el Triage Log; nada diferido.
- Revisión de seguimiento: `false`. Los dos medium parchados solo corrigen documentación; no queda riesgo de código sin verificar. Parches por veredicto: high 0, medium 2, low 2.
- Verificación (orquestador): `./dotnet.sh build` → 0 advertencias, 0 errores; `./dotnet.sh test` tres veces → 111 de 111 cada vez, antes y después de los parches; `scripts/verificar-arranque.sh` contra el AppHost → 0, las tres filas pasan (antes de los parches, que solo tocan comentarios y README). Las nueve filas de la matriz tienen prueba que corrió y pasó.
- Para Gerardo:
  - `[ASSUMPTION]` `IActorActual` en el Kernel, junto a `IClock`, con `Task<Actor> ObtenerAsync()`.
  - `[ASSUMPTION]` `Sistema` se declara (`ActorDelHost.DeclararSistema()`), no se deduce.
  - `[ASSUMPTION]` Un ámbito sin estado de autenticación y sin `Sistema` lanza `InvalidOperationException`.
  - `[ASSUMPTION]` El rechazo va por fuera de la validación: marcado e inválido sin sesión da `NoAutorizado`.
  - `[ASSUMPTION]` BL0013 suprimido con `#pragma` en una línea de `ActorDelHost`.
  - Abierto: un módulo no ve `ActorDelHost`; cómo obtiene un ámbito `Sistema` el trabajo programado de Notificaciones se decide con el primer trabajo programado.
  - Abierto: un endpoint de Minimal API no fija el estado de autenticación; pedir el actor ahí lanza. Se decide con el primer endpoint (archivo de calendario, AD-10).
- Riesgos: el rechazo solo se ejerció pidiendo el handler al contenedor; ninguna página SSR ni circuito usa todavía un caso de uso marcado.
