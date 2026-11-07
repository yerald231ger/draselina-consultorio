---
title: 'Tope semanal por teléfono'
type: 'feature'
ticket: '7'
created: '2026-11-03'
status: 'done'
baseline_revision: '51d8a5b8999977202afd19d86fb271bf116cd1a7'
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

**Problem:** Un mismo teléfono puede reservar todas las Citas que quiera en una semana; FR-7 pide un tope para frenar reservas excesivas.

**Approach:** Al reservar en línea, Agenda pide a `IPacientesApi` los Pacientes que comparten el teléfono, cuenta sus Citas de la semana de calendario de la Cita, y `Cita.Agendar` compara contra el tope de `ReglasDeReserva` y rechaza (AD-13, RN-18); Tus datos explica el rechazo y ofrece WhatsApp.

## Boundaries & Constraints

**Always:**
- AD-13 y RN-18: el handler pide los `PatientId` que comparten el teléfono y cuenta; la `Cita` decide (T-13: el handler no decide; T-1: la Cita no consulta). Es un límite blando, no transaccional.
- Semana de calendario de lunes a domingo, en la fecha local de la Cita (AD-17); cuentan las Citas `Agendada` de cualquier origen, incluidas las del consultorio; las canceladas no cuentan (supuestos de AD-13 y RN-18).
- Se rechaza cuando las Citas de esa semana ya alcanzaron el tope (con tope 3, la cuarta). Código cerrado `agenda.tope_semanal`.
- Pacientes contesta solo identidad (RN-29): normaliza el teléfono como en `BuscarOCrear` y devuelve ids; nunca cuenta Citas.
- Tus datos, ante `agenda.tope_semanal`: "Este número ya tiene el máximo de citas de esta semana." con el componente `MensajeDeWhatsApp` (EXPERIENCE.md, Tope semanal alcanzado). Los datos del formulario se conservan.
- La Cita del consultorio no se bloquea por el tope (AD-13); no existe todavía y no se toca.

**Never:**
- Límite de frecuencia y anti-bots (entrada 11); `solicitud_id` (entrada 8).
- Guardar el teléfono en la Cita o en Agenda (E-12).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Bajo el tope | Dos Citas Agendadas esa semana con el teléfono | La tercera se guarda | — |
| Alcanza el tope | Tres Citas Agendadas esa semana | La cuarta no se guarda | `Fallo(agenda.tope_semanal)` |
| Otro nombre, mismo teléfono | Tres Citas a nombre de otros Pacientes con ese teléfono | No se guarda | `Fallo(agenda.tope_semanal)` |
| Semana siguiente | Tres Citas la semana pasada | Se guarda | — |
| Lunes y domingo | Citas el domingo anterior y el lunes siguiente | No cuentan para la semana de lunes a domingo de la Cita | — |
| Cancelada no cuenta | Tres Citas, una Cancelada | Se guarda | — |
| Del consultorio cuenta | Dos en línea y una del consultorio (SQL directo) esa semana | No se guarda | `Fallo(agenda.tope_semanal)` |
| Tope configurable | Tope 1 y una Cita esa semana | No se guarda | `Fallo(agenda.tope_semanal)` |
| Mensaje | Rechazo por tope en Tus datos | Aviso, WhatsApp y datos conservados | Sin error técnico |

</frozen-after-approval>

## Code Map

Rutas bajo `src/DraSelina.Consultorio.Web/` salvo que digan otra cosa.

- `Modules/Pacientes/Contracts/IPacientesApi.cs` -- hoy solo `BuscarOCrearAsync`; agregar la consulta de ids por teléfono.
- `Modules/Pacientes/Features/PacientesApi.cs` -- `TelefonoDeMexico` (libphonenumber → E.164); reutilizar para la consulta nueva.
- `Modules/Pacientes/Domain/IPacientes.cs`, `Infrastructure/Pacientes.cs` -- índice `ix_paciente_telefono` existe para esta consulta (AD-13).
- `Modules/Agenda/Domain/Cita.cs` -- `Agendar(..., ReglasDeAgenda reglas, now, out cita)`; orden de rechazos actual: tipo, pasado, ventana, horario. El tope va después de horario.
- `Modules/Agenda/Domain/ReglasDeAgenda.cs` -- ya lleva `TopeSemanalPorTelefono`.
- `Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs` -- flujo tx → candado → reglas y agenda → `Cita.Agendar` → guardar → bitácora y evento → confirmar; el conteo entra antes de `Cita.Agendar`.
- `Modules/Agenda/Features/ICitasActivas.cs`, `Infrastructure/CitasActivas.cs` -- lectura con `AsNoTracking`; agregar el conteo por pacientes y rango; índice `ix_cita_paciente_id_inicio`.
- `Modules/Agenda/Domain/HoraLocal.cs` -- fecha local y conversión a UTC para los límites de la semana.
- `Modules/Agenda/Contracts/AgendarCitaEnLinea.cs` -- `CodigosDeAgenda`.
- `UI/Pages/Reserva/TusDatos.razor` -- `Aviso(codigo)`; `UI/Componentes/MensajeDeWhatsApp.razor` (entrada 5).
- `tests/DraSelina.Consultorio.Domain.Tests/CitaTests.cs`, `tests/DraSelina.Consultorio.IntegrationTests/ReservaEnLineaTests.cs`, `TusDatosTests.cs` -- reloj fijo; `ReservaEnLineaTests.SinSesion` para llamar al handler fuera de una petición.

## Tasks & Acceptance

**Execution:**
- [x] `Modules/Pacientes/Contracts/IPacientesApi.cs`, `Features/PacientesApi.cs`, puerto e infraestructura -- ids de los Pacientes con ese teléfono (teléfono inválido → lista vacía).
- [x] `Modules/Agenda/Features/ICitasActivas.cs`, `Infrastructure/CitasActivas.cs` -- contar Citas `Agendada` de esos pacientes entre dos instantes.
- [x] `Modules/Agenda/Domain/Cita.cs` -- `Agendar` recibe el conteo de la semana y rechaza `TopeSemanal` al alcanzar `TopeSemanalPorTelefono`.
- [x] `Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs` -- semana local de la Cita, pide ids, cuenta, pasa el conteo; mapea el código.
- [x] `Modules/Agenda/Contracts/AgendarCitaEnLinea.cs`, `UI/Pages/Reserva/TusDatos.razor` -- código, aviso y WhatsApp.
- [x] Pruebas -- dominio (tope, bajo tope, tope 1); integración: cada fila de la matriz contra la base migrada; página: el aviso con WhatsApp y los datos conservados.

**Acceptance Criteria:**
- Given tres Citas Agendadas en una semana con un teléfono, when se reserva una cuarta esa semana con ese teléfono y cualquier nombre, then no se guarda y Tus datos muestra el aviso con WhatsApp; when se reserva en la semana siguiente, then se guarda.
- Given la solución, when corren todas las pruebas, then pasan.

## Implementation Notes

- `IPacientesApi.ConTelefonoAsync(telefono)` → ids (`IPacientes.ConTelefonoAsync`, `AsNoTracking` por `ix_paciente_telefono`); teléfono inválido → vacía.
- `ICitasActivas.AgendadasDeAsync(pacientes, desde, hasta)`: cuenta `Agendada` por inicio en `[desde, hasta)`.
- `HoraLocal.SemanaDe(fecha)`: de las 00:00 locales del lunes a las del lunes siguiente, en UTC.
- `Cita.Agendar(..., reglas, citasDeLaSemana, now, out cita)`: `RechazoAlAgendar.TopeSemanal` después de horario; conteo negativo lanza.
- El handler pide los ids antes de abrir la transacción de Agenda y cuenta después del candado.
- `HorariosLibresTests.Un_dia_lleno_aparece_con_la_lista_vacia` y `TipoDeConsultaYDiaYHoraTests.Un_dia_con_todas_sus_horas_ocupadas_se_ve_Lleno_y_no_se_puede_elegir` llenaban un día con cinco Citas del mismo teléfono; ahora cada una usa otro teléfono.

## Plan Change Log

## Review Triage Log

**Pasada 1 (quick), 2026-11-03.** Veredictos: high 0, medium 0, low 2, false 0, maybe-false 0. Sin parches. AppHost: las tres filas pasan.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | Ninguna prueba de integración muestra que cuentan las Citas en los bordes interiores de la semana (lunes 00:00, domingo noche local) | low | rechazado | `HoraLocal.SemanaDe` está probada en dominio y la consulta cuenta exactamente ese rango; la prueba de los bordes exteriores ya fallaría con una semana cortada en UTC del lado de fuera |
| 2 | La prueba de `ConTelefonoAsync` vive en la clase de pruebas de la reserva | low | rechazado | Sin daño: corre y pasa; es una cuestión de ubicación |

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` -- expected: todas pasan (Docker arriba).
