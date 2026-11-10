---
title: 'Pacientes: búsqueda e historial'
type: 'feature'
ticket: '7'
created: '2026-11-10'
status: 'built'
baseline_revision: 'cc72d9bc19efa7a246ec718ccacd2e11720173f1'
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

**Problem:** El Administrador no puede buscar a un Paciente ni ver su historial de Citas, así que no identifica a quien cancela tarde o no se presenta seguido (CAP-15, FR-18).

**Approach:** Según la Decision del 2026-11-09, dos consultas de administración, cada una de su módulo: `BuscarPacientes` en Pacientes (sin citas, RN-29) y `HistorialDelPaciente` en Agenda (con el encabezado que pide a Pacientes). Dos páginas: la lista con búsqueda y la del Paciente con su historial, enlazado al Detalle de Cita; "Pacientes" entra a la navegación.

## Boundaries & Constraints

**Always:**
- `BuscarPacientes(Texto)` en `Pacientes.Contracts` (`IRequiereAdministrador`): busca sobre `nombre_busqueda` (minúsculas, sin acentos, AD-18), con el texto normalizado igual; da por Paciente id, nombre como se guardó, teléfono y si tiene correo. Nada de citas.
- La búsqueda es "contiene" en cualquier parte del nombre ("lop" encuentra a "Mariana López" y a "Rosa Lopera"); sin texto, la lista muestra todos los Pacientes en orden alfabético, hasta 50, con "Escribe un nombre para buscar entre los demás" cuando hay más (Gerardo, 2026-11-10).
- Plan conservado completo, por encima de 1,600 tokens (Gerardo, 2026-11-10).
- `HistorialDelPaciente(PatientId)` en `Agenda.Contracts` (`IRequiereAdministrador`): las Citas del Paciente, de la más reciente a la más antigua, con fecha, hora, tipo, origen y estado mostrado con su distinción (Agendada, Atendida, No se presentó, Cancelada por el Paciente, tardía o por el Consultorio, con `EstadoMostrado`); el encabezado (nombre, teléfono, correo) lo pide el handler a `IPacientesApi.ObtenerContactoConTelefonoAsync` (5.6, AD-10). Un Paciente que no existe → `Resultado` con código de no encontrado.
- El historial lleva arriba un resumen calculado por Agenda en la misma consulta: total de Citas, cuántas No se presentó y cuántas Cancelaciones tardías ("5 citas · 1 no se presentó · 2 cancelaciones tardías"); las partes en cero se omiten (Gerardo, 2026-11-10).
- Páginas en Interactive Server con `[Authorize]`: `/admin/pacientes` (lista y búsqueda) y `/admin/pacientes/{PatientId}` (historial). El texto buscado nunca va en la dirección (AD-11, AD-23): vive en la página y "Volver a Pacientes" lo conserva durante la sesión del navegador; al recargar, la búsqueda se vacía (Gerardo, 2026-11-10, en la revisión: reemplaza el `[ASSUMPTION]` de `?q=`, que contradecía AD-23). Cada Cita del historial enlaza a `/admin/citas/{CitaId}`; el Detalle (5.6) no cambia.
- "Pacientes" en `LayoutAdministracion` después de "Agenda", activo en `/admin/pacientes` y sus subrutas (Decision del 2026-11-09 de la navegación).
- Teléfono con el mismo formato del Detalle; "sin correo" con la etiqueta de la Agenda; estados con los chips de la Agenda (ámbar solo la tardía).
- `ToString` de los resultados sin nombre, teléfono, correo ni motivo (AD-23). Sin motivo de consulta ni nota en el historial: se ven en el Detalle.

**Never:** editar o fusionar Pacientes, buscar por teléfono, conteos o métricas fuera del resumen, exportar, borrar.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Sin acentos ni mayúsculas | "mariana" | "Mariana López" y "MARIANA Pérez" | — |
| Con acento | "lópez" y "lopez" | Ambos encuentran a "Mariana López" | — |
| Espacios | "  mariana  " | Igual que "mariana" | — |
| Parte del nombre | "riana" | Las dos Marianas | — |
| Sin texto | Búsqueda vacía, 3 Pacientes guardados | Los tres en orden alfabético | — |
| Más de 50 | Sin texto, 60 Pacientes | Los primeros 50 y "Escribe un nombre para buscar entre los demás" | — |
| Sin coincidencias | "zzz" | "No encontramos pacientes con ese nombre." | — |
| Sin correo | Paciente sin correo | Su fila dice "sin correo" | — |
| Historial | Paciente con Atendida, No se presentó, Cancelada por el Paciente, Cancelada tardía, Cancelada por el Consultorio y una Agendada futura | Todas, de la más reciente a la más antigua, cada una con su estado y enlace a su Detalle | — |
| Resumen | El historial anterior | "6 citas · 1 no se presentó · 1 cancelación tardía" | — |
| Sin Citas | Paciente que reservó y nunca tuvo Cita (AD-28) | Encabezado y "Este paciente no tiene citas.", sin resumen | — |
| Paciente inexistente | `PatientId` desconocido | "Este paciente no existe." y enlace a Pacientes | — |
| Sin sesión | GET a las páginas / consultas como Paciente | Redirige a `/admin/entrar` / No autorizado | — |
| Navegación | Cualquier página de administración | "Agenda · Pacientes · Salir"; "Pacientes" activo en sus páginas | — |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Pacientes/Infrastructure/PacientesDbContext.cs:57` -- `nombre_busqueda` ya existe (`NombreCompleto.Busqueda`); `Domain/NombreCompleto.cs` sabe normalizar el texto para buscar. El índice `(nombre_busqueda)` del modelo de datos: verificar que exista y crearlo en una migración de Pacientes si falta.
- `src/DraSelina.Consultorio.Web/Modules/Pacientes/Contracts/IPacientesApi.cs` -- hoy el único archivo de Contracts de Pacientes; `ObtenerContactoConTelefonoAsync` (5.6) da nombre, teléfono E.164 y correo. Pacientes aún no tiene handlers de consulta: el primero va en `Pacientes/Features`, con su puerto de lectura en `Infrastructure` y su registro en `Pacientes/Infrastructure/Registro.cs`.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Features/EstadoMostrado.cs` -- estado mostrado compartido. `Agenda/Features/ICitasDelPaciente.cs` + `Infrastructure/CitasDelPaciente.cs` (5.4, Paciente reconocido) -- lectura de Citas por Paciente: reutilizar o ampliar sin duplicar.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/DetalleDeCita.cs` + handler (5.6) -- patrón de consulta de administración con encabezado pedido a Pacientes.
- `src/DraSelina.Consultorio.Web/UI/Layout/LayoutAdministracion.razor:22` -- `NavLink` de Agenda; agregar Pacientes.
- `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/DetalleDeCita.razor`, `Agenda.razor` -- formatos de teléfono y fecha, chips (`Etiqueta`, `ClaseDeEtiqueta`), `[PersistentState]`.
- `tests/DraSelina.Consultorio.ArchitectureTests` -- UI solo usa Contracts: las páginas usan `Pacientes.Contracts` y `Agenda.Contracts`.
- `tests/DraSelina.Consultorio.IntegrationTests/DetalleDeCitaTests.cs`, `tests/DraSelina.Consultorio.UI.Tests/DetalleDeCitaTests.cs` -- modelos de prueba.

## Tasks & Acceptance

**Execution:**
- [x] `src/DraSelina.Consultorio.Web/Modules/Pacientes/Contracts/BuscarPacientes.cs` + `Features/` + `Infrastructure/` (+ migración si falta el índice) + `Registro.cs`.
- [x] `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/HistorialDelPaciente.cs` + `Features/` + `Infrastructure/` + `Registro.cs`.
- [x] `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/Pacientes.razor`, `HistorialDelPaciente.razor`, `UI/Layout/LayoutAdministracion.razor`, `wwwroot/css/consultorio.css`.
- [x] Pruebas: integración de las dos consultas (cada fila de datos de la matriz, sin sesión, inexistente) y por HTTP (redirección sin sesión, navegación); bUnit de las dos páginas.

**Acceptance Criteria:**
- Given la lista, when se busca "mariana" y se abre a "Mariana López", then su historial distingue Atendida, No se presentó, Cancelada y Cancelación tardía (FR-18).
- Given el historial, when se abre una Cita, then llega a su Detalle (5.6).

## Review Triage Log

Pasada 1 (lente quick, más un hallazgo propio): high 0, medium 1, low 0, false 0, maybe-false 0; el lente no encontró nada.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | `Pacientes.razor`: el texto buscado, que es parte del nombre de un Paciente, viaja en la dirección (`/admin/pacientes?q=mariana`) | medium | intent_gap | AD-23 (invariante del diseño): nombre "nunca aparece […] en la cadena de consulta"; AD-11: en la cadena de consulta viajan solo tipo, fecha y hora. El `[ASSUMPTION]` del bloque congelado que lo permite es un error del plan (lo escribió el facilitador); también lo señaló el constructor. Resuelto por Gerardo (2026-11-10): el texto vive en la página; se conserva el código de la entrada y se corrige solo esto, en lugar de revertir y reconstruir como pide el flujo (desviación decidida por él). |

## Verification

**Commands:**
- `dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.
