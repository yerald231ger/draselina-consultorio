---
title: 'Pantalla: Citas que quedan fuera y quitar el horario propio'
type: 'feature'
ticket: '3'
created: '2026-11-17'
status: done
baseline_revision: '7c7b5ab3c4a25e50c1b6a8e4138ca57efd64ccae'
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

**Problem:** Los comandos de la 6.2 ya aceptan la elección por Cita y el borrado, pero el Calendario de atención no muestra las Citas que quedan fuera ni deja quitar el horario propio: con Citas fuera solo dice que no se guardó.

**Approach:** Antes de guardar o quitar, una consulta de vista previa lista las Citas que quedarían fuera, con nombre, hora y "sin correo"; el Administrador elige cancelar o conservar por Cita (o "cancelar todas") y la página manda esa lista a `GuardarAgendaCustom` o `BorrarAgendaCustom`. Si el comando responde que la lista ya no coincide, la vuelve a cargar con un aviso.

## Boundaries & Constraints

**Always:**
- La vista previa usa `CitasQueQuedanFuera` de la 6.1 (la misma regla que el comando) y los contactos en lote de `IPacientesApi.ObtenerContactosAsync` (AD-10); `IRequiereAdministrador`; solo lee.
- Para quitar, los Tramos son los de la base de ese día de la semana, y la pantalla dice cuáles ("vuelve a la semana tipo: 3:30 pm a 6:00 pm", o "no se atiende").
- Preselección del Assumption de RN-10: las en línea sin elegir, las del Consultorio en "conservar"; "cancelar todas" marca todas en cancelar y cada una se puede cambiar. No se guarda hasta que todas tengan elección.
- Sin Citas fuera, guardar y quitar van directo, como en la 6.1 (quitar pide confirmar).
- Conflicto `CitasFuera` al guardar o quitar: aviso, lista recargada, se conservan las elecciones de las Citas que siguen en ella.
- Al guardar con Citas elegidas, el mensaje dice que se cancelan en unos instantes y sus Pacientes reciben la disculpa (EXPERIENCE.md).
- En el mes, "no se atiende", "horario especial" y "día abierto" se distinguen también por estilo, no solo por texto.

**Never:**
- Cambiar los comandos ni sus reglas (6.2), ni la Agenda (5.2).
- Nombre, motivo de consulta o correo en registros (AD-23).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Lista al guardar | Sáb. cerrado, 4 Citas (1 sin correo, 1 del Consultorio) | Lista en orden de hora; "sin correo" señalada; la del Consultorio en conservar, las en línea sin elegir | — |
| Cancelar todas | Tocar el atajo y cambiar la 12:30 a conservar | Manda 3 cancelar y 1 conservar | — |
| Falta elegir | Una en línea sin elección | No manda nada | Mensaje junto a la lista |
| Conflicto | El comando responde `CitasFuera` | Aviso y lista nueva | — |
| Quitar sin Citas fuera | Custom de un miércoles | Dice a qué Tramos vuelve; tras confirmar, borra | — |
| Quitar con Citas fuera | Custom que abría un sábado con 2 Citas | La misma lista y elección antes de borrar | — |
| Vista previa sin sesión | — | — | No autorizado |

</frozen-after-approval>

## Code Map

- `UI/Pages/Administracion/CalendarioDeAtencion.razor` -- página de la 6.1: `GuardarAsync`, `MensajeGeneral`, `Abrir`, `ClaseDelDia`; se agrega el paso de la lista y "Quitar horario propio".
- `Modules/Agenda/Contracts/GuardarAgendaCustom.cs`, `BorrarAgendaCustom.cs` -- `Citas` con `DecisionSobreCita` (6.2).
- `Modules/Agenda/Features/CitasQueQuedanFuera.cs` -- `DeAsync(TramosVigentes)`; `CitaQueQuedaFuera` con `Origen`.
- `Modules/Agenda/Features/IAgendaDelRango.cs` -- leer la base del día para quitar (`TramosBase(dia)`).
- `Modules/Agenda/Features/DiasDeLaAgendaHandler.cs` -- patrón de contactos en lote y "sin correo" (`Correo is null`).
- `Modules/Pacientes/Contracts/IPacientesApi.cs:73` -- `ObtenerContactosAsync(ids)` → `Contactos(Nombre, Correo)`.
- `UI/Pages/Reserva/FormatoDeCita.cs` -- `Hora`, `Largo`.
- Pruebas: `tests/…UI.Tests/CalendarioDeAtencionTests.cs` (fakes de la 6.1), `tests/…IntegrationTests/ElegirCitaPorCitaTests.cs` (escenario del sábado con cuatro Citas).

## Tasks & Acceptance

**Execution:**
- [x] `Modules/Agenda/Contracts/CitasFueraDelHorario.cs` -- consulta (Fecha, Tramos, o Quitar) → Tramos con que se comparó, origen (base o custom) y Citas fuera (CitaId, hora local de inicio y fin, Paciente, sin correo, origen); `PrintMembers` oculta el nombre.
- [x] `Modules/Agenda/Features/CitasFueraDelHorarioHandler.cs` + alta en `Registro.cs`.
- [x] `UI/Pages/Administracion/ListaDeCitasFuera.razor` -- componente: filas con hora, Paciente, "sin correo", cancelar/conservar (radios con nombre accesible) y "Cancelar todas".
- [x] `UI/Pages/Administracion/CalendarioDeAtencion.razor` -- vista previa antes de guardar o quitar; manda decisiones; conflicto; "Quitar horario propio" con los Tramos de la base; mensaje de éxito con cancelaciones; clase por tipo de día.
- [x] `wwwroot/css/consultorio.css` -- lista y variantes de tipo de día.
- [x] Pruebas: integración de la consulta (custom y quitar, sin correo, origen, sin sesión); bUnit de cada fila de la matriz.

**Acceptance Criteria:**
- Given UJ-7 en la página, when Gerardo toca "Cancelar todas", cambia la 12:30 a conservar y guarda, then el comando recibe tres cancelar y una conservar y la página dice que se cancelan en unos instantes.
- Given un mes con los tres tipos de día con custom, when se pinta, then cada uno lleva su clase propia además de su texto.

## Implementation Notes

- Implementado y revisado en esta sesión (el entorno niega los subagentes; misma decisión que en la 6.1 y la 6.2).
- `GuardarAsync` pide la vista previa cuando los Tramos del formulario están completos y cambiaron desde la última; si hay Citas fuera, se detiene para que el Administrador elija y el siguiente "Guardar" manda la lista. Con Tramos incompletos va directo al comando, que dice qué falta.
- Las elecciones se guardan por `CitaId` (nulo: sin elegir) y sobreviven a la recarga por conflicto; las nuevas toman la preselección del Assumption de RN-10.
- "Quitar horario propio" abre un panel aparte del formulario (`_quitando`) con los Tramos de la base y, si hay, la misma lista.
- Clases nuevas por tipo de día: `--no-se-atiende`, `--especial`, `--abierto`.
- El texto de conflicto cambió: ahora la página recarga la lista ("La lista de citas cambió mientras la revisabas…").
- Pruebas: `CitasFueraDelHorarioTests` (3, integración) y 7 bUnit nuevas en `CalendarioDeAtencionTests`. Suite completa: 1,076 pasan.

## Plan Change Log

## Review Triage Log

**Pasada 1 (lente Quick, en esta sesión): 0 high, 0 medium, 1 low, 1 false, 0 maybe-false.**

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Al aparecer la lista, el foco no se mueve ni se anuncia: con lector de pantalla puede pasar inadvertida. | low | rechazado | El botón sigue siendo "Guardar" y la lista queda justo encima; el arreglo pide manejo de foco con JS. Queda para el recorrido de la 6.8. |
| Las casillas marcadas por "Cancelar todas" no se verían marcadas en el navegador porque `checked` es un atributo. | false | rechazado | Blazor aplica `checked` de un `input` como propiedad del elemento, no solo como atributo; la prueba bUnit confirma el estado tras el clic. |

## Verification

**Commands:**
- `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"; dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.
