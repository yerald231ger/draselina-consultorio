# Sistema de citas — Dra. Selina Medina

Monolito modular sobre .NET 10. La arquitectura está en
`_bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md`.

## Requisito: el SDK de .NET 10

`global.json` pide el SDK 10.0.402 o una banda posterior de .NET 10 (`rollForward: latestFeature`), instalado en el
sistema. No hace falta un SDK local.

## Compilar y probar

```bash
./dotnet.sh --version   # 10.0.402 o posterior
./dotnet.sh build       # 0 errores, 0 advertencias (las advertencias son errores)
./dotnet.sh test        # todas las pruebas; necesita Docker en ejecución
```

`./dotnet.sh` es el `dotnet` del sistema; se conserva para que los comandos de este documento sigan funcionando.

## Pruebas

`./dotnet.sh test` corre los cuatro proyectos de `tests/`. No hace falta el AppHost arriba ni ningún otro comando.

- `Kernel.Tests`, `Domain.Tests`, `ArchitectureTests` y `UI.Tests` no usan base de datos ni Docker.
- `IntegrationTests` corre contra PostgreSQL real y **necesita Docker en ejecución**. Sin Docker estas pruebas
  fallan con un mensaje que lo dice; no se omiten.

### El arnés de integración

`tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs`. Una vez por corrida:

1. Levanta PostgreSQL (`postgres:18.3`, la misma versión mayor que el AppHost) en un contenedor de Testcontainers.
2. Migra la base ejecutando el `MigrationService` real como proceso, igual que el AppHost. Si sale con un código
   distinto de 0, la corrida falla y muestra el código y la salida del proceso.
3. Crea el esquema `pruebas` con las tablas de los tipos de prueba.
4. Al terminar, borra el contenedor de PostgreSQL. El contenedor de limpieza de Testcontainers
   (`testcontainers-ryuk-*`) sigue listado unos segundos más y desaparece solo.

La base es una sola para toda la corrida y la comparten todas las pruebas, que corren en paralelo: cada prueba
crea los datos que necesita con valores propios y no depende del orden ni de lo que dejó otra.

### Escribir una prueba de integración

La clase recibe el arnés por su constructor; no hay que declarar nada más:

```csharp
public sealed class MiPruebaTests(Arnes arnes)
{
    [Fact]
    public async Task Hace_algo_contra_la_base()
    {
        var cadena = arnes.CadenaDeConexion;                // la base de la corrida, ya migrada
        await using var web = arnes.ArrancarWeb();          // la web en memoria contra esa base
        using var cliente = web.CreateClient();
        // ...
    }
}
```

Lo que da el arnés:

| Miembro | Para qué |
| --- | --- |
| `CadenaDeConexion` | La base migrada: esquemas `agenda`, `pacientes`, `notificaciones` y `pruebas`. |
| `ArrancarWeb()` / `ArrancarWeb(cadena)` | La web con su `Program` real, en memoria, contra la base migrada o contra la que se indique. Lleva además los tipos de prueba, dados de alta con `AddPruebas`. |
| `CrearBaseVaciaAsync()` | Una base nueva sin migrar en el mismo servidor; devuelve su cadena de conexión. |
| `CrearContextoDePruebas()` / `CrearContextoDePruebas(cadena)` | Un `PruebasDbContext` sobre el esquema `pruebas` de la base de la corrida, o de otra. |
| `CrearBaseMigradaAsync()` | Una base nueva, propia de la prueba, igual a la de la corrida: migrada y con el esquema `pruebas`. |
| `ArrancarWebSinEntregaAutomatica(cadena, reloj)` | La web sin el servicio que entrega eventos y con un `RelojDePrueba`: la prueba da cada vuelta y mueve el tiempo. |
| `Arnes.DarUnaVueltaDeEntregaAsync(web)` | Da una vuelta de entrega de eventos en esa web; devuelve cuántos eventos intentó. |
| `Arnes.Registros(web)` | Lo que esa web ha registrado (avisos, errores), para afirmar que algo se registró. |

**Dónde poner un tipo de prueba.** Un tipo que solo existe para probar (una entidad, un comando, una consulta o
un handler de prueba) vive en `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/`. Si tiene tabla, se agrega
su `DbSet` a `Pruebas/PruebasDbContext.cs`: el arnés crea la tabla en el esquema `pruebas` al arrancar la corrida,
sin migraciones. Ningún esquema de producción recibe tablas de prueba. `Apunte` es el ejemplo mínimo. Un comando
de prueba se escribe como dice "Casos de uso".

Para migrar otra base por el mismo camino: `await MigrationService.EjecutarAsync(cadena)`.

**Probar la entrega de eventos.** Todas las webs en memoria de la corrida entregan los eventos de la base de la
corrida, cada una por su cuenta: una prueba no puede afirmar ahí cuántas veces ni cuándo llegó un evento. La que
lo necesita (reintentos, orden, dos instancias) usa `EscenarioDeEntrega`: una base propia, una web sin entrega
automática y un reloj que solo ella mueve. Ninguna prueba espera minutos reales.

```csharp
await using var escenario = await EscenarioDeEntrega.CrearAsync(arnes);
var eventoId = await escenario.PublicarAsync(id => new EventoDePrueba(id, FallasAntesDeEntregar: 1), claveDeOrden: Guid.CreateVersion7());

await escenario.VueltaAsync();                          // el handler falla
escenario.Reloj.Avanzar(TimeSpan.FromMinutes(1));
await escenario.VueltaAsync();                          // ahora llega

var fila = await escenario.EventoAsync(eventoId);       // la fila de pruebas.outbox
var rastro = await escenario.EntregasAsync(eventoId);   // lo que dejó cada handler, en orden
```

Lo que una prueba afirma sale de la base: la fila del outbox y el rastro (`EntregaRecibida`) que cada handler de
prueba deja al recibir un evento.

## Casos de uso

Todo caso de uso es un comando o una consulta con exactamente un handler, que devuelve `Resultado<T>` (AD-10). No
hay mediador ni librería de despacho: quien necesita un caso de uso pide su handler al contenedor.

Todavía no hay ningún caso de uso en `src/`. Los ejemplos de esta sección son el código real del comando de
prueba `Anotar`, que vive en `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/`. Al final de cada parte se
dice qué cambia en un módulo.

### La forma

El comando es una clase con propiedades `set` (nunca `record`) que implementa `ICommand<T>`; la consulta
implementa `IQuery<T>`. `T` es el valor que trae el `Resultado` en el éxito. Los códigos de fallo son constantes,
un conjunto cerrado. El comando de prueba, en `Pruebas/Anotar.cs`:

```csharp
public sealed class Anotar : ICommand<Guid>
{
    public Guid Id { get; set; }

    public string Texto { get; set; } = "";

    public Desenlace Desenlace { get; set; } = Desenlace.Exito;   // solo de la prueba: cómo termina su handler
}

public static class CodigosDeAnotacion
{
    public const string TextoRepetido = "anotacion.texto_repetido";
    public const string RechazadaAlFinal = "anotacion.rechazada_al_final";
    public const string NoExiste = "anotacion.no_existe";
}
```

El handler implementa `ICommandHandler<TComando, T>` o `IQueryHandler<TConsulta, T>` (las dos en
`BuildingBlocks/Contracts/`). Tiene un solo método:

```csharp
Task<Resultado<T>> HandleAsync(TComando comando, CancellationToken cancellationToken);
```

Un fallo esperado se devuelve como `Resultado.Fallo(codigo)`, con un código del conjunto cerrado; nunca se lanza
ni se comunica con texto. El handler llega a la base solo por puertos (AD-4): no recibe un `DbContext`.

**En un módulo:** el comando, la consulta y los códigos viven en `Modules/<Módulo>/Contracts/`, y el handler en
`Modules/<Módulo>/Features/`.

### Cómo se pide un handler

Por su interfaz, al contenedor. Quien llama nunca conoce la clase que la implementa. Así lo piden las pruebas
(`CasoDeUsoTests.cs`), dentro de un ámbito:

```csharp
await using var web = arnes.ArrancarWeb();
await using var ambito = web.Services.CreateAsyncScope();
var handler = ambito.ServiceProvider.GetRequiredService<ICommandHandler<Anotar, Guid>>();

var resultado = await handler.HandleAsync(comando, TestContext.Current.CancellationToken);
// resultado.EsExito, resultado.Valor; si no: resultado.Forma, resultado.Codigo, resultado.Errores
```

**En la interfaz:** un componente pide lo mismo, la interfaz del handler, con `@inject`.

Pedir el handler de un comando que no tiene lanza la excepción del contenedor
(`InvalidOperationException: No service for type '...ICommandHandler`2[...]' has been registered`), que nombra el
tipo del comando. La web no comprueba al arrancar "un caso de uso, un handler": lo comprueba una prueba (ver
"Un handler por caso de uso").

### Cómo registra un módulo sus handlers

Uno por línea y por su interfaz; nada se descubre por reflexión ni por nombre. El registro de los tipos de
prueba, en `Pruebas/Registro.cs`:

```csharp
public static IServiceCollection AddPruebas(this IServiceCollection servicios, string cadenaDeConexion) =>
    servicios
        .AddDbContext<PruebasDbContext>(opciones => opciones.UseNpgsql(cadenaDeConexion))
        .AddScoped<ITransaccionDePruebas, TransaccionDePruebas>()
        .AddScoped<IOutboxDePruebas, OutboxDePruebas>()
        .AddScoped<IAnotaciones, Anotaciones>()
        .AddScoped<IEntregasRecibidas, EntregasRecibidas>()
        .AddSingleton<LlamadasDeInscribir>()
        .AddScoped<ICommandHandler<Anotar, Guid>, AnotarHandler>()
        .AddScoped<ICommandHandler<Inscribir, Guid>, InscribirHandler>()
        .AddScoped<IQueryHandler<LeerAnotacion, AnotacionLeida>, LeerAnotacionHandler>()
        .AddSingleton<LlamadasDeAdministracion>()
        .AddScoped<ICommandHandler<Administrar, Actor>, AdministrarHandler>()
        .AddScoped<IQueryHandler<LeerAdministracion, Actor>, LeerAdministracionHandler>()
        .AddScoped<ICommandHandler<PreguntarActor, Actor>, PreguntarActorHandler>()
        .AddScoped<IEventHandler<AnotacionHecha>, AlHacerseUnaAnotacion>()
        .AddScoped<IEventHandler<EventoDePrueba>, EscuchaDePrueba>()
        .AddScoped<IEventHandler<EventoConDosHandlers>, PrimeraEscucha>()
        .AddScoped<IEventHandler<EventoConDosHandlers>, SegundaEscucha>();
```

**En un módulo:** las líneas `AddScoped` de sus handlers y de sus puertos van en su
`Modules/<Módulo>/Infrastructure/Registro.cs`, dentro de `AddAgenda`, `AddPacientes` o `AddNotificaciones`, que ya
registran el contexto y el puerto de transacción. El host solo llama a esos tres métodos. Los handlers de evento
(`IEventHandler<TEvento>`) se registran igual; ver "Eventos entre módulos".

### La validación de forma

La forma de la entrada se declara con DataAnnotations sobre el comando (AD-12): `[Required]`, `[StringLength]`,
`[EmailAddress]`, `[Range]`... y, para una regla que cruza campos, `IValidatableObject`. El mensaje que ve el
usuario lo pone el atributo (`ErrorMessage`), en español; nadie lo traduce después. **Todo atributo de
validación de un comando lleva su `ErrorMessage`**: sin él, .NET da su mensaje en inglés ("The Nombre field is
required."). Lo hace cumplir `MensajesDeValidacionTests`, que recorre todos los comandos. Los atributos no llevan
reglas de negocio: esas viven en el agregado. El comando de prueba `Inscribir`, en `Pruebas/Inscribir.cs`:

```csharp
public sealed class Inscribir : ICommand<Guid>, IValidatableObject
{
    [Required(ErrorMessage = MensajeNombreRequerido)]
    [StringLength(50, ErrorMessage = MensajeNombreLargo)]
    public string Nombre { get; set; } = "";

    [Required(ErrorMessage = MensajeCorreoRequerido)]
    [EmailAddress(ErrorMessage = MensajeCorreoMalFormado)]
    [StringLength(40, ErrorMessage = MensajeCorreoLargo)]
    public string Correo { get; set; } = "";

    [Range(18, 120, ErrorMessage = MensajeEdadFueraDeRango)]
    public int Edad { get; set; }
    // ...
}
```

El handler no valida nada de esto: un decorador del host (`ValidacionDeComandos.cs`) envuelve a todo handler de
comando y valida antes de llamarlo.

- **Si el comando no pasa**, el handler no se ejecuta y vuelve `Resultado` con `Forma = Invalido`. `Errores`
  lleva cada mensaje bajo el nombre de su propiedad, tal cual lo da el atributo; un campo que rompe dos atributos
  lleva los dos mensajes. No hay excepción. Por ejemplo, con `Nombre = ""`, `Correo = "no-es-correo"` y
  `Edad = 5`:

  ```text
  Errores["Nombre"] = ["El nombre es obligatorio."]
  Errores["Correo"] = ["El correo no tiene la forma de un correo."]
  Errores["Edad"]   = ["La edad va de 18 a 120."]
  ```

- **Un error del objeto** (de `IValidatableObject.Validate`, sin nombre de propiedad) va bajo la propiedad `""`,
  la misma convención que Blazor usa para los errores del modelo. `Validate` solo corre si todos los atributos
  pasaron. Un error sin mensaje lleva "El valor no es válido.".
- **Si pasa**, el decorador llama al handler y devuelve su `Resultado` sin tocarlo. Un comando sin atributos
  ni `IValidatableObject` siempre pasa.
- **Las consultas no se validan**, ni los eventos.
- **Límite conocido:** DataAnnotations valida las propiedades del comando, no las de un objeto anidado ni los
  elementos de una colección. Un `[Required]` dentro de una clase que el comando lleva como propiedad no se revisa.
- **Límite conocido:** el `ValidationContext` se crea sin proveedor de servicios: un atributo o un `Validate` que
  llama a `validationContext.GetService(...)` recibe `null`. La validación de forma no consulta servicios.

**Dónde se agregan decoradores.** La lista está en un solo lugar, `DecoradoresDeHandlers.Aplicar`
(`DecoradoresDeHandlers.cs`, raíz de `Web`), con Scrutor. Se aplica al construir el contenedor
(`builder.Host.UseServiceProviderFactory` en `Program.cs`), no con un `Decorate` suelto en `Program.cs`: así
alcanza a todo handler registrado, también a los que el arnés de pruebas registra después de `Program.cs`, y cada
decorador envuelve una sola vez. Un módulo registra sus handlers como siempre; no hace nada para quedar envuelto.
Hoy son dos; de afuera hacia adentro, un comando pasa por el rechazo sin Administrador, la validación y el handler;
una consulta, por el rechazo y el handler.

### El actor y la administración

**Quién actúa** (AD-22) lo dice un solo servicio del host, `IActorActual` (`Kernel/IActorActual.cs`), con uno de los
tres valores de `Actor`. Un handler que necesita su actor (para la bitácora, por ejemplo) lo pide por su
constructor; nunca lo averigua a su manera:

```csharp
public sealed class CancelarCitaHandler(IActorActual actor, ...) : ICommandHandler<CancelarCita, Guid>
{
    public async Task<Resultado<Guid>> HandleAsync(CancelarCita comando, CancellationToken cancellationToken)
    {
        var quien = await actor.ObtenerAsync();   // Administrador, Paciente o Sistema
        // ...
```

La implementación es `ActorDelHost.cs` (raíz de `Web`), una por ámbito: el de la petición en SSR estático o el del
circuito en Interactive Server. Lee el usuario del `AuthenticationStateProvider` estándar de Blazor de ese ámbito.

| Ámbito | Actor |
| --- | --- |
| Usuario autenticado (`Identity.IsAuthenticated`) | `Administrador`: no hay registro público, así que quien tiene sesión es la administradora |
| Página Blazor (SSR) o circuito sin usuario | `Paciente` |
| Declarado `Sistema` por quien creó el ámbito | `Sistema` |
| Ninguno de los anteriores | No hay actor: `ObtenerAsync` lanza `InvalidOperationException` |

- **`Sistema` se declara, no se deduce.** Lo declara quien crea el ámbito, desde el host: pide `ActorDelHost` al
  contenedor y llama a `DeclararSistema()` antes de pedir el handler. No está en `IActorActual`: un handler no puede
  declararse `Sistema`. Un módulo no ve `ActorDelHost` (es de la raíz de `Web`, que las reglas de dependencia le
  prohíben); cómo obtiene un ámbito `Sistema` el trabajo programado de un módulo (Recordatorio y Agenda del día, en
  Notificaciones según AD-20) se decide con el primer trabajo programado. Hoy no hay ninguno; solo lo usan las
  pruebas.
- **La entrega de eventos no declara `Sistema`.** Una política que reacciona a un evento usa el actor que viene en
  el evento: las cancelaciones elegidas al guardar o borrar una custom quedan a nombre del `Administrador`.
- **Un ámbito sin actor** (creado a mano, que no viene de una página Blazor ni de un circuito y no se declaró
  `Sistema`) lanza al pedir el actor. Es un error de programación, no un `Resultado`. Un endpoint de Minimal API
  no fija el estado de autenticación, así que pedir el actor ahí lanza (y una consulta marcada lanza en vez de
  volver `NoAutorizado`); cómo obtiene su actor un endpoint así se decide con el primer endpoint.

**Un caso de uso de administración** se marca con `IRequiereAdministrador` (`BuildingBlocks/Contracts/`), en su
clase de `Contracts`:

```csharp
public sealed class GuardarAgendaCustom : ICommand<Guid>, IRequiereAdministrador { ... }
```

Un decorador del host (`RechazoSinAdministrador.cs`) envuelve a todo handler de comando y de consulta. Si el caso
de uso está marcado y el actor no es `Administrador` (también si es `Sistema`), devuelve `Resultado` con
`Forma = NoAutorizado`, sin errores ni código, y el handler no corre. Va por fuera de la validación: un comando
marcado e inválido sin sesión vuelve `NoAutorizado`, no `Invalido`. Un caso de uso sin marca pasa directo, sin
pedir el actor. No se confía en que la página esté protegida. Las pruebas fijan el usuario del ámbito como lo fija
Blazor, por `IHostEnvironmentAuthenticationStateProvider.SetAuthenticationState` (`ActorYAdministracionTests`).

Todavía no hay inicio de sesión (ASP.NET Core Identity llega con la épica 5) ni actor `Paciente` por enlace de
gestión.

### Un handler por caso de uso

`UnHandlerPorCasoDeUsoTests` recorre todas las clases que implementan `ICommand<T>` o `IQuery<T>`, en `Web` y en
los tipos de prueba, y pide al contenedor de la web del arnés todos sus handlers: cada caso de uso debe tener
exactamente uno. Si alguno tiene cero o más de uno, la prueba falla y nombra el tipo y cuántos encontró. Un
comando nuevo en un módulo queda cubierto sin tocar la prueba. `ComandoSinHandler` es la única excepción, a
propósito: existe para probar qué pasa al pedir un handler que no existe.

### La regla de la transacción

Un comando, una transacción, un módulo. El handler del comando abre la transacción de su módulo por un puerto,
guarda de forma explícita y la confirma. Toda salida con un `Resultado` no exitoso deshace antes de devolver.
Quien llama al handler nunca abre, confirma ni guarda. Una consulta corre sin transacción y nunca guarda.

El handler del comando de prueba, en `Pruebas/Anotar.cs` (aquí sin las ramas de `Desenlace`, que solo sirven a
las pruebas, y sin el evento que publica antes de confirmar, que está en "Eventos entre módulos"):

```csharp
public sealed class AnotarHandler(ITransaccionDePruebas transaccion, IAnotaciones anotaciones, ...) : ICommandHandler<Anotar, Guid>
{
    public async Task<Resultado<Guid>> HandleAsync(Anotar comando, CancellationToken cancellationToken)
    {
        await using var tx = await transaccion.AbrirAsync(cancellationToken);          // abrir

        if (await anotaciones.ExisteTextoAsync(comando.Texto, cancellationToken))
        {
            await tx.DeshacerAsync(cancellationToken);                                 // deshacer en el fallo
            return Resultado.Fallo(CodigosDeAnotacion.TextoRepetido).Como<Guid>();
        }

        anotaciones.Agregar(new Anotacion { Id = comando.Id, Texto = comando.Texto });
        await anotaciones.GuardarAsync(cancellationToken);                             // guardar, explícito

        await tx.ConfirmarAsync(cancellationToken);                                    // confirmar
        return comando.Id;
    }
}
```

**En un módulo** cambian dos cosas. El handler usa el puerto de transacción de su módulo
(`ITransaccionDeAgenda`, `ITransaccionDePacientes` o `ITransaccionDeNotificaciones`, declarados en `Features` e
implementados en `Infrastructure` sobre su `DbContext`; no hay un tipo compartido entre módulos). Y llega a la
base por el puerto de escritura de su agregado, declarado en `Domain` (AD-4), no por un puerto de prueba como
`IAnotaciones`: el agregado tiene setters privados y se crea y cambia por sus propios métodos, no con un
inicializador como `Anotacion`.

Lo que no se confirma no queda. La transacción se suelta con `await using` al salir del handler; soltarla sin
haber confirmado la deshace. Por eso un handler que lanza, o que termina sin confirmar, no deja nada en la base
ni una transacción abierta, aunque el ámbito del contenedor siga vivo (en administración dura lo que el
circuito). Al deshacer, el contexto del módulo también olvida las entidades que seguía: el siguiente comando del
mismo ámbito no guarda ni lee lo que dejó uno que no se confirmó. Una transacción abierta termina una sola vez:
se confirma o se deshace, nunca las dos cosas.

### Escribir un comando de prueba

Los comandos, consultas y handlers de prueba viven solo en
`tests/DraSelina.Consultorio.IntegrationTests/Pruebas/`, nunca en `src/`. `Anotar` y `LeerAnotacion` son el
ejemplo completo.

1. La clase del comando (o la consulta), sus códigos y su handler, en `Pruebas/`. El handler usa
   `ITransaccionDePruebas` y un puerto propio (`IAnotaciones`), no el contexto.
2. Si guarda algo: la entidad en `Pruebas/` y su `DbSet` en `Pruebas/PruebasDbContext.cs`.
3. El handler y sus puertos se registran en `Pruebas/Registro.cs` (`AddPruebas`). El arnés llama a `AddPruebas`
   al arrancar la web en memoria.
4. La prueba pide el handler al contenedor de la web en memoria, dentro de un ámbito, como en "Cómo se pide un
   handler"; nunca crea la clase.

Un evento de prueba y su handler también viven en `Pruebas/` (`Pruebas/Eventos.cs`), se publican por
`IOutboxDePruebas` y se guardan en `pruebas.outbox`, que el arnés da de alta en la web en memoria con
`AddOutbox<PruebasDbContext>()`.

## Eventos entre módulos

Un módulo le avisa a otro con un evento de integración (AD-3). No hay librería de mensajería: el módulo que
publica guarda el evento en la tabla `outbox` de su propio esquema, en la misma transacción que el cambio, y un
servicio del host lo entrega después a sus handlers. Quien publica no sabe quién escucha.

Hoy solo Agenda tiene outbox (`agenda.outbox`) y todavía no hay eventos de negocio en `src/`. Los ejemplos son
el código real de los tipos de prueba, en `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/`.

### Publicar desde un handler

El handler del comando publica por el puerto de outbox de su módulo (`IOutboxDeAgenda`, en `Features`), con la
transacción ya abierta y antes de confirmar. El evento y el cambio quedan juntos, o ninguno: si el handler
deshace, lanza o no confirma, el evento no queda. Publicar sin una transacción abierta lanza.

```csharp
await using var tx = await transaccion.AbrirAsync(cancellationToken);

anotaciones.Agregar(new Anotacion { Id = comando.Id, Texto = comando.Texto });
await anotaciones.GuardarAsync(cancellationToken);

var eventoId = Guid.CreateVersion7();
var ocurridoEn = reloj.UtcNow;                       // IClock inyectado, nunca la hora del sistema
await outbox.PublicarAsync(
    new AnotacionHecha(eventoId, ocurridoEn, comando.Id), eventoId, claveDeOrden: comando.Id, ocurridoEn, cancellationToken);

await tx.ConfirmarAsync(cancellationToken);
```

Publicar pide cuatro cosas: el evento, su `EventoId` (será el `id` de la fila), su clave de orden y cuándo
ocurrió. El evento es un `record` de `Modules/<Módulo>/Contracts/`, se guarda serializado como JSON y no lleva
datos personales (AD-23). La columna `tipo` guarda el nombre de su clase (`AnotacionHecha`): **el nombre de un
tipo de evento debe ser único en toda la aplicación**. Si dos tipos de evento que tienen handler se llaman
igual, la web no arranca: el catálogo de eventos lanza al arrancar el host. Esa comprobación solo ve los tipos de
evento con algún handler registrado, así que cuidar que los nombres sean únicos le toca a quien escribe el evento.
Si un tipo se renombra, las filas pendientes con el nombre anterior ya no encuentran handler.
Ese nombre es el de la clase concreta del evento, no el de un tipo base ni el de una interfaz: el handler se
registra para el mismo tipo concreto que se publica. Por eso los eventos son `sealed record`.

### Escribir y registrar un handler de evento

El módulo que escucha implementa `IEventHandler<TEvento>` (en `BuildingBlocks/Contracts/`) en su carpeta
`Features`, y lo registra en su `Registro.cs`, uno por línea y por su interfaz:

```csharp
public sealed class AlHacerseUnaAnotacion(IAnotaciones anotaciones, IEntregasRecibidas entregas) : IEventHandler<AnotacionHecha>
{
    public async Task HandleAsync(AnotacionHecha evento, CancellationToken cancellationToken)
    {
        // ...
    }
}

// en el registro del módulo
.AddScoped<IEventHandler<AnotacionHecha>, AlHacerseUnaAnotacion>()
```

- **Debe tolerar recibir el mismo evento más de una vez.** La entrega es "al menos una vez": si otro handler del
  mismo evento falla, o la web se apaga a media entrega, el evento se entrega de nuevo a todos sus handlers. El
  handler deduplica por `EventoId` (AD-3) o hace algo que repetido da el mismo resultado.
- Un evento puede tener varios handlers; se llaman uno tras otro, cada uno en su propio ámbito del contenedor. Si
  el `HandleAsync` de uno lanza, los demás lo reciben igual, y el evento se reintenta. Si un handler no se puede
  construir (su constructor o una dependencia falla), ningún handler de ese evento lo recibe, y el evento se
  reintenta.
- Un handler que cambia datos abre la transacción de su módulo por su puerto, como el handler de un comando. La
  transacción del outbox no es la suya.
- Fallar es lanzar. Un handler que termina sin lanzar cuenta como entregado.
- Un evento sin ningún handler registrado queda `Entregado` y se registra un aviso.
- Un handler tiene 30 segundos (`OpcionesDeEntrega.LimitePorHandler`) para terminar. Al vencerse se cancela su
  `CancellationToken` y la entrega cuenta como fallida: el evento se reintenta según el calendario y la vuelta
  sigue con los demás. Por eso todo handler pasa el `CancellationToken` a lo que llama (correo, base, red).

### Dar de alta el outbox de un módulo

Para que un módulo nuevo publique hacen falta cuatro cosas, las mismas que tiene Agenda:

1. El puerto en `Features` (`IOutboxDe<Módulo>`) y su implementación en `Infrastructure`, sobre el contexto del
   módulo. No hay un tipo compartido entre módulos: cada uno declara lo suyo, como con la transacción.
2. La fila (`FilaDeOutbox`) mapeada en su `DbContext` a la tabla `outbox`, con las nueve columnas de
   `agenda.outbox`, y una migración del módulo que la crea ("Crear una migración").
3. El registro del puerto en su `Registro.cs`.
4. El alta en `Program.cs`, que es lo que le dice al host que ese outbox existe:

```csharp
builder.Services
    .AddSingleton<IClock, RelojDelSistema>()
    .AddEntregaDeEventos()
    .AddOutbox<AgendaDbContext>();
```

El host lee y actualiza el outbox por el contexto de ese módulo, en el esquema por defecto del contexto. El
módulo no usa ningún tipo del host.

### La entrega

`ServicioDeEntregaDeEventos` (host) da una vuelta al arrancar y después una cada 5 segundos. En cada vuelta
entrega todo lo que ya puede entregarse de cada outbox, del más antiguo al más nuevo. Cada evento se entrega en su
propia transacción: aparta la fila con `FOR UPDATE SKIP LOCKED`, llama a los handlers y la marca.

- **Dos instancias.** Dos webs sobre la misma base nunca entregan el mismo evento a la vez.
- **Caídas.** Un evento que quedó `Pendiente` se entrega cuando la web vuelve a arrancar.
- **El servicio nunca tumba la web.** Si no puede leer un outbox (por ejemplo, con la base sin migrar) registra
  el error y lo intenta en la siguiente vuelta.
- **Reintentos.** Si un handler lanza, el evento sigue `Pendiente`, suma un intento y espera: 1 min, 5 min,
  15 min, 1 h, 4 h y 12 h después de cada falla. Al fallar el séptimo intento queda `Fallido`. El calendario está
  en un solo lugar: `CalendarioDeReintentos.cs`.
- **Orden por clave.** Un evento no se entrega mientras haya otro anterior (por `ocurrido_en`) con la misma
  `clave_de_orden` todavía `Pendiente`: los eventos de una misma Cita llegan en el orden en que ocurrieron, aunque
  uno esté esperando un reintento. Los de otras claves siguen su camino.
- **El tiempo** se lee de `IClock`, nunca de la hora del sistema ni de `now()` de la base.
- La fila queda apartada mientras corren sus handlers: un handler lento alarga esa transacción.

### Qué significa `Fallido` y cómo se ve en la tabla

| `estado` | Significa | `intentos` | `proximo_intento_en` | `entregado_en` |
| --- | --- | --- | --- | --- |
| `Pendiente` | Falta entregarlo: es nuevo, o falló y espera su siguiente intento | 0 a 6 | Desde cuándo puede intentarse | nulo |
| `Entregado` | Todos sus handlers terminaron sin lanzar | Los que fallaron antes | El del último intento | Cuándo se entregó |
| `Fallido` | Falló siete veces: nadie lo vuelve a intentar | 7 | El del séptimo intento | nulo |

Un evento `Fallido` es un aviso que no llegó (por ejemplo, un correo de confirmación que no salió). Al quedar
así se registra un error que nombra su `EventoId` y su tipo. Ya no detiene a los siguientes de su clave. Las
filas entregadas y las fallidas se quedan en la tabla. Para verlos:

```sql
select id, tipo, ocurrido_en, intentos from agenda.outbox where estado = 'Fallido' order by ocurrido_en;
```

## Arrancar

Hace falta Docker en ejecución. Un solo comando levanta todo:

```bash
./dotnet.sh run --project src/DraSelina.Consultorio.AppHost
```

El AppHost de Aspire arranca, en este orden:

1. `postgres`: PostgreSQL en un contenedor, con la base `consultorio`. Sin volumen: cada arranque parte de una
   base vacía. La contraseña la genera Aspire en cada arranque; no hay secretos en el repositorio.
2. `mailpit`: Mailpit en un contenedor. Atrapa los correos que la web envía por SMTP (la confirmación de cada
   Cita, épica 4) y los muestra, con sus adjuntos, en su página, que se abre desde el panel de Aspire. Sin volumen:
   cada arranque parte sin correos. La web recibe el servidor por la cadena de conexión `mailpit` y el remitente por
   `Notificaciones__Remitente` (`citas@consultorio.local`). Si Mailpit no responde, la reserva no se detiene: el
   outbox reintenta el correo.
3. `migraciones`: el `MigrationService`. Aplica las migraciones de Agenda, Pacientes y Notificaciones y termina.
4. `web`: la aplicación, en <http://localhost:5080>. Arranca solo si `migraciones` terminó con código 0. La web
   nunca crea ni migra nada (AD-25).

La clave del Enlace de gestión (AD-19) es el parámetro secreto del AppHost `clave-enlace-de-gestion`; el AppHost
se la pasa a la web como `EnlaceDeGestion__Clave`, junto con su propia dirección como `EnlaceDeGestion__UrlBase`.
La misma dirección le llega como `Notificaciones__UrlDelSitio`: a ella llevan los botones de los correos sin Enlace
de gestión ("Agendar otra cita", "Elegir otro horario" a `/reservar`).
Si no se da `Parameters:clave-enlace-de-gestion`, Aspire genera una nueva en cada arranque, así que los enlaces de
gestión cambian entre arranques (como la base, que también parte vacía). Para enlaces estables, da una clave de al
menos 32 caracteres por la variable de entorno `Parameters__clave-enlace-de-gestion` al arrancar el AppHost, o por
user-secrets: la primera vez `./dotnet.sh user-secrets init --project src/DraSelina.Consultorio.AppHost` (agrega un
`UserSecretsId` al proyecto) y después
`./dotnet.sh user-secrets set "Parameters:clave-enlace-de-gestion" "<clave>" --project src/DraSelina.Consultorio.AppHost`.
La clave nunca va en el repositorio.

El panel de Aspire queda en <http://localhost:15080>; la dirección con el token de entrada sale en la consola
(`Login to the dashboard at ...`). Ahí se ven los cuatro recursos con su estado, sus registros y las trazas: pide
una página a la web y aparece su traza `GET /`.

Todo corre por HTTP (`ASPIRE_ALLOW_UNSECURED_TRANSPORT` en el perfil del AppHost) porque el equipo no tiene un
certificado de desarrollo de confianza. La CLI `aspire` no hace falta.

## Verificar el arranque

Con el AppHost arriba, en otra terminal:

```bash
scripts/verificar-arranque.sh   # sale con 0 si las tres filas pasan
```

Comprueba: los tres recursos en su estado y `GET /` con 200; un historial de migraciones en cada esquema
(`agenda`, `pacientes`, `notificaciones`), la tabla `agenda.outbox` y ningún historial en `public`; y el punto de salud `/health` con 200. Necesita
`docker`, `curl` y `kubectl`: con `kubectl` le pregunta al orquestador de Aspire el estado y el código de salida
de cada recurso (no hace falta ningún clúster).

## Base visual

El sitio y la reserva se pintan con Bootstrap, Montserrat y los tokens de
`_bmad-output/initiative-draselina/ux-appointment-booking/DESIGN.md`. Todo se sirve desde
`src/DraSelina.Consultorio.Web/wwwroot/`, por `app.MapStaticAssets()`. Los archivos no se renombran: solo las URLs
que se arman con `@Assets["…"]` (las dos hojas, en `App.razor`) llevan la huella. Las fuentes se piden desde la hoja
con una URL relativa y se sirven sin huella.

| Archivo | Qué es | De dónde sale | Licencia |
| --- | --- | --- | --- |
| `lib/bootstrap/bootstrap.min.css` | Bootstrap 5.3.8, solo el CSS (sin JS) | npm `bootstrap@5.3.8`, `dist/css/bootstrap.min.css`, vía jsDelivr | MIT, `lib/bootstrap/LICENSE` |
| `fonts/montserrat/montserrat-latin-{400,500,600,700}-normal.woff2` | Montserrat, latín, los cuatro pesos de DESIGN.md | npm `@fontsource/montserrat@5.3.0`, `files/`, vía jsDelivr | OFL 1.1, `fonts/montserrat/OFL.txt` |
| `css/consultorio.css` | La hoja propia: `@font-face`, tokens y estilos del layout público | Este repositorio | — |

**Los tokens.** Los colores, tipografías, radios y espaciados de la cabecera YAML de DESIGN.md (`colors`,
`typography`, `rounded`, `spacing`; el bloque `components` no) son variables CSS en `css/consultorio.css`, con
prefijo `--dsm-` y el nombre de su clave: colores `--dsm-primary`, `--dsm-ground`...; tipografías
`--dsm-display-font-size`, `--dsm-eyebrow-letter-spacing`...; radios `--dsm-rounded-lg`; espaciados
`--dsm-spacing-touch-min`. Bootstrap se ajusta a ellos (`--bs-body-bg: var(--dsm-ground)`, `.btn-primary` en vino),
no al revés. En un estilo nuevo, un color, radio, espaciado o valor tipográfico que tiene su `--dsm-*` se escribe con
la variable, nunca con el valor; las medidas de layout sin token (el alto de 54 px del botón, un ancho máximo) pueden
ir escritas:

```css
.mi-tarjeta { background: var(--dsm-surface); border: 1px solid var(--dsm-border); border-radius: var(--dsm-rounded-xl); }
```

Si un valor cambia, cambia primero en DESIGN.md y después en la hoja. `BaseVisualTests` compara cada color de
DESIGN.md con su `--dsm-*` y falla nombrando el que falte o difiera.

**Nada de terceros.** Ninguna página ni hoja pide nada a un CDN ni a Google Fonts: ni `<link>`, ni `<script>`, ni
`url(...)` con `http(s)://`. Para actualizar Bootstrap o la fuente, se descarga la versión nueva de la distribución
oficial, se reemplaza el archivo con su licencia y se actualiza esta tabla.

**Sin script.** `App.razor` no carga `blazor.web.js` ni ningún otro script: el sitio y la reserva van en SSR
estático y cada página llega completa, sin conexión interactiva (AD-11). `BaseVisualTests` lo comprueba.

El layout público es `UI/Layout/LayoutPublico.razor`, el layout por defecto de `Routes.razor`.

## Errores en el formulario

Un comando inválido vuelve como `Resultado` con `Forma = Invalido` y sus mensajes por propiedad ("La validación de
forma"). Dos componentes de `UI/Componentes/` los muestran; una página no pinta errores por su cuenta.

- **`ErroresDelResultado`** va dentro del `EditForm` y recibe el `Resultado` del último envío. Pasa los errores en
  cascada a cada `Campo`, pinta arriba de los campos los errores del objeto (propiedad `""`) y decide quién lleva el
  foco: el primer error en orden de lectura (el del objeto, si lo hay; si no, el primer `Campo` con error).
- **`Campo`** es el campo de texto de DESIGN.md: etiqueta en negritas, control de 50 px, ayuda opcional y cada
  mensaje de su propiedad debajo del control, en vino. Con error, el control lleva `aria-invalid="true"` y
  `aria-describedby` con los ids de la ayuda y de cada mensaje. El id del control es el nombre de la propiedad
  (`Nombre`; la ayuda `Nombre-ayuda`, los mensajes `Nombre-error-1`, `Nombre-error-2`...). Parámetros: `Para`,
  `Etiqueta`, `Ayuda`, `Tipo` (`text` por omisión, `email`, `tel`); cualquier otro atributo (`autocomplete`,
  `inputmode`) pasa al control, pero no reemplaza los propios del `Campo` (`id`, `type`, `class`,
  `aria-describedby`, `autofocus`). Solo texto: los controles de número, fecha, lista o área de texto llegan
  con la historia que los necesite.

Así arma una página su formulario:

```razor
<EditForm Model="Comando" FormName="datos" OnValidSubmit="EnviarAsync">
    <ErroresDelResultado Resultado="resultado">
        <Campo Para="() => Comando.Nombre" Etiqueta="Nombre completo" autocomplete="name" />
        <Campo Para="() => Comando.Correo" Etiqueta="Correo" Tipo="email" Ayuda="Te enviamos la confirmación." />
    </ErroresDelResultado>
    <button type="submit" class="btn btn-primary">Continuar</button>
</EditForm>

@code {
    [SupplyParameterFromForm]
    public DatosDelPaciente Comando { get; set; } = new();

    private Resultado? resultado;
}
```

El `EditForm` se liga al comando con `[SupplyParameterFromForm]`: en SSR estático es lo que llena `Comando` con lo
que trae el POST; sin él, el comando llega vacío y todo campo sale "obligatorio". `EnviarAsync` pide el
`ICommandHandler` del comando (con `@inject`) y guarda en `resultado` el `Resultado` que vuelve. Sin `DataAnnotationsValidator` ni ninguna otra validación en la página (AD-12): `OnValidSubmit` se dispara
siempre y los errores vienen solo del `Resultado`.

- **Toda propiedad validada necesita su `Campo`** para que su error se vea. Un error de una propiedad sin `Campo`
  no se muestra en ningún lado.
- **El foco.** En SSR estático el primer error lleva `autofocus`: la respuesta al envío es una página completa y el
  navegador lo aplica al cargarla, sin script. En interactivo, al llegar un `Resultado` inválido nuevo, se enfoca
  una vez después de pintar, con `FocusAsync`.
- **`aria-invalid` lo pinta el `InputText`.** `ErroresDelResultado` copia los errores de campo al `EditContext`
  del `EditForm` y los borra al enviarse el formulario; el `InputText` de cada `Campo` pinta `aria-invalid` (y su
  clase `invalid`) con ellos.
- **Fuera de su lugar lanza.** Un `Campo` fuera de `ErroresDelResultado`, o un `ErroresDelResultado` fuera de un
  `EditForm`, lanza `InvalidOperationException` al pintarse: así no queda un campo cuyos errores nunca se ven.
- **Estilos** en `css/consultorio.css` (`.dsm-campo*`, `.dsm-errores-del-objeto`), solo con tokens. El error va en
  `--dsm-primary`, nunca en `--dsm-danger`.

**Cómo se prueba.** `tests/DraSelina.Consultorio.UI.Tests` pinta los componentes con bUnit, sin navegador ni
Docker. `FormularioDePrueba.razor` es un formulario armado como arriba sobre `ComandoDePrueba`, y el handler se pide
envuelto en el `ValidacionDeComandos` real, así que los mensajes salen de DataAnnotations. Cada prueba fija el modo
con `SetRendererInfo`: `new RendererInfo("Static", isInteractive: false)` para SSR estático (se afirma `autofocus`
y que no hubo llamadas a JS) o `new RendererInfo("Server", isInteractive: true)` para interactivo (se afirma la
llamada a enfocar con `JSInterop.VerifyFocusAsyncInvoke`). bUnit no hace un envío HTTP real: en el modo estático
se pinta como en SSR, pero el envío es un evento, no un POST.

## Crear una migración

Cada módulo con datos tiene su `DbContext` en `Modules/<Módulo>/Infrastructure/`, con su esquema y su historial
de migraciones dentro de ese esquema (AD-2, AD-25). La herramienta de EF Core se instala una vez, local al
repositorio (`dotnet-tools.json`):

```bash
./dotnet.sh tool restore
```

Para agregar una migración a un módulo, por ejemplo `CrearCitas` en Agenda:

```bash
./dotnet.sh ef migrations add CrearCitas \
  --project src/DraSelina.Consultorio.Web \
  --context AgendaDbContext \
  --output-dir Modules/Agenda/Infrastructure/Migrations \
  --namespace DraSelina.Consultorio.Agenda.Infrastructure.Migrations
```

- `--output-dir` y `--namespace` van siempre: sin ellos la migración cae en la carpeta y el namespace por
  defecto del proyecto, y las pruebas de arquitectura fallan.
- No hace falta una base en ejecución: generar una migración no abre conexión.
- No hay que aplicarla a mano ni registrarla en ningún lado: el `MigrationService` aplica todas las pendientes
  en el siguiente arranque. Nunca se usa `dotnet ef database update` ni `EnsureCreated`.

## Estructura

Todo el código de la aplicación vive en un solo proyecto, `DraSelina.Consultorio.Web`. Los módulos y sus capas
son carpetas y namespaces dentro de él, no proyectos.

```text
src/
  DraSelina.Consultorio.AppHost/
  DraSelina.Consultorio.ServiceDefaults/
  DraSelina.Consultorio.MigrationService/
  DraSelina.Consultorio.Web/
    Kernel/                       # DraSelina.Consultorio.Kernel
    BuildingBlocks/Contracts/     # DraSelina.Consultorio.BuildingBlocks.Contracts
    Modules/Agenda/{Contracts,Domain,Features,Infrastructure}/         # DraSelina.Consultorio.Agenda.<Capa>
    Modules/Pacientes/{Contracts,Domain,Features,Infrastructure}/
    Modules/Notificaciones/{Contracts,Features,Infrastructure}/
    Modules/Contenido/{Contracts,Contenido}/
    UI/                           # DraSelina.Consultorio.UI
tests/
  DraSelina.Consultorio.Kernel.Tests/
  DraSelina.Consultorio.Domain.Tests/       # agregados y objetos de valor de Agenda y Pacientes, sin base
  DraSelina.Consultorio.ArchitectureTests/
  DraSelina.Consultorio.IntegrationTests/   # arnés y pruebas contra PostgreSQL real (Docker)
  DraSelina.Consultorio.UI.Tests/           # componentes de UI/ con bUnit, sin Docker
scripts/
  verificar-arranque.sh # comprueba el arranque contra el AppHost levantado
```

## Reglas de dependencia

Qué namespace puede usar a cuál, y qué proyecto puede referenciar a cuál, lo fijan las dos tablas de
"Invariants & Rules" de la arquitectura (AD-1). Están como datos en
`tests/DraSelina.Consultorio.ArchitectureTests/Reglas.cs`. Como todo compila en un solo ensamblado, el compilador
no impide una dependencia prohibida: la impiden estas pruebas.

- Cada archivo pertenece a una capa por su carpeta, y su namespace debe corresponder a esa carpeta.
- Las pruebas leen las directivas `using` (y los nombres `DraSelina.Consultorio.*` escritos completos) de cada
  `.cs` y `.razor` de `Web`. Una dependencia que la tabla no permite las hace fallar: por ejemplo, un archivo de
  `Modules/Agenda/Domain/` con `using Microsoft.EntityFrameworkCore;`.
- `Kernel`, `BuildingBlocks.Contracts`, todo `*.Contracts` y todo `*.Domain` solo usan `System.*`.
- No se permiten `global using`: llegarían a todas las capas sin que la prueba los vea.
- Por lo mismo, `Web` no tiene `using` implícitos fuera de `System.*`: los de `Microsoft.*` que agrega el SDK web
  se quitan en el `.csproj`. La prueba lo comprueba contra el `GlobalUsings.g.cs` que genera la compilación, así
  que necesita la solución compilada (el proyecto de pruebas compila `Web` antes, sin referenciar su ensamblado).
- Los componentes de `UI/` llevan el namespace `DraSelina.Consultorio.UI` por el `@namespace` de
  `UI/_Imports.razor`; un `.razor` con el `@namespace` de otra capa hace fallar las pruebas.
- Una carpeta nueva bajo `Modules/` o un proyecto nuevo en `src/` que la arquitectura no nombra hace fallar las
  pruebas: primero se cambia la arquitectura, después `Reglas.cs`.

Límite conocido: la prueba ve directivas `using`, no tipos. Un tipo de un paquete escrito con su nombre completo
(sin `using`) en una capa pura no se detecta; eso queda a la revisión de cada historia.

Las versiones de paquetes se fijan en `Directory.Packages.props` y solo salen de la tabla Stack de la arquitectura.
