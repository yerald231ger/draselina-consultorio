---
title: 'Agenda por día y por semana'
type: 'feature'
ticket: '2'
created: '2026-11-09'
status: 'built'
baseline_revision: '2518d3f0906c43559fc7a782c6be18b292a7e8be'
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

**Problem:** La Agenda de la 5.1 solo muestra hoy, sin moverse de día, sin semana, sin las marcas que el Administrador necesita para resolver excepciones (sin correo, fuera de horario, Agenda custom) y sin refrescarse; la alerta de cancelación tardía no lleva a ella (CAP-12, FR-14).

**Approach:** Una consulta de administración de Agenda que da varios días de una vez (cada uno con su Agenda custom y sus Citas ya marcadas) reemplaza a `CitasDeLaFecha`; la página la usa en vista de día y de semana, con navegación por la dirección, filtro de canceladas y una nueva consulta cada 30 segundos; la alerta lleva "Abrir la Agenda" a la fecha de la Cita.

## Boundaries & Constraints

**Always:**
- La página no compone módulos (AD-10): la consulta reúne Citas, Tramos vigentes y Agenda custom de Agenda, y contactos en lote de Pacientes. Lleva `IRequiereAdministrador` (AD-22). Ninguna marca se decide en la página.
- Atendida se deriva (RN-19, RN-31). "Fuera de horario" = Cita de origen consultorio que no cabe completa en ningún Tramo vigente de su fecha (INV-C6); su nota se muestra. Una en línea nunca lleva la marca.
- "sin correo" = el Paciente no tiene correo guardado (RN-30), en cualquier estado de la Cita.
- La fecha y la vista viajan en la dirección: `/admin?fecha=2026-11-10&vista=semana`; sin parámetros, hoy en vista de día. Fechas en hora local del consultorio (AD-17). Nada de datos personales en la dirección (AD-11, AD-23).
- Canceladas ocultas por omisión; un control las muestra, con su distinción (por el Paciente, tardía, por el Consultorio) y los colores de la 5.1 (ámbar solo la tardía).
- Refresco: la página vuelve a consultar cada 30 segundos mientras el circuito está abierto `[ASSUMPTION de AD-11]`; el intervalo se inyecta para las pruebas; el temporizador se libera al salir.
- La semana muestra siempre los siete días, de lunes a domingo; un día sin Tramos se ve atenuado con "No se atiende" y, si tiene Citas manuales, ahí aparecen (Gerardo, 2026-11-09).
- Textos del día con Agenda custom, siempre con su motivo: "No se atiende · <motivo>" (custom sin Tramos), "Día abierto · <motivo>" (custom en un día cuya base no tiene Tramos) y "Horario especial · <motivo>" (cualquier otra custom, recorte o ampliación) (Gerardo, 2026-11-09).
- Plan conservado completo, por encima de 1,600 tokens (Gerardo, 2026-11-09).
- Vista del día usable en un teléfono; la de semana apila los días en pantallas angostas.
- "Abrir la Agenda" en la alerta apunta a `{UrlDelSitio}/admin?fecha=<fecha local de la Cita>`, armada por `IDireccionDelSitio`; el resto de la alerta no cambia.

**Never:** enlaces al Detalle de Cita (entrada 6), botón "Agendar cita" (entrada 4), acciones sobre Citas, editar Agenda custom (épica 6), canal en tiempo real (AD-11), nombre o motivo en registros (AD-23).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Día sin parámetros | GET `/admin`, hoy lunes 9 | Vista de día del 9, botones "Anterior", "Hoy", "Siguiente", cambio a semana | — |
| Navegar | "Siguiente" en vista de día | `/admin?fecha=2026-11-10&vista=dia`, Citas del 10 | — |
| Fecha inválida | `?fecha=hola` | Hoy | Sin error |
| Semana | `?fecha=2026-11-11&vista=semana` | Lunes 9 a domingo 15, cada día con sus Citas | — |
| Sin correo | Paciente sin correo | Etiqueta "sin correo" | — |
| Fuera de horario | Cita del consultorio sábado 10:15 sin Tramos, nota "lo pidió Selina" | Marca "Fuera de horario" y la nota | — |
| Consultorio dentro | Cita del consultorio martes 4:00 dentro del Tramo | Sin marca | — |
| Agenda custom | Martes con custom 15:30–17:00 "cirugía" | El día se distingue y dice el motivo | — |
| Canceladas | 1 Agendada, 1 Cancelada tardía | Por omisión solo la Agendada y un aviso de 1 cancelada oculta; con el control, las dos | — |
| Refresco | Página abierta; se agenda una Cita en línea para ese día | Aparece en la siguiente vuelta, sin recargar | Si una vuelta falla, se conserva lo último mostrado |
| Alerta | Cancelación tardía de una Cita del jueves 12 | El correo trae "Abrir la Agenda" a `/admin?fecha=2026-11-12` | — |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/Agenda.razor` -- página de la 5.1: `[Authorize]`, Interactive Server, `[PersistentState] AgendaCargada` (el prerender pasa al circuito), etiquetas y clases de chip (`--tardia` ámbar, `--neutro`). Se reescribe sobre la consulta nueva; conserva `[PersistentState]` (la clave debe incluir fecha y vista: si cambian, se consulta de nuevo), las etiquetas y `DiaVacio`, `PacienteNoEncontrado`, `NoSeCargo`.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/CitasDeLaFecha.cs`, `Features/CitasDeLaFechaHandler.cs`, `Features/ICitasDeLaFecha.cs`, `Infrastructure/CitasDeLaFechaPersistidas.cs` -- consulta de la 5.1; solo la usa la Agenda. Se reemplaza por la de varios días (mismo patrón: handler en Features, puerto de lectura en Infrastructure, registro en `Infrastructure/Registro.cs`); `EstadoEnLaAgenda` y `CancelacionEnLaAgenda` se conservan.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/TramosDeLaFecha.cs` -- lee base del día y custom de la fecha y deja a `AgendaAtencion.Reconstituir(...).TramosVigentes(fecha)` decidir (INV-V1). Patrón a seguir para un rango: una lectura de la base y de las customs del rango, no una por día.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/AgendaAtencion.cs` -- `TramosVigentes(fecha)` (con `OrigenDeTramos`), `AgendaCustom.Motivo`, `TramosBase(dia)`. `Domain/Tramo.cs:46` `Contiene(inicio, fin)` decide si una Cita cabe en un Tramo.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/EstadoCita.cs` -- `OrigenDeCita.Consultorio`; `Cita.Nota`.
- `src/DraSelina.Consultorio.Web/Modules/Pacientes/Contracts/IPacientesApi.cs` -- `ObtenerContactosAsync(ids)` en lote (5.1): `Contactos.Correo` nulo = sin correo.
- `src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/PlantillaDeAlertaTardia.cs`, `AlertaDeCancelacionTardia.cs`, `IDireccionDelSitio.cs`, `Infrastructure/DireccionDelSitio.cs`, `MarcoDeCorreo.Boton` -- la alerta sin botón; el patrón del botón está en `PlantillaDeCancelacion.cs:117`. Quitar el comentario "Sin el botón…".
- `src/DraSelina.Consultorio.Web/Identidad/Registro.cs` -- `RegresoSeguro` ya acepta `/admin?…`: el regreso tras entrar conserva fecha y vista.
- `src/DraSelina.Consultorio.Web/UI/Pages/Reserva/FormatoDeCita.cs` -- formatos de fecha y hora en español que ya usa la Agenda.
- `src/DraSelina.Consultorio.Web/wwwroot/css/consultorio.css` -- clases `dsm-agenda__*` y chips de la 5.1; tokens de `DESIGN.md`.
- `tests/DraSelina.Consultorio.IntegrationTests/CitasDeLaFechaTests.cs` -- escenario con inserción directa en `agenda.cita`; se adapta a la consulta nueva. `AlertaDeCancelacionTardiaTests.cs` -- donde va la prueba del botón.
- `tests/DraSelina.Consultorio.UI.Tests/AgendaTests.cs` -- bUnit de la 5.1 con handler falso; se adapta.

## Tasks & Acceptance

**Execution:**
- [x] `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/` -- consulta `DiasDeLaAgenda(Desde, Hasta)` (`IRequiereAdministrador`, a lo más 7 días; un rango inválido vuelve `Resultado` inválido) que da por fecha: el tipo de día (base con Tramos, sin Tramos, o custom con su motivo y si no se atiende), sus Tramos vigentes y sus Citas con lo de la 5.1 más `SinCorreo`, `FueraDeHorario` y `Nota`. `ToString` sin nombre, motivo ni nota. Se borra `CitasDeLaFecha`.
- [x] `src/DraSelina.Consultorio.Web/Modules/Agenda/Features/` + `Infrastructure/` + `Registro.cs` -- handler y puertos de lectura del rango (Citas en cualquier estado; base y customs del rango en una lectura cada uno).
- [x] `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/Agenda.razor` (+ componentes que hagan falta en `UI/Pages/Administracion/`) -- vista de día y de semana, navegación por la dirección, filtro de canceladas, marcas, refresco con intervalo inyectable, `[PersistentState]` con fecha y vista.
- [x] `src/DraSelina.Consultorio.Web/wwwroot/css/consultorio.css` -- semana en columnas en escritorio, apilada en teléfono; marcas del día custom y de fuera de horario con los tokens de `DESIGN.md`.
- [x] `src/DraSelina.Consultorio.Web/Modules/Notificaciones/...` -- `IDireccionDelSitio.Agenda(fecha)`, botón "Abrir la Agenda" en la alerta (HTML y texto).
- [x] Pruebas: integración de la consulta (cada fila de la matriz que toca datos: semana, sin correo, fuera y dentro de horario, custom con motivo y "no se atiende", rango inválido, sin sesión), bUnit de la página (navegación, fecha inválida, semana, filtro, refresco con intervalo corto y handler que devuelve una Cita nueva en la segunda vuelta, vuelta fallida conserva lo mostrado), integración de la alerta con el enlace.

**Acceptance Criteria:**
- Given la Agenda abierta en el AppHost, when un Paciente reserva en línea para ese día, then la Cita aparece en menos de 30 segundos sin recargar.
- Given `/admin` sin sesión con `?fecha=…&vista=semana`, when se entra con contraseña y código, then se llega a esa semana.
- Given una página de administración que deja de mostrarse, when pasa el intervalo, then no se consulta más (el temporizador se liberó).

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 0, low 5, false 1, maybe-false 0; 4 a patch, 1 rechazado (low con arreglo que agrega guardas), 1 falso.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | `Agenda.razor`: `?fecha=9999-12-31` o `0001-01-01` lanzan en `AddDays` (página y handler) | low | rechazado | Real, pero solo con una dirección escrita a mano en un área de un solo usuario; el arreglo agrega una guarda de rango en página y handler. |
| 2 | `Agenda.razor` `OnParametersSetAsync`/`UnaVueltaAsync`: al navegar se pinta el encabezado nuevo sobre los días viejos, y una vuelta de refresco con la fecha vieja que termina después de navegar escribe `Cargada` de la fecha vieja | low | patch | `_fecha` y `_semana` cambian antes del `await`; el marcado no revisa `Cargada.EsDe(_fecha, _semana)`; `UnaVueltaAsync` guarda sin comparar con la fecha vigente. Se ve en cada navegación. |
| 3 | `Agenda.razor` `RefrescarAsync`: una `OperationCanceledException` que no viene de salir de la página escapa del ciclo lanzado con `_ =` y el refresco se detiene sin registro | low | patch | `UnaVueltaAsync` excluye toda OCE del `catch`; `RefrescarAsync` solo la atrapa si `cancelacion.IsCancellationRequested`. Arreglo directo: en la vuelta, atrapar la OCE que no es del cierre. |
| 4 | `Agenda.razor`: "hoy" solo se calcula en `OnParametersSetAsync`; abierta en `/admin` después de medianoche sigue pidiendo y diciendo "Hoy" del día anterior | low | patch | `_hoy` y `_fecha` (sin `fecha` en la dirección) no se recalculan en `UnaVueltaAsync`. La página está hecha para quedarse abierta. |
| 5 | `Agenda.razor` `OnParametersSetAsync`: si la consulta de una navegación lanza, el circuito muere | low | patch | El `await Consulta.HandleAsync` no tiene `try/catch`, a diferencia de la vuelta de refresco. Arreglo directo: atrapar y mostrar `NoSeCargo` o conservar lo mostrado. |
| 6 | `DiasDeLaAgendaHandler.Cita`: un Paciente que Pacientes no conoce sale "sin correo" (`[ASSUMPTION]`) | false | — | El plan define "sin correo" como "el Paciente no tiene correo guardado (RN-30)": un Paciente que no existe no tiene correo guardado; es la única lectura. Además Pacientes nunca borra (no hay ARCO en el piloto). Se le informa a Gerardo. |

## Design Notes

Tipo de día para la marca: custom sin Tramos → "No se atiende"; custom en un día cuya base no tiene Tramos → día abierto; cualquier otra custom → horario cambiado. Las tres llevan el motivo.

## Verification

**Commands:**
- `dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.

**Manual checks (if no CLI):**
- Con el AppHost: semana con un sábado manual visible; reservar en línea con la Agenda abierta y verla aparecer.
