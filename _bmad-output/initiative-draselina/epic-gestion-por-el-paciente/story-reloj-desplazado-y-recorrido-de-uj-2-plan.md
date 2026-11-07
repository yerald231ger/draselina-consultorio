---
title: 'Reloj desplazado y recorrido de UJ-2'
type: 'chore'
ticket: '5'
created: '2026-11-07'
status: 'built'
baseline_revision: 'af6c0469a2e04f61e636eb2324006482e503c0aa'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** En local no se puede tener una Cita a menos de 2 horas, porque en línea solo se reserva desde mañana. Por eso Gerardo no puede recorrer la parte tardía de UJ-2 (Done when 1 de la épica).

**Approach:** `RelojDelSistema` suma `Reloj:Desplazamiento`, que lee con `IOptionsMonitor` de `appsettings.Development.json` de la web. Ese archivo se recarga al guardarse, así que el reloj cambia sin reiniciar y sin perder la base (Decision de Gerardo, corregida el 2026-11-07). Fuera de Development, el host no arranca si el desplazamiento viene puesto y es distinto de cero. Después, Gerardo recorre UJ-2 en el teléfono: reserva, reprograma tres días antes y, con el reloj desplazado, cancela a una hora de la Cita y ve la cancelación tardía.

</frozen-after-approval>

## Implementation Notes

Ruta oneshot: son unas 60 líneas en tres archivos de la web y dos pruebas. El AppHost no cambia.

- Por la revisión, el desplazamiento vive en `appsettings.Development.local.json`, que ya está ignorado por `appsettings.*.local.json` en `.gitignore` y la web carga solo en Development con `reloadOnChange`. El archivo no existe en el repositorio: Gerardo lo crea para su recorrido. Es la misma intención (un archivo de la web que se recarga al guardar), en un archivo que no se puede subir por error.
- `Arnes` aplica `PostConfigure<OpcionesDeReloj>` para anular el desplazamiento: con el archivo local a 30 días, AgendaDeAtencionTests, TusDatosTests y CalendarioTests pasan (72 de 72).
- El MigrationService registra `new RelojDelSistema()`: sin monitor, la hora real en cualquier entorno.
- Un valor que no es duración lanza con un mensaje que nombra el archivo y el formato.
- Las pruebas del reloj se movieron de Domain.Tests a IntegrationTests (no son de dominio).

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 1, low 4, false 0, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| El desplazamiento del archivo de Development llega a las pruebas de integración y el archivo está en el repositorio | medium | patch | Real: `WebApplicationFactory` corre en Development con la raíz de la web. Ahora se usa el archivo local ignorado y `Arnes` anula el valor; comprobado con 30 días. |
| El MigrationService inyecta el monitor y aplicaría un desplazamiento en cualquier entorno | low | patch | Real: el contenedor elige el constructor con `IOptionsMonitor<>`. Arreglo directo: instancia sin monitor. |
| Un valor mal escrito rompe cada lectura del reloj sin decir por qué | low | patch | Real con la recarga en caliente. Arreglo directo: mensaje claro con el archivo y el formato. |
| El cableado Development/no Development de `Program.cs` no tiene prueba de host | low | rechazado | Real, pero son tres líneas y la regla (`ExigirSoloEnDevelopment`) sí está probada; una prueba de host en Production pide montar otro entorno completo. |
| Las pruebas del reloj están en el proyecto de dominio | low | patch | Real frente al comentario del .csproj. Arreglo directo: mover el archivo. |

## Verification

**Commands:**
- `dotnet build`: debe compilar sin advertencias.
- `dotnet test`: debe pasar la suite completa.

**Manual checks:**
- Gerardo, con el AppHost y desde el teléfono:
  1. Reserva para mañana.
  2. Reprograma desde el enlace a otro día.
  3. Con `{ "Reloj": { "Desplazamiento": "…" } }` en `src/DraSelina.Consultorio.Web/appsettings.Development.local.json`, abre el enlace a menos de 2 horas del inicio: ya no hay "Reprogramar".
  4. Cancela y ve la confirmación de cancelación tardía.
