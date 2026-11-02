---
title: 'Una reserva en línea se guarda de punta a punta'
type: 'feature'
ticket: '1'
created: '2026-11-02'
status: 'built'
baseline_revision: '84521863d5227515895538eb0af49f4dca6121b2'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-draselina/architecture-appointment-booking/ddd-and-data-model-appointment-booking.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** No existe ninguna Cita ni ningún Paciente: la solución tiene los módulos vacíos y nada recorre una reserva real de la página a PostgreSQL.

**Approach:** Camino trazador de la épica 2. La página Tus datos recibe tipo, fecha y hora por la cadena de consulta, envía `AgendarCitaEnLinea`; el handler pide a Pacientes `BuscarOCrear`, crea la Cita con su Consentimiento y la guarda bajo la restricción de exclusión; la misma respuesta muestra Cita confirmada con fecha, hora y dirección de `IContenidoApi`.

## Boundaries & Constraints

**Always:**
- Reglas de la Parte I del documento de dominio y de la arquitectura tal como están: AD-8 (rango `[inicio, fin)` en UTC, `23P01` → `Fallo` con código, nunca excepción), AD-11 (SSR estático; en la cadena de consulta solo `tipo`, `fecha`, `hora`; nombre, teléfono, correo y motivo solo en el cuerpo), AD-17 (fecha y hora locales de `America/Mexico_City`, guardadas en UTC; reloj por `IClock`), AD-18 (ids Guid v7 del dominio; identidad del Paciente por índice único con collation ICU no determinista), AD-23 (motivo, nombre, teléfono y correo nunca en registros ni telemetría), AD-24 (Consentimiento en la Cita: fecha y hora, versión del aviso, canal `en_linea`), AD-28 (`BuscarOCrear` confirma en su propia transacción antes de la de Agenda; un correo distinto reemplaza al guardado).
- Formato de la cadena de consulta, que usa la entrada 5: `?tipo=<TipoDeVisitaId>&fecha=AAAA-MM-DD&hora=HH:mm`.
- `IContenidoApi` entrega los datos del consultorio y la versión del aviso de privacidad; las entradas 5 y 10 la reutilizan sin cambiarla.
- Decisión (Gerardo, 2026-11-02): los datos del consultorio son los del mockup y son los correctos: Hospital Star Médica, consultorio 101; Mariano Arista No. 735, Col. Barrio de Tequisquiapan, San Luis Potosí.
- Decisión (Gerardo, 2026-11-02): la versión del aviso de privacidad es `2026-11-borrador`; cuando el abogado entregue el texto revisado cambia a su fecha de publicación (por ejemplo `2027-01`).
- `Cita.Agendar` ya cumple INV-C1 a C4, C7, C9, C10 e INV-C11; INV-C5 (tramos, rejilla, ventana) y el tope semanal llegan en las entradas 4 y 7 como parámetros nuevos del mismo método.

**Never:**
- Tramos, rejilla, ventana, candado por fecha (entrada 4), bitácora y `CitaAgendada` (entrada 6), tope semanal (entrada 7), `solicitud_id` y el regreso a Día y hora (entrada 8), enlace de gestión y `token_hash` (entrada 9), archivo de calendario (entrada 10), anti-bots (entrada 11), escritorio (entrada 12).
- Llaves foráneas o joins entre esquemas; `EnsureCreated` o `database update`; la web nunca migra.
- Scripts en la página, `blazor.web.js` o algo pedido a un CDN.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Reserva nueva | Tipo "Primera consulta", mañana 16:00, datos válidos y Consentimiento | Paciente nuevo; Cita `Agendada`, `en_linea`, 30 min; Cita confirmada con fecha, hora y dirección | — |
| Mismo Paciente | Mismo teléfono, nombre con otras mayúsculas, acentos o espacios | Mismo `PatientId`; un correo nuevo reemplaza al guardado | — |
| Otro Paciente | Mismo teléfono, otro nombre | `PatientId` distinto | — |
| Traslape | Ya hay una Cita `Agendada` en `[16:00, 16:30)` | No se crea Cita | `Fallo("agenda.horario_ocupado")`; la página muestra el aviso sin error técnico |
| Cancelada no choca | La Cita previa está `Cancelada` | Se crea la Cita nueva | — |
| Teléfono imposible | `12345` o un número no válido en México | No se crea Paciente ni Cita | `Invalido` en `Telefono` con mensaje en español |
| Sin Consentimiento | Casilla sin marcar | No se reserva | `Invalido` en `Consentimiento` |
| Pasado o tipo inválido | Hora ya pasada, tipo inexistente o no reservable en línea | No se crea Cita | `Fallo` con código cerrado de Agenda |

</frozen-after-approval>

## Code Map

Rutas bajo `src/DraSelina.Consultorio.Web/` salvo que digan otra cosa.

- `Kernel/Resultado.cs` -- `Exito`, `Invalido`, `Fallo(codigo)`, `Como<T>()`. No hay forma "conflicto": se usa `Fallo` con códigos cerrados en `Contracts` (patrón `"anotacion.texto_repetido"`).
- `Kernel/CitaId.cs`, `PatientId.cs`, `TipoDeVisitaId.cs`, `IClock.cs` -- reutilizar; `Nuevo()` ya genera Guid v7.
- `BuildingBlocks/Contracts/ICommand.cs`, `ICommandHandler.cs` -- `HandleAsync(comando, ct)`; comandos como clase con propiedades `set`.
- `Modules/Agenda/Features/ITransaccionDeAgenda.cs`, `Modules/Pacientes/Features/ITransaccionDePacientes.cs` -- `AbrirAsync` → `ConfirmarAsync`/`DeshacerAsync`; deshacer antes de todo `Resultado` no exitoso.
- `Modules/*/Infrastructure/Registro.cs` -- `AddAgenda`/`AddPacientes`; registrar handlers y puertos uno por línea.
- `Modules/*/Infrastructure/*DbContext.cs` -- columnas en `snake_case` explícitas; `Configurar` fija el historial por esquema. No tocar el outbox de Agenda.
- `Modules/Contenido/Contracts/`, `Modules/Contenido/Contenido/` -- vacías (`.gitkeep`); namespaces `DraSelina.Consultorio.Contenido.Contracts` y `DraSelina.Consultorio.Contenido`.
- `Program.cs` -- registrar Contenido aquí o con un `AddContenido` propio.
- `src/DraSelina.Consultorio.MigrationService/Program.cs` -- ya migra Agenda y Pacientes; no se agrega contexto.
- `ValidacionDeComandos.cs` -- valida DataAnnotations y `IValidatableObject` antes del handler.
- `UI/Componentes/Campo.razor`, `ErroresDelResultado.razor`; patrón de formulario en `README.md` (~608–630): `EditForm` + `[SupplyParameterFromForm]` + `@inject ICommandHandler<…>`, sin `DataAnnotationsValidator`. `Campo` solo hace texto: la casilla de Consentimiento y el área del motivo se arman en la página.
- `UI/Pages/Inicio.razor` -- no se toca (su botón es de la entrada 5; `BaseVisualTests` lo revisa).
- `wwwroot/css/consultorio.css` -- clases `dsm-*` y tokens `--dsm-*`; el error usa `--dsm-primary`.
- `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs` -- `CrearBaseMigradaAsync` + `ArrancarWebSinEntregaAutomatica(cadena, RelojDePrueba)`; imitar `CasoDeUsoTests.cs` (handlers) y `BaseVisualTests.cs` (páginas).
- `tests/.../MensajesDeValidacionTests.cs`, `UnHandlerPorCasoDeUsoTests.cs` -- todo atributo lleva `ErrorMessage`; un handler por comando.
- `tests/DraSelina.Consultorio.ArchitectureTests/Reglas.cs` -- ya permite los namespaces nuevos; `Domain` solo usa `Kernel`; `Infrastructure` no usa contratos de otros módulos.
- `Directory.Packages.props` -- agregar `libphonenumber-csharp` 9.0.42 (fila del Stack).

## Tasks & Acceptance

**Execution:**
- [ ] `Directory.Packages.props`, `DraSelina.Consultorio.Web.csproj` -- referenciar `libphonenumber-csharp` -- el Stack lo nombra para validar celulares de México.
- [ ] `Modules/Pacientes/Domain/` -- `Paciente` (`Registrar`, `ActualizarCorreo`), objetos de valor `Telefono` (E.164 de México), `NombreCompleto` (original, normalizado y de búsqueda), `CorreoElectronico`; puerto `IPacientes` -- INV-P1 a P3, RN-25 a RN-30.
- [ ] `Modules/Pacientes/Contracts/` -- `IPacientesApi.BuscarOCrearAsync(nombre, telefono, correo)` → `Resultado<PatientId>` y sus códigos.
- [ ] `Modules/Pacientes/Features/` -- implementación de `IPacientesApi` en su propia transacción, idempotente frente a la violación del índice único (lee el existente).
- [ ] `Modules/Pacientes/Infrastructure/` + migración `CrearPacientes` -- tabla `pacientes.paciente`, collation `pacientes.es_ci_ai`, índice único `(telefono, nombre_normalizado)`, índices de `telefono` y `nombre_busqueda`; registro.
- [ ] `Modules/Contenido/Contracts/`, `Modules/Contenido/Contenido/` -- `IContenidoApi` con `DatosDelConsultorio` y `VersionDelAvisoDePrivacidad`; valores de las decisiones de Boundaries; registro desde el host.
- [ ] `Modules/Agenda/Domain/` -- `Cita.Agendar` (INV-C1 a C4, C7, C9 a C11), `RangoDeTiempo`, `Consentimiento`, `EstadoCita`, `OrigenDeCita`, `TipoDeVisita`; puertos `ICitas` (agregar, guardar → `Fallo` por traslape) e `ITiposDeVisita`.
- [ ] `Modules/Agenda/Contracts/` -- `AgendarCitaEnLinea : ICommand<CitaAgendadaEnLinea>` (DataAnnotations con mensajes en español, `Telefono` validado sin ASP.NET) y códigos cerrados (`agenda.horario_ocupado`, `agenda.tipo_no_reservable`, `agenda.en_el_pasado`).
- [ ] `Modules/Agenda/Features/` -- handler: convierte fecha y hora local a UTC, pide `BuscarOCrear`, abre la transacción de Agenda, crea y guarda la Cita, devuelve fecha, hora, tipo y datos para la confirmación.
- [ ] `Modules/Agenda/Infrastructure/` + migración `CrearCitas` -- `agenda.tipo_de_visita` con los dos tipos sembrados (ids fijos), `agenda.cita` sin `solicitud_id` ni `token_hash`, `EXCLUDE USING gist (tstzrange(inicio, fin, '[)') WITH &&) WHERE (estado <> 'Cancelada')`, los `CHECK` e índices; `ICitas` traduce `23P01`.
- [ ] `UI/Pages/Reserva/TusDatos.razor` (`/reservar/datos`) -- formulario según el mockup `Datos` (sin el espacio de Turnstile); al confirmar, pinta Cita confirmada en la misma respuesta.
- [ ] `UI/Pages/Reserva/CitaConfirmada.razor` (componente) y `wwwroot/css/consultorio.css` -- fecha y hora en grande, tipo y duración, lugar y dirección, "Volver al inicio".
- [ ] `tests/DraSelina.Consultorio.IntegrationTests/` -- pruebas de la matriz contra PostgreSQL; prueba de página: GET de Tus datos con `tipo`, `fecha`, `hora` y POST que devuelve la confirmación; prueba de que la Cita queda en UTC.
- [ ] `tests/` de dominio -- `Telefono`, `NombreCompleto` y `Cita.Agendar` sin base.
- [ ] Épica 2, Notes -- `Decision:` sobre `btree_gist` según lo que muestre la migración.

**Acceptance Criteria:**
- Given el AppHost arriba, when se abre `/reservar/datos?tipo=<Primera consulta>&fecha=<mañana>&hora=16:00`, se llenan los datos y se confirma, then se ve "Tu cita está confirmada" con esa fecha, hora y la dirección del consultorio.
- Given una Cita confirmada, when se revisa la URL, los registros y la telemetría, then no aparecen nombre, teléfono, correo ni motivo.
- Given la solución, when corren todas las pruebas, then pasan, incluidas las de arquitectura y la de mensajes de validación.

## Implementation Notes

## Plan Change Log

## Review Triage Log

**Pasada 1 (quick), 2026-11-02.** Veredictos: high 0, medium 1, low 4, false 0, maybe-false 0.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | `fecha`/`hora` armadas a mano en la cadena de consulta tiran un 500: hora en un hueco de horario de verano (`ConvertTimeToUtc` lanza) o `9999-12-31` (`inicio + Duracion` se desborda después de confirmar el Paciente) | medium | patch | Reproducido por el revisor. `America/Mexico_City` no tiene horas inválidas desde 2022 (comprobado: 0 entre 2026 y 2035), así que basta con que Tus datos acepte solo fechas locales de hoy a un año: lo demás cae en "No encontramos el horario…", ruta ya existente |
| 2 | Una reserva rechazada (pasado o tipo no reservable) ya creó o actualizó al Paciente, incluido el correo | low | rechazado | Real, pero RN-28/AD-28 aceptan un Paciente sin Cita, y el mismo reemplazo de correo (RN-30) ocurre con una reserva válida con ese teléfono y nombre; adelantar las reglas al handler duplicaría lo que decide `Cita.Agendar` (T-13) |
| 3 | EF Core registra cada traslape como Error (`SaveChangesFailed`) antes del `catch`; el comentario de `Pacientes.cs` atribuye la ausencia de PII a algo que no la garantiza | low | patch (solo el comentario) | El registro de Error es real pero raro en un consultorio y cambiarlo requiere configuración de EF: rechazado. El comentario es engañoso: lo que evita teléfono y nombre en el registro es que Npgsql oculta `Detail` sin `Include Error Detail`; corrección directa del comentario |
| 4 | AC2 solo probado en registros de llamadas directas, no por la página ni en telemetría | low | rechazado | No se mostró ninguna fuga: la cadena de consulta solo lleva `tipo`, `fecha`, `hora`, y no hay instrumentación de Npgsql (pregunta abierta de la épica 1); es un hueco de pruebas, no un defecto |
| 5 | `duracion_min` y `orden` son `integer`, el modelo de datos dice `smallint` | low | rechazado | La Parte II solo obliga lo que respalda una regla; sin daño y el cambio exige rehacer la migración |

## Design Notes

- **La confirmación se pinta en la respuesta del POST, sin redirección.** Una URL con el `CitaId` dejaría ver la Cita a quien la adivine (Guid v7 es en parte temporal), y la entrada 9 pondrá ahí el enlace de gestión. El doble envío lo resuelve la entrada 8 con `solicitud_id`.
- **`solicitud_id` y `token_hash` no se crean aquí.** Las entradas 8 y 9 agregan cada columna con su regla; la base local se recrea en cada arranque (AppHost sin volumen), así que `token_hash not null` no necesita relleno.
- **`btree_gist`:** la restricción usa solo un rango y GiST soporta `tstzrange` sin la extensión (modelo de datos, Observaciones). Se crea sin ella; si PostgreSQL la exige, se agrega y se anota.

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` -- expected: todas pasan (Docker arriba).
- `./dotnet.sh run --project src/DraSelina.Consultorio.AppHost` y `scripts/verificar-arranque.sh` -- expected: las tres filas pasan, con las migraciones nuevas en su historial.

**Manual checks (if no CLI):**
- En el teléfono, la página Tus datos a 390 px se parece al mockup `Datos` y la confirmación al mockup `Confirmada` sin enlace ni calendario.
