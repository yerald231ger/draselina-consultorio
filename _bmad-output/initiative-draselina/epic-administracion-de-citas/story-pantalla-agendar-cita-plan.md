---
title: 'Pantalla Agendar cita'
type: 'feature'
ticket: '4'
created: '2026-11-10'
status: 'built'
baseline_revision: '902d2173c02d0c29fc880e25a4de938ec5597d72'
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

**Problem:** El comando de la 5.3 no tiene pantalla: el Administrador todavía no puede capturar una Cita pactada por WhatsApp ni la del sábado que aceptó Selina (UJ-4, UJ-6, CAP-13).

**Approach:** Una página de administración "Agendar cita", abierta desde la Agenda, con el formulario ligado al comando `AgendarCitaPorConsultorio`; dos consultas de apoyo dicen, mientras se captura, si el Paciente ya existe (con su última Cita) y si el horario cae fuera de los Tramos, para pedir la nota antes de guardar.

## Boundaries & Constraints

**Always:**
- Página en Interactive Server con `[Authorize]` (regla de arquitectura de la 5.1) bajo `/admin`; el formulario se liga directo al comando (Consistency Conventions, sin modelo de formulario aparte) y muestra los errores por campo con `ErroresDelResultado` y `Campo` (AD-12).
- La Agenda (5.2) lleva el botón principal "Agendar cita", que abre la página con la fecha que se está viendo ya puesta. Al guardar se vuelve a la Agenda de la fecha de la Cita, en vista de día, y la Cita está ahí.
- Paciente reconocido: con teléfono y nombre capturados, una consulta de Agenda (`IRequiereAdministrador`) dice si ya existe y da su próxima y su anterior Cita (fecha, hora, tipo y estado mostrado como en la Agenda), apoyándose en un método nuevo de `IPacientesApi` que busca por teléfono y nombre sin crear (RN-25, RN-29: Pacientes no conoce citas). Solo informa: no cambia el comando ni impide guardar. Si no existe, no dice nada.
- El Paciente reconocido muestra dos líneas, cada una solo si existe: "Próxima", su Cita Agendada futura más cercana, y "Anterior", la última que ya empezó, en cualquier estado mostrado (Atendida, No se presentó, Cancelada con su distinción) (Gerardo, 2026-11-10).
- Plan conservado completo, por encima de 1,600 tokens (Gerardo, 2026-11-10).
- Aviso de horario: con tipo, fecha y hora capturados, una consulta de Agenda (`IRequiereAdministrador`) dice si la Cita caería fuera de los Tramos vigentes o en un día que no se atiende, con la misma regla de la Cita (`Cita.QuedaFueraDeHorario`, 5.3; la página no la recalcula). Fuera de horario: aviso visible y la nota pasa a obligatoria en pantalla; nunca impide guardar.
- Tipos de visita: todos los activos, reservables en línea o no, en su orden; una consulta de administración nueva (la de la reserva solo da los reservables).
- Hora libre, fuera de la rejilla. Fecha local del consultorio (AD-17). Teléfono, correo y motivo con los mismos campos y mensajes de la reserva cuando aplican; correo y motivo opcionales; casilla de Consentimiento por canal externo, obligatoria.
- Mensajes de conflicto: traslape ("Ese horario ya lo ocupa otra cita.") y hora pasada ("Esa hora ya pasó."), los únicos dos que impiden guardar (EXPERIENCE.md); `NotaObligatoria` va como error del campo Nota. Textos de tú, español de México `[ASSUMPTION: texto]`.
- El botón de guardar se deshabilita mientras el comando corre; un segundo clic no manda otro comando.
- Nada de datos personales en la dirección ni en registros (AD-11, AD-23).

**Never:** cambiar el comando o el dominio de la 5.3, editar o cancelar Citas, el Detalle de Cita (entrada 6), elegir el Paciente de una lista, sugerir horarios libres.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Abrir desde la Agenda | "Agendar cita" en la Agenda del martes 10 | Página con la fecha 10 puesta y la lista de tipos activos | — |
| Paciente conocido | Teléfono y nombre de un Paciente con una Cita pasada y una futura | "Ya existe", "Próxima: …" y "Anterior: … · estado" | — |
| Conocido sin Citas futuras | Solo Citas pasadas | "Ya existe" y solo "Anterior" | — |
| Paciente nuevo | Teléfono y nombre sin coincidencia | Sin aviso | — |
| Mismo teléfono, otro nombre | RN-25 | Sin aviso (es otro Paciente) | — |
| Dentro de horario | Martes 4:00 pm, tipo de 30 min | Sin aviso; nota opcional | — |
| Fuera de horario | Sábado 10:15 | Aviso "fuera de horario"; nota obligatoria | Guardar sin nota: error en el campo Nota, no se agenda |
| Guardar fuera de horario | Sábado 10:15 con nota | Vuelve a la Agenda del sábado; la Cita está con su nota | — |
| Traslape | Hora encimada con otra Cita | "Ese horario ya lo ocupa otra cita."; el formulario conserva lo capturado | — |
| Hora pasada | Hoy, hace una hora | "Esa hora ya pasó."; conserva lo capturado | — |
| Sin consentimiento | Casilla vacía | Error en la casilla | — |
| Datos inválidos | Teléfono de 8 dígitos | Error en el campo con el mensaje de la reserva | — |
| Doble clic | Dos clics en guardar | Un solo comando | — |
| Sin sesión | GET a la página | Redirige a `/admin/entrar` | — |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/AgendarCitaPorConsultorio.cs` -- el comando de la 5.3 (clase con `set`, DataAnnotations, `IRequiereAdministrador`) y su resultado `CitaAgendadaPorConsultorio(CitaId, Fecha, Hora, FueraDeHorario)`. `CodigosDeAgenda` (en `AgendarCitaEnLinea.cs`): `HorarioOcupado`, `EnElPasado`, `NotaObligatoria`, `TipoNoReservable`. No se toca.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs:282` -- `QuedaFueraDeHorario(tramosVigentes)` y `QuedaFueraDe(rango, tramos)`: la regla del aviso. Exponer la versión por rango (sin Cita) para la consulta, sin duplicarla.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Features/ITramosDeLaFecha.cs` -- Tramos vigentes de una fecha.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/DiasDeLaAgenda.cs` -- `EstadoEnLaAgenda`, `CancelacionEnLaAgenda` y cómo se deriva Atendida (RN-31) en `DiasDeLaAgendaHandler`: la última Cita usa el mismo estado mostrado.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/TiposDeVisitaReservables.cs` + handler -- modelo para la consulta de tipos activos.
- `src/DraSelina.Consultorio.Web/Modules/Pacientes/Contracts/IPacientesApi.cs`, `Features/PacientesApi.cs` (`TelefonoDeMexico`), `Domain/IPacientes.cs:15` (`BuscarAsync(Telefono, NombreCompleto)`) -- la búsqueda sin crear ya existe en el puerto; falta exponerla en la API (teléfono o nombre inválidos → nulo, sin registrar nada).
- `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/Agenda.razor` -- agregar el botón "Agendar cita" con la fecha vista; `RegresoSeguro` (Identidad) ya acepta `/admin/...?...`.
- `src/DraSelina.Consultorio.Web/UI/Componentes/Campo.razor`, `ErroresDelResultado.razor` -- campos con errores del `Resultado`; `UI/Pages/Reserva/TusDatos.razor` -- cómo se usan para nombre, celular, correo y motivo (área de texto aparte).
- `tests/DraSelina.Consultorio.UI.Tests/AgendaTests.cs` -- bUnit con handlers falsos y `RendererInfo` de servidor. `tests/DraSelina.Consultorio.IntegrationTests/AgendarCitaPorConsultorioTests.cs` -- escenario con sesión y Pacientes sembrados.

## Tasks & Acceptance

**Execution:**
- [x] `src/DraSelina.Consultorio.Web/Modules/Pacientes/...` -- `IPacientesApi.BuscarAsync(nombre, telefono)` → `PatientId?`, sin crear.
- [x] `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/` + `Features/` + `Infrastructure/` + `Registro.cs` -- consultas `PacienteReconocido(nombre, telefono)`, `HorarioDeLaCitaDelConsultorio(tipo, fecha, hora)` y `TiposDeVisitaActivos`, las tres `IRequiereAdministrador`; `ToString` sin datos personales.
- [x] `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/AgendarCita.razor` -- la página; `Agenda.razor` -- el botón.
- [x] `src/DraSelina.Consultorio.Web/wwwroot/css/consultorio.css` -- aviso de fuera de horario y de Paciente reconocido con los tokens de `DESIGN.md`.
- [x] Pruebas: integración de las tres consultas (incluido RN-25 y sin sesión) y un recorrido HTTP con sesión que guarda y llega a la Agenda; bUnit de la página para cada fila de la matriz.

**Acceptance Criteria:**
- Given un Paciente conocido, when se capturan su teléfono y nombre y se guarda, then la Cita queda con su mismo `PatientId` (no se crea otro).
- Given la página abierta, when se cambian tipo, fecha u hora, then el aviso de horario se actualiza sin recargar.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 1, low 2, false 0, maybe-false 0; 3 a patch.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | `PacientesApi.BuscarAsync` (nuevo) usa `IPacientes.BuscarAsync`, que rastrea (`Pacientes.cs:12-15`, sin `AsNoTracking`); el `PacientesDbContext` es del circuito, así que el Paciente reconocido queda en caché y al guardar `BuscarOCrearAsync` lo recibe viejo: si cambió su correo en línea entre reconocer y guardar, `ActualizarCorreo` compara contra el viejo y no reemplaza (RN-30) | medium | patch | Verificado: `AddDbContext` con ámbito (`Pacientes/Infrastructure/Registro.cs:18`) y en Interactive Server el ámbito es el circuito; `ConTelefonoAsync` sí usa `AsNoTracking`. Arreglo directo: la búsqueda sin crear lee sin rastrear. |
| 2 | `AgendarCita.razor`: un error en un campo que no es `Campo` (Nota, Consentimiento, tipo, día, hora) no recibe el foco | low | patch | `ErroresDelResultado` solo enfoca un `Campo` que lo reclama; `TusDatos.razor:124,137` resuelve Motivo y Consentimiento con `LlevaElFoco`. AD-12 y el componente: el primer error en orden de lectura lleva el foco. |
| 3 | `PantallaAgendarCitaTests`: el criterio "mismo `PatientId`" se prueba con el comando en un ámbito nuevo, sin la consulta `PacienteReconocido` antes en el mismo ámbito, que es lo que hace la página | low | patch | Es justo la interacción del hallazgo 1 y no se ejercita. Arreglo: una prueba que en un mismo ámbito reconoce, cambia el correo fuera y guarda, y comprueba el mismo `PatientId` y el correo nuevo. |

## Verification

**Commands:**
- `dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.

**Manual checks (if no CLI):**
- Con el AppHost: UJ-4 (Paciente conocido por WhatsApp) y la primera mitad de UJ-6 (sábado con nota).
