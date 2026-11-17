---
title: 'Dar horario propio a una fecha'
type: 'feature'
ticket: '1'
created: '2026-11-17'
status: done
baseline_revision: '494eb1f2fb1d9c936e8ad2b893d109bc58f1daf1'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-draselina/architecture-appointment-booking/ddd-and-data-model-appointment-booking.md'
  - '{project-root}/_bmad-output/initiative-draselina/ux-appointment-booking/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** El Administrador no puede dar a una fecha su horario propio (CAP-11): `AgendaAtencion` ya sabe registrar y cambiar una custom, pero ningún comando ni pantalla lo usa, y abrir un sábado o recortar un martes exige tocar la base de datos.

**Approach:** Camino trazador de la épica 6: el comando `GuardarAgendaCustom` registra o cambia la custom de una fecha bajo el candado de fecha, con bitácora del Administrador, y devuelve conflicto si quedan Citas fuera (la elección por Cita es de la 6.2); la página "Calendario de atención" muestra el mes y edita la fecha tocada; un servicio "Citas que quedan fuera" que reusará la 6.4.

## Boundaries & Constraints

**Always:**
- Una Cita queda fuera si está Agendada, empieza después de ahora y no cabe completa en un solo Tramo nuevo de su fecha (AD-6, RN-9); la regla es `Cita.QuedaFueraDe`, no se reimplementa.
- Orden del handler: actor, transacción, `ICandadoDeFecha.TomarAsync(fecha)`, y solo después leer la agenda y las Citas (AD-26, RN-20). Si quedan Citas fuera: `Fallo(CitasFuera)`, deshacer, nada guardado.
- Duración mínima de INV-A6 = la del Tipo de visita activo más corto (`ITiposDeVisitaActivos`).
- Registrar si la fecha no tiene custom, cambiar si la tiene; bitácora `AgendaCustom` / `Registrada` o `Cambiada`, `EntidadId` = fecha `yyyy-MM-dd`, actor Administrador con su id, detalle antes y después (motivo y Tramos).
- `IRequiereAdministrador`; sin sesión, No autorizado (AD-22).
- La pantalla dice "no se atiende", "horario especial" o "día abierto" con su motivo, con la misma clasificación que `TipoDeDia` de la Agenda (no una segunda).
- Navegación: agregar "Calendario de atención" en `LayoutAdministracion` (Traspaso de la épica 5).

**Never:**
- Borrar la custom, elegir cancelar o conservar Citas, eventos de dominio, P-1: son de la 6.2 y la 6.3.
- Cancelar o mover una Cita (RN-8).
- Guardar una custom de una fecha anterior a hoy (local): `Fallo(FechaPasada)` antes de abrir la transacción.
- Motivo de la custom en registros o telemetría (como `DiaDeLaAgenda`).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Abrir sábado | Sáb. sin custom, 10:00–13:00, "sábado abierto" | Registrada; `/reservar` ofrece ese sábado, el siguiente no | — |
| Cerrar día sin Citas | Mié. sin Citas, cero Tramos, "vacaciones" | Registrada, "no se atiende" | — |
| Cambiar custom | Fecha con custom, otros Tramos | Cambiada; bitácora con antes y después | — |
| Citas fuera | Mar. con Cita Agendada 17:30, Tramo 15:30–17:00 | Nada guardado | `Fallo(agenda.citas_fuera)` |
| Cita ya pasada o cancelada | Igual, pero la Cita ya empezó o está cancelada | Registrada | — |
| Encimados | 10:00–12:00 y 11:00–13:00 | Nada guardado | `Fallo(agenda.tramos_encimados)` |
| Tramo corto | 10:00–10:15 con tipo de 30 min | Nada guardado | `Fallo(agenda.tramo_corto)` |
| Sin motivo / inicio ≥ fin | — | Nada guardado | `Invalido` del campo (AD-12) |
| Fecha pasada | Ayer | Nada guardado | `Fallo(agenda.fecha_pasada)` |
| Sin sesión | — | — | No autorizado |

</frozen-after-approval>

## Code Map

- `Modules/Agenda/Domain/AgendaAtencion.cs` -- `RegistrarCustom`/`CambiarCustom`/`TramosVigentes` y `RechazoDeAgendaAtencion`; no cambiar sus reglas.
- `Modules/Agenda/Domain/IAgendaAtencion.cs`, `Infrastructure/AgendaAtencionPersistida.cs` -- `ObtenerAsync(hoy)` y `GuardarAsync`; lanza con custom pasada.
- `Modules/Agenda/Domain/Cita.cs:293` -- `Cita.QuedaFueraDe(rango, vigentes)`.
- `Modules/Agenda/Features/AgendarCitaPorConsultorioHandler.cs` -- patrón a copiar: actor e id, `ITransaccionDeAgenda`, candado, `IBitacoraDeAgenda`, `Deshacer` en cada fallo.
- `Modules/Agenda/Features/ICitasDeLaAgenda.cs` -- Citas de un rango UTC con estado e inicio/fin: base del servicio de Citas fuera.
- `Modules/Agenda/Features/IAgendaDelRango.cs` + `DiasDeLaAgendaHandler.cs:71` -- lectura del mes y la clasificación `Tipo(...)` que se extrae para compartir.
- `Modules/Agenda/Contracts/DiasDeLaAgenda.cs` -- `TipoDeDia`, `TramoDeLaAgenda`, `PrintMembers` que oculta el motivo.
- `Modules/Agenda/Contracts/AgendarCitaEnLinea.cs:102` -- `CodigosDeAgenda`.
- `Modules/Agenda/Infrastructure/Registro.cs` -- alta explícita de handlers y puertos.
- `UI/Pages/Administracion/AgendarCita.razor` -- patrón de página: `[Authorize]`, InteractiveServer, handlers inyectados, `ErroresDelResultado`.
- `UI/Layout/LayoutAdministracion.razor` -- navegación.
- Pruebas: `tests/…IntegrationTests/AgendarCitaPorConsultorioTests.cs` (escenario, reloj, sesión), `DiasDeLaAgendaTests.cs`; bUnit en `tests/…UI.Tests/AgendaTests.cs`; dominio en `AgendaAtencionTests.cs`.

## Tasks & Acceptance

**Execution:**
- [x] `Modules/Agenda/Contracts/GuardarAgendaCustom.cs` -- comando (Fecha, Motivo, Tramos con Inicio/Fin) con DataAnnotations, `ICommand<AgendaCustomGuardada>`, `IRequiereAdministrador`, `ToString` sin motivo; códigos nuevos en `CodigosDeAgenda`.
- [x] `Modules/Agenda/Contracts/MesDeAtencion.cs` -- consulta del mes (año, mes) → días con `TipoDeDia`, motivo, Tramos vigentes y si hay custom; `IRequiereAdministrador`.
- [x] `Modules/Agenda/Features/CitasQueQuedanFuera.cs` -- servicio: dada una fecha y sus Tramos nuevos, las Citas Agendadas futuras que quedan fuera (id, paciente, inicio, fin, origen), sobre `ICitasDeLaAgenda` y `Cita.QuedaFueraDe`; documentado como compartido con la 6.4.
- [x] `Modules/Agenda/Features/GuardarAgendaCustomHandler.cs` -- orquesta según Always; mapea `RechazoDeAgendaAtencion` a códigos.
- [x] `Modules/Agenda/Features/MesDeAtencionHandler.cs` + extraer la clasificación de `DiasDeLaAgendaHandler` a un helper compartido.
- [x] `Modules/Agenda/Infrastructure/Registro.cs` -- dar de alta lo nuevo.
- [x] `UI/Pages/Administracion/CalendarioDeAtencion.razor` -- `/admin/calendario`: mes con navegación, cada día con sus Tramos o su tipo y motivo; al tocar un día de hoy en adelante, formulario con Tramos (agregar y quitar filas; ninguno = no se atiende) y motivo, precargado con sus Tramos vigentes; mensajes por código; en conflicto, "Esta fecha tiene Citas que quedarían fuera" sin guardar.
- [x] `UI/Layout/LayoutAdministracion.razor` -- enlace "Calendario de atención".
- [x] Pruebas: dominio si falta algo; integración de cada fila de la matriz más bitácora y `HorariosLibres`; integración de `MesDeAtencion`; bUnit de la página (tipos de día, formulario, mensaje de conflicto, enlace en navegación).

**Acceptance Criteria:**
- Given el AppHost arriba, when Gerardo da al sábado 10:00–13:00 "sábado abierto", then ese sábado aparece en `/reservar` con Horarios libres y el siguiente no.
- Given una custom guardada, when se abre el Calendario de atención, then ese día muestra su tipo y su motivo y la Agenda (5.2) lo marca igual.
- Given una custom rechazada por cualquier fila de error, when se consulta la base, then no hay fila nueva en `agenda_custom` ni en `bitacora`.

## Implementation Notes

- Implementado directo en esta sesión: el entorno negó lanzar el subagente de implementación.
- SDK: `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"` (el `dotnet` del PATH no trae 10.0.4xx).
- La forma de los Tramos la valida `GuardarAgendaCustom` como `IValidatableObject`, con el error bajo `Tramos[i]`; el decorador `ValidacionDeComandos` la aplica.
- `CitasQueQuedanFuera` se registra como servicio con ámbito; lee por `ICitasDeLaAgenda` (todas las Citas del día) y filtra Agendadas con inicio después de ahora.
- Clasificación del día extraída a `Features/ClasificacionDelDia.cs`; `DiasDeLaAgendaHandler` ya la usa.
- Sin Tipos de visita activos, el handler lanza `InvalidOperationException`: hoy no puede pasar (no hay cómo desactivarlos hasta la 6.7). La 6.7 debe decidir qué pasa si se desactivan todos.
- La página calcula hoy con `ZonaDelConsultorio` (la UI no puede usar `HoraLocal` del dominio, como en la Agenda) y usa las clases existentes `btn`, `dsm-boton-neutro`, `visually-hidden`; CSS nuevo `dsm-calendario__*` al final de `consultorio.css`.
- `PacientesBusquedaEHistorialTests` fijaba la navegación en Agenda y Pacientes: ahora incluye "Calendario de atención".
- Pruebas: `GuardarAgendaCustomTests` (15, integración) y `CalendarioDeAtencionTests` (12, bUnit). Suite completa: 1,052 pasan.

- Revisión: el entorno negó el subagente revisor; Gerardo eligió que la lente Quick la haga esta misma sesión, sin mirada independiente (2026-11-17).

## Plan Change Log

## Review Triage Log

**Pasada 1 (lente Quick, en esta sesión): 0 high, 0 medium, 3 low, 1 false, 0 maybe-false.**

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| `CalendarioDeAtencion.razor` repetía el texto de las marcas de `Agenda.Marca`: si una cambia, el Calendario y la Agenda dicen cosas distintas del mismo día. | low | patch | `Agenda.Marca(TipoDeDia, motivo)` es ahora la única; el Calendario la llama. |
| El AC "la Agenda (5.2) lo marca igual" no tenía prueba que guardara por el comando y leyera `DiasDeLaAgenda`. | low | patch | `Abrir_un_sabado…` ahora comprueba `DiaAbierto` y el motivo en `DiasDeLaAgenda`. |
| La página consulta el mes dos veces (prerenderizado y circuito); la Agenda lo evita con `[PersistentState]`. | low | rechazado | Sin efecto para el usuario más que una consulta extra; el arreglo agrega estado persistente y lógica de vigencia. |
| `CitasQueQuedanFuera` podría leer fuera de la transacción y perder una Cita recién reservada. | false | rechazado | `CitasDeLaAgenda` usa el mismo `AgendaDbContext` con ámbito que abrió la transacción, y lee después del candado de fecha que toma también toda reserva (AD-26). |

Verificación después de los parches: `dotnet build` sin advertencias; `dotnet test` 1,052 pasan. Queda sin correr el chequeo manual con el AppHost (primera fila de la matriz en `/admin/calendario` y `/reservar`).

## Design Notes

`[ASSUMPTION]` En la 6.1 el comando no lleva aún la lista vista: cualquier Cita fuera es conflicto. La 6.2 agrega la lista con la decisión por Cita al mismo comando.

`[ASSUMPTION]` Guardar con los mismos Tramos y motivo es éxito sin bitácora.

`[ASSUMPTION]` El Calendario abre en el mes actual, navega por meses, y los días pasados se ven pero no se editan.

## Verification

**Commands:**
- `dotnet build` -- sin errores ni advertencias nuevas.
- `dotnet test` -- todas las pruebas pasan (integración con PostgreSQL real por el arnés).

**Manual checks (if no CLI):**
- Con el AppHost, recorrer la primera fila de la matriz en `/admin/calendario` y `/reservar`.
