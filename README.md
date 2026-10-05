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

```text
src/
  Consultorio.{AppHost,ServiceDefaults,MigrationService,Web,UI}/
  BuildingBlocks/{Kernel,Contracts}/
  Modules/{Agenda,Pacientes,Notificaciones,Contenido}/
tests/
  DraSelina.Consultorio.Kernel.Tests/
  DraSelina.Consultorio.ArchitectureTests/
spikes/                 # prototipos desechables; no son parte de la solución
```

## Reglas de referencia

Qué proyecto puede referenciar a cuál lo fija la tabla de AD-1 de la arquitectura. Esa tabla está como datos en
`tests/DraSelina.Consultorio.ArchitectureTests/TablaDeReferencias.cs` y las pruebas leen los `.csproj` de `src/`:

- Una referencia entre proyectos que la tabla no permite hace fallar las pruebas.
- `Kernel`, `BuildingBlocks.Contracts`, todo `*.Contracts` y todo `*.Domain` no llevan paquetes ni frameworks.
- Un proyecto nuevo en `src/` que la tabla no nombra hace fallar las pruebas: primero se cambia la arquitectura,
  después la tabla.

Las versiones de paquetes se fijan en `Directory.Packages.props` y solo salen de la tabla Stack de la arquitectura.
