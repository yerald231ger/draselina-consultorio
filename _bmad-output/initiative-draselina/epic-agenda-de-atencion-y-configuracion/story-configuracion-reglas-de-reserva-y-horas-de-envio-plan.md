---
title: 'Configuración: reglas de reserva y horas de envío'
type: 'feature'
ticket: '6'
created: '2026-11-17'
status: 'built'
baseline_revision: 'c02e5d77616e5a9becf8e7e0d9300a2a4b6dc774'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-draselina/architecture-appointment-booking/ddd-and-data-model-appointment-booking.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Las reglas de reserva (anticipación, ventana, umbral de cancelación tardía, tope semanal) solo cambian por base de datos, y las horas de envío tienen comando (4.7) pero no pantalla (CAP-16).

**Approach:** En Agenda, el comando `CambiarReglasDeReserva` sobre `ReglasDeReserva.Cambiar` con su bitácora, y su consulta. En Configuración, dos secciones nuevas, cada una con su formulario y su botón: "Reglas de reserva" (Agenda) y "Avisos por correo" (horas y destinatarios de Notificaciones, 4.7). Cada sección pide sus valores a su módulo (AD-14).

## Boundaries & Constraints

**Always:**
- `CambiarReglasDeReserva` (`IRequiereAdministrador`): los cuatro valores requeridos y a lo más `short.MaxValue` (la columna es `smallint`); los límites inferiores los decide `ReglasDeReserva.Cambiar` y su rechazo va como `Invalido` bajo la propiedad, con mensaje propio.
- Transacción, bitácora `ReglasDeReserva/Cambiada` a nombre del Administrador con su id, `EntidadId` "1", detalle con los cuatro valores antes y después. Los mismos valores: éxito sin bitácora.
- Consulta `ReglasDeReservaVigentes` (`IRequiereAdministrador`) con los cuatro valores.
- Surte efecto en la siguiente reserva, sin reiniciar: `HorariosLibres` ya lee las reglas en cada consulta.
- "Avisos por correo" usa `ConfiguracionDeEnvioVigente` y `CambiarConfiguracionDeEnvio` tal como están; sus errores de forma bajo cada campo.
- Cada sección es su componente y muestra "Se guardó" o sus errores; la página solo las compone.
- Etiquetas en lenguaje del Administrador, con la unidad (días, meses, minutos, citas por semana) `[ASSUMPTION: texto]`.

**Never:**
- Cambiar `ReglasDeReserva` ni el comando de Notificaciones. Tipos de consulta (6.7).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Subir la ventana | Ventana 2 → 3 | Guardado; la siguiente `HorariosLibres` llega al tercer mes | — |
| Valor negativo | Anticipación −1 | Nada cambia | `Invalido` bajo `AnticipacionMinimaDias` con su mensaje |
| Cero no permitido | Tope 0 o ventana 0 | Nada cambia | `Invalido` bajo esa propiedad |
| Mismos valores | Sin cambios | Éxito, sin bitácora | — |
| Hora del Recordatorio | 09:00 → 08:00 | Guardado en Notificaciones | — |
| Correo inválido | "selina@" | Nada cambia | Mensaje bajo el campo |
| Sin sesión | Cualquiera de los dos comandos | — | No autorizado |

</frozen-after-approval>

## Code Map

- `Modules/Agenda/Domain/ReglasDeReserva.cs` -- `Cambiar(...)` y `RechazoDeReglasDeReserva`; no cambia.
- `Modules/Agenda/Domain/IReglasDeReserva.cs`, `Infrastructure/ReglasDeReservaPersistidas.cs` -- obtener rastreado y guardar.
- `Modules/Agenda/Features/HorariosLibresHandler.cs:34` -- lee las reglas en cada consulta.
- `Modules/Agenda/Features/CambiarAgendaBaseHandler.cs` -- patrón de comando con transacción, bitácora y sin cambios.
- `Modules/Notificaciones/Contracts/CambiarConfiguracionDeEnvio.cs`, `ConfiguracionDeEnvioVigente.cs` -- comando y consulta de la 4.7.
- `UI/Pages/Administracion/Configuracion.razor`, `SeccionSemanaTipo.razor` -- página y patrón de sección (6.5).
- Pruebas: escenario de `CambiarAgendaBaseTests`; `ConfiguracionDeEnvioTests.cs` (4.7); fakes de `SeccionSemanaTipoTests`.

## Tasks & Acceptance

**Execution:**
- [x] `Modules/Agenda/Contracts/CambiarReglasDeReserva.cs`, `ReglasDeReservaVigentes.cs`.
- [x] `Modules/Agenda/Features/CambiarReglasDeReservaHandler.cs`, `ReglasDeReservaVigentesHandler.cs` + alta en `Registro.cs`.
- [x] `UI/Pages/Administracion/SeccionReglasDeReserva.razor`, `SeccionAvisosPorCorreo.razor`; `Configuracion.razor` las agrega después de la semana tipo.
- [x] CSS si hace falta.
- [x] Pruebas: integración de cada fila de Agenda de la matriz y de la consulta; bUnit de las dos secciones (cargar, guardar, errores bajo el campo).

**Acceptance Criteria:**
- Given la ventana en 3 meses, when un Paciente pide horarios, then aparecen fechas del tercer mes sin reiniciar la web.
- Given Configuración, when se abre, then muestra la semana tipo, las reglas y los avisos con sus valores guardados.

## Implementation Notes

- Implementado y revisado en esta sesión (el entorno niega los subagentes; misma decisión que en las entradas anteriores).
- El comando valida forma (requeridos y a lo más `short.MaxValue`); los límites inferiores los decide `ReglasDeReserva.Cambiar` y van como `Invalido` bajo su propiedad.
- Dos secciones nuevas, `SeccionReglasDeReserva` y `SeccionAvisosPorCorreo`, cada una con su formulario; `AvisoDeSeccion` pinta "Se guardó." o el error que no es de un campo.
- No hizo falta CSS más que el contenedor de sección.
- Pruebas: `CambiarReglasDeReservaTests` (9, integración) y `ReglasYAvisosDeConfiguracionTests` (4, bUnit). La hora del Recordatorio guardada en Notificaciones ya la cubren las pruebas de la 4.7. Suite completa: 1,102 pasan.

## Plan Change Log

## Review Triage Log

**Pasada 1 (lente Quick, en esta sesión): 0 high, 0 medium, 1 low, 0 false, 0 maybe-false.**

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| La bitácora de las reglas guardaba el detalle en PascalCase (`VentanaMeses`); el resto de la bitácora usa camelCase. | low | patch | Detalle con propiedades camelCase; la prueba lee `ventanaMeses`. |

## Verification

**Commands:**
- `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"; dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.
