# Registro del prototipo de arranque

Entrada 1 de la épica Plataforma base. Corrida del 2026-10-05 en macOS 27.0 (arm64), Docker 29.8.0.

Este prototipo es desechable. No es la solución real ni su semilla: vive fuera de `src/` y nadie debe copiar de aquí sin leer la sección "Lo que no se debe copiar".

## Resumen

| Pregunta | Respuesta | Condición |
| --- | --- | --- |
| 1. ¿Arrancan juntos .NET 11, Wolverine 6 y el AppHost de Aspire 13? | **Sí, con condición** | Hay que agregar el paquete `WolverineFx.RuntimeCompilation`, que no está en la tabla Stack. |
| 2. ¿El middleware de DataAnnotations de Wolverine entrega `Resultado` inválido? | **No.** Lanza excepción. | Un middleware propio de 15 líneas sí lo entrega, y se probó. |
| 3. ¿Un `EditForm` se liga directo a un comando de `Contracts`? | **Sí** | Las tres formas probadas ligan; el `record` con `init` y el posicional no admiten `@bind-Value`. |
| Runtime elegido | **.NET 11** (11.0.0-rc.1.26425.128) | No hubo que retroceder a .NET 10. |

## Versiones resueltas

Todas son las de la sección Stack de la arquitectura; ninguna tuvo que sustituirse.

| Pieza | Versión que corrió | De dónde sale el dato |
| --- | --- | --- |
| SDK de .NET | 11.0.100-rc.1.26425.128 (local, en `.dotnet/`) | `dotnet --version` dentro de `spikes/arranque/` |
| Runtime | .NET 11.0.0-rc.1.26425.128 | `/diag`, campo `runtime` (`RuntimeInformation.FrameworkDescription`) |
| Aspire AppHost SDK, `Aspire.Hosting.PostgreSQL` | 13.6.0 | `dotnet list Arranque.AppHost package`; el AppHost imprime `Aspire AppHost version: 13.6.0+56f3e9c0…` |
| WolverineFx, `.EntityFrameworkCore`, `.Postgresql`, `.DataAnnotationsValidation` | 6.45.0 | `dotnet list Arranque.Web package`; `/diag` devuelve `wolverine=6.45.0.0` |
| **WolverineFx.RuntimeCompilation** | 6.45.0 | **No está en la tabla Stack.** Ver pregunta 1. |
| EF Core (`Microsoft.EntityFrameworkCore`, `.Relational`) | 10.0.12 | `/diag` devuelve `efcore=10.0.12.0` |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | `dotnet list Arranque.Web package` |
| PostgreSQL | 18.3, imagen `postgres:18.3` | La elige Aspire 13.6.0 por defecto; `select version()` |

Existe WolverineFx 6.46.0 en NuGet; se usó 6.45.0 porque la arquitectura pide 6.45.x.

`Arranque.Contracts` no tiene ningún `PackageReference` ni `FrameworkReference`: usa `Microsoft.NET.Sdk` y DataAnnotations de la biblioteca base (AD-12). `dotnet list Arranque.Contracts package` responde `No packages were found for this framework`.

## Cómo se instaló el SDK y cómo se repite la corrida

El equipo tiene los SDK 6 a 10.0.102 en `/usr/local/share/dotnet`. El de .NET 11 se instaló solo dentro del prototipo; la instalación del sistema no se tocó.

```bash
cd spikes/arranque
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --version 11.0.100-rc.1.26425.128 --install-dir ./.dotnet
```

El 2026-10-05 se usó `--channel 11.0 --quality preview`, que ese día resolvió a `11.0.100-rc.1.26425.128`; para repetir la corrida se usa la forma con `--version`, que es la que coincide con `global.json` (`rollForward: disable`). La forma con `--version` no se corrió.

`global.json` fija esa versión y apunta a `.dotnet` con `sdk.paths`. La carpeta pesa 548 MB y está en `.gitignore` (`.dotnet/`).

Tres cosas que hay que saber para repetirlo:

1. **Hay que estar dentro de `spikes/arranque/`.** `global.json` se busca desde la carpeta actual hacia arriba. Desde la raíz del repositorio, `dotnet --version` responde `10.0.102` y `dotnet build spikes/arranque` falla con `error NETSDK1045: The current .NET SDK does not support targeting .NET 11.0`. El comando del plan `dotnet build spikes/arranque` solo funciona como `cd spikes/arranque && dotnet build`.
2. **`global.json` redirige el SDK, no el runtime.** Compilar funciona con el `dotnet` del sistema, pero los procesos que lanza el AppHost buscan el runtime 11 y el del sistema no lo tiene. Por eso `arrancar.sh` exporta `DOTNET_ROOT` y `PATH` hacia `.dotnet/` antes de `dotnet run --project Arranque.AppHost`.
3. La CLI `aspire` no está instalada; no hizo falta. El AppHost avisa `warning ASPIRE010: … AspireUseCliBundle=false`, y arranca igual.

```bash
cd spikes/arranque
dotnet --version            # 11.0.100-rc.1.26425.128
dotnet build                # Build succeeded. 1 Warning(s) (ASPIRE010), 0 Error(s)
./arrancar.sh               # modo propio por defecto; ./arrancar.sh nativo para el middleware de Wolverine. Deja el AppHost en primer plano
./verificar.sh              # en otra terminal; sale 0 en modo propio
```

`verificar.sh` pide la página, toma la cookie y el token antiforgery, envía el formulario como lo haría el navegador y cuenta filas con `psql` dentro del contenedor. Su código de salida es el número de filas que fallan; si no hay contenedor `postgres-*` en ejecución sale con 99. La contraseña de PostgreSQL la genera Aspire en cada corrida y el script la lee dentro del contenedor; no hay secretos en el repositorio.

## Pregunta 1 — ¿Arrancan juntos?

**Sí, con una condición de configuración.**

### Primera corrida: la web no arrancó

Con exactamente los paquetes de la tabla Stack, el AppHost levantó PostgreSQL y lanzó la web sobre .NET 11; EF Core creó la tabla; y Wolverine se negó a arrancar. Registro de la web (lo guarda DCP en su carpeta temporal `aspire-dcp*/…_err`):

```text
Unhandled exception. System.InvalidOperationException: Wolverine is running in TypeLoadMode.Dynamic,
which compiles handler/middleware code at runtime, but no IAssemblyGenerator (Roslyn) is registered.
Core WolverineFx no longer ships the runtime compiler. Either add the 'WolverineFx.RuntimeCompilation'
NuGet package (it auto-registers when referenced, or call opts.UseRuntimeCompilation() in
UseWolverine(...)), or pre-generate code with 'dotnet run -- codegen write' and set
opts.CodeGeneration.TypeLoadMode = TypeLoadMode.Static.
   at Wolverine.Runtime.WolverineRuntime.logCodeGenerationConfiguration() … WolverineRuntime.HostService.cs:line 509
```

No es un fallo de .NET 11: es un cambio de empaquetado de Wolverine 6, que ya no trae el compilador en el paquete base. La configuración lo arregla, así que según la regla del plan no se retrocede a .NET 10. Se agregó `WolverineFx.RuntimeCompilation` 6.45.0 y nada más.

Ejemplo de lo que significa: Wolverine no llama al handler por reflexión; al arrancar escribe y compila una clase por comando (aquí `Internal.Generated.WolverineHandlers.RegistrarNotaHandler1832391897`). Ese paquete es el compilador. La otra salida que ofrece el mensaje es generar ese código antes y versionarlo (`TypeLoadMode.Static`); **no se probó**.

### Segunda corrida: arriba

```text
$ ./arrancar.sh propio
info: Aspire.Hosting.DistributedApplication[0]
      Aspire AppHost version: 13.6.0+56f3e9c0d216c0c7069dabb49dd0464e4827744f
      Now listening on: http://localhost:15180
      Distributed application started. Press Ctrl+C to shut down.

$ kubectl --kubeconfig <carpeta de DCP>/kubeconfig get executables,containers -o custom-columns=NAME:.metadata.name,STATE:.status.state
NAME                        STATE
web-uprvgksu                Running
aspire-dashboard-dupkhbqj   Running
postgres-ejptaevm           Running

$ curl -s -o /dev/null -w 'GET / -> %{http_code}\n' http://localhost:5180/
GET / -> 200

$ curl -s http://localhost:5180/diag
{"runtime":".NET 11.0.0-rc.1.26425.128","wolverine":"6.45.0.0","efcore":"10.0.12.0","modo":"propio", …}
```

Qué quedó ejercitado en esa corrida, no solo compilado:

- El AppHost con destino `net11.0`, el recurso PostgreSQL, `AddDatabase` y `WaitFor`: la web arrancó después de la base y recibió su cadena de conexión.
- La generación y compilación de código de Wolverine en tiempo de ejecución sobre .NET 11.
- `PersistMessagesWithPostgresql`: Wolverine creó sus 8 tablas en el esquema `wolverine`.
- `UseEntityFrameworkCoreTransactions` con `AutoApplyTransactions`, y EF Core 10.0.12 con Npgsql 10.0.3 corriendo sobre el runtime 11.

Lo que **no** se ejercitó: outbox con eventos, mensajes programados, `ServiceDefaults` y OpenTelemetry, y ningún proyecto de pruebas. Tampoco HTTPS: el AppHost corrió por HTTP con `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` porque no hay certificado de desarrollo de confianza en el equipo (`warn: No trusted Aspire development certificate was found`).

Es una candidata (RC 1). `[ASSUMPTION]` La versión definitiva de noviembre se comporta igual; no se puede comprobar hoy.

## Pregunta 2 — ¿El fallo de validación llega como `Resultado`?

**No con el middleware de Wolverine. Sí con uno propio.**

### Nativo (`./arrancar.sh nativo`, `opts.UseDataAnnotationsValidation()`)

Quien invoca `bus.InvokeAsync<Resultado>(comando)` recibe una excepción, y Wolverine además la escribe como error en el registro. `./verificar.sh` sale con 2:

```text
== Fila: Comando válido (forma A, clase con set)
  PASA   válido / (clase-set): fila nueva (3 -> 4)
== Fila: Comando inválido (forma A)
  FALLA  inválido / (requerido vacío): http=200 estado='excepcion' error-en-Comando.Texto=''
         excepcion='Wolverine.DataAnnotationsValidation.Internals.ValidationException'
         filas 4 -> 4 handler 4 -> 4 errores-en-log 4 -> 5
  FALLA  inválido / (más de 20 caracteres): … excepcion='Wolverine.DataAnnotationsValidation.Internals.ValidationException'
         filas 4 -> 4 handler 4 -> 4 errores-en-log 5 -> 6
RESULTADO: 2 fila(s) fallan
exit=2
```

Registro de la web en ese envío:

```text
fail: Arranque.Contracts.RegistrarNota
      Invocation of Arranque.Contracts.RegistrarNota failed!
      Wolverine.DataAnnotationsValidation.Internals.ValidationException: Validation failure on: Arranque.Contracts.RegistrarNota
         at Wolverine.DataAnnotationsValidation.Internals.FailureAction`1.Throw(T message, ICollection`1 failures) … FailureAction.cs:line 14
         at Internal.Generated.WolverineHandlers.RegistrarNotaHandler1832391897.HandleAsync(MessageContext context, CancellationToken cancellation)
```

Lo que sí hace bien el nativo: el handler no corre y no se escribe ninguna fila. La página respondió 200 solo porque el prototipo atrapa la excepción para poder mostrarla; sin ese `catch` sería una excepción no manejada.

`[ASSUMPTION]` El único punto de extensión del paquete es `IFailureAction<T>`, cuyo método es `void Throw(T message, ICollection<ValidationResult> failures)`: podría cambiar *qué* excepción se lanza, no devolver un valor. Esto se leyó en el código fuente del paquete (commit `d1b9208`) y **no se corrió**: no se probó ningún `IFailureAction<T>` propio.

### Propio (`./arrancar.sh propio`, `Arranque.Web/ValidacionPropia.cs`)

Un método `Before(IComando)` registrado con `opts.Policies.ForMessagesOfType<IComando>().AddMiddleware(typeof(ValidacionPropia))`. Si el comando no pasa `Validator.TryValidateObject`, devuelve `(HandlerContinuation.Stop, [Resultado.Invalido(errores)])`; Wolverine detiene la cadena y entrega ese `Resultado` como respuesta de `InvokeAsync<Resultado>`. `./verificar.sh` sale con 0:

```text
== Fila: Arranque
  PASA   GET / -> 200
  runtime=.NET 11.0.0-rc.1.26425.128 wolverine=6.45.0.0 efcore=10.0.12.0 modo-de-validacion=propio
  contenedor=postgres-ejptaevm PostgreSQL 18.3
  tablas de wolverine: 8
== Fila: Comando válido (forma A, clase con set)
  PASA   válido / (clase-set): fila nueva (0 -> 1)
== Fila: Comando inválido (forma A)
  PASA   inválido / (requerido vacío): http=200 estado='invalida' error-en-Comando.Texto='El texto es obligatorio.'
         excepcion='' filas 1 -> 1 handler 1 -> 1 errores-en-log 0 -> 0
  PASA   inválido / (más de 20 caracteres): http=200 estado='invalida'
         error-en-Comando.Texto='El texto no puede pasar de 20 caracteres.' excepcion='' filas 1 -> 1 handler 1 -> 1 errores-en-log 0 -> 0
== Observación: formas B (record con init) y C (record posicional)
  OBS    válido /init (record-init): fila nueva (1 -> 2)
  OBS    inválido /init (requerido vacío): http=200 estado='invalida' error-en-Comando.Texto='El texto es obligatorio.' … errores-en-log 0 -> 0
  OBS    válido /posicional (record-posicional): fila nueva (2 -> 3)
  OBS    inválido /posicional (requerido vacío): http=200 estado='invalida' error-en-Comando.Texto='El texto es obligatorio.' … errores-en-log 0 -> 0
== Fila: sin excepción no manejada
  errores (Error/Critical) en el registro de la web desde el arranque: 0
RESULTADO: todas las filas pasan
exit=0
```

Consulta directa a PostgreSQL después de esa corrida (3 envíos válidos, 4 inválidos):

```text
$ docker exec postgres-ejptaevm sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" psql -U postgres -d arranque -c "select count(*) from arranque.notas"'
 count
-------
     3
```

El error llega con el nombre de la propiedad del comando (`Texto`) y la página lo pinta junto al campo: `<div class="validation-message" data-valmsg-for="Comando.Texto">El texto es obligatorio.</div>`.

Límites de lo probado:

- El middleware propio se aplica por un marcador (`IComando`, que aquí hace el papel de `ICommand<T>`). Un mensaje sin el marcador no se valida.
- Solo se probó con `InvokeAsync<Resultado>` en línea. `[ASSUMPTION]` Con un comando que llega por cola el `Resultado` inválido se trataría como mensaje en cascada; no se probó porque el piloto despacha los comandos desde la interfaz.
- Los errores de una regla a nivel de clase (sin `MemberNames`) saldrían con campo vacío; no se probó.

## Pregunta 3 — ¿`EditForm` ligado al comando?

**Sí.** Con `[SupplyParameterFromForm]` en SSR estático, sin `blazor.web.js`, el comando de `Contracts` es el modelo del formulario y es el mismo objeto que se entrega a `IMessageBus`. `[ASSUMPTION]` Con el `ISender` de la arquitectura sería igual; el prototipo no tiene `ISender`. No hizo falta un modelo intermedio.

Segunda mitad, la forma que exige. Las tres ligan en las dos corridas (filas `OBS` de envío válido, con su fila nueva). El error por campo en las formas B y C solo se vio en modo `propio`; en modo `nativo` sus envíos inválidos salieron `OBS NO FUNCIONA`, con la misma excepción que la forma A:

| Forma | Liga desde el formulario | `@bind-Value` | Nota |
| --- | --- | --- | --- |
| A. Clase, propiedades `set`, constructor sin parámetros (`RegistrarNota`) | Sí | Sí | La única que admite la sintaxis normal de Blazor. |
| B. `record` con `init`, constructor sin parámetros (`RegistrarNotaInit`) | Sí | No | Se escribe `Value="@Comando.Texto" ValueExpression="() => Comando.Texto"`. |
| C. `record` posicional, sin constructor sin parámetros (`RegistrarNotaPosicional`) | Sí | No | Igual que B. Los atributos necesitan `[property: Required]`. |

Sobre `@bind-Value` en B: se probó y no compila, `error CS8852: Init-only property or indexer 'RegistrarNotaInit.Texto' can only be assigned…`, porque `@bind-Value` genera una asignación a la propiedad. En C no se compiló la variante; `[ASSUMPTION]` falla igual, por la misma razón.

Condición común a las tres: las páginas del prototipo no usan `<DataAnnotationsValidator />`. Envían siempre y pintan los errores que vuelven en el `Resultado`, con un `ValidationMessageStore` (`Components/Envio.cs`). Así el servidor es quien valida, como pide AD-12.

**No se probó** la validación en el navegador de AD-12 (los atributos estándar validados también en la página estática), que es lo que se perdería al retroceder a .NET 10. El HTML que salió no trae atributos `required` ni `data-val-*` en el `<input>`; queda para la entrada que construya el primer formulario real.

## Hallazgo fuera de las tres preguntas

Cuando una invocación falla, Wolverine escribe en el registro el mensaje como texto (`[ASSUMPTION]` con su `ToString()`; se dedujo de la salida, no del código). Un `record` imprime sus propiedades; una clase, solo su nombre. De la corrida en modo nativo:

```text
Arranque.Contracts.RegistrarNota: Invocation of Arranque.Contracts.RegistrarNota failed!
Arranque.Contracts.RegistrarNotaInit: Invocation of RegistrarNotaInit { Texto =  } failed!
Arranque.Contracts.RegistrarNotaPosicional: Invocation of RegistrarNotaPosicional { Texto = nota record-posicional } failed!
```

La tercera línea es de una corrida anterior del script, cuando el texto de prueba medía 22 caracteres y por eso fallaba la validación; el texto se acortó después.

Ejemplo de por qué importa: si `ConfirmarReserva` fuera un `record` con nombre, teléfono y motivo de consulta, cualquier fallo de su handler dejaría esos datos en el registro, que es justo lo que AD-23 prohíbe. Con el middleware propio la validación ya no pasa por ese camino, pero cualquier otra excepción del handler sí. No se decide nada aquí; se anota para quien defina la forma de los comandos.

## Lo que no se debe copiar

- **`EnsureCreated` al arrancar** (`Arranque.Web/Program.cs`). Contradice AD-25 a propósito: aquí no existe `MigrationService`, que es la entrada 3. La aplicación real nunca crea ni migra su esquema al arrancar.
- El handler, el `DbContext` y la página viven todos en `Arranque.Web`. No hay capas, puertos ni módulos.
- `Diagnostico` y `/diag` existen solo para que `verificar.sh` pueda contar ejecuciones del handler y errores del registro.
- El `catch (Exception)` de `Components/Envio.cs` existe solo para mostrar qué recibe quien invoca.
- `Resultado` es un mínimo de dos campos, no el del Kernel.

## Corrida en .NET 10

No se hizo. La regla del plan la pide solo si .NET 11 falla por una causa que la configuración no arregla, y el único fallo se arregló agregando un paquete. `Directory.Build.props` deja el destino en una sola propiedad (`DestinoArranque`), pero esa ruta no se probó.

## Líneas `Decision` propuestas

Son propuestas. Las asienta Gerardo; este registro no modifica la arquitectura, la épica ni `tickets.toml`.

- **Decision (runtime):** la solución se crea sobre .NET 11. Wolverine 6.45.0, el AppHost de Aspire 13.6.0, EF Core 10.0.12 y Npgsql 10.0.3 arrancaron y atendieron un comando de punta a punta sobre .NET 11.0.0-rc.1.26425.128. No se retrocede a .NET 10.
- **Decision (pregunta 1):** se agrega `WolverineFx.RuntimeCompilation` 6.45.x a la tabla Stack, referenciado por el host. Sin él Wolverine 6 no arranca en modo dinámico. Queda abierto, para cuando haya despliegue, si producción usa código pregenerado (`TypeLoadMode.Static`) en su lugar.
- **Decision (pregunta 2):** con su configuración por defecto (`opts.UseDataAnnotationsValidation()`), el middleware de DataAnnotations de Wolverine lanza `ValidationException` y no entrega `Resultado`. Un middleware propio de Wolverine, aplicado a todo mensaje que implemente el marcador de comando, sí devuelve `Resultado` inválido con errores por nombre de propiedad y detiene la cadena; eso es lo que se corrió. Se propone que la validación antes del handler la haga ese middleware propio. El punto de extensión `IFailureAction<T>` del paquete no se probó; sacar `WolverineFx.DataAnnotationsValidation` del Stack es una propuesta que depende de eso. La propuesta cambia la redacción de AD-12 ("con el middleware de DataAnnotations de Wolverine"), que es una invariante del diseño: la cambia Gerardo.
- **Decision (pregunta 3):** los formularios en SSR estático se ligan directo al comando de `Contracts` con `[SupplyParameterFromForm]`; no hay modelos de formulario aparte. `[ASSUMPTION]` Los comandos que se ligan a un formulario se escriben como clase con propiedades `set`, que es la única forma que admite `@bind-Value` y la que no imprime sus datos en el registro; las otras dos formas también ligan, así que es una elección y no una restricción.
