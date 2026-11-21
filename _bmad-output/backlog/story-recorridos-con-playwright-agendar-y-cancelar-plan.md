---
title: 'Recorridos con Playwright: agendar con y sin correo, y cancelar desde el correo'
type: 'feature'
ticket: '1'
created: '2026-11-20'
status: 'built'
baseline_revision: 'ac8445c6ef008cf16916ca6fe46a6bf0d543c548'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/backlog/story-recorridos-con-playwright-agendar-y-cancelar.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** No hay pruebas que recorran la aplicación en un navegador real. Los caminos felices del Paciente (agendar y cancelar) solo se prueban a mano en los recorridos con el tester.

**Approach:** Un proyecto nuevo, `tests/DraSelina.Consultorio.E2ETests`, arranca el AppHost completo con Aspire.Hosting.Testing y maneja Chromium con Playwright en vista de celular. Son tres pruebas: agendar con correo, agendar sin correo y cancelar con el Enlace de gestión tomado de la API de Mailpit. Corren solo en local.

## Boundaries & Constraints

**Always:**
- Los criterios 1 a 7 del ticket mandan.
- Vista de celular (un dispositivo de Playwright con Chromium).
- Cada prueba agenda su propia Cita con un celular distinto, porque existe el tope semanal por teléfono, y pasa sola o en cualquier orden.
- El formulario de Tus datos se envía después del tiempo mínimo anti-bots (`VerificacionAntiBots.TiempoMinimo`, 3 s) y sin tocar el campo trampa.
- La contraseña del Administrador se pasa como argumento temporal (`--Parameters:clave-administrador=...`).
- Los navegadores se instalan desde .NET (`Microsoft.Playwright.Program.Main(["install", "chromium"])`), sin PowerShell.
- El proyecto está en la solución y compila en GitHub Actions, pero Actions no ejecuta sus pruebas.

**Never:**
- Cambiar la web, el AppHost, la verificación anti-bots o el límite de frecuencia para que las pruebas pasen.
- Reprogramar, el Administrador, accesibilidad o capturas visuales.
- Sleeps fijos para esperar el correo: se consulta Mailpit hasta un tiempo límite.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Con correo | Primer horario libre, datos con correo único | "Tu cita está confirmada", y Mailpit recibe un correo a esa dirección con el mismo Enlace de gestión | Si el correo no llega en el tiempo límite, la prueba falla con un mensaje claro |
| Sin correo | Mismos datos sin correo | Confirmación con el panel "Guarda este enlace" antes de la tarjeta; ningún correo nuevo en Mailpit para esa Cita | — |
| Cancelar | Enlace del correo de una Cita propia, a más de 2 horas | Confirmación de la cancelación; al reabrir el enlace, el estado dice Cancelada | — |

</frozen-after-approval>

## Code Map

- `tests/DraSelina.Consultorio.IntegrationTests/DraSelina.Consultorio.IntegrationTests.csproj` -- modelo del csproj de pruebas: `OutputType` Exe, xunit.v3 con Microsoft.Testing.Platform (`global.json`), `<Using Include="Xunit" />`.
- `Directory.Build.props` -- net11.0, `TreatWarningsAsErrors`, versiones centrales.
- `Directory.Packages.props` -- agregar `Microsoft.Playwright` 1.65.1 y `Aspire.Hosting.Testing` 17.0.0, con su comentario (solo en los recorridos locales).
- `DraSelina.Consultorio.slnx` -- agregar el proyecto nuevo junto a los demás de `tests/`.
- `.github/workflows/pruebas.yml` -- corre `./dotnet.sh build` y luego `./dotnet.sh test --no-build` sobre la solución; GitHub define `CI=true`.
- `src/DraSelina.Consultorio.AppHost/AppHost.cs` -- recursos `postgres`, `mailpit`, `migraciones` y `web` (endpoint `http`), y el parámetro `clave-administrador`. No se toca.
- `src/DraSelina.Consultorio.Web/UI/Pages/Reserva/TipoDeConsulta.razor` -- `/reservar`, botón "Ver horarios disponibles" (el primer tipo ya viene elegido).
- `src/DraSelina.Consultorio.Web/UI/Componentes/SelectorDeDiaYHora.razor` -- horas como `a.dsm-hora` dentro de `ul[aria-label="Horas"]`; luego el enlace "Continuar" (`DiaYHora.razor`).
- `src/DraSelina.Consultorio.Web/UI/Pages/Reserva/TusDatos.razor` -- etiquetas "Nombre completo", "Celular", "Correo (opcional)", "Motivo de la consulta", casilla `#Consentimiento` y botón "Confirmar cita".
- `src/DraSelina.Consultorio.Web/UI/Pages/Reserva/CitaConfirmada.razor` -- h1 "Tu cita está confirmada"; el enlace sale como texto en `.dsm-enlace-de-gestion__direccion`. Sin correo, la nota y el panel van antes de la tarjeta.
- `src/DraSelina.Consultorio.Web/UI/Pages/Gestion/TuCita.razor`, `Cancelar.razor`, `ConfirmacionDeCancelacion.razor` -- "Cancelar cita" (`a.dsm-gestion__cancelar`), el botón de confirmación `button.dsm-boton-cancelar`, el h1 `#cita-cancelada` y el chip "Cancelada" en Tu cita.
- Mailpit -- API HTTP en el endpoint `http` del recurso `mailpit`: `GET /api/v1/search?query=to:<correo>` y `GET /api/v1/message/{ID}` (campos `Text` y `HTML`). El correo sale por el outbox, así que llega con retraso.

## Tasks & Acceptance

**Execution:**
- [ ] `Directory.Packages.props` -- agregar los dos paquetes -- versiones centrales.
- [ ] `tests/DraSelina.Consultorio.E2ETests/DraSelina.Consultorio.E2ETests.csproj` -- proyecto de pruebas con xunit.v3, Microsoft.Playwright y Aspire.Hosting.Testing; referencia al AppHost; `IsTestProject` en false cuando `CI` es `true`, con su comentario -- así Actions compila el proyecto pero no corre sus pruebas.
- [ ] `tests/DraSelina.Consultorio.E2ETests/` (fixture de ensamblado) -- instala Chromium una vez, arranca el AppHost con la clave temporal, espera a que la web esté sana, expone la URL de la web y la de Mailpit, y lo apaga al terminar -- un solo arranque por corrida.
- [ ] `tests/DraSelina.Consultorio.E2ETests/` (cliente de Mailpit) -- busca por destinatario, consultando hasta un tiempo límite, y extrae el Enlace de gestión del cuerpo -- correo con retraso por el outbox.
- [ ] `tests/DraSelina.Consultorio.E2ETests/` (pruebas) -- las tres pruebas en vista de celular, con un ayudante que agenda (celular y correo únicos por prueba, espera del tiempo mínimo antes de enviar) -- criterios 1 a 5.
- [ ] `tests/DraSelina.Consultorio.E2ETests/README.md` -- requisitos (Docker y SDK), el comando único y la nota de que no corre en Actions -- criterio 7.
- [ ] `DraSelina.Consultorio.slnx` y `.github/workflows/pruebas.yml` -- agregar el proyecto, y en el flujo un comentario de por qué los recorridos no corren ahí -- criterio 6.

**Acceptance Criteria:**
- Given Docker activo, when se corre `./dotnet.sh test --project tests/DraSelina.Consultorio.E2ETests`, then las tres pruebas pasan.
- Given `CI=true`, when se corre `./dotnet.sh test --no-build` sobre la solución, then las pruebas de E2ETests no se ejecutan y las demás sí.

## Implementation Notes

- Implementado directo, sin subagente: el permiso para lanzarlo fue negado en esta sesión; el flujo prevé implementar desde el plan.
- `Projects.DraSelina_Consultorio_AppHost` se genera solo con la referencia al AppHost; no hizo falta metadato extra.
- Exclusión en CI: `IsTestProject=false` no basta con Microsoft.Testing.Platform; `dotnet test` decide por `IsTestingPlatformApplication`. El `.csproj` apaga los dos con `CI=true`. Verificado: `CI=true ./dotnet.sh test --no-build` corre 1,173 pruebas y ninguna de E2ETests; la solución compila completa.
- Inestabilidad encontrada: con las pruebas de integración en paralelo, `WaitForLoadStateAsync()` (evento load) llegó a pasar de 30 s tras elegir la hora (2 de 5 corridas de la solución). Se quitó toda espera del evento load: las pruebas esperan lo visible (el encabezado, el día marcado con `aria-current`, el botón siguiente), las navegaciones directas usan `DOMContentLoaded` y las expectativas esperan hasta 15 s.
- "Sin correo": como no hay destinatario, se comprueba que ningún correo contenga el token del enlace tras 12 s (dos vueltas del outbox, que entrega cada 5 s). Es la única espera fija y es la del propio criterio: probar que algo no llega.
- Archivos: `Directory.Packages.props`, `DraSelina.Consultorio.slnx`, `.github/workflows/pruebas.yml` (comentario) y `tests/DraSelina.Consultorio.E2ETests/` (`.csproj`, `Consultorio.cs`, `Mailpit.cs`, `RecorridosDelPacienteTests.cs`, `README.md`).

## Plan Change Log

## Review Triage Log

Pase 1 (lente quick, 2026-11-21). Veredictos: high 0, medium 2, low 3, false 2, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia / acción |
|---|---|---|---|
| Criterio 1: no se comprueban la fecha y la hora elegidas en la confirmación | medium | patch | Cierto: solo se miraba el h1. `ElegirElPrimerHorarioLibreAsync` devuelve el día, la fecha y la hora elegidos y `AgendarAsync` los compara con `.dsm-confirmada__dia` y `.dsm-confirmada__fecha`. |
| Se toma cualquier correo al destinatario, no la confirmación | medium | patch | Cierto: el Recordatorio ("Mañana tienes cita", `PlantillaDeRecordatorio`) lleva el mismo enlace y la Cita es para mañana. `Mailpit.ConfirmacionParaAsync` busca además por el asunto de `PlantillaDeConfirmacion.Titulo`. La comprobación de la frase con correo se dejó: la frase varía según la hora del recordatorio y la negativa basta para distinguir los dos casos. |
| El recorrido no pasa por "Agendar cita" | low | patch | Cierto frente al When del criterio 1. `AgendarAsync` abre `/` y toca "Agendar cita" (enlace exacto, el primero). |
| La búsqueda por token podría no encontrar nunca nada y la prueba sin correo pasaría en falso | low | patch | No demostrado que falle, pero nada probaba que la consulta funciona. La prueba con correo comprueba `CorreosConAsync(token) > 0` sobre su propio correo. |
| README sin la versión del SDK | low | patch | Corrección directa: se agregó la versión de `global.json` y dónde la busca `dotnet.sh`. |
| El bucle de días recarga el día ya mostrado | false | rechazado | El propio revisor lo dice: no es un defecto; si el día mostrado no tiene horas, volver a tocarlo no cambia el resultado y el mensaje es exacto. |
| La exclusión en CI podría no sostenerse | false | rechazado | Verificado en esta sesión: `CI=true ./dotnet.sh build` y `CI=true ./dotnet.sh test --no-build` corren 1,173 pruebas sin las de E2ETests y la solución compila. Que `CI=true` en local también las excluya es lo que pide el criterio 6. |

Verificación tras los parches: E2ETests 3/3; `CI=true` 1,173 sin E2ETests; la solución completa pasa con E2ETests (1 min 23 s).

## Design Notes

El AppHost arranca una sola vez por corrida (assembly fixture de xunit v3). La base parte vacía en cada arranque, así que los celulares y correos únicos (por ejemplo, un sufijo por prueba más la hora) bastan. El horario es el primer libre del día que la página ya muestra, porque los horarios empiezan desde mañana y así la Cita queda a más de 2 horas.

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: sin advertencias ni errores.
- `./dotnet.sh test --project tests/DraSelina.Consultorio.E2ETests` -- expected: 3 pruebas pasan.
- `CI=true ./dotnet.sh test --no-build` -- expected: las 1,173 pruebas de siempre pasan, sin las de E2ETests.
