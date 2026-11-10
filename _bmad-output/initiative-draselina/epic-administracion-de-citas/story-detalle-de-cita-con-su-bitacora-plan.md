---
title: 'Detalle de Cita con su bitácora'
type: 'feature'
ticket: '6'
created: '2026-11-10'
status: 'built'
baseline_revision: '5d7512072b6bd33223f7cdff2647feed7c10dd66'
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

**Problem:** Desde la Agenda no se puede abrir una Cita: el Administrador no ve sus datos completos ni quién la cambió, y no tiene dónde marcar No se presentó o cancelarla por el Consultorio (CAP-12, CAP-14, CAP-17, FR-16, FR-17, FR-20).

**Approach:** Una página de administración "Detalle de Cita", abierta desde cada tarjeta de la Agenda, que pinta una consulta de Agenda con los datos de la Cita, el contacto del Paciente (pedido a Pacientes), las acciones que hoy proceden y su bitácora; las dos acciones llaman a los comandos de la 5.5.

## Boundaries & Constraints

**Always:**
- Página en Interactive Server con `[Authorize]` en `/admin/citas/{CitaId}` (regla de arquitectura de la 5.1). La consulta lleva `IRequiereAdministrador` (AD-22) y reúne todo: Cita, tipo, bitácora de la Cita y el contacto del Paciente, que pide a Pacientes con un método nuevo de `IPacientesApi` que da nombre, teléfono y correo (AD-10, RN-29). La página no compone módulos ni decide reglas.
- Datos: Paciente (nombre, teléfono, correo o "sin correo: avisa a mano"), tipo de consulta, fecha, hora de inicio y fin, origen ("En línea" o "Del consultorio"), nota (si tiene), motivo de consulta (si tiene), Consentimiento (canal, fecha y versión del aviso), estado mostrado con su distinción de cancelación y los colores de la Agenda (ámbar solo la tardía).
- Acciones: la consulta dice cuáles proceden ahora con la misma regla del dominio (RN-31): "No se presentó" solo si está Agendada y su inicio ya llegó; "Cancelar" solo si está Agendada y su inicio no ha llegado. Ninguna en otro estado. Las dos piden confirmación en la propia página, sin diálogos del navegador; la de cancelar dice que el Paciente recibirá una disculpa por correo, o, sin correo, que hay que avisarle a mano. Un rechazo del comando (por ejemplo, la hora pasó mientras se confirmaba) se muestra y la página vuelve a pedir la consulta.
- Después de confirmar una acción la página se queda en el Detalle, con el estado y la bitácora ya actualizados y "Volver a la Agenda" (Gerardo, 2026-11-10).
- Plan conservado completo, por encima de 1,600 tokens (Gerardo, 2026-11-10).
- Bitácora: todas las entradas de la Cita, en orden de ocurrencia, con cuándo (fecha y hora local), quién (Paciente, Administrador o Sistema) y qué, con lo que su `detalle` ya guarda: Agendada (en línea o del consultorio), Reprogramada (de qué fecha y hora a cuál), Cancelada (por el Paciente, tardía o por el Consultorio), No se presentó. Nunca motivo, nota ni contacto en la bitácora (AD-23).
- La Agenda (5.2) enlaza cada tarjeta al Detalle, en vista de día y de semana; el Detalle tiene "Volver a la Agenda" a la fecha de la Cita.
- Usable en un teléfono: datos en una columna, acciones al alcance.
- Una Cita que no existe muestra "Esta cita no existe." y el enlace a la Agenda; nada de datos personales en la dirección (solo el `CitaId`) ni en registros (AD-11, AD-23).

**Never:** editar datos de la Cita o del Paciente, reprogramar desde administración, reenviar correos, la lista de Pacientes (entrada 7), revertir No se presentó.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Cita futura en línea | Agendada mañana 4:00, Paciente con correo | Datos completos, "Agendada", solo "Cancelar"; bitácora con "Agendada · en línea · Paciente" | — |
| Cita empezada | Agendada, inicio hace 10 min | "Atendida", solo "No se presentó" | — |
| Cita del consultorio | Origen consultorio con nota | "Del consultorio" y la nota | — |
| Sin correo | Paciente sin correo | "sin correo: avisa a mano"; la confirmación de cancelar lo repite | — |
| Cancelar | Confirmar cancelación | Estado "Cancelada (por el consultorio)", sin acciones; la bitácora suma la entrada del Administrador | — |
| Cancelar sin confirmar | Primer clic | Pide confirmación; nada cambia hasta confirmar | — |
| Marcar | Confirmar No se presentó | Estado "No se presentó", sin acciones; bitácora suma la entrada | — |
| Rechazo | Confirmar Cancelar después del inicio | Mensaje del rechazo; la página se recarga con "No se presentó" disponible | — |
| Reprogramada | Cita reprogramada por el Paciente | Bitácora: Agendada, luego "Reprogramada de … a …" por Paciente | — |
| Cancelada tarde | Cancelada por el Paciente dentro del umbral | "Cancelada (tardía)" ámbar; bitácora "Cancelada · tardía · Paciente"; sin acciones | — |
| Inexistente | `CitaId` desconocido | "Esta cita no existe." y enlace a la Agenda | — |
| Sin sesión | GET a la página / consulta como Paciente | Redirige a `/admin/entrar` / No autorizado | — |
| Desde la Agenda | Clic en una tarjeta | Abre su Detalle | — |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/MarcarNoSePresento.cs`, `CancelarCitaPorConsultorio.cs` -- los comandos de la 5.5 y sus resultados; códigos `CitaNoAgendada`, `AunNoEmpieza`, `YaEmpezo`, `CitaNoEncontrada` en `CodigosDeAgenda` (`AgendarCitaEnLinea.cs`). No se tocan.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs` -- `MarcarNoSePresento` (inicio llegado) y `CancelarPorConsultorio` (inicio no llegado): la consulta debe decidir las acciones con la misma regla; exponerla desde el dominio (por ejemplo, métodos `PuedeMarcarNoSePresento(now)` / `PuedeCancelarPorConsultorio(now)` que las transiciones también usan), sin duplicarla en el handler.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Features/EstadoMostrado.cs` -- estado mostrado (Atendida derivada) y distinción de cancelación, compartido por la Agenda y el Paciente reconocido.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/FilaDeBitacora.cs` -- `agenda.bitacora` (`ocurrido_en`, `actor_tipo`, `actor_id`, `entidad`, `entidad_id`, `accion`, `detalle` jsonb). Detalles guardados: Agendada (`inicio`, `fin`, `tipoDeVisitaId`, `origen`, y `fueraDeHorario` en la del consultorio), Reprogramada (`inicioAnterior`, `finAnterior`, `inicioNuevo`, `finNuevo`), Cancelada por el Paciente (`inicio`, `fin`, `motivo`), Cancelada por el Consultorio y No se presentó (`inicio`, `fin`, `estadoAnterior`, `estadoNuevo`, `motivo`). Un puerto de lectura nuevo, sin rastrear, por `entidad = 'Cita'` y `entidad_id`.
- `src/DraSelina.Consultorio.Web/Modules/Pacientes/Contracts/IPacientesApi.cs` -- `ObtenerContactosAsync` da nombre y correo, no teléfono; agregar uno que dé los tres, sin rastrear.
- `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/DiaEnLaAgenda.razor:46` -- la tarjeta (`<li data-cita=...>`) donde va el enlace; `Agenda.razor` -- `Etiqueta`, clases de chip, formatos de fecha (`UI/Pages/Reserva/FormatoDeCita.cs`).
- `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/AgendarCita.razor` -- ejemplo de página de administración con comandos, `ErroresDelResultado`, botón deshabilitado mientras corre.
- `tests/DraSelina.Consultorio.IntegrationTests/NoSePresentoYCancelarPorConsultorioTests.cs`, `PantallaAgendarCitaTests.cs` -- escenarios con sesión y Citas sembradas; `tests/DraSelina.Consultorio.UI.Tests/AgendarCitaTests.cs` -- bUnit con handlers falsos.

## Tasks & Acceptance

**Execution:**
- [ ] `src/DraSelina.Consultorio.Web/Modules/Pacientes/...` -- contacto con teléfono.
- [ ] `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs` -- las dos reglas de "procede ahora" expuestas y usadas por las transiciones.
- [ ] `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/DetalleDeCita.cs` + `Features/` + `Infrastructure/` + `Registro.cs` -- la consulta (`IRequiereAdministrador`) y la lectura de la bitácora; `ToString` sin datos personales.
- [ ] `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/DetalleDeCita.razor`, `DiaEnLaAgenda.razor` (enlace), `wwwroot/css/consultorio.css`.
- [ ] Pruebas: integración de la consulta (cada fila de datos de la matriz, bitácora en orden con reprogramación y cancelaciones, sin sesión, inexistente) y bUnit de la página (acciones por estado, confirmación, rechazo que recarga, sin correo, inexistente, enlace desde la Agenda).

**Acceptance Criteria:**
- Given una Cita reservada en línea y cancelada después por el Consultorio, when se abre su Detalle, then la bitácora muestra primero "Agendada · en línea · Paciente" y después "Cancelada · por el Consultorio · Administrador".
- Given el Detalle abierto, when se confirma una acción, then el estado y la bitácora mostrados son los nuevos sin recargar a mano.

## Review Triage Log

Pasada 1 (lente quick, más un riesgo que reportó el constructor): high 0, medium 1, low 6, false 0, maybe-false 0; 5 a patch, 2 rechazados (low, raros y con arreglo que agrega ramas).

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 0 | `Agenda/Infrastructure/Citas.cs` `ObtenerPorIdParaCambiarAsync` y `ObtenerPorTokenHashParaCambiarAsync`: bloquean la fila y luego hacen una lectura rastreada; el `AgendaDbContext` dura el circuito y EF devuelve la instancia que ya rastrea sin refrescarla. En el Detalle: un Cancelar rechazado deja la Cita rastreada; si el Paciente la cancela por su enlace y el Administrador confirma otra vez, el comando decide sobre "Agendada", reescribe el motivo y manda una segunda disculpa (reportado por el constructor) | medium | patch | Verificado en el código: `SingleOrDefaultAsync` rastreado tras el `for update`; `TransaccionDeAgenda.AbrirAsync` no limpia el rastreo. Es de la 5.5 pero esta página lo hace alcanzable. Arreglo directo: limpiar el rastreo al abrir la transacción del comando (Agenda y Pacientes, que tiene el mismo patrón con `BuscarOCrear`), con una prueba que reproduzca la secuencia en un mismo ámbito. |
| 1 | `DetalleDeCita.razor:1`: un `CitaId` que no es GUID da el 404 genérico, no "Esta cita no existe." | low | rechazado | Solo con un enlace cortado o escrito a mano en un área de un solo usuario; el arreglo agrega el parseo propio de la ruta. |
| 2 | `DetalleDeCita.razor` `CargarAsync`: si el comando se guarda pero la recarga falla, dice "Vuelve a entrar" y conserva los botones viejos | low | rechazado | Requiere que la base falle justo entre el comando y la consulta; el arreglo agrega una rama y un mensaje propio. |
| 3 | `DetalleDeCita.razor` `OnParametersSetAsync`: un prerender fallido (`Fallo: true`) pasa al circuito por `[PersistentState]` y nunca se reintenta | low | patch | `Vigente` compara solo el `Id`. Arreglo directo: un cargado con fallo no cuenta como vigente. |
| 4 | Bitácora: "cuándo" y "Reprogramada de … a …" sin año | low | patch | La misma página ya pone el año cuando no es el actual; en diciembre y enero se cruzan años. Arreglo directo: el mismo criterio. |
| 5 | `consultorio.css` `.dsm-agenda__abrir::after { border-radius: inherit; }`: el anillo de foco sale con esquinas cuadradas | low | patch | `inherit` toma el radio del `<a>` (0), no del `li`. Arreglo directo: el token del radio de la tarjeta. |
| 6 | `consultorio.css` `.dsm-detalle__acciones`: desde 576 px la confirmación se encoge a su contenido | low | patch | Solo los `.btn` llevan `flex: 1 1 0`. Arreglo directo: la confirmación también ocupa la fila. |

## Verification

**Commands:**
- `dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.

**Manual checks (if no CLI):**
- Con el AppHost: abrir una Cita desde la Agenda en escritorio y en vista de teléfono; cancelar una y ver la disculpa en Mailpit.
