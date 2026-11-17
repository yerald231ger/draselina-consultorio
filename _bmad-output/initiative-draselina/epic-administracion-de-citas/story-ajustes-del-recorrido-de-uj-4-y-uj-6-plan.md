---
title: 'Ajustes del recorrido de UJ-4 y UJ-6'
type: 'bugfix'
ticket: '10'
created: '2026-11-16'
status: 'built'
baseline_revision: '77fffaa132ea6d98df7fde8d4f22c61633802a82'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** El recorrido de la entrada 8 (su plan, "Resultado del recorrido") encontró una falla y cinco detalles en la administración. La falla: con la sesión de administración abierta en el mismo navegador, una reserva desde `/reservar` (o un cambio por el Enlace de gestión) queda en la bitácora y en sus eventos a nombre del Administrador, contra AD-22.

**Approach:** `ActorDelHost` da Administrador solo cuando hay sesión y la petición es del área de administración; además, los cinco ajustes de pantalla, cada uno con su prueba.

## Boundaries & Constraints

**Always:**
- **Actor (AD-22):** Administrador solo si el usuario está autenticado y la petición es del área `/admin` o llega por el circuito de Interactive Server (solo la administración abre circuito, AD-11). Una petición HTTP fuera de `/admin` (la reserva pública, `/c/...`, el archivo de calendario) es Paciente aunque traiga la cookie de sesión. `Sistema` sigue igual. Un solo servicio decide (AD-22); ningún handler cambia.
- **Pacientes:** al volver a `/admin/pacientes` no se ve la lista completa antes de la filtrada: mientras la búsqueda recordada no se conoce (el prerender), la página no pinta la lista; la pinta al conectarse el circuito, ya filtrada `[ASSUMPTION: un estado "Cargando…" breve en lugar de la lista]`.
- **Detalle de Cita:** se quitan los campos Fecha y Hora; quedan en el encabezado.
- **Agendar cita:** el error de un campo desaparece cuando ese campo cambia; los demás errores siguen hasta el siguiente envío.
- **Mayúsculas:** "por el Consultorio" y "por el Paciente" con mayúscula, como en `EXPERIENCE.md` (Glosario), en el chip de la Agenda, del Detalle y del historial, y en la bitácora; "tardía" igual que hoy.
- **Accesibilidad:** la casilla de Consentimiento y "Mostrar canceladas" se anuncian por su etiqueta y su estado (marcada / no marcada), no como "True" u "on".

**Never:** cambiar reglas de dominio, comandos, consultas o textos fuera de los seis puntos; tocar el sitio público (sus pendientes están en `deferred-work.md`); "Atendida" contra "En curso" y el "aviso 2026-11-borrador" (Open question de la épica).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Reserva con sesión | Cliente con la cookie de administración reserva en `/reservar` | Bitácora "Agendada" y `CitaAgendada` con actor Paciente | — |
| Enlace con sesión | Mismo cliente cancela por `/c/<token>` | Bitácora y `CitaCancelada` con actor Paciente | — |
| Reserva sin sesión | Cliente anónimo | Paciente (sin cambio) | — |
| Comando de administración | Cancelar por el Consultorio desde el Detalle (circuito) | Administrador con su id (sin cambio) | — |
| Comando marcado sin sesión en `/admin` | — | No autorizado (sin cambio) | — |
| Volver a Pacientes | Búsqueda "dobl" recordada | Nunca se pinta la lista completa; se ve "Cargando…" y luego solo la filtrada | — |
| Detalle | Cualquier Cita | Sin campos Fecha y Hora; el encabezado los conserva | — |
| Error que se corrige | "Falta el tipo de consulta." y se elige un tipo | El error del tipo desaparece; el de otro campo sigue | — |
| Mayúsculas | Cita cancelada por el Consultorio | Chip "Cancelada (por el Consultorio)"; bitácora "Cancelada · por el Consultorio · Administrador" | — |
| Accesibilidad | Casilla de Consentimiento y "Mostrar canceladas" | Cada `input` tiene su etiqueta asociada y no expone "True"/"on" como nombre | — |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/ActorDelHost.cs` -- hoy: autenticado = Administrador en cualquier ámbito. Agregar el criterio del área: en una petición HTTP (hay `HttpContext`), solo si la ruta empieza con `/admin`; en el circuito (sin `HttpContext` de petición), como hoy. `IHttpContextAccessor` no se usa todavía en el proyecto: registrarlo en `Program.cs` o tomar la ruta de otra forma confiable para SSR estático y endpoints.
- `src/DraSelina.Consultorio.Web/Program.cs` -- composición del actor y del pipeline.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs`, `CancelarCitaPorEnlaceHandler.cs`, `ReprogramarCitaPorEnlaceHandler.cs` -- piden el actor a `IActorActual`; no se tocan.
- `tests/DraSelina.Consultorio.IntegrationTests/ActorYAdministracionTests.cs` (`FijarUsuario` sin `HttpContext`), `EntrarConSegundoFactorTests.ClienteConSesionAsync`, `ReservaEnLineaTests` (cliente con cookies que envía Tus datos con antiforgery) -- para la prueba HTTP: cliente con sesión que reserva y cancela por enlace.
- `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/Pacientes.razor`, `BusquedaRecordada.cs` -- prerender con ámbito nuevo pinta la lista sin filtrar; `RendererInfo.IsInteractive` distingue.
- `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/DetalleDeCita.razor` -- campos Fecha y Hora; `Que(...)` de la bitácora con "por el Paciente"/"por el Consultorio".
- `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/Agenda.razor` -- `Etiqueta(...)` (chips "Cancelada (por el consultorio)", "(por el paciente)"), usada también por Detalle e historial; casilla "Mostrar canceladas".
- `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/AgendarCita.razor`, `UI/Componentes/ErroresDelResultado.razor`, `Campo.razor` -- cómo se copian los errores del `Resultado` al `EditContext`; la casilla de Consentimiento (`InputCheckbox`).
- `tests/DraSelina.Consultorio.UI.Tests/` (`AgendaTests`, `DetalleDeCitaTests`, `AgendarCitaTests`, `PacientesTests`) -- adaptar las aserciones de textos y agregar las de cada ajuste.

## Tasks & Acceptance

**Execution:**
- [x] `src/DraSelina.Consultorio.Web/ActorDelHost.cs` (+ `Program.cs`) -- el criterio del área.
- [x] `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/Pacientes.razor` -- sin lista hasta conocer la búsqueda.
- [x] `DetalleDeCita.razor` -- sin Fecha y Hora; `Agenda.razor` y `DetalleDeCita.razor` -- mayúsculas.
- [x] `AgendarCita.razor` (o `ErroresDelResultado.razor`, si ahí es más limpio y no cambia el sitio público) -- el error del campo se borra al cambiarlo.
- [x] `Agenda.razor`, `AgendarCita.razor` -- etiquetas asociadas de las dos casillas.
- [x] Pruebas: integración HTTP del actor (cada fila de actor de la matriz), bUnit de cada ajuste de pantalla.

**Acceptance Criteria:**
- Given la sesión de administración y una reserva en línea en el mismo navegador, when se abre el Detalle de esa Cita, then la bitácora dice "Agendada · en línea · Paciente".

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 0, low 1, false 1, maybe-false 1; 1 a patch, 1 falso, 1 rechazado sin verificar.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | Casillas de Consentimiento y "Mostrar canceladas": "True" y "on" son su valor (`value="True"` de `InputCheckbox`, el valor por omisión del DOM), no la falta de etiqueta; el cambio solo agrega `aria-labelledby` y `for`/`id`, y las pruebas no lo distinguen | maybe-false | rechazado | Un lector de pantalla anuncia una casilla por su nombre accesible y su estado, no por su `value`; "True"/"on" pudo salir de la herramienta de árbol de accesibilidad de la segunda revisión, que muestra el valor. Se decide con VoiceOver en el AppHost. Si fuera real sería low. |
| 2 | `ActorFueraDeLaAdministracionTests`: la prueba HTTP con la cookie cubre cancelar por el Enlace de gestión, no reprogramar | low | patch | El Intent nombra "un cambio por el Enlace de gestión"; `ReprogramarCitaPorEnlaceHandler` pide el actor igual. Arreglo directo: un caso más en la prueba. |
| 3 | `ActorDelHost.EnElAreaDeAdministracion`: cualquier ámbito que no sea el de la petición cuenta como administración, más amplio que "el circuito" | false | — | Un ámbito nuevo creado durante una petición tiene su propio `AuthenticationStateProvider` sin estado: `ObtenerAsync` lanza "sin actor", nunca da Administrador. Y ningún código crea ámbitos dentro de una petición (solo `EntregaDeEventos` y `EnviosProgramados`, que declaran Sistema o usan el actor del evento). |

## Verification

**Commands:**
- `dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.

**Manual checks (if no CLI):**
- Con el AppHost: reservar en `/reservar` con `/admin` abierto en otra pestaña y ver el Detalle; volver a Pacientes con una búsqueda.
