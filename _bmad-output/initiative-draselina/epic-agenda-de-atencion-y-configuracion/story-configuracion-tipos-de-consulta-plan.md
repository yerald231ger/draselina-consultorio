---
title: 'Configuración: Tipos de consulta'
type: 'feature'
ticket: '7'
created: '2026-11-17'
status: done
baseline_revision: '14baa89b78c595db21afb2a70a7821798feab4b5'
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

**Problem:** Los Tipos de visita solo se crean por migración: Gerardo no puede agregar "Infiltración", cambiar una duración ni dejar de ofrecer un tipo (CAP-16).

**Approach:** `TipoDeVisita` gana `Cambiar` y `Desactivar`; tres comandos (crear, cambiar, desactivar) con bitácora; y la sección "Tipos de consulta" de Configuración, con la lista de tipos activos editable y un formulario para agregar uno.

## Boundaries & Constraints

**Always:**
- Decision (Gerardo, 2026-11-17): se editan nombre, descripción, duración, reservable en línea y orden; se crean y se desactivan; no se borran ni se reactivan. Desactivar lo quita de `/reservar` y de Agendar cita; sus Citas siguen en la Agenda y el historial.
- Decision (Gerardo, 2026-11-17): no se puede desactivar el último tipo activo: `Fallo(UltimoTipoActivo)` y nada cambia. Que ninguno sea reservable en línea sí se permite.
- Forma: nombre requerido (a lo más 80), descripción opcional (a lo más 300), duración entre 5 y 480 minutos `[ASSUMPTION: límites]`, orden de 0 a `short.MaxValue`.
- Cambiar o desactivar un tipo que no existe o ya está inactivo: `Fallo(TipoNoEncontrado)` `[ASSUMPTION: un inactivo no se edita]`. Desactivar dos veces no repite bitácora.
- Bitácora `TipoDeVisita` a nombre del Administrador con su id, `EntidadId` el id: `Registrada` al crear; `Cambiada` al cambiar o desactivar, con antes y después (desactivar: `activo` de verdadero a falso). Sin cambios: sin bitácora.
- Una duración nueva vale en la siguiente consulta de `HorariosLibres`; los Tramos ya guardados no se revalidan (T-6).
- `ITiposDeVisita` agrega obtener para cambiar, agregar y guardar (dominio 2.2); una consulta nueva da los tipos activos con descripción y orden para la sección.
- Sección propia en `Configuracion.razor`, después de "Avisos por correo"; desactivar pide confirmar.

**Never:**
- Borrar o reactivar tipos; tocar Citas existentes; cambiar `TiposDeVisitaActivos` ni `TiposDeVisitaReservables`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Crear solo del consultorio | "Infiltración", 45 min, no reservable | No está en `/reservar`, sí en Agendar cita; bitácora Registrada | — |
| Cambiar duración | "Primera consulta" 30 → 45 | La siguiente `HorariosLibres` ofrece rejilla de 45 | — |
| Desactivar | "Consulta de seguimiento" con una Cita | Fuera de `/reservar` y de Agendar cita; su Cita sigue en el historial | — |
| Último activo | Desactivar el único activo | Nada cambia | `Fallo(agenda.ultimo_tipo_activo)` |
| Inexistente o inactivo | Cambiar o desactivar | Nada cambia | `Fallo(agenda.tipo_no_encontrado)` |
| Forma | Sin nombre, duración 0 o 600 | Nada cambia | `Invalido` bajo el campo |
| Sin sesión | Los tres comandos | — | No autorizado |

</frozen-after-approval>

## Code Map

- `Modules/Agenda/Domain/TipoDeVisita.cs` -- `Crear`, `SeAgendaEnLinea`; agregar `Cambiar` y `Desactivar`.
- `Modules/Agenda/Domain/ITiposDeVisita.cs`, `Infrastructure/TiposDeVisita.cs` -- hoy solo `ObtenerAsync` sin rastrear.
- `Modules/Agenda/Features/ITiposDeVisitaActivos.cs`, `Infrastructure/TiposDeVisitaActivosPersistidos.cs` -- patrón del puerto de lectura.
- `Modules/Agenda/Contracts/TiposDeVisitaActivos.cs`, `TiposDeVisitaReservables.cs` -- lo que Agendar cita y `/reservar` ya filtran por activo y reservable.
- `Modules/Agenda/Features/CambiarReglasDeReservaHandler.cs` -- patrón de comando con transacción, bitácora y sin cambios.
- `Modules/Agenda/Contracts/AgendarCitaEnLinea.cs` -- `CodigosDeAgenda`.
- `UI/Pages/Administracion/Configuracion.razor`, `SeccionReglasDeReserva.razor`, `AvisoDeSeccion.razor` -- patrón de sección.
- Pruebas: `tests/…Domain.Tests` (nueva clase de `TipoDeVisita`); escenario de `CambiarReglasDeReservaTests`; `TiposDeVisitaSembrados`.

## Tasks & Acceptance

**Execution:**
- [x] `Domain/TipoDeVisita.cs` -- `Cambiar(...)`, `Desactivar()`; pruebas de dominio.
- [x] `Domain/ITiposDeVisita.cs` + `Infrastructure/TiposDeVisita.cs` -- obtener rastreado, agregar, guardar, contar activos.
- [x] `Contracts/CrearTipoDeVisita.cs`, `CambiarTipoDeVisita.cs`, `DesactivarTipoDeVisita.cs`, `TiposDeConsultaConfigurables.cs`; códigos nuevos.
- [x] `Features/` -- los tres handlers y el de la consulta (con su puerto de lectura); alta en `Registro.cs`.
- [x] `UI/Pages/Administracion/SeccionTiposDeConsulta.razor`; `Configuracion.razor` la agrega.
- [x] Pruebas de integración de cada fila de la matriz; bUnit de la sección (lista, editar, agregar, desactivar con confirmación, errores).

**Acceptance Criteria:**
- Given "Infiltración" creada, when el Administrador abre Agendar cita, then la ve con 45 min, y `/reservar` no la ofrece.
- Given una Cita de "Consulta de seguimiento", when ese tipo se desactiva, then el historial del Paciente sigue mostrando la Cita.

## Implementation Notes

- Implementado y revisado en esta sesión (el entorno niega los subagentes; misma decisión que en las entradas anteriores).
- `ITiposDeVisita.ContarActivosParaCambiarAsync` bloquea las filas activas con `FOR UPDATE` antes de leer el tipo: dos desactivaciones a la vez no dejan cero activos.
- Los tres handlers van en archivos propios; `DetalleDeTipo` guarda la bitácora en camelCase.
- Los formularios de cada tipo usan ids `tipo-<guid>` (un id que empieza con dígito no sirve como selector CSS).
- La nota de la 6.1 (sin tipos activos el comando lanza) ya no puede ocurrir: no se desactiva el último.
- Pruebas: `TipoDeVisitaTests` (3, dominio), `TiposDeVisitaEnConfiguracionTests` (7, integración), `SeccionTiposDeConsultaTests` (5, bUnit). Suite completa: 1,117 pasan.

## Plan Change Log

## Review Triage Log

**Pasada 1 (lente Quick, en esta sesión): 0 high, 0 medium, 1 low, 0 false, 0 maybe-false.**

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Con "último tipo activo" o "no encontrado", la sección mostraba además "No se pudo guardar. Intenta de nuevo.". | low | patch | El aviso genérico no recibe el Resultado cuando hay mensaje propio; la prueba bUnit comprueba que no aparece. |

## Verification

**Commands:**
- `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"; dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.
