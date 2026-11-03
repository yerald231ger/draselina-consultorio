---
title: 'La reserva valida tramos, rejilla y ventana'
type: 'feature'
ticket: '4'
created: '2026-11-02'
status: 'built'
baseline_revision: 'f9e968fdfc86e5e8bf2d12b143c537e825dba53b'
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

**Problem:** Hoy `AgendarCitaEnLinea` guarda una Cita a cualquier hora futura: un sábado, a las 3:45 pm o dentro de un año, aunque la consulta de horarios libres nunca la ofrezca.

**Approach:** El handler toma el candado consultivo de la fecha local (AD-26), lee los tramos vigentes después de tomarlo, arma `ReglasDeAgenda` con `ReglasDeReserva`, y `Cita.Agendar` exige INV-C5: la Cita cabe en un tramo vigente, empieza en la rejilla y cae dentro de la ventana de reserva.

## Boundaries & Constraints

**Always:**
- INV-C5, RN-20, AD-7 (`Cita.Agendar` valida tramos vigentes recibidos como entrada, rejilla y ventana), AD-26 (candado consultivo de transacción por fecha local `(inicio AT TIME ZONE 'America/Mexico_City')::date`, tomado dentro de la transacción de Agenda y antes de leer los tramos).
- La rejilla y la ventana son las de `CalculadoraDeDisponibilidad` (T-21): usar `EstaEnLaRejilla` y `Ventana`; ninguna reimplementación.
- `ReglasDeAgenda` (Parte I, objetos de valor) es lo que recibe la `Cita`: tramos vigentes de la fecha más los valores de `ReglasDeReserva`; el agregado nunca lee configuración (T-1).
- Rechazos con códigos cerrados de Agenda: `agenda.fuera_de_horario` (no cabe en un tramo vigente o no empieza en la rejilla) y `agenda.fuera_de_ventana` (antes de la anticipación mínima o después de la ventana); `agenda.en_el_pasado` y `agenda.tipo_no_reservable` siguen como están.
- En Tus datos, los dos códigos nuevos se muestran con el mismo aviso: "Ese horario ya no está disponible. Vuelve a elegir otro horario." Nunca un error técnico (AD-8).

**Never:**
- Tope semanal (entrada 7), `solicitud_id` y regreso a Día y hora (entrada 8), bitácora y evento (entrada 6).
- Validar tramos o rejilla en la Cita del consultorio (AD-7; no existe todavía).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| En la rejilla | Martes 4:00 pm, base sembrada | Cita guardada | — |
| Fuera de la rejilla | Martes 3:45 pm | No se guarda | `Fallo(agenda.fuera_de_horario)` |
| No cabe completa | Martes 5:45 pm (fin 6:15) | No se guarda | `Fallo(agenda.fuera_de_horario)` |
| Día sin tramos | Sábado sin custom; custom "no se atiende" | No se guarda | `Fallo(agenda.fuera_de_horario)` |
| Custom abre un sábado | Custom sábado 10:00–11:00, reserva 10:30 | Cita guardada | — |
| Hoy con anticipación 1 | Hoy a una hora futura | No se guarda | `Fallo(agenda.fuera_de_ventana)` |
| Después de la ventana | Hoy + 2 meses + 1 día | No se guarda | `Fallo(agenda.fuera_de_ventana)` |
| Último día de la ventana | Hoy + 2 meses, en la rejilla | Cita guardada | — |
| Concurrencia sobre la fecha | Dos reservas simultáneas del mismo horario | Una Cita; la otra `agenda.horario_ocupado` | Sin error técnico |

</frozen-after-approval>

## Code Map

Rutas bajo `src/DraSelina.Consultorio.Web/` salvo que digan otra cosa.

- `Modules/Agenda/Domain/Cita.cs` -- `Agendar(paciente, tipo, inicio, motivo, consentimiento, now, out cita)` devuelve `RechazoAlAgendar?`; agregar el parámetro `ReglasDeAgenda` y los rechazos nuevos al enum.
- `Modules/Agenda/Domain/CalculadoraDeDisponibilidad.cs` -- `EstaEnLaRejilla(TramosVigentes, hora, duracion)` y `Ventana(reglas, now)` → `VentanaDeReserva(PrimeraFecha, UltimaFecha)`; reutilizar, no copiar.
- `Modules/Agenda/Domain/AgendaAtencion.cs` -- `TramosVigentes(fecha)`; `IAgendaAtencion.ObtenerAsync(customsDesde)` con hoy local.
- `Modules/Agenda/Domain/ReglasDeReserva.cs`, `IReglasDeReserva.cs` -- valores de reglas.
- `Modules/Agenda/Domain/HoraLocal.cs` -- fecha y hora locales de un instante.
- `Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs` -- hoy: tipo → `BuscarOCrear` → abre tx → `Cita.Agendar` → guarda. El candado y la lectura de tramos van después de abrir la tx; `Codigo(rechazo)` mapea enum a código.
- `Modules/Agenda/Features/ITransaccionDeAgenda.cs`, `Infrastructure/TransaccionDeAgenda.cs` -- la transacción abierta; el candado se toma sobre su conexión.
- `Modules/Agenda/Contracts/AgendarCitaEnLinea.cs` -- `CodigosDeAgenda`: agregar los dos códigos.
- `UI/Pages/Reserva/TusDatos.razor` -- `Aviso(codigo)`: agregar los dos códigos.
- `tests/DraSelina.Consultorio.Domain.Tests/CitaTests.cs` -- pasar `ReglasDeAgenda` en las llamadas existentes.
- `tests/DraSelina.Consultorio.IntegrationTests/ReservaEnLineaTests.cs`, `TusDatosTests.cs`, `HorariosLibresTests.cs` -- reloj fijo (`Manana` = martes 2026-11-03). Las que reservan fuera de la rejilla para probar traslapes (p. ej. una Cita contigua o una del consultorio fuera de rejilla) deben crear esa Cita con SQL directo, como Cita del consultorio, no con el handler en línea.

## Tasks & Acceptance

**Execution:**
- [ ] `Modules/Agenda/Domain/ReglasDeAgenda.cs` -- valor: `TramosVigentes` de la fecha de la Cita más anticipación mínima, ventana, umbral y tope.
- [ ] `Modules/Agenda/Domain/Cita.cs` -- `Agendar` recibe `ReglasDeAgenda`; rechazos `FueraDeHorario` y `FueraDeVentana` con la rejilla y la ventana de la calculadora; `EnElPasado` se evalúa antes.
- [ ] `Modules/Agenda/Features/ICandadoDeFecha.cs` + `Infrastructure/CandadoDeFecha.cs` -- `pg_advisory_xact_lock` con una llave derivada de la fecha local, dentro de la transacción abierta.
- [ ] `Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs` -- abre tx → candado → agenda y reglas → `ReglasDeAgenda` → `Cita.Agendar` → guarda; mapea los códigos nuevos; registrar el puerto.
- [ ] `Modules/Agenda/Contracts/AgendarCitaEnLinea.cs`, `UI/Pages/Reserva/TusDatos.razor` -- códigos y aviso.
- [ ] Pruebas -- dominio: cada fila de la matriz salvo concurrencia; integración: la matriz contra la base migrada, incluida la concurrencia con dos reservas simultáneas; ajustar las existentes según el Code Map.

**Acceptance Criteria:**
- Given la base sembrada y el reloj fijo, when se reserva el martes a las 3:45 pm, en sábado, hoy o después de la ventana, then no se guarda ninguna Cita y vuelve su código; when se reserva el martes a las 4:00 pm, then se guarda.
- Given la solución, when corren todas las pruebas, then pasan.

## Implementation Notes

## Plan Change Log

## Review Triage Log

**Pasada 1 (quick), 2026-11-02.** Veredictos: high 0, medium 0, low 2, false 0, maybe-false 0. Sin parches.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | `ReservaEnLineaTests` copia la clase del candado (`0x41474645`) en lugar de usar `CandadoDeFecha.ClaseDeCandado` | low | rechazado | Si la llave cambia, la prueba del candado falla a la vista (la reserva no espera); compartir la constante agrega superficie pública o `InternalsVisibleTo` |
| 2 | La prueba de dos reservas simultáneas pasa también sin el candado: el perdedor recibe `horario_ocupado` por la restricción de exclusión | low | rechazado | Verifica exactamente el resultado de la fila de la matriz; el candado lo cubre `La_reserva_espera_el_candado_de_su_fecha_y_lee_los_tramos_despues_de_tomarlo`, que según el implementador falla si se quita el candado |

Cambio fuera del plan aceptado: `Arnes.cs` sube `max_connections` del PostgreSQL de pruebas a 400 (solo pruebas; con 100 las bases por prueba agotaban conexiones).

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` -- expected: todas pasan (Docker arriba).
