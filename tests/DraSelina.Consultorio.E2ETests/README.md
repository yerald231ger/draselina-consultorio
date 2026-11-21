# Recorridos con Playwright

Tres pruebas de extremo a extremo de los caminos felices del Paciente, en vista de celular:

- Agendar con correo: la confirmación y el correo en Mailpit con el mismo Enlace de gestión.
- Agendar sin correo: el panel del Enlace de gestión antes de la tarjeta, y ningún correo.
- Cancelar con el Enlace de gestión que llegó por correo.

Arrancan el AppHost completo una vez por corrida (PostgreSQL, migraciones, Mailpit y la web) y lo apagan al terminar.
La base parte vacía, como con `dotnet run`. El alcance está en la fila Pruebas de la arquitectura.

## Requisitos

- Docker en ejecución.
- El SDK de .NET de `global.json` (11.0.100 o una banda posterior de .NET 11); `./dotnet.sh` lo busca en el PATH y
  en `~/.dotnet`. No hace falta PowerShell.

## Correr

```
./dotnet.sh test --project tests/DraSelina.Consultorio.E2ETests
```

La primera vez descarga Chromium (en la caché de Playwright del usuario) y las imágenes de los contenedores, y tarda
más. `./dotnet.sh test` de toda la solución también los corre.

## Solo en local

En GitHub Actions (`CI=true`) el proyecto compila, pero `dotnet test` no lo ejecuta: el `.csproj` deja de declararse
proyecto de pruebas cuando `CI` es `true`.
