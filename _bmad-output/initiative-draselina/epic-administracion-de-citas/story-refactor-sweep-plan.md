---
title: 'Refactor sweep'
type: 'refactor'
ticket: '9'
created: '2026-11-16'
status: 'built'
baseline_revision: '85714ea855e8c19da8f55fd67c9c98a339e7cfee'
route: 'full'
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

**Problem:** La épica 5 dejó tres duplicaciones vistas en sus planes y revisiones: la regla de "fuera de horario" en dos lugares, la presentación de una Cita (etiqueta, chip, "sin correo", teléfono) dentro de dos páginas y tomada desde otras cuatro, y la siembra de Citas y la sesión simulada copiadas entre pruebas.

**Approach:** Alcance aprobado por Gerardo el 2026-11-16: una sola regla de "fuera de horario" en el dominio, la presentación de una Cita de administración en una sola clase, y las ayudas de prueba en el arnés. Sin cambios de comportamiento salvo el caso de A.

## Boundaries & Constraints

**Always:**
- **A. Fuera de horario:** `DiasDeLaAgendaHandler` usa `Cita.QuedaFueraDe(rango, tramosVigentes)` (5.3, ya usada por `HorarioDeLaCitaDelConsultorioHandler`), como pide T-13: la regla vive en el agregado. El único cambio visible: una Cita del consultorio que cruza la medianoche local se marca fuera de horario, igual que la decide la Cita al crearse.
- **B. Presentación:** `Etiqueta`, `ClaseDeEtiqueta` (y sus sobrecargas), la etiqueta "sin correo" y `Telefono` pasan de `Agenda.razor` y `DetalleDeCita.razor` a una sola clase estática de `UI/Pages/Administracion`; las seis páginas que hoy las toman (Agenda, DiaEnLaAgenda, DetalleDeCita, AgendarCita, Pacientes, HistorialDelPaciente) la usan. Textos, clases y formato idénticos.
- **C. Pruebas:** una sola ayuda en `tests/DraSelina.Consultorio.IntegrationTests` para sembrar una fila de `agenda.cita` (hoy copiada en `DiasDeLaAgendaTests`, `ReservaEnLineaTests`, `CitasActivasTests`) y una para fijar el usuario del ámbito (hoy en `ActorYAdministracionTests` y `DiasDeLaAgendaTests`); las pruebas las usan sin cambiar lo que comprueban.
- Las pruebas existentes pasan sin cambiar sus aserciones, salvo las que nombran la ubicación de lo movido.

**Never:** lo que las revisiones rechazaron a propósito (fechas extremas en la Agenda, `CitaId` no GUID en el Detalle, `[Route]` en la regla de arquitectura, mensaje tras un fallo entre comando y recarga); el sitio público; cambiar textos, reglas o contratos fuera de A.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Dentro de horario | Cita del consultorio martes 4:00–4:30, Tramo 3:30–6:00 | Sin marca (igual que hoy) | — |
| Fuera de horario | Cita del consultorio sábado 10:15 sin Tramos | "Fuera de horario" (igual que hoy) | — |
| Cruza la medianoche | Cita del consultorio 11:45 pm a 12:15 am | "Fuera de horario", como `Cita.QuedaFueraDe` | — |
| En línea | Cualquier Cita en línea | Nunca la marca (igual que hoy) | — |
| Presentación | Cada estado y cancelación en las seis páginas | Mismos textos y clases que antes | — |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Agenda/Features/DiasDeLaAgendaHandler.cs:98` -- `FueraDeHorario: cita.Origen == OrigenDeCita.Consultorio && !vigentes.Tramos.Any(t => t.Contiene(inicio, fin))`, con horas locales.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs` -- `public static bool QuedaFueraDe(RangoDeTiempo rango, TramosVigentes tramos)` (fecha de inicio distinta de la de fin, o ningún Tramo la contiene). `HorarioDeLaCitaDelConsultorioHandler.cs:33` la usa.
- `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/Agenda.razor` -- `Etiqueta`, `ClaseDeEtiqueta`, `SinCorreo`; `DetalleDeCita.razor` -- `Telefono`. Usos: `DiaEnLaAgenda.razor:67,76`, `AgendarCita.razor:494`, `HistorialDelPaciente.razor:61,71,91`, `DetalleDeCita.razor:61`, `Pacientes.razor:66,69`.
- `tests/DraSelina.Consultorio.IntegrationTests/DiasDeLaAgendaTests.cs`, `ReservaEnLineaTests.cs`, `CitasActivasTests.cs` -- `insert into agenda.cita`; `ActorYAdministracionTests.cs`, `DiasDeLaAgendaTests.cs` -- `FijarUsuario`. `Arnes.cs` -- donde viven las ayudas compartidas.
- `tests/DraSelina.Consultorio.UI.Tests/` -- pruebas que llaman `Agenda.Etiqueta` o `DetalleDeCita.Telefono` por su nombre.

## Tasks & Acceptance

**Execution:**
- [x] `DiasDeLaAgendaHandler.cs` -- A, con una prueba de integración de la Cita que cruza la medianoche.
- [x] `UI/Pages/Administracion/` (clase nueva + las seis páginas) -- B.
- [x] `tests/DraSelina.Consultorio.IntegrationTests/` -- C.

**Acceptance Criteria:**
- Given la regla de "fuera de horario", when se busca en `Modules/Agenda/Features`, then ningún handler compara Tramos por su cuenta: todos llaman a `Cita.QuedaFueraDe`.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 0, low 4, false 0, maybe-false 0; 3 grupos a patch.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1-2 | La prueba de la medianoche pasa también con la regla vieja, y el "único cambio visible" de A no existe: `Tramo.Contiene` exige `inicio < fin`, así que 23:45–00:15 ya salía fuera de horario; las dos reglas solo difieren en Citas de 24 horas o más | low | patch | Verificado en `Tramo.cs:46`. El error está en el plan (lo escribió el facilitador): A es un refactor sin cambio de comportamiento. Arreglo directo: la prueba y el comentario la describen como prueba de que la marca se conserva, no como cambio. |
| 3 | `DiasDeLaAgendaHandler.cs:100` usa `RangoDeTiempo.Crear` sobre filas leídas, que valida; T-6 pide reconstituir sin validar, y `CitasActivas.cs:38` usa `Reconstituir` | low | patch | Real contra T-6 (invariante del diseño). Arreglo directo: `RangoDeTiempo.Reconstituir`. |
| 4 | Siete archivos de integración siguen con su propia copia de la sesión simulada (`SetAuthenticationState`) en lugar de `Arnes.FijarUsuario` | low | patch | El Problem del plan nombra "la sesión simulada copiadas entre pruebas"; la lista de Always C se quedó corta. Arreglo directo: usar la ayuda y borrar las copias. |

## Verification

**Commands:**
- `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"; dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.
