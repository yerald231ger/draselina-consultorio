---
title: 'Cita del consultorio en el dominio y el caso de uso'
type: 'feature'
ticket: '3'
created: '2026-11-10'
status: 'built'
baseline_revision: '95242a52e111c0bc9a5e2c4438a53df09b942a64'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** El Administrador no puede registrar las Citas que se pactan por teléfono o WhatsApp, ni las que Selina acepta fuera de horario (CAP-13, FR-15, UJ-4, UJ-6); `Cita` solo sabe agendarse en línea.

**Approach:** `Cita.AgendarPorConsultorio` en el dominio, con sus reglas de AD-7, y el comando de administración `AgendarCitaPorConsultorio`, que reconoce o registra al Paciente, guarda la Cita con su bitácora a nombre del Administrador y publica `CitaAgendada` con origen consultorio. Las notificaciones existentes (confirmación y Recordatorio) reaccionan solas. La pantalla es de la entrada 4.

## Boundaries & Constraints

**Always:**
- `AgendarPorConsultorio` ignora Agenda base, custom, rejilla, ventana y tope semanal, incluso en un día que no se atiende (RN-13, AD-7). Lo único que la impide: traslapar otra Cita (la base, 23P01 → `CodigosDeAgenda.HorarioOcupado`, AD-8) o empezar en el pasado (INV-C4, RN-31; hoy más tarde sí se puede).
- Nota obligatoria cuando la Cita no cabe completa en ningún Tramo vigente de su fecha (INV-C6); dentro de los Tramos es opcional. La decide la Cita con los Tramos vigentes que le pasa el handler (T-1). Sin nota fuera de horario: código nuevo `CodigosDeAgenda.NotaObligatoria`.
- Cualquier tipo de visita activo, reservable en línea o no; el fin = inicio + duración del tipo (INV-C3). Hora libre, fuera de la rejilla.
- Motivo de consulta opcional (`[ASSUMPTION]` del modelo de datos). Correo opcional; si llega, reemplaza al guardado (RN-30, AD-28).
- Consentimiento con canal `externo` y la versión vigente del aviso (AD-24, supuesto de FR-15): el comando exige que el Administrador lo confirme; sin él no se agenda.
- `SolicitudId` nulo (AD-27 es solo de la reserva en línea). `TokenHash` como en línea: la Cita del consultorio también tiene Enlace de gestión.
- Comando con `IRequiereAdministrador` (AD-22). Un comando, una transacción, un módulo (Consistency Conventions): Cita, bitácora y outbox juntos; `BuscarOCrear` de Pacientes antes, en su propia transacción (AD-28).
- Toma el candado de la fecha local (AD-26) antes de leer los Tramos vigentes, como la reserva en línea (Gerardo, 2026-11-10).
- Plan conservado completo, por encima de 1,600 tokens (Gerardo, 2026-11-10).
- Bitácora: actor Administrador con su id (`IActorActual.IdDelAdministradorAsync`, 5.1), acción Agendada, detalle con inicio, fin, tipo, origen y si cae fuera de horario. Nunca motivo, nota ni contacto (AD-23).
- `CitaAgendada` con `Origen` consultorio y actor Administrador; la confirmación (4.1) y el Recordatorio (4.2) ya la atienden sin cambios (supuesto de FR-21).
- Una Cita del consultorio nunca abre disponibilidad (RN-14): `HorariosLibres` no ofrece nada por ella; sí deja de ofrecer lo que traslapa.

**Never:** pantalla (entrada 4), "Paciente reconocido" con su última Cita (entrada 4), cancelar o marcar No se presentó (entrada 5), tocar la reserva en línea o sus reglas, cambios a notificaciones.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Dentro de horario | Martes 4:00 pm, Tramo 3:30–6:00, sin nota | Cita Agendada, origen consultorio, fin 4:30 | — |
| Fuera de la rejilla | Martes 4:10 pm | Se guarda | — |
| Sábado cerrado sin nota | Sábado 10:15 sin Tramos, nota vacía | `NotaObligatoria`; nada guardado | `Resultado` fallido |
| Sábado cerrado con nota | Igual, nota "lo pidió Selina" | Se guarda con la nota; `HorariosLibres` sigue sin ofrecer ese sábado | — |
| Cabe a medias | Martes 5:45–6:15 con Tramo hasta 6:00, sin nota | `NotaObligatoria` | — |
| Hoy más tarde | Hoy 7:00 pm (fuera de la ventana) con nota | Se guarda | — |
| Pasado | Hoy, una hora antes de ahora | `EnElPasado` | — |
| Traslape | Ya hay una Cita 4:00–4:30, nueva 4:15 | `HorarioOcupado`; nada guardado, ni bitácora ni evento | — |
| Sin consentimiento | Casilla sin confirmar | `Resultado` inválido por campo | Validación de forma |
| Tipo no reservable en línea | Tipo activo que solo se agenda por teléfono | Se guarda | — |
| Tipo inactivo o inexistente | — | `TipoNoReservable` | — |
| Sin sesión | Paciente | No autorizado; el handler no corre | — |
| Paciente existente | Mismo teléfono y nombre que uno guardado | Usa su `PatientId` (RN-25) | — |
| Con correo | Paciente con correo | Sale la confirmación por correo y se programa el Recordatorio | — |
| Ocupa un horario público | Cita del consultorio martes 4:00 | `HorariosLibres` del martes ya no ofrece 4:00 ni nada que la traslape; no ofrece nada nuevo | — |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs` -- `Cita.Agendar` (líneas 84-180) es el modelo: validaciones con `ArgumentException` para lo que la forma ya exigió, `RechazoAlAgendar` para reglas de negocio, `TokenHash = EnlaceDeGestion.Derivar(clave, id).Hash`, `CreadaEn`. `OrigenDeCita.Consultorio` y `Nota` ya existen; agregar `AgendarPorConsultorio` y `RechazoAlAgendar.NotaObligatoria` (el enum está en la línea 305).
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Tramo.cs:46` -- `Contiene(inicio, fin)`: decide si la Cita cabe en un Tramo. `AgendaAtencion.TramosVigentes`, `TramosVigentes` (record con `Fecha`, `Tramos`, `Origen`).
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Consentimiento.cs` -- `Consentimiento.Crear(en, version, CanalDeConsentimiento.Externo)`.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs` -- modelo del handler: `BuscarOCrearAsync` antes de la transacción, `transaccion.AbrirAsync`, `candado.TomarAsync(fechaLocal)`, `citas.Agregar`/`GuardarAsync` (`GuardadoDeCita` distinto de `Guardada` = traslape), `DejarRastroAsync` (bitácora + `outbox.PublicarAsync(new CitaAgendada(...), eventoId, claveDeOrden: cita.Id.Valor, ...)`), `tx.ConfirmarAsync`; `DatoDelPacienteInvalido` traduce los códigos de Pacientes. La versión del aviso viene de `contenido.VersionDelAvisoDePrivacidad`. No se toca.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Features/ITramosDeLaFecha.cs` -- Tramos vigentes de una fecha, sin rastrear.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/AgendarCitaEnLinea.cs` -- forma del comando (clase con `set`, DataAnnotations, `TelefonoDeDiezDigitos`, `Correo` que normaliza vacío a nulo) y `CodigosDeAgenda` (línea 102): agregar `NotaObligatoria`.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/Registro.cs` -- registrar el handler.
- `src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/ConfirmacionAlAgendar.cs`, `RecordatorioAlAgendar.cs` -- reaccionan a `CitaAgendada` de cualquier origen; no se tocan.
- `src/DraSelina.Consultorio.Web/Kernel/IActorActual.cs` -- `IdDelAdministradorAsync()`.
- `tests/DraSelina.Consultorio.Domain.Tests/` -- pruebas de dominio de `Cita` (modelo para las nuevas). `tests/DraSelina.Consultorio.IntegrationTests/ReservaEnLineaTests.cs`, `ActorYAdministracionTests.FijarUsuario`, `Arnes.Canal(web)`, `DarUnaVueltaDeEntregaAsync` -- modelo de integración con sesión de Administrador y correo.

## Tasks & Acceptance

**Execution:**
- [x] `src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs` -- `AgendarPorConsultorio(paciente, tipo, inicio, nota, motivo, consentimiento, tramosVigentes, now, clave, out cita)` y `RechazoAlAgendar.NotaObligatoria`.
- [x] `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/AgendarCitaPorConsultorio.cs` -- comando (`IRequiereAdministrador`): tipo, fecha, hora, nombre, teléfono, correo opcional, motivo opcional, nota opcional, consentimiento externo obligatorio; resultado con `CitaId`, fecha, hora y si cae fuera de horario. `CodigosDeAgenda.NotaObligatoria`.
- [x] `src/DraSelina.Consultorio.Web/Modules/Agenda/Features/AgendarCitaPorConsultorioHandler.cs` + `Infrastructure/Registro.cs` -- el caso de uso.
- [x] Pruebas de dominio (cada regla de la matriz sin base) e integración (sesión, traslape, bitácora con id, evento con origen, correo en `Canal`, Recordatorio programado, `HorariosLibres`).

**Acceptance Criteria:**
- Given una Cita del consultorio guardada, when se lee `agenda.bitacora`, then hay una entrada `Agendada` con actor Administrador, su `actor_id` y sin motivo, nota ni contacto en `detalle`.
- Given un traslape, when falla el guardado, then no quedan Cita, bitácora ni mensaje en el outbox.
- Given la 5.2, when se agenda un sábado cerrado con nota, then `DiasDeLaAgenda` lo marca fuera de horario con su nota.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 0, low 1, false 1, maybe-false 0; 1 a patch, 1 falso.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | `AgendarCitaPorConsultorioHandler`: una Cita rechazada (nota, pasado, traslape) deja al Paciente creado o con su correo actualizado, y la matriz dice "nada guardado" | false | — | AD-28 y RN-28, invariantes del diseño: `BuscarOCrear` confirma antes de la transacción de Agenda y "un Paciente sin Cita es un resultado aceptable"; el plan lo pide así (Always) y la reserva en línea hace lo mismo. "Nada guardado" en la matriz es de Agenda: Cita, bitácora y outbox, que las pruebas comprueban. |
| 2 | `AgendarCitaPorConsultorioHandler.cs:50`: `!tipo.Activo` repite en el handler la regla que ya decide `Cita.AgendarPorConsultorio` (`Cita.cs:238`), y la rama del dominio queda inalcanzable desde el caso de uso | low | patch | T-13 y AD-12: el handler orquesta, la regla vive en el agregado; `AgendarCitaEnLineaHandler` solo revisa `tipo is null`. Arreglo directo: quitar `!tipo.Activo` del handler; la prueba de integración del tipo inactivo sigue cubriéndolo por el dominio. |

## Verification

**Commands:**
- `dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.
