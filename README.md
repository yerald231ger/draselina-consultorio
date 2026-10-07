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
| `CadenaDeConexion` | La base migrada: esquemas `agenda`, `pacientes`, `notificaciones` y `pruebas`. |
| `ArrancarWeb()` / `ArrancarWeb(cadena)` | La web con su `Program` real, en memoria, contra la base migrada o contra la que se indique. Lleva además los tipos de prueba, dados de alta con `AddPruebas`. |
| `CrearBaseVaciaAsync()` | Una base nueva sin migrar en el mismo servidor; devuelve su cadena de conexión. |
| `CrearContextoDePruebas()` | Un `PruebasDbContext` sobre el esquema `pruebas`. |

**Dónde poner un tipo de prueba.** Un tipo que solo existe para probar (una entidad, un comando, una consulta o
un handler de prueba) vive en `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/`. Si tiene tabla, se agrega
su `DbSet` a `Pruebas/PruebasDbContext.cs`: el arnés crea la tabla en el esquema `pruebas` al arrancar la corrida,
sin migraciones. Ningún esquema de producción recibe tablas de prueba. `Apunte` es el ejemplo mínimo. Un comando
de prueba se escribe como dice "Casos de uso".

Para migrar otra base por el mismo camino: `await MigrationService.EjecutarAsync(cadena)`.

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
tipo del comando. No hay ninguna comprobación al arrancar de "un comando, un handler".

### Cómo registra un módulo sus handlers

Uno por línea y por su interfaz; nada se descubre por reflexión ni por nombre. El registro de los tipos de
prueba, en `Pruebas/Registro.cs`:

```csharp
public static IServiceCollection AddPruebas(this IServiceCollection servicios, string cadenaDeConexion) =>
    servicios
        .AddDbContext<PruebasDbContext>(opciones => opciones.UseNpgsql(cadenaDeConexion))
        .AddScoped<ITransaccionDePruebas, TransaccionDePruebas>()
        .AddScoped<IAnotaciones, Anotaciones>()
        .AddScoped<ICommandHandler<Anotar, Guid>, AnotarHandler>()
        .AddScoped<IQueryHandler<LeerAnotacion, AnotacionLeida>, LeerAnotacionHandler>();
```

**En un módulo:** las líneas `AddScoped` de sus handlers y de sus puertos van en su
`Modules/<Módulo>/Infrastructure/Registro.cs`, dentro de `AddAgenda`, `AddPacientes` o `AddNotificaciones`, que ya
registran el contexto y el puerto de transacción. El host solo llama a esos tres métodos.

### La regla de la transacción

Un comando, una transacción, un módulo. El handler del comando abre la transacción de su módulo por un puerto,
guarda de forma explícita y la confirma. Toda salida con un `Resultado` no exitoso deshace antes de devolver.
Quien llama al handler nunca abre, confirma ni guarda. Una consulta corre sin transacción y nunca guarda.

El handler del comando de prueba, en `Pruebas/Anotar.cs` (aquí sin las ramas de `Desenlace`, que solo sirven a
las pruebas):

```csharp
public sealed class AnotarHandler(ITransaccionDePruebas transaccion, IAnotaciones anotaciones) : ICommandHandler<Anotar, Guid>
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

## Arrancar

Hace falta Docker en ejecución. Un solo comando levanta todo:

```bash
./dotnet.sh run --project src/DraSelina.Consultorio.AppHost
```

El AppHost de Aspire arranca, en este orden:

1. `postgres`: PostgreSQL en un contenedor, con la base `consultorio`. Sin volumen: cada arranque parte de una
   base vacía. La contraseña la genera Aspire en cada arranque; no hay secretos en el repositorio.
2. `migraciones`: el `MigrationService`. Aplica las migraciones de Agenda, Pacientes y Notificaciones y termina.
3. `web`: la aplicación, en <http://localhost:5080>. Arranca solo si `migraciones` terminó con código 0. La web
   nunca crea ni migra nada (AD-25).

El panel de Aspire queda en <http://localhost:15080>; la dirección con el token de entrada sale en la consola
(`Login to the dashboard at ...`). Ahí se ven los tres recursos con su estado, sus registros y las trazas: pide
una página a la web y aparece su traza `GET /`.

Todo corre por HTTP (`ASPIRE_ALLOW_UNSECURED_TRANSPORT` en el perfil del AppHost) porque el equipo no tiene un
certificado de desarrollo de confianza. La CLI `aspire` no hace falta.

## Verificar el arranque

Con el AppHost arriba, en otra terminal:

```bash
scripts/verificar-arranque.sh   # sale con 0 si las tres filas pasan
```

Comprueba: los tres recursos en su estado y `GET /` con 200; un historial de migraciones en cada esquema
(`agenda`, `pacientes`, `notificaciones`) y ninguno en `public`; y el punto de salud `/health` con 200. Necesita
`docker`, `curl` y `kubectl`: con `kubectl` le pregunta al orquestador de Aspire el estado y el código de salida
de cada recurso (no hace falta ningún clúster).

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
