# Recorrido guiado: la aplicación del consultorio

Objetivo de Gerardo: entender el sistema, su arquitectura y la calidad del código, módulo por módulo, de forma muy
estructurada. Se empieza por Agenda. Cada bloque queda revisado solo cuando Gerardo lo dice.

**Bloque actual:** D–H (presentadas juntas)

---

## Parte A — Agenda

### [x] A1. Intención — revisado (sin cambios)

Qué es el sistema y qué papel tiene Agenda en él.

El sistema reemplaza la agenda en papel y WhatsApp del consultorio de ortopedia de la Dra. Selina Medina. Tiene tres
caras:

- **Paciente:** reserva en línea sin cuenta y gestiona su Cita (reprogramar o cancelar) con un Enlace de gestión que
  recibe en pantalla y por correo.
- **Administrador (Gerardo o Selina):** ve la agenda del día y de la semana, agenda Citas pactadas por teléfono, marca
  "No se presentó", cancela y configura horarios y reglas.
- **Correo:** confirmaciones, recordatorios, avisos de cambio y la agenda del día a la doctora.

**Agenda es el núcleo.** Es el único contexto con modelo de dominio rico y la mayor cobertura de pruebas. Es dueña de
la Cita y sus estados, del horario de atención (semana tipo y días con horario propio), de las reglas de reserva y del
Enlace de gestión. Los otros módulos le preguntan por su contrato (`IAgendaApi`) o escuchan sus eventos; nunca tocan
sus tablas.

Fuente: la arquitectura ([architecture-appointment-booking.md](../architecture-appointment-booking/architecture-appointment-booking.md)),
el modelo de dominio ([ddd-and-data-model-appointment-booking.md](../architecture-appointment-booking/ddd-and-data-model-appointment-booking.md))
y el plan ([Solution Plan](../../../docs/Appointment%20Booking%20System%20—%20Solution%20Plan.md)).

### [x] A2. Grandes trazos — revisado (sin cambios)

Agenda vive en una carpeta dentro de la web, partida en cuatro capas que son namespaces, no proyectos. Para entender
el mecanismo, abrir en este orden:

1. [Registro.cs:25](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/Registro.cs#L25) — `AddAgenda`: todo lo que el módulo conecta al arrancar la web.
2. [Cita.cs:9](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs#L9) — el agregado `Cita`: sus estados y las reglas que decide sin base de datos.
3. [AgendarCitaEnLineaHandler.cs:43](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs#L43) — un caso de uso completo: validar, abrir la transacción, decidir con el dominio, guardar, publicar el evento.
4. [IAgendaApi.cs:6](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/IAgendaApi.cs#L6) — lo único que otros módulos pueden pedirle a Agenda.
5. [AgendaDbContext.cs:45](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/AgendaDbContext.cs#L45) — cómo se guarda todo en el esquema `agenda` de PostgreSQL.

### [x] A3. Las cuatro capas y quién vigila sus fronteras — revisado (sin cambios)

Agenda tiene cuatro carpetas que son namespaces: `Contracts` (lo público: comandos, resultados, eventos, `IAgendaApi`),
`Domain` (las reglas, sin base ni frameworks), `Features` (los casos de uso y los puertos que piden) e
`Infrastructure` (EF Core y la implementación de los puertos). Como todo está en un proyecto, las fronteras las
vigila una prueba que lee los `using` (y los nombres escritos completos) de cada archivo contra una tabla.

- [Reglas.cs:57](../../../tests/DraSelina.Consultorio.ArchitectureTests/Reglas.cs#L57) — la tabla de qué capa puede usar qué.
- [Verificador.cs:85](../../../tests/DraSelina.Consultorio.ArchitectureTests/Verificador.cs#L85) — el recorrido de cada archivo; capas puras solo con `System`.
- [SolucionEntregadaTests.cs:32](../../../tests/DraSelina.Consultorio.ArchitectureTests/SolucionEntregadaTests.cs#L32) — la prueba que corre la tabla sobre todo el código de la web.
- [VerificadorTests.cs:10](../../../tests/DraSelina.Consultorio.ArchitectureTests/VerificadorTests.cs#L10) — pruebas del propio verificador con casos prohibidos.
### [x] A4. El agregado Cita: estados, reglas y rechazos — revisado (sin cambios)

La Cita tiene tres estados: Agendada, y dos finales, No se presentó y Cancelada (con motivo Paciente, Tarde o
Consultorio). Solo cambia por sus métodos. Cada uno devuelve un rechazo con nombre o nulo, y lanza excepción solo
ante un error de programación. No lee la base: el llamador le pasa la hora actual, las reglas y los conteos. Lo que
no decide es el traslape entre Citas, porque eso lo decide la base (A8).

- [EstadoCita.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/EstadoCita.cs) — los estados, motivos y orígenes.
- [Cita.cs:84](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs#L84) — `Agendar` (en línea): el orden de los rechazos.
- [Cita.cs:199](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs#L199) — `AgendarPorConsultorio`: casi sin reglas, nota obligatoria fuera de horario.
- [Cita.cs:316](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs#L316) — `Reprogramar`; [Cita.cs:374](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs#L374) — `Cancelar` a tiempo o tarde.
- [Cita.cs:425](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs#L425) — `MarcarNoSePresento` y [Cita.cs:449](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs#L449) — `CancelarPorConsultorio`.
- [Cita.cs:469](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Cita.cs#L469) — los enums de rechazo.
### [x] A5. El horario de atención y los horarios libres — revisado (sin cambios)

El horario vive en `AgendaAtencion`: una semana tipo (Tramos por día) y fechas con horario propio ("custom", con
motivo) que reemplazan a la semana tipo ese día. `TramosVigentes(fecha)` decide cuál manda. Los horarios libres no se
guardan: `CalculadoraDeDisponibilidad` los calcula en cada consulta con la rejilla de cada Tramo (en pasos de la
duración del tipo), quitando lo pasado y lo que traslapa una Cita activa. La misma rejilla la usa la Cita al agendar
y reprogramar, así lo que se ofrece y lo que se acepta no pueden diferir.

- [AgendaAtencion.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/AgendaAtencion.cs) — semana tipo, custom y `TramosVigentes`.
- [Tramo.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/Tramo.cs) — un periodo `[inicio, fin)` que no puede ser inválido.
- [CalculadoraDeDisponibilidad.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/CalculadoraDeDisponibilidad.cs) — `Calcular`, `Ventana`, `EstaEnLaRejilla` y la rejilla.
- [HorariosLibresHandler.cs:21](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/HorariosLibresHandler.cs#L21) — la consulta que lee lo necesario y llama a la calculadora.
- [AgendaDeAtencionSembrada.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/AgendaDeAtencionSembrada.cs) — el horario inicial: lunes a viernes, 15:30 a 18:00.
### [x] A6. Las reglas de reserva — revisado (sin cambios)

Cuatro valores configurables viven en el agregado único `ReglasDeReserva`: anticipación mínima, ventana en meses,
umbral de cancelación tardía y tope semanal por teléfono. El dominio decide qué valores sirven; el comando solo valida
la forma. Para decidir sobre una Cita, el handler arma `ReglasDeAgenda`: esos cuatro valores más los Tramos vigentes
de la fecha. Es lo único que la Cita recibe, y nunca lee configuración.

- [ReglasDeReserva.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/ReglasDeReserva.cs) — los cuatro valores y `Validar`.
- [ReglasDeAgenda.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/ReglasDeAgenda.cs) — el paquete que recibe la Cita.
- [CambiarReglasDeReserva.cs:22](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/CambiarReglasDeReserva.cs#L22) — el comando: solo forma.
- [CambiarReglasDeReservaHandler.cs:22](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/CambiarReglasDeReservaHandler.cs#L22) — cambia, mapea el rechazo al campo y deja bitácora.
- [AgendaDeAtencionSembrada.cs:14](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/AgendaDeAtencionSembrada.cs#L14) — los valores iniciales.
### [x] A7. Un caso de uso de punta a punta: agendar en línea — revisado (sin cambios)

La página Tus datos comprueba la verificación anti-bots y llama al handler (envuelto por los decoradores de validación).
El handler orquesta y no decide: revisa si la solicitud ya tiene Cita (doble envío), pide el Paciente a Pacientes
(que confirma en su propia transacción), abre la transacción de Agenda, toma el candado de la fecha, lee reglas y
horario, cuenta las Citas de la semana del teléfono, deja decidir a `Cita.Agendar`, guarda bajo la restricción de
exclusión y, en la misma transacción, escribe bitácora y evento en el outbox. Devuelve la confirmación con el enlace.

- [TusDatos.razor](../../../src/DraSelina.Consultorio.Web/UI/Pages/Reserva/TusDatos.razor) — la página que envía el comando.
- [AgendarCitaEnLineaHandler.cs:10](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs#L10) — el comentario que describe el flujo completo.
- [AgendarCitaEnLineaHandler.cs:43](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs#L43) — `HandleAsync`, de la solicitud repetida al commit.
- [AgendarCitaEnLineaHandler.cs:121](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs#L121) — guardar y los dos caminos de choque.
- [AgendarCitaEnLineaHandler.cs:173](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs#L173) — bitácora y outbox sin datos personales.
### [x] A8. Concurrencia: candados y la restricción de la base — revisado (sin cambios)

Tres mecanismos de PostgreSQL. La garantía de fondo es la restricción de exclusión GiST: dos Citas no canceladas no
pueden traslaparse, la escriba quien la escriba; el choque vuelve como `HorarioOcupado`, no como excepción. Encima, dos
candados consultivos de transacción ordenan a quienes leen y cambian el horario: uno por fecha local (reservas,
reprogramaciones y horario propio de ese día) y uno de toda la agenda de atención (cambios de semana tipo y de horario
propio). Para cambiar una Cita ya existente se bloquea su fila con `FOR UPDATE`.

- [20261102223335_CrearCitas.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/Migrations/20261102223335_CrearCitas.cs) — `EXCLUDE USING gist (tstzrange(inicio, fin, '[)') WITH &&) WHERE (estado <> 'Cancelada')`.
- [Citas.cs:50](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/Citas.cs#L50) — `GuardarAsync`: 23P01 → `HorarioOcupado`, 23505 → `SolicitudRepetida`; y el `FOR UPDATE` de las líneas 25 y 36.
- [CandadoDeFecha.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/CandadoDeFecha.cs) — `pg_advisory_xact_lock(clase, número de día)`.
- [CandadoDeLaAgendaDeAtencion.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/CandadoDeLaAgendaDeAtencion.cs) — una sola llave para toda la agenda.
- [GuardarAgendaCustomHandler.cs:57](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/GuardarAgendaCustomHandler.cs#L57) — el orden: fecha primero, agenda después.
### [x] A9. El Enlace de gestión — revisado (sin cambios)

El enlace `/c/<token>` es la única llave del Paciente para ver, reprogramar o cancelar su Cita, sin cuenta. El token
no se genera al azar ni se guarda: se deriva con `HMAC-SHA256(clave del host, CitaId)`, así sale igual cada vez que
hace falta (pantalla, confirmación, recordatorio, avisos). La Cita guarda solo el SHA-256 del token; al llegar un
enlace se calcula el hash y se busca la Cita por su índice único. Ni el token ni la clave aparecen en registros,
bitácora, eventos u outbox.

- [EnlaceDeGestion.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Domain/EnlaceDeGestion.cs) — `ClaveDeGestion`, `Derivar` y `HashDelToken`.
- [AgendaApi.cs:25](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/AgendaApi.cs#L25) — `ObtenerEnlaceDeGestion`: el único lugar que arma la dirección.
- [OpcionesDeEnlaceDeGestion.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/OpcionesDeEnlaceDeGestion.cs) — la clave sale de la configuración y se valida al arrancar.
- [CitaPorEnlaceHandler.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/CitaPorEnlaceHandler.cs) — de token a Cita.
### [x] A10. Persistencia: el contexto y los puertos pequeños — revisado (sin cambios)

Un `DbContext` por módulo, en su propio esquema `agenda`; los agregados se mapean tal cual (setters privados) y el
horario va en filas propias (`FilaDeTramoBase`, `FilaDeAgendaCustom`). Escritura por un puerto por agregado
(`ICitas`, `IAgendaAtencion`); cada pantalla lee por su puerto de lectura sin rastrear (`ICitasDelDia`,
`ICitasDeLaAgenda`…), que proyecta solo lo que muestra.

- [AgendaDbContext.cs:9](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/AgendaDbContext.cs#L9) — esquema, nombres de restricciones e índices.
- [ICitasDelDia.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/ICitasDelDia.cs) — un puerto de lectura típico.
### [x] A11. Hacia afuera: eventos, outbox e IAgendaApi — revisado (sin cambios)

Agenda publica cuatro eventos de integración en su propio outbox, en la misma transacción del cambio: `CitaAgendada`,
`CitaReprogramada`, `CitaCancelada` y `CambioDeAgendaBase`. Un servicio de la web los entrega a los handlers de otros
módulos, con reintentos. Las preguntas síncronas van por `IAgendaApi`.

- [CitaAgendada.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/CitaAgendada.cs) — un evento: ids e instantes, sin datos personales.
- [OutboxDeAgenda.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/OutboxDeAgenda.cs) — escribe la fila del evento.
- [AgendaApi.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/AgendaApi.cs) — la implementación del contrato.
### [x] A12. Las pruebas de Agenda — revisado (sin cambios)

Dos niveles. Dominio: unas 164 pruebas sin base, en milisegundos (Cita, AgendaAtencion, calculadora, enlace, reglas).
Integración: unas 27 clases contra PostgreSQL real con Testcontainers, incluidas las de concurrencia con envíos en
paralelo (`Task.WhenAll`).

- [CitaTests.cs](../../../tests/DraSelina.Consultorio.Domain.Tests/CitaTests.cs) — reglas de la Cita.
- [CalculadoraDeDisponibilidadTests.cs](../../../tests/DraSelina.Consultorio.Domain.Tests/CalculadoraDeDisponibilidadTests.cs) — horarios libres.
- [ReservaEnLineaTests.cs](../../../tests/DraSelina.Consultorio.IntegrationTests/ReservaEnLineaTests.cs) — el flujo de A7 contra la base, con concurrencia.
### [x] A13. Periferia: migraciones, siembras y opciones — revisado (sin cambios)

- [Migrations/](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/Migrations) — 8 migraciones; la restricción de exclusión en SQL explícito.
- [AgendaDeAtencionSembrada.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/AgendaDeAtencionSembrada.cs) y [TiposDeVisitaSembrados.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/TiposDeVisitaSembrados.cs) — datos iniciales.
- [OpcionesDeEnlaceDeGestion.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/OpcionesDeEnlaceDeGestion.cs) — la clave y la URL base, validadas al arrancar.

---

## Parte B — El camino de un comando

### [x] B1. Intención — revisado (sin cambios)

Todo caso de uso, de cualquier módulo, viaja igual: la página pide un `ICommandHandler`/`IQueryHandler`, el contenedor
le entrega el handler envuelto en decoradores (turno, rechazo sin Administrador, validación de forma) y el resultado
siempre es un `Resultado`, nunca una excepción esperada. Lo que cambia algo deja un evento en el outbox de su módulo;
un servicio de la web lo entrega después a los handlers de otros módulos, con reintentos.

### [x] B2. Grandes trazos — revisado (sin cambios)

1. [ICommandHandler.cs](../../../src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/ICommandHandler.cs) — el contrato de todo caso de uso.
2. [Resultado.cs](../../../src/DraSelina.Consultorio.Web/Kernel/Resultado.cs) — éxito, inválido (por campo), no autorizado o fallo con código.
3. [DecoradoresDeHandlers.cs:17](../../../src/DraSelina.Consultorio.Web/DecoradoresDeHandlers.cs#L17) — los decoradores, en orden, en un solo lugar.
4. [ActorDelHost.cs](../../../src/DraSelina.Consultorio.Web/ActorDelHost.cs) — quién actúa: Paciente, Administrador o Sistema.
5. [EntregaDeEventos.cs:35](../../../src/DraSelina.Consultorio.Web/EntregaDeEventos.cs#L35) — la vuelta que entrega los eventos de todos los outbox.

### [x] B3. Resultado: cuatro formas de terminar — revisado (sin cambios)

Todo handler devuelve `Resultado<T>` con una de cuatro formas: Éxito (con su valor), Inválido (errores por propiedad
del comando), NoAutorizado o Fallo (con un código de un conjunto cerrado por módulo). El dominio devuelve códigos; los
textos viven en la página que los muestra.

- [Resultado.cs](../../../src/DraSelina.Consultorio.Web/Kernel/Resultado.cs) — las formas y sus fábricas.
- [AgendarCitaEnLinea.cs:102](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/AgendarCitaEnLinea.cs#L102) — `CodigosDeAgenda`.
- [TusDatos.razor:310](../../../src/DraSelina.Consultorio.Web/UI/Pages/Reserva/TusDatos.razor#L310) — una página que reacciona a cada forma.
### [x] B4. Los decoradores e IRequiereAdministrador — revisado (decisión abierta en deferred-work)

Al construir el contenedor, `DecoradoresDeHandlers` envuelve todo handler registrado (con Scrutor). Una llamada pasa
por: turno del ámbito → rechazo sin Administrador (si el comando o la consulta lleva la marca `IRequiereAdministrador`)
→ validación de forma (solo comandos, DataAnnotations) → handler. Es la segunda capa: las páginas de administración
ya llevan `[Authorize]`, vigilado por una prueba de arquitectura. Hoy 23 comandos y consultas llevan la marca y 10 no.

- [DecoradoresDeHandlers.cs:17](../../../src/DraSelina.Consultorio.Web/DecoradoresDeHandlers.cs#L17) — el orden de los decoradores.
- [RechazoSinAdministrador.cs](../../../src/DraSelina.Consultorio.Web/RechazoSinAdministrador.cs) — lee la marca y pide el actor.
- [ValidacionDeComandos.cs](../../../src/DraSelina.Consultorio.Web/ValidacionDeComandos.cs) — DataAnnotations → `Resultado` inválido por propiedad.
- [IRequiereAdministrador.cs](../../../src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/IRequiereAdministrador.cs) — la marca.
- [TurnoDelAmbito.cs](../../../src/DraSelina.Consultorio.Web/TurnoDelAmbito.cs) — un handler a la vez por circuito.
### [x] B5. Quién actúa: IActorActual — revisado (sin cambios)

Un solo servicio por ámbito dice quién actúa: Paciente, Administrador o Sistema. Administrador = autenticado con la
cookie completa (después del segundo factor) y en el área de administración: una petición bajo `/admin` o el circuito
de Interactive Server, que solo abre la administración. Sistema no se deduce: lo declara el host para los trabajos en
segundo plano, y un handler no puede declararse Sistema.

- [Actor.cs](../../../src/DraSelina.Consultorio.Web/Kernel/Actor.cs) e [IActorActual.cs](../../../src/DraSelina.Consultorio.Web/Kernel/IActorActual.cs) — lo que ven los módulos.
- [ActorDelHost.cs](../../../src/DraSelina.Consultorio.Web/ActorDelHost.cs) — la implementación: `ObtenerAsync`, `EnElAreaDeAdministracion`, `DeclararSistema`.
### [x] B6. Outbox, entrega y reintentos — revisado (sin cambios)

Cada módulo tiene su tabla `outbox` en su esquema. Un servicio de la web da una vuelta cada 5 s: por cada outbox
aparta el evento pendiente más antiguo con `FOR UPDATE SKIP LOCKED`, respetando el orden por clave (la Cita), llama a
todos sus handlers (cada uno en su ámbito, con 30 s de límite) y lo marca Entregado, o programa el siguiente intento
(1 min, 5 min, 15 min, 1 h, 4 h, 12 h) hasta quedar Fallido con un error en el registro. Entrega al menos una vez: un
reintento vuelve a llamar a todos los handlers, así que cada uno debe ser idempotente.

- [Outbox.cs](../../../src/DraSelina.Consultorio.Web/Outbox.cs) — alta de outbox por módulo y el catálogo de eventos por nombre de tipo.
- [EntregaDeEventos.cs:63](../../../src/DraSelina.Consultorio.Web/EntregaDeEventos.cs#L63) — apartar, entregar, marcar.
- [EntregaDeEventos.cs:129](../../../src/DraSelina.Consultorio.Web/EntregaDeEventos.cs#L129) — la consulta con orden por clave y `SKIP LOCKED`.
- [CalendarioDeReintentos.cs](../../../src/DraSelina.Consultorio.Web/CalendarioDeReintentos.cs) — las esperas.
- [ServicioDeEntregaDeEventos.cs](../../../src/DraSelina.Consultorio.Web/ServicioDeEntregaDeEventos.cs) — la vuelta cada 5 s.
### [x] B7. Periferia — revisado (sin cambios)

- [ICommand.cs](../../../src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/ICommand.cs), [IQuery.cs](../../../src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/IQuery.cs), [IQueryHandler.cs](../../../src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/IQueryHandler.cs), [IEventHandler.cs](../../../src/DraSelina.Consultorio.Web/BuildingBlocks/Contracts/IEventHandler.cs) — los contratos base.
- [CitaId.cs](../../../src/DraSelina.Consultorio.Web/Kernel/CitaId.cs), [PatientId.cs](../../../src/DraSelina.Consultorio.Web/Kernel/PatientId.cs), [TipoDeVisitaId.cs](../../../src/DraSelina.Consultorio.Web/Kernel/TipoDeVisitaId.cs) — ids tipados sobre Guid v7.
- [IClock.cs](../../../src/DraSelina.Consultorio.Web/Kernel/IClock.cs) y [RelojDelSistema.cs](../../../src/DraSelina.Consultorio.Web/RelojDelSistema.cs) — el reloj inyectable.
- [ZonaDelConsultorio.cs](../../../src/DraSelina.Consultorio.Web/Kernel/ZonaDelConsultorio.cs) — America/Mexico_City.
- [TurnoDelAmbito.cs](../../../src/DraSelina.Consultorio.Web/TurnoDelAmbito.cs) — el turno que usa el decorador.
- [Program.cs:88](../../../src/DraSelina.Consultorio.Web/Program.cs#L88) — donde se enchufan los decoradores.

---

## Parte C — Pacientes

### [x] C1. Intención y grandes trazos — revisado (sin cambios)

Pacientes es dueño de quién es cada Paciente y cómo contactarlo: nombre, celular en E.164 y correo opcional. No hay
cuentas: un Paciente se reconoce por su celular más su nombre normalizado (sin distinguir mayúsculas ni acentos), y si
no existe se registra en el momento de reservar. Los demás módulos solo lo ven por `IPacientesApi`, y ningún dato
personal sale a registros (`ToString` oculto).

1. [IPacientesApi.cs](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Contracts/IPacientesApi.cs) — lo que ofrece: buscar o crear, contactos, teléfono compartido.
2. [Paciente.cs](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Domain/Paciente.cs) — el agregado, versión simple.
3. [PacientesApi.cs:21](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Features/PacientesApi.cs#L21) — `BuscarOCrearAsync`, con su propia transacción y reintento.
4. [PacientesDbContext.cs](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Infrastructure/PacientesDbContext.cs) — esquema `pacientes` y la collation `es_ci_ai`.

### [x] C2. El Paciente y sus objetos de valor — revisado (sin cambios)

Tres objetos de valor que no existen inválidos (`Crear` devuelve nulo si el texto no sirve): `Telefono` (E.164 de
México), `NombreCompleto` (original, normalizado y forma de búsqueda sin acentos, hasta 120) y `CorreoElectronico`
(hasta 254). El agregado `Paciente` solo registra y actualiza el correo.

- [Telefono.cs](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Domain/Telefono.cs), [NombreCompleto.cs](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Domain/NombreCompleto.cs), [CorreoElectronico.cs](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Domain/CorreoElectronico.cs).
### [x] C3. Reconocer o registrar: la identidad del Paciente — revisado (sin cambios)

Un Paciente es el par (celular en E.164, nombre normalizado). La igualdad la decide la base: un índice único sobre ese
par con la collation ICU `es_ci_ai`, que no distingue mayúsculas ni acentos. `BuscarOCrearAsync` valida los tres
datos, busca en su propia transacción, registra si no existe (o actualiza el correo si llega uno), y si dos reservas
del mismo Paciente nuevo chocan en el índice, deshace y vuelve a buscar una vez.

- [PacientesApi.cs:21](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Features/PacientesApi.cs#L21) — `BuscarOCrearAsync` y su reintento.
- [NombreCompleto.cs](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Domain/NombreCompleto.cs) — original y normalizado (espacios de más fuera).
- [Paciente.cs](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Domain/Paciente.cs) — `Registrar` y `ActualizarCorreo`; no decide la igualdad.
- Migración de Pacientes — `CREATE UNIQUE INDEX ux_paciente_identidad ON pacientes.paciente (telefono, nombre_normalizado)`.
### [x] C4. Lo que expone: contactos, teléfono compartido y búsqueda — revisado (sin cambios)

`IPacientesApi` da a los demás módulos lo mínimo: buscar o crear, los ids que comparten un celular (tope semanal),
si tiene correo, nombre y contactos para los correos. La búsqueda del Administrador (`BuscarPacientes`, marcada
`IRequiereAdministrador`) es "contiene" sobre el nombre sin acentos, alfabética, hasta 50 con aviso de que hay más.
Los registros que cruzan el contrato ocultan sus datos en `ToString`.

- [IPacientesApi.cs](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Contracts/IPacientesApi.cs) — el contrato y `Contactos` con `ToString` oculto.
- [BuscarPacientes.cs](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Contracts/BuscarPacientes.cs) — la consulta del Administrador, con `PrintMembers` oculto.
- [BusquedaDePacientes.cs](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Infrastructure/BusquedaDePacientes.cs) — `LIKE` sobre `nombre_busqueda`, sin rastrear.
### [x] C5. Periferia — revisado (sin cambios)

- [Registro.cs](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Infrastructure/Registro.cs), [TransaccionDePacientes.cs](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Infrastructure/TransaccionDePacientes.cs), migraciones de Pacientes.

---

## Parte D — Notificaciones

### [ ] D1. Notificaciones de punta a punta — en curso

Sin dominio: casos de uso e infraestructura. Escucha los eventos de Agenda y manda confirmación, avisos de
reprogramación y cancelación y alerta de cancelación tardía; programa el Recordatorio y la Agenda del día, que un
servicio envía cada minuto. Cada correo sale una sola vez por evento: `EnvioUnico` registra el mensaje antes de enviar,
bajo un índice único, en la transacción del módulo. Un solo canal, SMTP con MailKit (Mailpit en local), sin
autenticación todavía.

- [ConfirmacionAlAgendar.cs](../../../src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/ConfirmacionAlAgendar.cs) — un handler típico.
- [EnvioUnico.cs](../../../src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/EnvioUnico.cs) — una vez por evento.
- [EnviosProgramados.cs](../../../src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/EnviosProgramados.cs) — Recordatorios y Agenda del día.
- [CanalDeCorreoSmtp.cs](../../../src/DraSelina.Consultorio.Web/Modules/Notificaciones/Infrastructure/CanalDeCorreoSmtp.cs) — el canal.
- [PlantillaDeConfirmacion.cs](../../../src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/PlantillaDeConfirmacion.cs) — una plantilla (texto y HTML).

## Parte E — Contenido

### [ ] E1. Contenido — en curso

Lo que dice el sitio y no cambia en tiempo de ejecución: datos del consultorio y de la doctora, padecimientos, aviso
de privacidad con su versión y WhatsApp. Sin base ni dominio: una API de solo lectura.

- [IContenidoApi.cs](../../../src/DraSelina.Consultorio.Web/Modules/Contenido/Contracts/IContenidoApi.cs) y [ContenidoApi.cs](../../../src/DraSelina.Consultorio.Web/Modules/Contenido/Contenido/ContenidoApi.cs).

## Parte F — Interfaz e Identity

### [ ] F1. Interfaz e Identity — en curso

Blazor Web App. Lo del Paciente (sitio, reserva, `/c/…`) es SSR estático, sin circuito; la administración es
Interactive Server. La interfaz solo ve Contracts y pinta textos a partir de códigos. Identity en el host: una sola
cuenta sembrada, sin registro público, contraseña más TOTP obligatorio, bloqueo de Identity por omisión, cookie
HttpOnly y SameSite Strict. `/c/…` y `/admin` salen con `no-store` y `noindex`; la reserva y `/c/…` con límite de
frecuencia por IP.

- [Program.cs:95](../../../src/DraSelina.Consultorio.Web/Program.cs#L95) — el pipeline: HTTPS, cabeceras, límite, autenticación, antiforgery.
- [Identidad/Registro.cs:45](../../../src/DraSelina.Consultorio.Web/Identidad/Registro.cs#L45) — Identity y la cookie.
- [Identidad/Paginas/](../../../src/DraSelina.Consultorio.Web/Identidad/Paginas) — entrar, segundo factor, activarlo, código de recuperación.
- [UI/Pages/Administracion/Agenda.razor](../../../src/DraSelina.Consultorio.Web/UI/Pages/Administracion/Agenda.razor) — una pantalla interactiva.
- [UI/Pages/Reserva/](../../../src/DraSelina.Consultorio.Web/UI/Pages/Reserva) — el flujo de reserva en SSR.

## Parte G — Hosting

### [ ] G1. AppHost, MigrationService y ServiceDefaults — en curso

El AppHost levanta PostgreSQL (con pgAdmin), Mailpit, el MigrationService y la web, en ese orden de dependencias. El
MigrationService migra los cuatro esquemas (agenda, pacientes, notificaciones, identidad), siembra al Administrador y
termina; la web arranca solo si terminó bien. ServiceDefaults pone OpenTelemetry, resiliencia y los chequeos de salud,
que hoy solo se publican en Development.

- [AppHost.cs](../../../src/DraSelina.Consultorio.AppHost/AppHost.cs) — recursos y orden.
- [MigrationService/Program.cs:69](../../../src/DraSelina.Consultorio.MigrationService/Program.cs#L69) — migrar y sembrar.
- [ServiceDefaults/Extensions.cs:137](../../../src/DraSelina.Consultorio.ServiceDefaults/Extensions.cs#L137) — `/health` y `/alive` solo en Development.

## Parte H — Pruebas

### [ ] H1. Las pruebas de la solución — en curso

1,176 pruebas en seis proyectos: arquitectura 28, dominio 257, Kernel 18, interfaz con bUnit 177, integración contra
PostgreSQL real 693 y recorridos con Playwright 3 (solo local). GitHub Actions corre todas menos los recorridos.

- [ArchitectureTests](../../../tests/DraSelina.Consultorio.ArchitectureTests) — las fronteras (A3).
- [Domain.Tests](../../../tests/DraSelina.Consultorio.Domain.Tests) — reglas sin base.
- [UI.Tests](../../../tests/DraSelina.Consultorio.UI.Tests) — componentes con bUnit.
- [IntegrationTests/Arnes.cs](../../../tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs) — el arnés con Testcontainers.
- [E2ETests](../../../tests/DraSelina.Consultorio.E2ETests) — los recorridos.
