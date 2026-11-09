---
title: 'Horas y destinatarios configurables'
type: 'feature'
ticket: '7'
created: '2026-11-09'
status: done
baseline_revision: 'c5c3a7c8f748fca3bb5e7a64163639a773f41a41'
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

**Problem:** Las dos horas de envío y los dos destinatarios internos solo cambian editando la base. CAP-16 y el Done when 6 de la épica piden cambiarlos sin desplegar, y la pantalla de Configuración de la épica 6 necesita una consulta y un comando que componer (AD-14).

**Approach:** Una consulta y un comando de administración de Notificaciones sobre `configuracion_de_envio`:
- **Qué cambian:** la hora del Recordatorio, la hora de la Agenda del día, el correo de la doctora y el correo del Administrador.
- **Validación:** de forma (AD-12).
- **Cuándo aplica:** el siguiente envío ya usa los valores nuevos.
- **Recordatorios ya programados:** al cambiar la hora del Recordatorio, los pendientes se mueven a la hora nueva; el que quede con su hora ya pasada se registra `Omitido` (Decision de Gerardo, 2026-11-09).

## Boundaries & Constraints

**Always:**
- **Consulta y comando:**
  - Los contratos van en `Notificaciones/Contracts`: la consulta `ConfiguracionDeEnvioVigente : IQuery<ConfiguracionDeEnvio…>` y el comando `CambiarConfiguracionDeEnvio : ICommand`.
  - Los dos llevan `IRequiereAdministrador` (AD-22): sin sesión vuelven `NoAutorizado` por el decorador existente, sin llegar al handler. Son los primeros de producción que lo usan.
- **Validación de forma** (DataAnnotations, AD-12), con mensajes en español de tú:
  - Las dos horas son obligatorias.
  - Los dos correos son obligatorios, con `[EmailAddress]` y a lo más 254 caracteres.
  - Una hora o un correo inválidos vuelven como `Resultado` inválido bajo su propiedad.
- **El handler del comando:**
  - En una transacción de Notificaciones, actualiza la única fila.
  - Si la hora del Recordatorio cambió, por cada fila de `recordatorio_programado` recalcula `programado_para` con `HoraDelRecordatorio.ProgramadoPara(inicio, nueva)`.
  - Si la nueva queda en o antes de `IClock.UtcNow`, borra la fila y registra `mensaje` `Recordatorio` `Omitido` con `cita_id`. Si no, la actualiza.
  - Todo en la misma transacción.
- **Lo que no cambia:** la Agenda del día y la alerta ya leen la configuración en cada envío (4.5, 4.6). Basta con guardar.
- **Sin bitácora:** la configuración de envío no está en CAP-17 ni en `agenda.bitacora`, que es de Agenda.
- **Nada a registros:** ni las direcciones ni los valores van a registros (AD-23). El `ToString` del comando las oculta.

- Decision (Gerardo, 2026-11-09): el plan se conserva completo aunque pasa de 1,600 tokens.

**Never:**
- La pantalla de Configuración (épica 6).
- Las reglas de reserva o los tipos de visita (AD-14: son de Agenda).
- Cambiar el remitente o `UrlDelSitio` (vienen de la configuración del host).
- Recalcular Recordatorios ya enviados.
- Crear ASP.NET Core Identity (épica 5): las pruebas fijan el usuario como lo hace `ActorYAdministracionTests`.

## I/O & Edge-Case Matrix

Reloj de prueba: lunes 2 de noviembre a las 12:00 local.

| Escenario | Acción | Resultado |
|---|---|---|
| Sin sesión | comando o consulta | `NoAutorizado`; nada cambia |
| Leer | consulta con sesión | 09:00, 14:00, `doctora@consultorio.local`, `administrador@consultorio.local` |
| Cambiar la Agenda del día | comando con 13:00 | a las 13:00 del martes sale "Tus citas de hoy" y a las 12:59 no |
| Cambiar el correo de la doctora | comando con otra dirección | la siguiente Agenda del día va a esa dirección |
| Cambiar el correo del Administrador | comando con otra dirección | la siguiente alerta tardía va ahí |
| Mover Recordatorios | Cita del jueves 5; hora de 09:00 a 10:00 | su fila pasa al miércoles 4 a las 10:00; a las 09:00 no sale y a las 10:00 sí |
| Recordatorio que queda en el pasado | Cita del martes 3; hora de 13:00 a 11:00 (el lunes a las 11:00 ya pasó) | su fila desaparece y queda `mensaje` `Recordatorio` `Omitido` |
| Hora inválida o correo mal escrito | comando | `Resultado` inválido bajo la propiedad; nada cambia |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/IRequiereAdministrador.cs`, `src/DraSelina.Consultorio.Web/DecoradoresDeHandlers.cs` y `RechazoSinAdministrador.cs`: el marcador y el decorador que ya rechaza sin usuario.
- `src/DraSelina.Consultorio.Web/ValidacionDeComandos.cs`: la validación de forma que devuelve `Resultado` inválido.
- `.../Modules/Agenda/Contracts/AgendarCitaEnLinea.cs` y `ReprogramarCitaPorEnlace.cs`: el modelo de comando con DataAnnotations y `ToString` que oculta datos.
- `.../Modules/Notificaciones/Contracts/RecordatorioPrevisto.cs` (4.3) y `Features/RecordatorioPrevistoHandler.cs`: el primer contrato y handler de consulta de Notificaciones.
- `.../Notificaciones/Features/IConfiguracionDeEnvio.cs` e `Infrastructure/ConfiguracionDeEnvio.cs` (4.5, 4.6): agregar la lectura completa y la escritura.
- `.../Notificaciones/Features/IRecordatorios.cs` e `Infrastructure/Recordatorios.cs` (4.2), con `HoraDelRecordatorio.ProgramadoPara`: agregar "todos los pendientes" para recalcular. `MoverAsync` y `BorrarAsync` ya existen.
- `.../Notificaciones/Infrastructure/Registro.cs`: registrar la consulta y el comando por su interfaz.
- `tests/DraSelina.Consultorio.IntegrationTests/ActorYAdministracionTests.cs`: cómo se fija el usuario autenticado o anónimo en un ámbito (`FijarUsuario`).
- `RecordatorioTests.cs`, `AgendaDelDiaTests.cs` y `AlertaDeCancelacionTardiaTests.cs`: los modelos de vuelta y de canal de prueba.

## Tasks & Acceptance

**Execution:**
- [x] `.../Notificaciones/Contracts/ConfiguracionDeEnvioVigente.cs` y `CambiarConfiguracionDeEnvio.cs` (nuevos).
- [x] `.../Notificaciones/Features/ConfiguracionDeEnvioVigenteHandler.cs` y `CambiarConfiguracionDeEnvioHandler.cs` (nuevos).
- [x] `.../Notificaciones/Features/IConfiguracionDeEnvio.cs`, `IRecordatorios.cs` y sus implementaciones.
- [x] `.../Notificaciones/Infrastructure/Registro.cs`.
- [x] `tests/.../IntegrationTests/ConfiguracionDeEnvioTests.cs` (nuevo): cada fila de la matriz.

**Acceptance Criteria:**
- Given las pruebas de arquitectura, when corren, then los contratos nuevos viven en `Notificaciones/Contracts` y la UI podrá componerlos sin ver Features.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 1, low 0, false 0, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Un `CitaAgendada` o `CitaReprogramada` reentregado después de que el cambio de hora dejó el Recordatorio `Omitido` lo vuelve a crear ya vencido, y la siguiente vuelta lo envía | medium | patch | Real: el `Omitido` del comando no lleva `evento_id`, y la guarda de los handlers solo busca `Enviado`; comparan con `OcurridoEn`, no con ahora. Arreglo: la guarda también se detiene con un `Omitido` de esa Cita registrado desde `OcurridoEn`. |

El revisor confirmó: contratos y registro según las reglas de capas, el rechazo sin sesión antes de la validación, la regla de recálculo en una transacción, nada de direcciones en `ToString` y el mismo orden de bloqueos en todos los caminos (sin bloqueo mutuo). Las decisiones del implementador (`ICommand<ConfiguracionDeEnvioValores>`, el nombre del tipo y el `FOR SHARE` al leer la hora en las transacciones de la 4.2) quedan para confirmación de Gerardo.

Corrección aplicada con un ajuste del implementador: la guarda (`CerradoDesdeAsync`) cuenta un `Omitido` solo si no tiene `evento_id`, que es el que deja el cambio de configuración. Contar también el `Omitido` de un evento rompería un caso válido: una Cita agendada con el Recordatorio ya vencido y luego reprogramada a una fecha posterior, si los dos eventos llegan tarde, se quedaría sin Recordatorio para la fecha nueva. La reentrega del mismo evento sigue cubierta por `ExisteAsync`. Dos pruebas nuevas fallan con la guarda vieja y pasan con la nueva; la suite completa pasa 698 de 698.

## Verification

**Commands:**
- `dotnet build`: debe compilar sin advertencias.
- `dotnet test tests/DraSelina.Consultorio.IntegrationTests --filter "FullyQualifiedName~ConfiguracionDeEnvioTests"`: deben pasar.
- `dotnet test`: debe pasar la suite completa.
