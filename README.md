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
./dotnet.sh test        # todas las pruebas
```

Por qué la envoltura: `global.json` redirige el SDK pero no el runtime. Con el `dotnet` del sistema la solución
compila, pero las pruebas no arrancan porque el sistema no tiene el runtime 11.

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
