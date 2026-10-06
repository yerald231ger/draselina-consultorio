# Sistema de citas — Dra. Selina Medina

Monolito modular sobre .NET 11. La arquitectura está en
`_bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md`.

## Requisito: el SDK local de .NET 11

El SDK se instala dentro del repositorio, en `.dotnet/` (ignorada por git). No toca la instalación del sistema.

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version 11.0.100-rc.1.26425.128 --install-dir .dotnet
```

## Compilar y probar

Siempre con `./dotnet.sh`, que es `dotnet` con el SDK y el runtime de `.dotnet/`:

```bash
./dotnet.sh --version   # 11.0.100-rc.1.26425.128
./dotnet.sh build       # 0 errores, 0 advertencias (las advertencias son errores)
./dotnet.sh test        # todas las pruebas; necesita Docker en ejecución
```

Por qué la envoltura: `global.json` redirige el SDK pero no el runtime. Con el `dotnet` del sistema la solución
compila, pero las pruebas no arrancan porque el sistema no tiene el runtime 11.

## Pruebas

`./dotnet.sh test` corre los tres proyectos de `tests/`. No hace falta el AppHost arriba ni ningún otro comando.

- `Kernel.Tests` y `ArchitectureTests` no usan base de datos.
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
| `CadenaDeConexion` | La base migrada: esquemas `agenda`, `pacientes`, `notificaciones`, `wolverine` y `pruebas`. |
| `ArrancarWeb()` / `ArrancarWeb(cadena)` | La web con su `Program` real, en memoria, contra la base migrada o contra la que se indique. Lleva además los tipos de prueba, dados de alta como un módulo. Contra una base sin migrar no arranca. |
| `CrearBaseVaciaAsync()` | Una base nueva sin migrar en el mismo servidor; devuelve su cadena de conexión. |
| `CrearContextoDePruebas()` | Un `PruebasDbContext` sobre el esquema `pruebas`. |

**Dónde poner un tipo de prueba.** Un tipo que solo existe para probar (una entidad, un comando, una consulta, un
evento, un handler o un puerto de prueba) vive en `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/`, nunca
en `src/`. Si tiene tabla, se agrega su `DbSet` a `Pruebas/PruebasDbContext.cs`: el arnés crea la tabla en el
esquema `pruebas` al arrancar la corrida, sin migraciones. Ningún esquema de producción recibe tablas de prueba.
`Apunte` es el ejemplo mínimo. Los comandos de prueba se explican en "Casos de uso".

Para migrar otra base por el mismo camino: `await MigrationService.EjecutarAsync(cadena)`.

## Casos de uso

Todo caso de uso es un comando o una consulta, con exactamente un handler, y devuelve `Resultado` (AD-10). La
interfaz lo despacha por `ISender`, que el host implementa sobre Wolverine (`SenderDeWolverine.cs`). `ISender` es
exclusivo de la interfaz (AD-3) y la interfaz nunca usa `IMessageBus`.

```csharp
var resultado = await sender.SendAsync(comando);   // la página o una Minimal API
// Éxito: el cambio y sus mensajes quedaron confirmados. Fallo: nada quedó. Nunca hay Commit ni SaveChanges aquí.
```

### Cómo se forma

| Pieza | Forma | Dónde vive |
| --- | --- | --- |
| Comando | Clase con propiedades `set` que implementa `ICommand<T>`; nunca `record`. | `Modules/<Módulo>/Contracts/` |
| Consulta | Clase con propiedades `set` que implementa `IQuery<T>`. | `Modules/<Módulo>/Contracts/` |
| Códigos de fallo | Constantes `string` en una clase estática, junto al comando: el conjunto cerrado del módulo. | `Modules/<Módulo>/Contracts/` |
| Evento | Puede ser `record`. | `Modules/<Módulo>/Contracts/` |
| Handler | Clase pública cuyo nombre termina en `Handler`, con un método `Handle` que recibe el comando o la consulta y devuelve `Resultado<T>` (o `Task<Resultado<T>>`). Lo demás que necesita lo recibe como parámetro de `Handle`. Uno por comando o consulta. | `Modules/<Módulo>/Features/` |
| Puerto | La interfaz por la que el handler llega a la base (AD-4): de escritura en `Domain/`, de lectura en `Features/`. Su implementación recibe el `DbContext` del módulo. | implementación en `Modules/<Módulo>/Infrastructure/` |

`T` es el valor que el `Resultado` trae en el éxito. Wolverine encuentra el handler por su nombre y su método: no
se registra en ningún lado. El handler de un módulo nunca recibe el `DbContext`: recibe su puerto, y guardar es
una operación del puerto que el handler llama.

```csharp
public sealed class Anotar : ICommand<Guid>
{
    public Guid Id { get; set; }
    public string Texto { get; set; } = "";
}

public static class CodigosDeAnotacion
{
    public const string TextoRepetido = "anotacion.texto_repetido";
}

public sealed record AnotacionHecha(Guid AnotacionId);

public static class AnotarHandler
{
    public static async Task<Resultado<Guid>> Handle(Anotar comando, IAnotaciones anotaciones, IMessageBus bus, CancellationToken cancelacion)
    {
        if (await anotaciones.ExisteTextoAsync(comando.Texto, cancelacion))
        {
            return Resultado.Fallo(CodigosDeAnotacion.TextoRepetido).Como<Guid>();   // fallo esperado: código, no excepción
        }

        anotaciones.Agregar(new Anotacion { Id = comando.Id, Texto = comando.Texto });
        await anotaciones.GuardarAsync(cancelacion);                                 // el handler guarda; el host no
        await bus.PublishAsync(new AnotacionHecha(comando.Id));                      // se escribe en la misma transacción
        return comando.Id;                                                           // éxito: el host confirma
    }
}
```

Es el comando de prueba de `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/Anotar.cs`, recortado; su puerto
está en `Pruebas/Anotaciones.cs`.

- **Fallo esperado:** `Resultado.Fallo(codigo).Como<T>()`, con un código del conjunto cerrado. Nunca una
  excepción ni un texto.
- **Fallo inesperado:** una excepción. `ISender` no la convierte en `Resultado`: sale tal cual a quien despachó.
- **Sin handler:** `ISender` lanza una excepción que nombra el tipo del comando.

### Un comando, una transacción, un módulo

La política la fija el host una sola vez (`UnaTransaccionPorModulo.cs`, dada de alta en `Program.cs`). Ni el
comando ni el handler declaran nada: no hay atributos de transacción, y el handler no abre ni confirma.

- **Dueño de la transacción:** el `DbContext` del módulo donde vive el handler. Lo decide una tabla del host,
  namespace del handler → contexto (ver "Dar de alta un módulo").
- **Quién guarda:** el handler, de forma explícita, por su puerto. El host nunca llama a `SaveChanges`: lo que el
  handler agrega y no guarda, no queda.
- **Cierre de un comando:** el host abre la transacción antes del handler. Si el handler devuelve un `Resultado`
  exitoso, confirma juntos el cambio y los mensajes publicados. Si devuelve uno no exitoso (fallo, inválido, no
  autorizado) o lanza, deshace todo: ni cambio ni mensajes, aunque el handler ya hubiera guardado y publicado.
  Quien despachó recibe el `Resultado` tal cual, o la excepción.
- **Mensajes:** lo que el handler publica (`IMessageBus.PublishAsync`, `ScheduleAsync`) va a colas locales
  durables: se escribe en el esquema `wolverine` dentro de esa misma transacción (outbox) y se entrega después de
  confirmar. Un mensaje puede entregarse más de una vez; su handler debe tolerarlo.
- **Handler de un evento:** corre igual, en la transacción del módulo donde vive, y el host confirma salvo
  excepción.
- **Consultas:** corren sin transacción y nadie guarda por ellas.
- **Dos contextos:** si un handler alcanza, además de su puerto, la API de otro módulo, su transacción sigue
  siendo la de su módulo. Lo que el otro contexto haga queda fuera de ella (AD-28).

El host se niega a arrancar, con un error que nombra al handler, cuando:

- el handler de un comando o de un evento vive en un namespace que no es de ningún módulo dado de alta: ninguno
  corre en silencio sin transacción;
- el handler de un `ICommand<T>` no devuelve exactamente `Resultado<T>` (o `Task<Resultado<T>>`) con esa misma
  `T`, o un comando tiene más de un handler;
- un evento tiene, en la misma cadena, handlers de más de un módulo.

### Dar de alta un módulo

Dos cosas, y las dos ya están hechas para Agenda, Pacientes y Notificaciones:

1. El `DbContext` del módulo se registra con `optionsLifetime: ServiceLifetime.Singleton`, como en los
   `Registro.cs` de `Modules/*/Infrastructure/`. Sin eso Wolverine rechaza el handler
   (`InvalidServiceLocationException`).
2. Su renglón en la tabla de `Program.cs`:

   ```csharp
   builder.Services.AddTransaccionDeModulo<AgendaDbContext>("DraSelina.Consultorio.Agenda.Features");
   ```

   El namespace es exacto: el de los handlers del módulo.

Los puertos del módulo se registran en su `Registro.cs` con su clase concreta
(`AddScoped<IPuerto, Implementacion>()`): así Wolverine construye el puerto con el mismo contexto que tiene la
transacción, como se ve con el puerto de prueba.

### La web contra una base sin migrar

La web usa el almacenamiento de mensajes del esquema `wolverine`, que crea el `MigrationService`, y no crea nada
al arrancar. Contra una base sin migrar se niega a arrancar y lo dice
(`The Wolverine message storage ... is missing`); la base queda como estaba (AD-25).

### Escribir un comando de prueba

En `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/`, nunca en `src/`:

1. El comando (o la consulta), sus códigos, su evento y su handler, todos públicos y en el namespace
   `DraSelina.Consultorio.IntegrationTests.Pruebas`. `Anotar.cs` y `LeerAnotacion.cs` son el modelo.
2. Si guarda algo, su entidad y su `DbSet` en `PruebasDbContext` (la tabla queda en el esquema `pruebas`), y la
   operación que necesite en el puerto `IAnotaciones`, o un puerto nuevo registrado en `Pruebas/Registro.cs`.
3. No hay que registrar nada más. El arnés da de alta los tipos de prueba en la web en memoria igual que un
   módulo (`Pruebas/Registro.cs`: contexto, puerto y renglón namespace → contexto) y hace que Wolverine descubra
   los handlers del ensamblado de pruebas. La política es la del host.

La prueba despacha por `ISender`, resuelto del contenedor de la web en memoria (`CasoDeUsoTests.cs`):

```csharp
await using var web = arnes.ArrancarWeb();
await using var ambito = web.Services.CreateAsyncScope();
var sender = ambito.ServiceProvider.GetRequiredService<ISender>();

var resultado = await sender.SendAsync(new Anotar { Id = Guid.CreateVersion7(), Texto = "..." }, cancelacion);
```

Para comprobar que un mensaje quedó guardado sin depender de la velocidad de quien lo procesa, se programa para
más tarde (`ScheduleAsync`): se queda como fila en el esquema `wolverine` y `Consultas.MensajesGuardadosAsync` lo
encuentra por un valor de su cuerpo. Para comprobar que llega, se publica de inmediato y se espera, con límite de
tiempo, el rastro que deja su handler.

## Arrancar

Hace falta Docker en ejecución. Un solo comando levanta todo:

```bash
./dotnet.sh run --project src/DraSelina.Consultorio.AppHost
```

El AppHost de Aspire arranca, en este orden:

1. `postgres`: PostgreSQL en un contenedor, con la base `consultorio`. Sin volumen: cada arranque parte de una
   base vacía. La contraseña la genera Aspire en cada arranque; no hay secretos en el repositorio.
2. `migraciones`: el `MigrationService`. Aplica las migraciones de Agenda, Pacientes y Notificaciones, crea el
   almacenamiento de Wolverine en el esquema `wolverine` y termina.
3. `web`: la aplicación, en <http://localhost:5080>. Arranca solo si `migraciones` terminó con código 0. La web
   nunca crea ni migra nada (AD-25); arranca Wolverine sobre el esquema `wolverine` que dejó `migraciones`.

El panel de Aspire queda en <http://localhost:15080>; la dirección con el token de entrada sale en la consola
(`Login to the dashboard at ...`). Ahí se ven los tres recursos con su estado, sus registros y las trazas: pide
una página a la web y aparece su traza `GET /`.

Todo corre por HTTP (`ASPIRE_ALLOW_UNSECURED_TRANSPORT` en el perfil del AppHost) porque el equipo no tiene un
certificado de desarrollo de confianza. La CLI `aspire` no hace falta.

## Verificar el arranque

Con el AppHost arriba, en otra terminal:

```bash
scripts/verificar-arranque.sh   # sale con 0 si las cuatro filas pasan
```

Comprueba: los tres recursos en su estado y `GET /` con 200; un historial de migraciones en cada esquema
(`agenda`, `pacientes`, `notificaciones`) y ninguno en `public`; el esquema `wolverine` con sus tablas; y el punto
de salud `/health` con 200. Necesita `docker`, `curl` y `kubectl`: con `kubectl` le pregunta al orquestador de
Aspire el estado y el código de salida de cada recurso (no hace falta ningún clúster).

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
  DraSelina.Consultorio.ArchitectureTests/
  DraSelina.Consultorio.IntegrationTests/   # arnés y pruebas contra PostgreSQL real (Docker)
scripts/
  verificar-arranque.sh # comprueba el arranque contra el AppHost levantado
spikes/                 # prototipos desechables; no son parte de la solución
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
