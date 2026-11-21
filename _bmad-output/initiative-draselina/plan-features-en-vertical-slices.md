---
title: 'Features en vertical slices'
type: 'refactor'
ticket: ''
created: '2026-11-21'
status: 'built'
baseline_revision: 'ac89cf7c261f84d8c492a24055fd1529821a3845'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 1
context:
  - '{project-root}/_bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `Modules/<Módulo>/Features/` es una carpeta plana con todo junto: en Agenda, 60 archivos con 32 casos de uso, 18 interfaces y 10 helpers; en Notificaciones, 26 con casos de uso, puertos y plantillas mezclados. Cuesta encontrar los casos de uso, y Gerardo quiere que Features muestre los casos de uso.

**Approach:** vertical slices, con una carpeta por caso de uso dentro de `Features/` y un archivo por tipo. Lo que solo usa ese caso de uso (puerto de lectura, records, helpers, plantillas) va en su carpeta. Lo que usan dos o más casos de uso, y los puertos transversales (transacción, outbox, candados, bitácora), va en `Features/Compartido/`. Las reglas de dependencia no cambian.

**Decisiones (Gerardo, 2026-11-21):**
- Namespace plano: todo sigue en `DraSelina.Consultorio.<Módulo>.Features`, sin importar la subcarpeta. Un namespace por slice choca con el comando del mismo nombre (CS0118, comprobado). En Rider se apaga la inspección de namespace contra carpeta solo en el proyecto Web.
- Una carpeta por caso de uso, no por área.
- Cada plantilla de correo exclusiva va al slice que la usa. Las compartidas van a `Compartido/`.
- Ningún caso de uso llama código del slice de otro. Lo que hoy se comparte desde un handler se saca a `Compartido/` (Gerardo, 2026-11-21, al ver las cuatro llamadas entre slices).

## Boundaries & Constraints

**Always:** un archivo por tipo. Mover con `git mv`. La carpeta del slice se llama como su comando o consulta, que es el nombre del handler sin `Handler`. Los casos de uso que no son handlers (eventos, `AgendaApi`, `PacientesApi`, `EnviosProgramados`) usan el nombre de su clase. Todo documento que describa Features se actualiza, y el cambio se registra en el `.memlog.md` de su carpeta.

**Never:** cambiar namespaces, comportamiento ni las pruebas existentes, salvo lo que exige sacar a `Compartido/` el código de las cuatro llamadas entre slices: sus tipos nuevos, el registro de DI del servicio nuevo y la construcción de `AgendaApi` en `EnlaceDeGestionTests`. No tocar `Domain`, `Contracts` ni las migraciones; en `Infrastructure` solo `Registro.cs`. No juntar tipos en un archivo. No crear capas nuevas.

</frozen-after-approval>

## Code Map

- `Modules/{Agenda,Pacientes,Notificaciones}/Features/*.cs` -- lo que se mueve, bajo `src/DraSelina.Consultorio.Web/`. El reparto está en Tasks.
- `tests/DraSelina.Consultorio.ArchitectureTests/Verificador.cs:109,137,147` -- la capa se elige por la carpeta más larga y el namespace debe ser igual a la capa o estar dentro de ella. Un archivo en `Features/X/` con namespace `<M>.Features` pasa sin cambios.
- `tests/DraSelina.Consultorio.ArchitectureTests/SolucionEntregadaTests.cs` -- aquí va la prueba nueva de la estructura.
- No hay tabla namespace → `DbContext` en `src/`. Los decoradores no miran el namespace.
- Documentos: `architecture-appointment-booking.md:39` (tabla de capas), `:117` (AD-4), `:366-368` (árbol); `ddd-and-data-model-appointment-booking.md:409,579`; `README.md:147,357,399,430,464,720-722`.

## Tasks & Acceptance

**Execution:**
- [x] `Modules/Agenda/Features/` -- `git mv`: cada `<X>Handler.cs` a `<X>/`, y `CancelarCitasElegidas.cs` y `AgendaApi.cs` a su carpeta. Lo exclusivo va con su slice: `CitaPorEnlace/ICitasDelEnlace`, `DetalleDeCita/{ICitaDelDetalle,IBitacoraDeLaCita}`, `TiposDeVisitaEnConfiguracion/ITiposDeVisitaEditables`, `TiposDeVisitaReservables/ITiposDeVisitaReservables`, `AgendaApi/{GeneradorDeCalendario,ICitasDelDia}`. A `Compartido/` (20): `IAgendaDelRango`, `ITramosDeLaFecha`, `ICitasActivas`, `ICitasDeLaAgenda`, `ICitasDelCalendario`, `ICitasDelPaciente`, `ITiposDeVisitaActivos`, `EstadoMostrado`, `ClasificacionDelDia`, `CitasQueQuedanFuera`, `CancelacionPorConsultorio`, `DuracionMinima`, `DetalleDeTipo`, `EventosDeAgendaCustom`, `ITransaccionDeAgenda`, `IOutboxDeAgenda`, `IBitacoraDeAgenda`, `ICandadoDeFecha`, `ICandadoDeLaAgendaDeAtencion`, `OpcionesDeEnlaceDeGestion` -- 33 carpetas de slice + `Compartido/`.
- [x] `Modules/Notificaciones/Features/` -- slices: `AlertaDeCancelacionTardia/` (+`PlantillaDeAlertaTardia`), `AvisoAlCancelar/` (+`PlantillaDeCancelacion`), `AvisoAlReprogramar/` (+`PlantillaDeReprogramacion`), `EnviosProgramados/` (+`PlantillaDeAgendaDelDia`, `PlantillaDeRecordatorio`), `CambiarConfiguracionDeEnvio/`, `ConfiguracionDeEnvioVigente/`, `ConfirmacionAlAgendar/`, `RecordatorioAlAgendar/`, `RecordatorioAlCancelar/`, `RecordatorioAlReprogramar/`, `RecordatorioPrevisto/`. A `Compartido/`: `EnvioUnico`, `MarcoDeCorreo`, `FormatoDeFechaEnCorreo`, `PlantillaDeConfirmacion`, `IMensajes`, `IRecordatorios`, `INotificationChannel`, `IConfiguracionDeEnvio`, `IDireccionDelSitio`, `ITransaccionDeNotificaciones`.
- [x] `Modules/Pacientes/Features/` -- `BuscarPacientes/` (+`IBusquedaDePacientes`), `PacientesApi/`, `Compartido/ITransaccionDePacientes`.
- [x] `src/DraSelina.Consultorio.Web/DraSelina.Consultorio.Web.csproj.DotSettings` -- nuevo; `CheckNamespace` = `DO_NOT_SHOW`. [ASSUMPTION] Rider lee este archivo como capa compartida del proyecto. No se puede probar desde la terminal.
- [x] `tests/DraSelina.Consultorio.ArchitectureTests/SolucionEntregadaTests.cs` -- prueba nueva: en `Modules/*/Features/` no hay `.cs` sueltos en la raíz; solo carpetas.
- [x] `architecture-appointment-booking.md` -- tabla de capas (`:39`), AD-4 (`:117`), árbol (`:366-368`): `Features/<CasoDeUso>/` y `Features/Compartido/`, con namespace plano. Va con nota fechada y memlog en la carpeta.
- [x] `ddd-and-data-model-appointment-booking.md:409,579` -- mismas precisiones; memlog en la misma carpeta.
- [x] `README.md:147,357,399,430,464,720-722` -- rutas y la regla de dónde va cada cosa.
- [x] `Agenda/Features/Compartido/CalendarioDeLaCita.cs` -- nuevo, `public sealed class CalendarioDeLaCita(IOptions<OpcionesDeEnlaceDeGestion>, IContenidoApi, IClock)`. Recibe de `AgendaApi` la clave y la URL base, `Enlace(CitaId)` (lo que hoy hace `ObtenerEnlaceDeGestion`) y `Archivo(CitaDelCalendario?)`, con la misma regla RN-22/RN-31 y el mismo `GeneradorDeCalendario`. `git mv AgendaApi/GeneradorDeCalendario.cs Compartido/`.
- [x] `Agenda/Features/AgendaApi/AgendaApi.cs` -- constructor `(CalendarioDeLaCita, ICitasDelCalendario, ITramosDeLaFecha, ICitasDelDia)`; delega `ObtenerEnlaceDeGestion` y `ObtenerArchivoDeCalendarioAsync` en el calendario. Se borra `Archivo` público.
- [x] `Agenda/Features/ArchivoDeCalendarioPorEnlace/ArchivoDeCalendarioPorEnlaceHandler.cs` -- recibe `CalendarioDeLaCita` en vez de `AgendaApi`.
- [x] `Agenda/Infrastructure/Registro.cs` -- `.AddScoped<CalendarioDeLaCita>()`.
- [x] `tests/DraSelina.Consultorio.IntegrationTests/EnlaceDeGestionTests.cs:141` -- `Api(clave)` construye `AgendaApi` con un `CalendarioDeLaCita` (contenido y reloj en `null!`, como hoy).
- [x] `Agenda/Features/Compartido/RastroDeAgendaCustom.cs` -- nuevo, `internal static class` con `Detalle(AgendaCustom)` y `PublicarAsync(IOutboxDeAgenda, IEventoDeAgendaCustom, CancellationToken)`, que salen de `GuardarAgendaCustomHandler`. Lo usan `GuardarAgendaCustomHandler` y `BorrarAgendaCustomHandler`.
- [x] `Agenda/Features/Compartido/ValoresDeReglasDeReserva.cs` -- nuevo, `internal static class` con `De(ReglasDeReserva)`, que sale de `ReglasDeReservaVigentesHandler.Valores`. Lo usan `ReglasDeReservaVigentesHandler` y `CambiarReglasDeReservaHandler`.
- [x] `Agenda/Features/Compartido/SemanaTipoGuardada.cs` -- nuevo, `internal static class` con `DeLunesADomingo` y `LeerAsync(IAgendaDelRango, CancellationToken)`, que salen de `SemanaTipoHandler`. Lo usan `SemanaTipoHandler` y `SemanaDeAtencionHandler`.
- [x] Los `<see cref>` que apuntaban a los métodos movidos -- apuntar a la clase nueva.

**Acceptance Criteria:**
- Given la nueva estructura, when se lista `Modules/<Módulo>/Features/`, then en la raíz solo hay carpetas: una por caso de uso y `Compartido/`.
- Given un caso de uso, when se abre su carpeta, then están su handler y todo lo que solo él usa. Ningún otro slice usa en código (fuera de `///`) un tipo de esa carpeta. Solo `Infrastructure/Registro.cs` y las pruebas los nombran.
- Given el cambio, when se corren build y tests, then 0 advertencias y todas las pruebas pasan, incluida la prueba nueva de estructura.
- Given la arquitectura, el documento de dominio y el README, when se leen, then describen la misma estructura sin contradecirse.

## Implementation Notes

- Implementado directamente, sin subagente: el clasificador de permisos rechazó el subagente.
- `git mv` de 60 + 26 + 4 archivos. Agenda: 33 slices y `Compartido/` (20). Notificaciones: 11 slices y `Compartido/` (10). Pacientes: 2 slices y `Compartido/` (1). Ningún `.cs` cambia de contenido.
- Nuevos: `src/DraSelina.Consultorio.Web/DraSelina.Consultorio.Web.csproj.DotSettings` y la prueba `Features_solo_tiene_carpetas_de_caso_de_uso_y_Compartido` en `SolucionEntregadaTests.cs`.
- Documentos: tabla de capas, AD-4 y Structural Seed de la arquitectura; 2.6, políticas y el historial del documento de dominio; README (5 lugares y el árbol); memlog de `architecture-appointment-booking/`.
- Hallazgo: cuatro llamadas en código entre slices, que ya existían. (1) `ArchivoDeCalendarioPorEnlaceHandler` recibe la clase `AgendaApi`. (2) `BorrarAgendaCustomHandler` usa `GuardarAgendaCustomHandler.Detalle` y `.PublicarAsync`. (3) `CambiarReglasDeReservaHandler` usa `ReglasDeReservaVigentesHandler.Valores`. (4) `SemanaDeAtencionHandler` usa `SemanaTipoHandler.LeerAsync`. Moverlos a `Compartido/` cambia código, y eso lo prohíbe el bloque congelado. Queda para que Gerardo decida. Las demás menciones entre slices son `<see cref>` en comentarios `///`.
- Iteración 1: las cuatro salen a `Agenda/Features/Compartido/`: `CalendarioDeLaCita` (con `GeneradorDeCalendario`, movido con `git mv`), `RastroDeAgendaCustom`, `ValoresDeReglasDeReserva` y `SemanaTipoGuardada`. `AgendaApi` delega en `CalendarioDeLaCita` y ya no tiene `Archivo` público; `ArchivoDeCalendarioPorEnlaceHandler` recibe `CalendarioDeLaCita`; `Registro.cs` lo registra; `EnlaceDeGestionTests.Api` lo construye. Fuera de `///`, ningún slice nombra un tipo de la carpeta de otro (comprobado con grep). La regla se agregó a la tabla de capas, a 2.6 del documento de dominio, al README y al memlog.
- Verificado: `./dotnet.sh build` 0 errores y 0 advertencias; `./dotnet.sh test` 1177 pasan, 0 fallan; `find ... -maxdepth 1 -type f` vacío; `git diff --stat -M ac89cf7 -- src` con contenido solo en el `.DotSettings`, `Registro.cs` y los archivos de las cuatro extracciones.

- Iteración 1: lo que dice arriba sobre "ningún `.cs` cambia" y "queda para que Gerardo decida" se reemplaza por esto. Las cuatro llamadas se sacaron a `Agenda/Features/Compartido/` (`CalendarioDeLaCita`, con `GeneradorDeCalendario` movido ahí; `RastroDeAgendaCustom`; `ValoresDeReglasDeReserva`; `SemanaTipoGuardada`), y una quinta, `OmitidoDelRecordatorio`, a `Notificaciones/Features/Compartido/`. Agenda `Compartido/` queda con 25 archivos y Notificaciones con 11. Build 0/0 y 1,177 pruebas pasan.

## Plan Change Log

- Iteración 1 (2026-11-21). Hallazgo propio al revisar: cuatro llamadas en código entre slices (ver Implementation Notes). Gerardo renegoció la intención y pidió sacarlas a `Compartido/`. Se agregaron una decisión y el ajuste de Never al bloque congelado, las tareas de extracción y un AC más estricto. No se revierte nada. KEEP: los `git mv`, la prueba de estructura, el `.DotSettings` y los documentos ya hechos.

## Review Triage Log

- Pasada 1 (quick, después de la iteración 1): high 0, medium 2, low 2, false 1, sin veredicto 1.
  - medium, patch: `OmitidoDelRecordatorio` estaba declarado en `RecordatorioAlAgendar/` y lo usaba también `RecordatorioAlReprogramar`. Se movió sin cambios a `Notificaciones/Features/Compartido/OmitidoDelRecordatorio.cs`. Ahora la búsqueda de llamadas entre slices revisa todos los tipos declarados, no solo los que tienen archivo propio, y da 0.
  - medium, patch: `docs/estructura-del-proyecto.md` seguía con `Features` plana. Se actualizó el árbol y la tabla.
  - low, rechazado: archivos con varios tipos (puerto con sus records). Ya existía, y la regla prohíbe juntar tipos nuevos, no la convención actual.
  - false: "solo Registro.cs nombra tipos de un slice". Infrastructure implementa los puertos de lectura por AD-4. Es redacción del AC, no un defecto.
  - rechazado: las notas del plan contradecían la iteración 1. Se corrige editando el plan, así que queda aclarado en Implementation Notes.
  - low: `AgendaApi.cs` y `SemanaTipoHandler.cs` aparecen como borrado más archivo nuevo y pierden su historial. Se resuelve al hacer commit: uno con los movimientos y otro con los cambios de contenido.

## Design Notes

Por qué el namespace es plano:

```csharp
using DraSelina.Consultorio.Agenda.Contracts;
namespace DraSelina.Consultorio.Agenda.Features.AgendarCitaEnLinea;
class AgendarCitaEnLineaHandler : ICommandHandler<AgendarCitaEnLinea, …>
// CS0118: 'AgendarCitaEnLinea' is a namespace but is used like a type
```

Con el namespace plano no hay choque, y las referencias entre slices y desde `Infrastructure` siguen iguales.

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 errores, 0 advertencias
- `./dotnet.sh test` -- expected: todas pasan (necesita Docker)
- `find src/DraSelina.Consultorio.Web/Modules/*/Features -maxdepth 1 -type f` -- expected: vacío
- `git diff --stat -M ac89cf7 -- src | grep -v "=>"` -- expected: solo el `.DotSettings`, `Registro.cs` de Agenda y los archivos de las cuatro extracciones
