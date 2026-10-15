---
title: 'La validación de forma llega como Resultado inválido'
type: 'feature'
ticket: '6'
created: '2026-10-15'
status: done
baseline_revision: '24d3d705000f59320a364035be2fc8d02bddcf79'
route: 'full'
route_source: 'auto'
risk: 'medium'
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

**Problem:** Nada valida hoy la forma de un comando antes de su handler: un comando con datos inválidos llega al handler tal cual. AD-12 pide que el backend valide siempre, antes del handler, con un decorador que aplica DataAnnotations y entrega el fallo como `Resultado` inválido por campo. Además, la épica decidió (2026-10-10) que esta entrada trae la prueba que hace cumplir "cada comando y cada consulta tiene exactamente un handler" (AD-10).

**Approach:** Un decorador del host envuelve a todo `ICommandHandler<TComando, T>`, registrado con Scrutor (entra al Stack, versión 6.1.0 como Cypma.Cop). Si el comando no pasa DataAnnotations devuelve `Resultado` inválido con los errores por nombre de propiedad y no llama al handler; si pasa, delega. Las consultas no se validan. Una prueba de integración recorre los `ICommand<T>` e `IQuery<T>` contra el contenedor de la web y exige un handler por cada uno.

## Boundaries & Constraints

**Always:**
- El decorador vive en el host (raíz de `DraSelina.Consultorio.Web`, namespace `DraSelina.Consultorio.Web`): la tabla de capas pone ahí los decoradores. Usa `System.ComponentModel.DataAnnotations.Validator.TryValidateObject(comando, contexto, errores, validateAllProperties: true)`; sin dependencias de ASP.NET.
- Envuelve **todo** handler de comando registrado en el contenedor, una sola vez, también los que registra el arnés de pruebas (`AddPruebas`, que entra por `ConfigureServices` de `WebApplicationFactory`, después de `Program.cs`). La lista de decoradores está en un solo lugar del host, para que la entrada 7 agregue el suyo ahí.
- El `Resultado` inválido se arma con `Resultado.Invalido(...)` del Kernel: cada error bajo el nombre de cada miembro de su `ValidationResult`, con su mensaje tal cual.
- `[ASSUMPTION]` Un error sin miembro (de `IValidatableObject` a nivel de objeto) va bajo la propiedad `""`, la misma convención que Blazor usa para errores del modelo; un error sin mensaje lleva "El valor no es válido.".
- `[ASSUMPTION]` El decorador no traduce mensajes: el idioma lo pone el atributo del comando (`ErrorMessage`).
- Paquete nuevo solo en `Directory.Packages.props` (`Scrutor` 6.1.0) y en el `.csproj` de Web. Build con 0 advertencias; pruebas de arquitectura y todas las existentes siguen pasando.

**Never:**
- No validar consultas ni eventos; no tocar `IEventHandler` ni la entrega de eventos.
- No validar en profundidad objetos anidados ni colecciones (DataAnnotations no lo hace); queda como límite conocido en el README.
- No agregar reglas de negocio ni atributos propios a contratos de producción; no hay comandos de producción todavía.
- No cambiar `Resultado`, el Kernel, `ICommandHandler` ni los documentos de arquitectura (son de Gerardo): si Scrutor 6.1.0 no corre sobre .NET 11, detenerse y decirlo.
- No comprobar "un handler por caso de uso" al arrancar la web: es una prueba (decisión de la épica).
- Nada de la entrada 7 (actor, `IRequiereAdministrador`) ni de la 9 (mostrar errores en el formulario).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Válido | Comando de prueba con todos los campos bien | El handler corre una vez y su `Resultado` llega tal cual | — |
| Inválido en varios campos | Nombre vacío, correo mal formado, edad fuera de rango | `Forma = Invalido`, errores exactamente bajo `Nombre`, `Correo` y `Edad`; el handler no corre | Sin excepción |
| Dos errores en un campo | Un campo que rompe dos atributos | Los dos mensajes bajo esa propiedad | Sin excepción |
| Error de objeto | Campos bien, `IValidatableObject.Validate` devuelve un error sin miembros | Error bajo `""`; el handler no corre | Sin excepción |
| Comando sin atributos | `Anotar` | Pasa directo al handler; las pruebas de la 1.5 y la 1.11 siguen iguales | — |
| Consulta | `LeerAnotacion` | El handler de la consulta no está envuelto por el validador | — |
| Una sola envoltura | Handler de comando pedido al contenedor de la web | Es el validador y envuelve directamente al handler real (no a otro validador) | — |
| Un handler por caso de uso | Todos los `ICommand<T>`/`IQuery<T>` de Web y de los tipos de prueba, contra la web del arnés | Cada uno tiene exactamente un handler | La prueba nombra el tipo que tiene 0 o más de 1 |
| El verificador detecta | Contenedor armado a mano con un comando sin handler y otro con dos | Nombra los dos | — |

</intent-contract>

## Code Map

- `src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/ICommandHandler.cs`, `IQueryHandler.cs`, `ICommand.cs`, `IQuery.cs` -- interfaces a decorar y marcadores para recorrer los casos de uso (`ICommand<T>`, `IQuery<T>`). No cambiar.
- `src/DraSelina.Consultorio.Web/Kernel/Resultado.cs` -- `Resultado.Invalido(IEnumerable<(string Propiedad, string Mensaje)>)` agrupa por propiedad y lanza si no hay errores; `.Como<T>()` lleva un no exitoso a `Resultado<T>`; `FormaDeResultado.Invalido`, `Errores`. No cambiar.
- `src/DraSelina.Consultorio.Web/Program.cs` -- registra módulos (`AddAgenda/AddPacientes/AddNotificaciones`), reloj, entrega de eventos. Aquí se activan los decoradores. Cypma.Cop lo hace con `builder.Services.Decorate(typeof(ICommandHandler<,>), typeof(ValidatorCommandHandler<,>))` en su `Program.cs`; aquí eso no alcanzaría a los handlers del arnés, que se registran después. Producción hoy no tiene ningún handler de comando: `Decorate` de Scrutor lanza si no encuentra ninguno; `TryDecorate` no.
- `src/DraSelina.Consultorio.Web/Outbox.cs`, `EntregaDeEventos.cs` -- ejemplo del estilo del host (extensiones `Add…` en una clase estática, comentarios en español con `<summary>`). No cambiar.
- `src/DraSelina.Consultorio.Web/DraSelina.Consultorio.Web.csproj` -- `<Using Remove="Microsoft.*" />`: todo `using` se escribe. Agregar `Scrutor`.
- `Directory.Packages.props` -- gestión central; comentario por paquete. Agregar `Scrutor` 6.1.0.
- `tests/DraSelina.Consultorio.ArchitectureTests/Reglas.cs` -- el host (raíz) no tiene restricciones de capa; un archivo nuevo en la raíz no cae en carpeta reservada.
- `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs` -- `ArrancarWeb()` (web real con `AddPruebas` + `AddOutbox<PruebasDbContext>`), clase privada `Web`. `CasoDeUsoTests.cs` muestra cómo pedir un handler en un ámbito y afirmar sobre `Resultado`.
- `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/Registro.cs` -- `AddPruebas` registra `ICommandHandler<Anotar, Guid>`, `IQueryHandler<LeerAnotacion, AnotacionLeida>` y los handlers de evento. El comando de prueba nuevo y su handler se registran aquí.
- `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/Anotar.cs`, `LeerAnotacion.cs` -- forma de un comando (clase con propiedades `set`) y de su handler. No cambiar su comportamiento.
- `README.md` -- sección de casos de uso (cómo se escribe un comando y su handler) y "Eventos entre módulos" como modelo de redacción. Agregar validación de forma.

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Packages.props`, `src/DraSelina.Consultorio.Web/DraSelina.Consultorio.Web.csproj` -- agregar Scrutor 6.1.0 -- el Stack lo trae para los decoradores.
- [x] `src/DraSelina.Consultorio.Web/ValidacionDeComandos.cs` (nombre libre en la raíz) -- el decorador `ICommandHandler<TComando, T>` que valida con DataAnnotations y devuelve `Resultado.Invalido(...).Como<T>()` o delega -- AD-12.
- [x] `src/DraSelina.Consultorio.Web/` (un archivo de decoradores) y `Program.cs` -- un solo lugar que lista los decoradores de handlers y los aplica a la colección **final** de servicios, de modo que alcancen también lo registrado por el arnés y se apliquen una sola vez. Mecanismo sugerido: aplicarlos al construir el contenedor (`builder.Host.UseServiceProviderFactory` con una fábrica que llama a `TryDecorate` de Scrutor y luego crea el proveedor por defecto con la validación de ámbitos que ya use la web). Si se elige otro mecanismo, debe cumplir la fila "Una sola envoltura" y anotarse en Implementation Notes.
- [x] `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/` -- un comando de prueba con DataAnnotations (por ejemplo `Inscribir`: `Nombre` requerido y largo máximo, `Correo` requerido y `EmailAddress`, `Edad` con `Range`, más `IValidatableObject` para el error de objeto) y su handler que deja constancia de cada llamada (un contador singleton o una fila en el esquema `pruebas`); registrarlos en `AddPruebas`.
- [x] `tests/DraSelina.Consultorio.IntegrationTests/ValidacionDeFormaTests.cs` -- una prueba por fila de la matriz de validación, contra la web real del arnés, pidiendo el handler por su interfaz.
- [x] `tests/DraSelina.Consultorio.IntegrationTests/UnHandlerPorCasoDeUsoTests.cs` -- recorre los tipos concretos no genéricos que implementan `ICommand<T>` o `IQuery<T>` en el ensamblado de Web y en el de pruebas; para cada uno pide al contenedor de la web todos los `ICommandHandler<C, T>`/`IQueryHandler<C, T>` y exige exactamente uno. Más una prueba del verificador contra un contenedor armado a mano con un faltante y un duplicado.
- [x] `README.md` -- cómo se declara la forma de un comando, qué devuelve el decorador, que las consultas no se validan, el error de objeto bajo `""`, el límite de objetos anidados, dónde se agregan decoradores y la prueba de un handler por caso de uso.

**Acceptance Criteria:**
- Given la web del arnés, when se pide `ICommandHandler<Inscribir, …>` y se manda un comando con datos inválidos, then vuelve `Resultado` inválido con el nombre de cada propiedad y el handler no corrió.
- Given la web del arnés, when se pide el handler de cualquier comando, then el validador lo envuelve exactamente una vez; el handler de una consulta no lo envuelve.
- Given la web real y los tipos de prueba, when corre la prueba de un handler por caso de uso, then pasa; y con un caso de uso sin handler o con dos, el verificador lo nombra.
- Given la solución, when `./dotnet.sh build` y `./dotnet.sh test`, then 0 advertencias y todas las pruebas pasan, incluidas las 88 que ya existían.
- Given el AppHost, when corre `scripts/verificar-arranque.sh`, then sale con 0.

## Implementation Notes

- **Mecanismo de decoradores:** el sugerido. `DecoradoresDeHandlers.cs` (raíz de Web) tiene la lista (`Aplicar`, con `TryDecorate` de Scrutor) y una fábrica que envuelve a `DefaultServiceProviderFactory`, aplica la lista en `CreateServiceProvider` y conserva `ValidateScopes`/`ValidateOnBuild` = `IsDevelopment()`, como la web sin fábrica. `Program.cs` la activa con `builder.Host.UseServiceProviderFactory(contexto => DecoradoresDeHandlers.Fabrica(contexto.HostingEnvironment))`. Scrutor 6.1.0 (lib net8.0) compila y corre sobre .NET 11 rc.1.
- **`ValidacionDeComandos.Siguiente`:** propiedad pública de solo lectura con el handler envuelto, para que la prueba "Una sola envoltura" afirme que el validador envuelve directamente al handler real sin reflexión sobre campos generados.
- **`[ASSUMPTION]` `ComandoSinHandler` queda fuera de la prueba de un handler por caso de uso**, en una lista explícita (`SinHandlerAProposito`): no tiene handler a propósito (la prueba de la 1.5 que pide un handler inexistente). La prueba del verificador lo usa como el caso sin handler; el duplicado son dos handlers privados de `LeerAnotacion` en un contenedor armado a mano, para no crear casos de uso de prueba que la otra prueba recorrería.
- **Comando de prueba:** `Pruebas/Inscribir.cs` (`Inscribir`, `InscribirHandler`, contador singleton `LlamadasDeInscribir`); el error de objeto lo activa una propiedad solo de prueba (`ErrorDelObjeto`), como `Desenlace` en `Anotar`. "Dos errores en un campo" usa `Correo` con `[EmailAddress]` y `[StringLength(40)]` (`[Required]` corta los demás atributos de su campo).
- **README:** además de lo pedido, se actualizó el bloque de `AddPruebas` citado en "Cómo registra un módulo sus handlers" y la frase de "Cómo se pide un handler" que decía que no había comprobación de un handler por comando.
- **Tras la revisión:** el descubrimiento de casos de uso pasó a `tests/.../CasosDeUso.cs` (compartido; ya no filtra solo clases, también structs). "Una sola envoltura" recorre todo `ICommand<T>` descubierto. El verificador se prueba con un comando sin handler, un comando con dos y una consulta con dos. README: "sin atributos ni `IValidatableObject`" y el límite de `ValidationContext` sin proveedor de servicios.
- **Verificación:** `./dotnet.sh build` 0 advertencias, 0 errores; `./dotnet.sh test` tres veces seguidas, 97/97 (88 previas + 9 nuevas); AppHost + `scripts/verificar-arranque.sh` salió con 0.

## Plan Change Log

## Review Triage Log

### 2026-10-15 — Review pass
- verdicts: 5 findings — high 0, medium 1, low 4, false 0, maybe-false 0
- findings:
  - `[medium]` `[patch]` La prueba "una sola envoltura" solo miraba `Anotar` e `Inscribir`; un comando futuro sin envolver o envuelto dos veces pasaría — ahora recorre todo `ICommand<T>` descubierto y exige un `ValidacionDeComandos<,>` cuyo `Siguiente` no es otro validador.
  - `[low]` `[patch]` El verificador probaba el duplicado con una consulta, nunca con un comando — ahora también registra dos handlers de `Anotar` y espera tres problemas nombrados.
  - `[low]` `[patch]` El descubrimiento de casos de uso filtraba `IsClass` y saltaba en silencio un `struct` — se quitó la condición; el descubrimiento vive en `CasosDeUso.cs`, compartido.
  - `[low]` `[patch]` El README decía que un comando sin atributos siempre pasa, pero `IValidatableObject` se ejecuta igual — dice "sin atributos ni `IValidatableObject`".
  - `[low]` `[patch]` `ValidationContext` se arma sin proveedor de servicios y eso no estaba dicho — una línea en los límites conocidos del README; el código no cambia.

### 2026-10-15 — Decisiones de Gerardo sobre lo que dejó la entrada

Gerardo confirmó las tres suposiciones (error de objeto bajo `""`; `ComandoSinHandler` en lista explícita; `Siguiente` pública) y decidió que todo atributo de validación de un comando lleva `ErrorMessage`, con una prueba que lo hace cumplir. Aprobó cambiar la fila de Scrutor en el Stack.

- **patch** — `tests/.../MensajesDeValidacionTests.cs`: recorre los comandos de Web y de prueba y falla por cada atributo de validación sin `ErrorMessage` (o `ErrorMessageResourceName`) escrito; se lee de `GetCustomAttributesData`, porque `[EmailAddress]` trae su propiedad `ErrorMessage` llena en inglés aunque nadie la escriba. Prueba del verificador con un tipo que no es comando. Comprobado quitando un `ErrorMessage` de `Inscribir`: la prueba falla.
- README: la regla, en "La validación de forma". Arquitectura: fila Scrutor sin `[ASSUMPTION]`.
- Verificación: build con 0 advertencias; 99 de 99 tres veces seguidas.

## Design Notes

- **Cómo se ve el decorador.** Corto y legible, en el estilo de Cypma.Cop:

  ```csharp
  public sealed class ValidacionDeComandos<TComando, T>(ICommandHandler<TComando, T> siguiente)
      : ICommandHandler<TComando, T> where TComando : ICommand<T>
  {
      public Task<Resultado<T>> HandleAsync(TComando comando, CancellationToken ct)
      {
          var errores = new List<ValidationResult>();
          if (Validator.TryValidateObject(comando, new ValidationContext(comando), errores, validateAllProperties: true))
              return siguiente.HandleAsync(comando, ct);
          return Task.FromResult(Resultado.Invalido(/* (miembro o "", mensaje) por error */).Como<T>());
      }
  }
  ```
- **Por qué no basta `Decorate` en `Program.cs`.** `WebApplicationFactory` aplica su `ConfigureServices` al construir la web, después de las líneas de `Program.cs`: un `Decorate` ahí no vería `AnotarHandler` ni el comando de prueba, y la prueba de validación pasaría por un camino que producción no usa. Aplicar los decoradores al construir el contenedor los aplica a todo lo registrado, una vez.
- **Un handler por caso de uso.** Hoy los únicos casos de uso son los de prueba; por eso la prueba recorre también el ensamblado de pruebas. Cuando los módulos tengan comandos, la misma prueba los cubre sin cambios.

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` (tres veces seguidas) -- expected: todas pasan cada vez.
- AppHost en segundo plano + `scripts/verificar-arranque.sh` -- expected: sale con 0; detener con `pkill -INT -f 'DraSelina.Consultorio.AppHost'`.

## Auto Run Result

- Resumen: un decorador del host (`ValidacionDeComandos`) valida con DataAnnotations todo comando antes de su handler y devuelve `Resultado` inválido por campo sin llamar al handler; las consultas no se validan. Los decoradores se listan en `DecoradoresDeHandlers.cs` y se aplican al construir el contenedor, así alcanzan también los handlers que registra el arnés. Una prueba exige un handler por cada comando y consulta.
- Archivos:
  - `Directory.Packages.props`, `src/DraSelina.Consultorio.Web/DraSelina.Consultorio.Web.csproj` — Scrutor 6.1.0.
  - `src/DraSelina.Consultorio.Web/ValidacionDeComandos.cs` — el decorador de validación.
  - `src/DraSelina.Consultorio.Web/DecoradoresDeHandlers.cs` — la lista de decoradores y la fábrica del contenedor.
  - `src/DraSelina.Consultorio.Web/Program.cs` — activa la fábrica.
  - `tests/.../Pruebas/Inscribir.cs`, `Pruebas/Registro.cs` — comando de prueba con DataAnnotations y su handler con contador.
  - `tests/.../ValidacionDeFormaTests.cs`, `UnHandlerPorCasoDeUsoTests.cs`, `CasosDeUso.cs` — pruebas de la matriz y de un handler por caso de uso.
  - `README.md` — secciones "La validación de forma" y "Un handler por caso de uso".
- Revisión (lente `quick`): 5 hallazgos, los 5 parchados (1 medium, 4 low); ninguno diferido ni rechazado.
- Revisión de seguimiento: `false`. Parches por veredicto: high 0, medium 1, low 4.
- Verificación (orquestador, después de los parches): `./dotnet.sh build` → 0 advertencias, 0 errores; `./dotnet.sh test` tres veces → 97 de 97 cada vez; `scripts/verificar-arranque.sh` contra el AppHost → 0, las tres filas pasan (corrido antes de los parches, que solo tocan pruebas y README). Las nueve filas de la matriz tienen prueba que corrió y pasó.
- Para Gerardo:
  - `[ASSUMPTION]` Un error de objeto (sin campo) va bajo la propiedad `""`, como en Blazor; un error sin mensaje lleva "El valor no es válido.".
  - `[ASSUMPTION]` El decorador no traduce: el idioma del mensaje lo pone el `ErrorMessage` de cada atributo. Sin `ErrorMessage`, DataAnnotations da su mensaje en inglés.
  - `[ASSUMPTION]` `ComandoSinHandler` (de la 1.5) queda fuera de la prueba de un handler por caso de uso, en una lista explícita: no tiene handler a propósito.
  - `ValidacionDeComandos.Siguiente` es una propiedad pública que solo usan las pruebas.
  - Scrutor 6.1.0 corre sobre .NET 11 rc.1: la nota `[ASSUMPTION]` de la tabla Stack de la arquitectura queda confirmada; cambiar esa redacción es suyo.
  - Diferencia con Cypma.Cop: allá los decoradores se registran con `Decorate` en `Program.cs`; aquí se aplican al construir el contenedor para alcanzar los handlers del arnés.
- Riesgos: producción no tiene comandos todavía, así que el decorador solo se ejerce con los tipos de prueba; `IValidatableObject.Validate` solo corre si todos los atributos pasan; objetos anidados y servicios en `ValidationContext` no se validan (README).
