---
title: 'Agenda inicial de la doctora: citas de 60 min, 3 a 7 pm y Agenda del día a las 8 am'
type: 'feature'
ticket: ''
created: '2026-11-24'
status: 'built'
baseline_revision: '794ee0ba691e67e371cf7776bfd84606580f4c3c'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/README.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Los datos iniciales siembran citas de 30 min, una Agenda base de lunes a viernes de 15:30 a 18:00 y la Agenda del día a las 14:00. Selina decidió otra cosa (Gerardo, 2026-11-23/24), y el sistema saldría ofreciendo horarios que ella no atiende.

**Approach:** Cambiar los datos iniciales en el modelo y aplicarlos con una migración nueva por módulo. Las migraciones ya existentes no se tocan. Se actualizan PRD, spec, modelo de datos y despliegue, y se anota en el memlog.

Decisiones de Selina (Gerardo, 2026-11-23/24):
- Primera consulta y Consulta de seguimiento duran 60 min y las dos se reservan en línea. No hay tipo posquirúrgico, que es igual al seguimiento. No hay tipo de urgencia: las urgencias quedan fuera del sitio y, si hace falta, se registran a mano en Administración.
- La Agenda base es de lunes a viernes, de 15:00 a 19:00. La última cita empieza a las 18:00, así que hay 4 al día: 3, 4, 5 y 6 pm.
- El sábado no es fijo: un sábado de atención se abre con la Agenda custom de esa fecha.
- La Agenda del día ("Tus citas de hoy") sale a las 8:00 y conserva el motivo de consulta.
- A los Pacientes sin correo a los que el Consultorio les cancela se les avisa a mano, al teléfono que muestra el sistema. No cambia el código.
- Se lanza sin fotos. El retrato queda con las iniciales. No cambia el código.

Decisiones de este plan (Gerardo, 2026-11-24):
- Pruebas de integración: después de migrar, el arnés deja una agenda de prueba fija (15:30–18:00 de lunes a viernes, tipos de 30 min, Agenda del día a las 14:00). Así las pruebas de comportamiento no dependen de la configuración de Selina. Una prueba aparte, sobre una base migrada sin esa agenda, comprueba que la migración siembra exactamente lo de Selina.
- Código y documentos van juntos en este plan, aunque pase del tamaño sugerido.

## Boundaries & Constraints

**Always:**
- Los valores salen de una sola fuente: `AgendaDeAtencionSembrada`, `TiposDeVisitaSembrados` y `FilaDeConfiguracionDeEnvio.Semilla`. La migración se genera con `./dotnet.sh ef migrations add` (README, "Crear una migración").
- Solo cambia lo sembrado. Las reglas de reserva, el Recordatorio de las 09:00 y los correos sembrados se quedan como están.
- Cada documento que cambia queda con su fuente: Gerardo, con la fecha.

**Never:**
- Editar migraciones ya existentes, sus Designer o borrar migraciones.
- Ejecutar `database update`, porque la web nunca migra (AD-25).
- Cambiar la lógica de la Rejilla, de la disponibilidad o de la Agenda del día.
- Agregar tipos de visita nuevos.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Día hábil sin citas | Base sembrada, martes futuro dentro de la ventana | Horarios 15:00, 16:00, 17:00 y 18:00 | No error expected |
| Sábado | Base sembrada, sábado sin Agenda custom | Sin horarios | No error expected |
| Sábado abierto | Agenda custom 10:00–13:00 ese sábado | 10:00, 11:00 y 12:00 | No error expected |
| Agenda del día | Configuración sembrada | Se programa a las 08:00 locales | No error expected |
| Base de datos ya migrada | Base local con las migraciones anteriores | La migración nueva actualiza las filas sembradas por su id | No error expected |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/AgendaDeAtencionSembrada.cs` -- `Inicio`/`Fin` (15:30/18:00 → 15:00/19:00), con su doc-comment. Ids fijos.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/TiposDeVisitaSembrados.cs` -- `DuracionMin = 30` en las dos filas → 60; doc-comment.
- `src/DraSelina.Consultorio.Web/Modules/Notificaciones/Infrastructure/FilaDeConfiguracionDeEnvio.cs` -- `Semilla.HoraAgendaDelDia` 14:00 → 08:00; doc-comment.
- `src/.../Modules/{Agenda,Notificaciones}/Infrastructure/Migrations/` -- migraciones nuevas. Una prueba de generación produjo solo `UpdateData`: 5 tramos, 2 duraciones y 1 hora, con el snapshot actualizado. El aviso "may result in the loss of data" es genérico de EF; la migración no borra nada.
- Comandos: `./dotnet.sh ef migrations add <Nombre> --project src/DraSelina.Consultorio.Web --context AgendaDbContext --output-dir Modules/Agenda/Infrastructure/Migrations --namespace DraSelina.Consultorio.Agenda.Infrastructure.Migrations`. Para Notificaciones es igual, con `NotificacionesDbContext` y su carpeta.
- Pruebas: con los valores nuevos fallan 101 de 1185. Todas son de integración y usan la base migrada, que trae los datos sembrados. Por archivo:
  - ReprogramarCita: 16
  - TipoDeConsultaYDiaYHora: 11
  - ReservaEnLinea: 9
  - ConfiguracionDeEnvio: 9
  - TusDatos: 8
  - otros 21 archivos: de 1 a 6 cada uno
  
  Tienen escritos a mano los valores 16:00, 15:30 y `AddMinutes(30)`, y `Sembrada` (09:00/14:00) en `ConfiguracionDeEnvioTests.cs:34`. Las pruebas de dominio y UI no fallan, porque arman sus propios datos.
- Documentos:
  - PRD `prd-appointment-booking.md`: :54 (UJ-5, 2:00 pm), :93 (30 min), :211 (valor inicial), :333 (FR-25), §10 preguntas 1, 3, 4, 6 y 9, §11 supuesto FR-25.
  - `spec-appointment-booking.md:32`: el máximo de horarios.
  - `ddd-and-data-model-appointment-booking.md`: :1006 y :1048–1051.
  - `ux-appointment-booking/EXPERIENCE.md:262`: UJ-5.
  - `addendum.md:43`: "Contenido pendiente", que ya está entregado salvo las fotos.
  - `docs/despliegue.md` §"Condiciones de lanzamiento": agregar el aviso de publicidad COFEPRIS.
- Memlog: `python3 _bmad/scripts/memlog.py append --workspace <carpeta> --type <tipo> --by user --text "..."`, en las carpetas de PRD y arquitectura.

- `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs` -- migra en dos sitios: `InitializeAsync` (:82, la base de la corrida) y `CrearBaseMigradaAsync` (:127). En ambos, después de `MigrationService.EjecutarAsync`, aplicar la agenda de prueba con SQL, por los ids sembrados: `agenda.agenda_base_tramo` inicio y fin, `agenda.tipo_de_visita.duracion_min` y `notificaciones.configuracion_de_envio.hora_agenda_del_dia`. Los valores de prueba van como constantes en el arnés, documentadas como "la agenda de prueba, no la de Selina". `CrearBaseVaciaAsync` seguido de `MigrationService.EjecutarAsync` da una base con lo sembrado real, para la prueba nueva.
- `BaseMigradaTests.cs` -- lugar natural para la prueba de la semilla real.

## Tasks & Acceptance

**Execution:**
- [x] Los 3 archivos de semilla -- valores nuevos y doc-comments con la fuente -- es la única fuente de los datos iniciales.
- [x] Migraciones nuevas de Agenda y Notificaciones -- generadas, revisadas: solo `UpdateData` -- aplican la semilla a bases ya migradas.
- [x] `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs` -- aplicar la agenda de prueba fija después de migrar, en los dos sitios -- las pruebas de comportamiento no cambian.
- [x] `tests/DraSelina.Consultorio.IntegrationTests/BaseMigradaTests.cs` -- sobre una base migrada sin agenda de prueba, comprobar: tramos de 15:00 a 19:00 de lunes a viernes y ninguno sábado ni domingo; los dos tipos de 60 min y reservables; Agenda del día a las 08:00 y Recordatorio a las 09:00; y que los horarios libres de un martes son 15:00, 16:00, 17:00 y 18:00 -- cubre la matriz.
- [x] Documentos listados en el Code Map -- valores nuevos; preguntas de §10 resueltas con su decisión y fuente; sábado por Agenda custom; aviso de publicidad en las condiciones de lanzamiento -- los documentos coinciden con el código.
- [x] Memlog del PRD y de la arquitectura -- una entrada por decisión -- deja registro de las reglas que cambiaron.

**Acceptance Criteria:**
- Given una base recién migrada, when se consultan los horarios libres de un día hábil, then se ofrecen 3, 4, 5 y 6 pm, y cada cita termina una hora después.
- Given el PRD, when se lee §10, then las preguntas 1, 3, 4, 6 y 9 aparecen resueltas, cada una con su decisión y su fuente.

## Implementation Notes

- Las migraciones nuevas son `20261124235901_SembrarAgendaDeSelina` (Agenda: 5 tramos y 2 duraciones) y `20261124235905_SembrarHoraDeAgendaDelDia` (Notificaciones: 1 hora), solo `UpdateData`. El EF 11 de diseño nombra la clase con el prefijo de la fecha (`_20261124235901_...`), usa namespace de archivo y agrega `LastMigrationId` al snapshot, que por eso cambia de formato completo; con `git diff -w` solo cambian los valores sembrados, el namespace y `ProductVersion` 10.0.12 → 11.0.0.
- En el arnés, los tramos se cambian todos (`update agenda.agenda_base_tramo` sin `where`): `TramosBase` y sus ids son `internal` y, recién migrada, la tabla solo tiene los cinco sembrados. Los tipos y la configuración, por su id.
- `BaseMigradaTests` suma cinco pruebas: tramos, tipos, horas de envío, horarios del martes y del sábado (que también cubre el sábado abierto con Agenda custom de 10:00 a 13:00).
- README, sección del arnés: el paso de la agenda de prueba.

## Plan Change Log

## Review Triage Log

Pasada 1 (lente quick): 0 high, 2 medium, 4 low, 0 false, 0 maybe-false; un hallazgo parcial rechazado.

| Hallazgo | Veredicto | Ruta | Evidencia |
| --- | --- | --- | --- |
| PRD FR-2 :104 sigue con 3:30–5:30 pm | medium | patch | Contradice FR-12, CAP-1 y el código (15:00–18:00). |
| UJ-3 (PRD :50, EXPERIENCE :226) con 3:30–6:00 y Cita de 5:30 | low | patch | Con base 3:00–7:00 y rejilla de 60 min la 5:30 no existe; corrección numérica directa. |
| AC "cada cita termina una hora después" sin prueba | medium | patch | `DiaConHorarios` solo trae inicios; ninguna prueba agenda sobre la semilla real. |
| Matriz "Agenda del día se programa a las 08:00" solo lee la columna | low | rechazado | La lógica de envío no cambió y ya la cubren las pruebas de envíos con la hora configurada; probar el envío agregaría complejidad. |
| §10 pregunta 9 dice "todos los días, antes de cualquier Cita" | low | patch | FR-25 solo envía en fechas con Tramos o Citas y FR-15 permite una Cita manual antes de las 8:00. |
| FR-26 y §11 siguen con el supuesto "el sitio entra en el piloto" | low | patch | La pregunta 4 ya está resuelta; corrección directa. |
| `docs/Appointment Booking System — Solution Plan.md` conserva valores viejos sin aviso | low | patch | Su encabezado remite al PRD, pero nada avisa del cambio; una línea en el encabezado. |

## Verification

**Commands:**
- `dotnet build` -- expected: sin errores ni advertencias nuevas
- `dotnet test` (con Docker encendido) -- expected: todo verde
- `git diff --stat -- '*Migrations/2026110*' '*Migrations/2026111*'` -- expected: vacío, porque ninguna migración anterior cambia
