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
- Una carpeta nueva bajo `Modules/` o un proyecto nuevo en `src/` que la arquitectura no nombra hace fallar las
  pruebas: primero se cambia la arquitectura, después `Reglas.cs`.

Límite conocido: la prueba ve directivas `using`, no tipos. Un tipo de un paquete escrito con su nombre completo
(sin `using`) en una capa pura no se detecta; eso queda a la revisión de cada historia.

Las versiones de paquetes se fijan en `Directory.Packages.props` y solo salen de la tabla Stack de la arquitectura.
