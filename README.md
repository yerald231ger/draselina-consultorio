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
| `ArrancarWeb()` / `ArrancarWeb(cadena)` | La web con su `Program` real, en memoria, contra la base migrada o contra la que se indique. |
| `CrearBaseVaciaAsync()` | Una base nueva sin migrar en el mismo servidor; devuelve su cadena de conexión. |
| `CrearContextoDePruebas()` | Un `PruebasDbContext` sobre el esquema `pruebas`. |

**Dónde poner un tipo de prueba.** Un tipo que solo existe para probar (una entidad, y más adelante un comando o
un handler de prueba) vive en `tests/DraSelina.Consultorio.IntegrationTests/Pruebas/`. Si tiene tabla, se agrega
su `DbSet` a `Pruebas/PruebasDbContext.cs`: el arnés crea la tabla en el esquema `pruebas` al arrancar la corrida,
sin migraciones. Ningún esquema de producción recibe tablas de prueba. `Apunte` es el ejemplo mínimo.

Para migrar otra base por el mismo camino: `await MigrationService.EjecutarAsync(cadena)`.

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
   nunca crea ni migra nada (AD-25).

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
