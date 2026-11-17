---
title: 'Configuración: la semana tipo'
type: 'feature'
ticket: '5'
created: '2026-11-17'
status: done
baseline_revision: '3d169d537c23f1fc635c77b5bda64fe1bb967fb2'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-draselina/ux-appointment-booking/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `CambiarAgendaBase` y su vista previa existen (6.4), pero no hay pantalla: Gerardo no puede cambiar la semana tipo (CAP-10, FR-12).

**Approach:** La página `/admin/configuracion`, con "Configuración" en la navegación, y su primera sección: la semana tipo de lunes a domingo, con varios Tramos por día y "copiar a otros días". Antes de guardar muestra las Citas que quedan fuera (no se cancelan); al guardar, las fechas con custom que no cambian.

## Boundaries & Constraints

**Always:**
- Una consulta nueva `SemanaTipo` (`IRequiereAdministrador`) da los siete días con sus Tramos guardados, de lunes a domingo, para llenar la sección.
- Cada día: sus Tramos (agregar y quitar; ninguno: "No se atiende") y "Copiar a otros días": se eligen los días destino y reciben una copia de los Tramos de ese día `[ASSUMPTION: diseño]`.
- Guardar pide primero `CambioDeAgendaBase`; si hay Citas fuera, las lista (fecha, hora, Paciente, sin correo) diciendo que no se cancelan y pide confirmar; sin Citas fuera guarda directo. Si los Tramos cambian después de ver la lista, se vuelve a pedir.
- Al guardar: "Se guardó la semana tipo" y, si hay, las fechas con horario propio que no cambian. Ningún día cambiado: lo dice.
- Los errores `Dias[i]` (encimados, Tramo corto, incompleto, al revés) se pintan en su día; `Dias` arriba de la sección.
- La sección es un componente propio dentro de la página, para que la 6.6 y la 6.7 agreguen las suyas.
- Navegación: "Configuración" después de "Calendario de atención" (Traspaso de la épica 5).

**Never:**
- Reglas de reserva, horas de envío, Tipos de consulta (6.6, 6.7). Cancelar Citas. Cambiar el comando o la vista previa de la 6.4.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Cargar | Base sembrada | Lun–vie 3:30 a 6:00 pm; sáb y dom "No se atiende" | — |
| Copiar | Lunes a 15:30–17:00 y copiarlo a mié y vie | Los tres con 15:30–17:00 | — |
| Citas fuera | La vista previa trae una Cita del lunes 17:30 | Lista con "no se cancelan"; no guarda hasta confirmar | — |
| Guardar | Sin Citas fuera, con custom el miércoles 4 | Mensaje con la fecha que no cambia | — |
| Error de un día | `Invalido` bajo `Dias[2]` | Mensaje en el miércoles; nada guardado | — |

</frozen-after-approval>

## Code Map

- `Modules/Agenda/Contracts/CambiarAgendaBase.cs`, `CambioDeAgendaBase.cs` -- comando y vista previa (6.4); `PropiedadDelDia`, mensajes.
- `Modules/Agenda/Features/CambioDeAgendaBaseHandler.cs` -- patrón para leer la base con `IAgendaDelRango` y `TramosBase(dia)`.
- `UI/Pages/Administracion/CalendarioDeAtencion.razor` -- patrón de edición de Tramos (`input type=time`, `@bind:format`), errores por propiedad, vista previa antes de guardar, `_ocupado`.
- `UI/Pages/Administracion/ListaDeCitasFuera.razor` -- estilo de lista de Citas fuera (aquí sin elección).
- `UI/Layout/LayoutAdministracion.razor` -- navegación; `tests/…IntegrationTests/PacientesBusquedaEHistorialTests.cs` fija sus enlaces.
- `UI/Pages/Reserva/FormatoDeCita.cs` -- `Hora`, `Dia`, `Largo`.
- Pruebas: fakes y reloj como en `tests/…UI.Tests/CalendarioDeAtencionTests.cs`; escenario de `CambiarAgendaBaseTests`.

## Tasks & Acceptance

**Execution:**
- [x] `Modules/Agenda/Contracts/SemanaTipo.cs` + `Features/SemanaTipoHandler.cs` + alta en `Registro.cs`.
- [x] `UI/Pages/Administracion/Configuracion.razor` -- `/admin/configuracion`, `[Authorize]`, Interactive Server, con la sección.
- [x] `UI/Pages/Administracion/SeccionSemanaTipo.razor` -- la sección completa.
- [x] `UI/Layout/LayoutAdministracion.razor` -- enlace "Configuración".
- [x] `wwwroot/css/consultorio.css` -- la semana y el copiar.
- [x] Pruebas: integración de `SemanaTipo` (y sin sesión) y de la navegación; bUnit de cada fila de la matriz.

**Acceptance Criteria:**
- Given la base sembrada, when Gerardo copia el lunes recortado a miércoles y viernes y guarda, then el comando recibe los siete días con esos tres cambiados.
- Given cualquier página de administración, when se pinta la navegación, then dice Agenda, Pacientes, Calendario de atención y Configuración.

## Implementation Notes

- Implementado y revisado en esta sesión (el entorno niega los subagentes; misma decisión que en las entradas anteriores).
- `SemanaTipo` lee la base con `IAgendaDelRango.LeerAsync` sobre un rango de un día cualquiera (sin custom) y la da de lunes a domingo.
- La página `Configuracion.razor` solo compone secciones; `SeccionSemanaTipo.razor` tiene su estado, consultas y comando.
- La vista previa se vuelve a pedir cuando cambia la semana (clave con los Tramos de cada día); con Citas fuera el botón dice "Guardar de todos modos".
- Los errores `Dias[i]` van al día por posición: el comando se arma en el mismo orden en que se pintan (lunes a domingo).
- `PacientesBusquedaEHistorialTests` y `CambiarAgendaBaseTests` se extendieron (navegación con Configuración; `SemanaTipo`).
- Una primera corrida de la suite tuvo 6 fallas de 0 ms en `ActorYAdministracionTests` y `ActorFueraDeLaAdministracionTests` (arranque del contenedor); la clase sola y la suite repetida pasan: 1,089.

## Plan Change Log

## Review Triage Log

**Pasada 1 (lente Quick, en esta sesión): 0 high, 0 medium, 0 low, 1 false, 0 maybe-false.**

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Con la lista de Citas fuera a la vista, editar un Tramo a medias y guardar mandaría el comando sin pedir la vista otra vez. | false | rechazado | Con Tramos incompletos el comando rechaza la forma y no guarda nada; al completarlos la clave cambia y se pide la vista previa de nuevo. |

## Verification

**Commands:**
- `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"; dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.
