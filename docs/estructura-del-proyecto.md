# Estructura del proyecto draselina

Estado al 2026-10-18, en `main` (commit `8e51f39`), al cerrar la épica 1, **Plataforma base**.

## 1. Resumen

| | |
|---|---|
| Qué es | El sistema de citas del consultorio de la Dra. Selina Medina Castillo (Traumatología y Ortopedia) |
| Forma | Monolito modular: una sola web (`DraSelina.Consultorio.Web`) con los módulos separados por namespace |
| Stack | .NET 10 (SDK 10.0.402 del sistema), Blazor (SSR estático), Aspire 13.6.0, EF Core 10, PostgreSQL 18, Scrutor 6.1 |
| Pruebas | xunit.v3, Testcontainers, `WebApplicationFactory`, bUnit. 128 pruebas en 4 proyectos |
| Lo que se ve hoy | Solo la página Inicio. La reserva, la administración y el login todavía no existen |

## 2. Árbol de carpetas

Solo lo que está en git. No se muestran `bin/`, `obj/` ni `TestResults/`.

```
draselina/
├── README.md                         Cómo arrancar, probar y escribir un caso de uso
├── DraSelina.Consultorio.slnx        La solución: 4 proyectos en src/ y 4 en tests/
├── Directory.Build.props             Propiedades comunes de compilación (gestión central de paquetes)
├── Directory.Packages.props          Las versiones de todos los paquetes, en un solo lugar
├── global.json                       Fija el SDK 10.0.402 y Microsoft.Testing.Platform
├── dotnet.sh                         Llama al SDK local: usar ./dotnet.sh en vez de dotnet
├── dotnet-tools.json                 Herramientas locales de dotnet (dotnet-ef)
├── skills-lock.json                  Versiones de las skills de BMad instaladas
│
├── src/
│   ├── DraSelina.Consultorio.AppHost/            Aspire: PostgreSQL → migraciones → web
│   │   ├── AppHost.cs
│   │   └── Properties/launchSettings.json        Panel de Aspire en el puerto 15080
│   │
│   ├── DraSelina.Consultorio.MigrationService/   Migra los esquemas de los módulos y termina (código 0 o 1)
│   │   └── Program.cs
│   │
│   ├── DraSelina.Consultorio.ServiceDefaults/    Telemetría (OpenTelemetry), salud y resiliencia
│   │   └── Extensions.cs
│   │
│   └── DraSelina.Consultorio.Web/                La aplicación (ver sección 3)
│       ├── Program.cs                            Composición: módulos, outbox, actor, decoradores, Blazor
│       ├── appsettings.json
│       ├── Properties/launchSettings.json        Web en http://localhost:5080
│       │
│       ├── ActorDelHost.cs                       Quién actúa: Administrador, Paciente o Sistema (AD-22)
│       ├── BaseDeDatos.cs                        Nombre "consultorio" y lectura de la cadena de conexión
│       ├── DecoradoresDeHandlers.cs              Aplica los decoradores con Scrutor al construir el contenedor
│       ├── ValidacionDeComandos.cs               Decorador: DataAnnotations → Resultado inválido (AD-12)
│       ├── RechazoSinAdministrador.cs            Decorador: lo marcado IRequiereAdministrador sin sesión → NoAutorizado
│       ├── Outbox.cs                             Alta de la entrega de eventos y de cada outbox
│       ├── EntregaDeEventos.cs                   Una vuelta: aparta, entrega y marca cada evento pendiente (AD-3)
│       ├── ServicioDeEntregaDeEventos.cs         BackgroundService que repite la vuelta cada 5 s
│       ├── CalendarioDeReintentos.cs             Esperas entre reintentos; 7 intentos y queda Fallido
│       ├── RelojDelSistema.cs                    IClock de producción
│       │
│       ├── Kernel/                               Tipos compartidos por todos. Solo la biblioteca base de .NET
│       │   ├── Actor.cs                          enum Paciente | Administrador | Sistema
│       │   ├── IActorActual.cs                   El puerto que piden los handlers para saber quién actúa
│       │   ├── IClock.cs                         El reloj inyectado (AD-17)
│       │   ├── Resultado.cs                      Resultado / Resultado<T>: Exito, Invalido, NoAutorizado, Fallo
│       │   ├── CitaId.cs                         Ids tipados, Guid v7 (AD-18)
│       │   ├── PatientId.cs
│       │   └── TipoDeVisitaId.cs
│       │
│       ├── BuildingBlocks/Contracts/             Las interfaces de los casos de uso y los eventos
│       │   ├── ICommand.cs                       ICommand<T>
│       │   ├── ICommandHandler.cs                HandleAsync(comando) → Resultado<T>
│       │   ├── IQuery.cs                         IQuery<T>
│       │   ├── IQueryHandler.cs                  HandleAsync(consulta) → Resultado<T>
│       │   ├── IEventHandler.cs                  Handler de un evento de integración
│       │   └── IRequiereAdministrador.cs         Marcador de "solo administración"
│       │
│       ├── Modules/
│       │   ├── Agenda/                           Esquema agenda. El único que hoy publica eventos
│       │   │   ├── Contracts/                    (vacío: .gitkeep)
│       │   │   ├── Domain/                       (vacío: .gitkeep)
│       │   │   ├── Features/
│       │   │   │   ├── ITransaccionDeAgenda.cs   Puerto: el handler abre, confirma o deshace su transacción
│       │   │   │   └── IOutboxDeAgenda.cs        Puerto: publicar un evento dentro de esa transacción
│       │   │   └── Infrastructure/
│       │   │       ├── AgendaDbContext.cs
│       │   │       ├── AgendaDbContextFactory.cs Para dotnet-ef en tiempo de diseño
│       │   │       ├── TransaccionDeAgenda.cs
│       │   │       ├── OutboxDeAgenda.cs
│       │   │       ├── FilaDeOutbox.cs           Fila de agenda.outbox
│       │   │       ├── Registro.cs               AddAgenda(): contexto, puertos y handlers
│       │   │       └── Migrations/               Inicial (2026-10-05) y CrearOutbox (2026-10-13)
│       │   │
│       │   ├── Pacientes/                        Esquema pacientes
│       │   │   ├── Contracts/                    (vacío)
│       │   │   ├── Domain/                       (vacío)
│       │   │   ├── Features/ITransaccionDePacientes.cs
│       │   │   └── Infrastructure/               DbContext, Factory, Transaccion, Registro, Migrations/ (Inicial)
│       │   │
│       │   ├── Notificaciones/                   Esquema notificaciones. Sin Domain (no tiene reglas propias)
│       │   │   ├── Contracts/                    (vacío)
│       │   │   ├── Features/ITransaccionDeNotificaciones.cs
│       │   │   └── Infrastructure/               DbContext, Factory, Transaccion, Registro, Migrations/ (Inicial)
│       │   │
│       │   └── Contenido/                        Textos del sitio. Sin base de datos
│       │       ├── Contracts/                    (vacío)
│       │       └── Contenido/                    (vacío)
│       │
│       ├── UI/                                   Blazor
│       │   ├── App.razor                         Documento HTML, hojas de estilo, <Routes>
│       │   ├── Routes.razor                      Router con LayoutPublico por defecto
│       │   ├── _Imports.razor
│       │   ├── Layout/LayoutPublico.razor        Encabezado y pie del sitio público
│       │   ├── Pages/Inicio.razor                "/": nombre, especialidad y botón "Agendar una cita" (sin destino)
│       │   └── Componentes/
│       │       ├── ErroresDelResultado.razor     Lleva los errores de un Resultado al formulario
│       │       └── Campo.razor                   Etiqueta + InputText + ayuda + errores accesibles
│       │
│       └── wwwroot/
│           ├── css/consultorio.css               Tokens --dsm-* y estilos propios
│           ├── fonts/montserrat/                 Montserrat 400/500/600/700 (woff2) + licencia OFL
│           └── lib/bootstrap/                    bootstrap.min.css + LICENSE
│
├── tests/
│   ├── DraSelina.Consultorio.ArchitectureTests/  Sin Docker
│   │   ├── Reglas.cs                             Las tablas de capas y proyectos de la arquitectura, como datos
│   │   ├── Lector.cs                             Lee los using de cada .cs y las referencias de cada .csproj
│   │   ├── Verificador.cs                        Compara lo leído contra las reglas
│   │   ├── VerificadorTests.cs                   Prueba al verificador con casos inventados
│   │   └── SolucionEntregadaTests.cs             Aplica las reglas a la solución real
│   │
│   ├── DraSelina.Consultorio.Kernel.Tests/       Sin Docker
│   │   ├── ResultadoTests.cs
│   │   ├── IdsTests.cs
│   │   └── ActorTests.cs
│   │
│   ├── DraSelina.Consultorio.UI.Tests/           Sin Docker (bUnit)
│   │   ├── ErroresEnElFormularioTests.cs         11 pruebas de ErroresDelResultado y Campo
│   │   ├── FormularioDePrueba.razor
│   │   ├── ComandoDePrueba.cs
│   │   └── _Imports.razor
│   │
│   └── DraSelina.Consultorio.IntegrationTests/   Necesita Docker (PostgreSQL real con Testcontainers)
│       ├── Arnes.cs                              Un contenedor por corrida; arranca la web real
│       ├── MigrationService.cs                   Corre el MigrationService contra la base de prueba
│       ├── BaseMigradaTests.cs                   Los esquemas quedan migrados
│       ├── MigracionFallidaTests.cs              Si falla una migración: código 1
│       ├── WebNoMigraTests.cs                    La web sobre una base vacía arranca y no crea nada (AD-25)
│       ├── EsquemaDePruebasTests.cs
│       ├── CasoDeUsoTests.cs                     Un comando confirma; un fallo vuelve como Resultado
│       ├── UnHandlerPorCasoDeUsoTests.cs         Exactamente un handler por comando o consulta
│       ├── TransaccionDeModuloTests.cs
│       ├── OutboxTests.cs                        El evento y el cambio se confirman juntos o ninguno
│       ├── EntregaDeEventosTests.cs              Llega después de confirmar; sobrevive a un reinicio
│       ├── ReintentosYOrdenTests.cs              Reintentos y orden por clave
│       ├── ValidacionDeFormaTests.cs             DataAnnotations → Resultado inválido
│       ├── MensajesDeValidacionTests.cs
│       ├── ActorYAdministracionTests.cs          Actor y rechazo sin sesión
│       ├── BaseVisualTests.cs                    La página Inicio y sus recursos estáticos
│       ├── CasosDeUso.cs, Consultas.cs, EscenarioDeEntrega.cs
│       └── Pruebas/                              Un módulo de mentira en el esquema "pruebas"
│           ├── PruebasDbContext.cs, TransaccionDePruebas.cs, OutboxDePruebas.cs, Registro.cs
│           ├── Anotar.cs, LeerAnotacion.cs, Inscribir.cs, Administrar.cs   Comandos y consultas de prueba
│           ├── Anotacion.cs, Anotaciones.cs, Apunte.cs, Eventos.cs
│           └── RegistrosDePrueba.cs, RelojDePrueba.cs
│
├── scripts/
│   └── verificar-arranque.sh                     Con el AppHost arriba: comprueba que todo levantó
│
├── docs/
│   ├── Appointment Booking System — Solution Plan.md   El plan de la solución vigente
│   ├── archive/                                  El plan original del 2026-10-03
│   ├── assets/                                   Logo, hoja membretada, receta, tarjeta, carnet (PDF/PNG/SVG)
│   └── estructura-del-proyecto.md                Este documento
│
├── _bmad-output/initiative-draselina/            Los documentos del producto (sección 6)
├── _bmad/                                        Configuración y scripts de BMad (memlog.py, tickets, etc.)
├── .agents/skills/                               Las skills de BMad
└── .claude/                                      Configuración de Claude Code y sus skills
```

## 3. Cómo está organizada la web

### Capas y quién puede usar a quién

Esto lo hacen cumplir las pruebas de arquitectura: leen los `using` de cada archivo y fallan si una capa usa algo que no le toca.

| Capa | Carpeta | Puede usar |
|---|---|---|
| `Kernel` | `Kernel/` | Nada (solo .NET base) |
| `BuildingBlocks.Contracts` | `BuildingBlocks/Contracts/` | Kernel |
| `<Módulo>.Contracts` | `Modules/<M>/Contracts/` | Kernel, BuildingBlocks.Contracts |
| `<Módulo>.Domain` | `Modules/<M>/Domain/` | Kernel |
| `<Módulo>.Features` | `Modules/<M>/Features/` | Su Domain, los Contracts de cualquier módulo, Kernel, BuildingBlocks.Contracts |
| `<Módulo>.Infrastructure` | `Modules/<M>/Infrastructure/` | Lo suyo, más EF Core |
| `UI` | `UI/` | Los Contracts de los módulos, BuildingBlocks.Contracts, Kernel |
| Host (raíz de Web) | `*.cs` sueltos | Todo: es quien compone |

Kernel, los Contracts y los Domain son **puros**: no pueden usar paquetes, solo la biblioteca base de .NET.

### Proyectos y referencias

```
AppHost ──► Web, MigrationService
MigrationService ──► Web, ServiceDefaults
Web ──► ServiceDefaults
ServiceDefaults ──► (nada)
```

### Un caso de uso, de punta a punta

```
Página Blazor
   │  pide ICommandHandler<MiComando, T> al contenedor
   ▼
RechazoSinAdministrador   ¿marcado IRequiereAdministrador y no es Administrador? → NoAutorizado
   ▼
ValidacionDeComandos      ¿falla una DataAnnotation? → Invalido (errores por campo)
   ▼
El handler                abre ITransaccionDe<Módulo>, guarda, publica en IOutboxDe<Módulo>, confirma
   │                      → Resultado: Exito(valor) | Fallo("CODIGO")
   ▼
ErroresDelResultado/Campo muestran los errores en el formulario

… después de confirmar:
ServicioDeEntregaDeEventos → EntregaDeEventos → cada IEventHandler<TEvento>
(reintenta según CalendarioDeReintentos; si la web se cae, el evento sigue Pendiente)
```

### Base de datos

Es una sola base, `consultorio`, con un esquema por módulo:

| Esquema | Tablas hoy |
|---|---|
| `agenda` | `outbox` y el historial de migraciones |
| `pacientes` | historial de migraciones |
| `notificaciones` | historial de migraciones |

La web nunca migra ni crea nada: eso lo hace el `MigrationService`, que termina antes de que arranque la web (AD-25).

## 4. Cómo se corre

| Para | Comando |
|---|---|
| Arrancar todo (necesita Docker) | `./dotnet.sh run --project src/DraSelina.Consultorio.AppHost` |
| El sitio | http://localhost:5080/ |
| El panel de Aspire | http://localhost:15080 |
| Comprobar el arranque | `bash scripts/verificar-arranque.sh` (con el AppHost arriba) |
| Todas las pruebas | `./dotnet.sh test` |
| Detener el AppHost | Ctrl+C, o `pkill -INT -f 'DraSelina.Consultorio.AppHost'` |

## 5. Pruebas

| Proyecto | Métodos de prueba | Docker | Qué cubre |
|---|---|---|---|
| ArchitectureTests | 24 | No | Capas, pureza, referencias entre proyectos |
| Kernel.Tests | 17 | No | Resultado, ids, actor |
| UI.Tests | 11 | No | Errores en el formulario (bUnit) |
| IntegrationTests | 68 | Sí | Migraciones, casos de uso, transacción, outbox, entrega, validación, actor, página Inicio |

Son 120 métodos. Algunos son `[Theory]` y corren con varios datos, así que al ejecutarse dan **128 pruebas**, todas en verde, con 0 advertencias de compilación.

## 6. Documentos del producto (`_bmad-output/initiative-draselina/`)

```
initiative-draselina/
├── initiative-draselina.md            La iniciativa
├── tickets.toml                       Las 7 épicas, en orden
├── prd-appointment-booking/           PRD + addendum
├── spec-appointment-booking/          Spec + trazabilidad
├── architecture-appointment-booking/
│   ├── architecture-appointment-booking.md        Las decisiones AD-n (invariantes del diseño)
│   ├── ddd-and-data-model-appointment-booking.md  Dominio; la Parte I es invariante
│   ├── c4-appointment-booking.md
│   └── reviews/
├── ux-appointment-booking/            DESIGN.md, EXPERIENCE.md, mockups
├── brainstorm-agenda-horario-configuracion/
├── change-quitar-wolverine/           La propuesta que quitó Wolverine (2026-10-06)
├── epic-plataforma-base/              Épica 1: tickets.toml + un plan por entrada
├── epic-reserva-en-linea/
├── epic-gestion-por-el-paciente/
├── epic-notificaciones/
├── epic-administracion-de-citas/
├── epic-agenda-de-atencion-y-configuracion/
└── epic-sitio-del-consultorio/
```

### Las épicas

| # | Épica | Estado |
|---|---|---|
| 1 | La solución arranca y hace cumplir la arquitectura (plataforma base) | **Terminada** (11/11) |
| 2 | Un paciente reserva su cita en línea | Pendiente |
| 3 | El paciente ve, reprograma y cancela con su enlace | Pendiente |
| 4 | Los avisos salen solos y nunca frenan la reserva | Pendiente |
| 5 | El administrador opera las citas desde una sola pantalla | Pendiente |
| 6 | El administrador decide cuándo se atiende y con qué reglas | Pendiente |
| 7 | El sitio presenta el consultorio y lleva a agendar | Pendiente |

### Las entradas de la épica 1, en el orden en que se construyeron

| # | Entrada |
|---|---|
| 1 | ¿Arrancan juntos .NET 11, Wolverine 6 y el AppHost de Aspire? (prueba de arranque) |
| 2 | Esqueleto de la solución con pruebas de arquitectura |
| 3 | El AppHost levanta PostgreSQL, las migraciones y la web, con telemetría |
| 4 | Arnés de pruebas de integración contra PostgreSQL real |
| 5 | Un caso de uso recorre su handler, su transacción y Resultado |
| 11 | Outbox propio y entrega de eventos |
| 6 | La validación de forma llega como Resultado inválido |
| 7 | Actor único y rechazo de administración sin sesión |
| 8 | Base visual y layout público en SSR estático |
| 9 | Los errores de validación se muestran en el formulario |
| 10 | Barrido final (refactor sweep) |

### Preguntas abiertas

Están en las notas de la épica 1. Se deciden con la primera historia que las necesite:

1. Cuánto dura el contexto de EF Core en Interactive Server (administración).
2. Cómo obtiene un ámbito `Sistema` el trabajo programado de un módulo.
3. El actor en una Minimal API.
4. `/health` sin base de datos.
5. `Actor` sin id de usuario.
6. Un comando que no devuelve valor.
7. AD-23 (sin datos personales) en las trazas.

## 7. Lo que falta para que la web haga algo

- **Contracts y Domain de cada módulo:** están vacíos. La épica 2 crea los primeros: Cita, Paciente y TipoDeVisita.
- **Botón "Agendar una cita":** no lleva a ningún lado. La épica 2 lo enlaza con la reserva.
- **Interactive Server, login y administración:** no están configurados. Llegan con la épica 5.
- **Recordatorios y agenda del día:** no existen. Llegan con la épica 4.
