# Recorrido guiado: la aplicación del consultorio

Objetivo de Gerardo: entender el sistema, su arquitectura y la calidad del código, módulo por módulo, de forma muy
estructurada. Se empieza por Agenda. Cada bloque queda revisado solo cuando Gerardo lo dice.

**Bloque actual:** Parte A cerrada; sigue la Parte B

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

## Partes siguientes (se detallan al llegar)

- B — El camino de un comando en la web (Kernel, Resultado, decoradores, outbox y entrega de eventos)
- C — Pacientes
- D — Notificaciones
- E — Contenido
- F — Interfaz pública y de administración, Identity
- G — Hosting: AppHost, MigrationService, ServiceDefaults
- H — Pruebas de toda la solución
