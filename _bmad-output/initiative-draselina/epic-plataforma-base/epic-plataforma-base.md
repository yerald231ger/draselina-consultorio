---
type: epic
title: "La solución arranca y hace cumplir la arquitectura"
parent: initiative-draselina
covers: []
after: []
assignee: ""
risk: medium
---

# La solución arranca y hace cumplir la arquitectura

## Description

La solución existe con sus proyectos, arranca en local con un solo comando y ya impide lo que la arquitectura prohíbe. Todo lo que las demás épicas comparten (despacho de casos de uso, validación, migraciones, actor, telemetría, base visual) tiene un solo dueño aquí.

## Outcome

Gerardo puede empezar cualquier historia de negocio sin decidir antes cómo se arma un caso de uso; la señal es que la primera historia de la épica 2 no agrega infraestructura.

## Requirements

- P1: Probar que .NET 11, Wolverine 6 y el AppHost de Aspire arrancan juntos; si no, .NET 10. (Arquitectura, Open Questions 1; spec, Constraints)
- P2: Confirmar que el fallo de validación DataAnnotations se entrega como `Resultado` inválido y que un `EditForm` se liga a un comando de `Contracts`. (Arquitectura, Open Questions 2 y 3)
- P3: Proyectos, namespaces y dependencias exactamente como las tablas, verificados con pruebas de arquitectura; `Kernel` con ids tipados, `IClock`, `Resultado` y `Actor`. (AD-1, AD-18, Structural Seed; dominio E-17, T-28)
- P4: El AppHost levanta web, `MigrationService` y PostgreSQL; un historial de migraciones por esquema; la web nunca migra al arrancar. (AD-2, AD-25)
- P5: Arnés de pruebas de integración contra PostgreSQL real. (Arquitectura, Consistency Conventions, Pruebas; Deferred)
- P6: Todo caso de uso es `ICommand<T>` o `IQuery<T>` con un handler (`ICommandHandler` o `IQueryHandler`) que la interfaz pide directamente y que devuelve `Resultado` con códigos cerrados; el handler de un comando abre y cierra su propia transacción. (AD-10, Consistency Conventions)
- P7: Validación de forma con DataAnnotations antes del handler, entregada como `Resultado` inválido por campo. (AD-12)
- P8: Un único servicio entrega el actor y un único decorador rechaza lo marcado `IRequiereAdministrador` sin usuario autenticado. (AD-22)
- P9: Telemetría solo por `Microsoft.Extensions.Logging` y OpenTelemetry mediante ServiceDefaults. (AD-21)
- P10: Base visual: tokens de `DESIGN.md` sobre Bootstrap y layout público móvil primero, en SSR estático. (AD-11; DESIGN.md)
- P11: Outbox propio: un módulo guarda sus eventos de integración en una tabla de su esquema, dentro de la transacción del cambio, y un servicio del host en segundo plano los entrega a sus handlers y reintenta los que fallan. (AD-3, AD-2)

## Done when

1. `aspire run` (o F5 en el AppHost) levanta PostgreSQL, aplica las migraciones y deja la aplicación web respondiendo, sobre el runtime que decidió la prueba de arranque.
2. Las pruebas de arquitectura fallan cuando se agrega una referencia que la tabla de AD-1 no permite.
3. Un comando de prueba, pedido por su `ICommandHandler`, se valida, cambia la base y deja su evento en el outbox en una sola transacción, y el evento llega después a su handler; con datos inválidos devuelve `Resultado` inválido con errores por campo, sin excepción.
4. Un comando marcado `IRequiereAdministrador` se rechaza sin usuario autenticado.
5. Una página pública se pinta en SSR estático con los tokens de `DESIGN.md` a 390 px.

## Boundaries

La plataforma: host, composición, proyectos base y arnés de pruebas. No contiene reglas de negocio, agregados ni pantallas de producto. No incluye ASP.NET Core Identity ni el inicio de sesión (épica 5), ni integración continua o despliegue (diferidos por la arquitectura).

## References

- parent — _bmad-output/initiative-draselina/initiative-draselina.md
- spec — _bmad-output/initiative-draselina/spec-appointment-booking/spec-appointment-booking.md, sección Capabilities
- trazabilidad — _bmad-output/initiative-draselina/spec-appointment-booking/trazabilidad.md
- architecture — _bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md, secciones Design Paradigm, Invariants & Rules, Stack, Structural Seed y Open Questions
- design — _bmad-output/initiative-draselina/ux-appointment-booking/DESIGN.md

## Notes

- Decision: "terminado" significa que corre en local con el AppHost y pasan sus pruebas de integración contra PostgreSQL real; no hay producción todavía (Gerardo, 2026-10-05).
- Decision: sin integración continua ni despliegue en esta épica; la arquitectura los difiere (sección Deferred) (Gerardo, 2026-10-05).
- Decision: esta épica no cubre capacidades; cada requisito cita su sección de la arquitectura (2026-10-05).
- Resuelto (2026-10-05): Wolverine 6 y el AppHost de Aspire arrancan sobre .NET 11; lo contestó la entrada 1 (ver las Decision de abajo).
- Resuelto (2026-10-06): Testcontainers o el soporte de pruebas de Aspire para el arnés de integración (arquitectura, Deferred); lo contestó la entrada 4 (ver la Decision de abajo).
- Decision: el arnés de integración usa Testcontainers (`Testcontainers.PostgreSql` 4.15.0), no el soporte de pruebas de Aspire. Razón: ya está en la tabla Stack y era lo que traía el plan; el soporte de Aspire arrancaría el AppHost entero con su orquestador en cada corrida, y la entrada 3 mostró que leer desde fuera el código de salida de `migraciones` exige `kubectl`. Con Testcontainers el mismo camino se conserva: el arnés ejecuta el `MigrationService` real como proceso contra el contenedor y lee su código de salida. Además, la entrada 5 pide un comando de prueba que vive solo en el proyecto de pruebas, y eso necesita la web en memoria dentro del proceso de pruebas; con el soporte de Aspire la web corre en otro proceso (Gerardo, 2026-10-06).
- Decision: los tipos que solo existen para probar guardan sus tablas en el esquema `pruebas`, que crea el arnés después de migrar; ningún esquema de producción recibe tablas de prueba. Hay una sola base por corrida, compartida por todas las pruebas de integración: cada prueba crea sus datos con valores propios y no depende del orden ni de lo que dejó otra (Gerardo, 2026-10-06).
- Decision: desglose en diez entradas aprobado (Gerardo, 2026-10-05). Bala trazadora: la entrada 1, el camino más delgado por AppHost, web, Wolverine, EF Core y PostgreSQL.
- Decision: secuencia 1 → 2 → 3 → 4 → 5 → 6 → 7; la 7 espera a la 6 porque ambas registran decoradores de los handlers en el host. La 8 solo espera a la 3 y puede ir en paralelo con 4 a 7, porque se queda en `Consultorio.UI`.
- Decision: el componente que muestra en el formulario los errores de un `Resultado` inválido se entrega aquí (entrada 9) y no en la épica 2, para que la primera historia de reserva no construya infraestructura (Gerardo, 2026-10-05).
- Decision: la entrada 7 no comprueba el actor Paciente por Enlace de gestión; ese enlace nace en la épica 3 (AD-19).
- Revisión independiente del desglose (2026-10-05): confirmó cobertura de P1 a P10 y del Done when; de ella salieron la entrada 4 separada, la entrada 9 y los traspasos nombrados en las entradas 2, 3 y 7.
- Reemplazada (2026-10-23, se pasa a .NET 10): la solución se crea sobre .NET 11. En la prueba de arranque (entrada 1) Wolverine 6.45.0, el AppHost de Aspire 13.6.0, EF Core 10.0.12 y Npgsql 10.0.3 arrancaron juntos y atendieron un comando de punta a punta sobre .NET 11.0.0-rc.1; no se retrocede a .NET 10 (Gerardo, 2026-10-05). Registro de la corrida: `spikes/arranque/REGISTRO.md`.
- Reemplazada (2026-10-06, se quita Wolverine): se agrega `WolverineFx.RuntimeCompilation` 6.45.x al Stack, referenciado por el host; sin él Wolverine 6 no arranca en modo dinámico. Queda abierto, para cuando haya despliegue, si producción usa código pregenerado (`TypeLoadMode.Static`) (Gerardo, 2026-10-05).
- Reemplazada (2026-10-06, se quita Wolverine): la validación de forma antes del handler la hace un middleware propio de Wolverine, aplicado a todo mensaje con el marcador de comando, que devuelve `Resultado` inválido con errores por nombre de propiedad y detiene la cadena. El middleware de DataAnnotations de Wolverine, con su configuración por defecto, lanza `ValidationException` y no entrega `Resultado`; su punto de extensión `IFailureAction<T>` no se probó. Cambia la redacción de AD-12 (Gerardo, 2026-10-05).
- Decision: un `EditForm` se liga directo al comando de `Contracts` con `[SupplyParameterFromForm]`; no hay modelos de formulario aparte. Todos los comandos se escriben como clase con propiedades `set`: es la única forma que admite `@bind-Value` y la que no imprime sus datos cuando Wolverine registra un fallo (AD-23). Eventos de integración y DTOs pueden ser `record` (Gerardo, 2026-10-05).
- Decision: los módulos, sus capas, el Kernel, los contratos base y la interfaz no son proyectos: son carpetas y namespaces dentro de `DraSelina.Consultorio.Web`. Quedan cuatro proyectos (`AppHost`, `ServiceDefaults`, `MigrationService`, `Web`) y sus carpetas llevan el nombre completo `DraSelina.Consultorio.*`. Un proyecto por capa no es mantenible para un solo desarrollador; separarlos se decide cuando haga falta (Gerardo, 2026-10-05).
- Reemplazada (2026-10-06, se quita Wolverine): la transacción de un comando es la del módulo donde vive su handler. El host lo fija una sola vez, por convención (un handler de `Agenda.Features` abre su transacción sobre `AgendaDbContext`), con una política propia de Wolverine; ningún handler ni comando lo declara. La política de fábrica (`AutoApplyTransactions`) no sirve: si las dependencias de un handler alcanzan dos `DbContext` (Agenda llamando a `IPacientesApi`), la web no arranca (Gerardo, 2026-10-06).
- Reemplazada (2026-10-06, se quita Wolverine): la política de transacción cubre comandos (`ICommand<T>`) y handlers de eventos; las consultas (`IQuery<T>`) corren sin transacción y nadie guarda por ellas (Gerardo, 2026-10-06).
- Reemplazada (2026-10-06, se quita Wolverine): contra una base que el `MigrationService` no ha migrado (sin el esquema `wolverine`), la web se niega a arrancar con un mensaje que dice qué falta, y no crea nada. La prueba de AD-25 pasa de "arranca y no crea nada" a "no arranca y no crea nada" (Gerardo, 2026-10-06).
- Reemplazada (2026-10-06, se quita Wolverine): el caso de uso es atómico visto desde quien lo llama, y quien llama nunca decide guardar. El handler guarda de forma explícita por su puerto (`SaveChanges` dentro del handler); el host nunca llama a `SaveChanges`. El host abre la transacción y al terminar confirma si el `Resultado` es exitoso, o deshace todo (cambio y mensajes) si no lo es o si hubo excepción; el llamador recibe el `Resultado` tal cual. Una Minimal API reutiliza el caso de uso enviando el mismo comando por `ISender` (Gerardo, 2026-10-06).
- Reemplazada (2026-10-06, se quita Wolverine): los mensajes publicados dentro de un handler van a colas locales durables de Wolverine: se guardan en el esquema `wolverine` en la misma transacción que el cambio y se procesan después en la misma web (Gerardo, 2026-10-06).
- Resuelto (2026-10-06): Wolverine se quita; ver las Decision de abajo. Sigue abierto, para el final, el uso de interceptores de EF Core para publicar eventos como `CitaAgendada` desde el agregado.
- Decision: se quita Wolverine de la solución. Razón: de lo que trae hecho, casi todo hubo que reemplazarlo o rodearlo (paquete extra para arrancar, validación que lanza excepción, política de transacción que no sirvió, código generado que no se ve en el repositorio). El reemplazo sigue el patrón de Cypma.Cop (Gerardo, 2026-10-06). Propuesta de cambio: `change-quitar-wolverine/`.
- Decision: no hay `ISender` ni mediador. La interfaz pide directamente el `ICommandHandler<TComando, T>` o `IQueryHandler<TConsulta, T>` del caso de uso; cada módulo registra los suyos (Gerardo, 2026-10-06).
- Decision: el handler de un comando abre la transacción de su módulo por un puerto, guarda de forma explícita y la confirma; toda salida con un `Resultado` no exitoso deshace antes de devolver. Quien llama nunca abre, confirma ni guarda. Las consultas corren sin transacción (Gerardo, 2026-10-06).
- Decision: lo transversal va en decoradores registrados con Scrutor, que entra al Stack: validación de forma (entrada 6) y rechazo de administración sin sesión (entrada 7) (Gerardo, 2026-10-06).
- Decision: outbox propio. Los eventos de integración se guardan en una tabla del esquema del módulo que publica (`agenda.outbox`), en la transacción del cambio; un servicio del host en segundo plano los entrega y reintenta (Gerardo, 2026-10-06).
- Decision: el Recordatorio y la Agenda del día son filas de Notificaciones más un servicio en segundo plano con temporizador; sin librería de tareas programadas (Gerardo, 2026-10-06).
- Decision: el `MigrationService` se queda, sin crear el esquema `wolverine`. Sobre una base sin migrar la web arranca y no crea nada: vuelve la prueba original de la entrada 4 (Gerardo, 2026-10-06).
- Decision: la entrada 5 construida sobre Wolverine (commit `b2e3c10`) se revierte con un commit de reversión y se rehace; su plan y su parche quedan como registro. La 5 se parte en dos (caso de uso, y outbox en la entrada 11) y la secuencia queda 5 → 11 → 6 → 7 (Gerardo, 2026-10-06).
- Decision: el método que implementa todo handler se llama `HandleAsync` (Gerardo, 2026-10-10).
- Decision: el puerto de transacción se queda repetido en cada módulo (`ITransaccionDe<Módulo>` e `ITransaccionAbiertaDe<Módulo>` en su `Features`, con su implementación en `Infrastructure`); no hay tipo compartido en el Kernel. Cada módulo queda autocontenido y la arquitectura no cambia (Gerardo, 2026-10-10).
- Decision: deshacer una transacción, o soltarla sin confirmar, también limpia lo que el contexto del módulo tenía en seguimiento: un comando que no se confirma no deja nada en la base ni en la memoria del contexto (Gerardo, 2026-10-10).
- Decision: que cada comando y cada consulta tenga exactamente un handler (AD-10) se hace cumplir con una prueba de integración que recorre los `ICommand<T>` e `IQuery<T>` de la aplicación contra el contenedor de la web real; no hay comprobación al arrancar. La prueba se construye en la entrada 6 (Gerardo, 2026-10-10).
- Open question: cuánto vive el contexto en las pantallas de administración (Interactive Server), donde el ámbito dura lo que el circuito y dos operaciones del mismo módulo pueden cruzarse sobre el mismo contexto. Se decide con la primera historia que tenga una pantalla Interactive Server; las salidas a la vista son un ámbito por página (`OwningComponentBase`) o un ámbito por operación. Lo construido en la entrada 5 sirve para cualquiera (Gerardo, 2026-10-10). Incluye el diferido de la revisión de la entrada 5: dos comandos del mismo módulo en curso a la vez en el mismo ámbito chocan (el segundo `AbrirAsync` lanza, y si el primero no confirma, su limpieza borra lo que el otro tenía en seguimiento); se cierra con esta decisión (entrada 10, 2026-10-16).
- Decision (entrada 11): un evento cuya entrega falla se reintenta con espera creciente y, al llegar al tope, queda marcado como fallido en la tabla y se registra un error; los demás eventos siguen fluyendo (Gerardo, 2026-10-10).
- Decision (entrada 11): el calendario es de 7 intentos, con esperas de 1 min, 5 min, 15 min, 1 h, 4 h y 12 h entre uno y otro (unas 17 horas en total). Los valores viven en un solo lugar del código (Gerardo, 2026-10-10).
- Decision (entrada 11): el servicio de entrega aparta los eventos pendientes con `FOR UPDATE SKIP LOCKED`, para que dos instancias de la web nunca entreguen el mismo evento a la vez (Gerardo, 2026-10-10).
- Decision (entrada 11): orden por clave. Un evento no se entrega mientras haya otro anterior con la misma clave de orden todavía pendiente; para Agenda la clave es el `CitaId`. Los eventos de otras claves siguen fluyendo, y si el anterior queda fallido los siguientes ya no esperan (Gerardo, 2026-10-10).
- Decision (entrada 11): `IEventHandler<TEvento>` vive en `BuildingBlocks.Contracts`, junto a `ICommandHandler` e `IQueryHandler`; está en la fila "Contratos base" de la arquitectura (Gerardo, 2026-10-14).
- Decision (entrada 11): el servicio de entrega da una vuelta al arrancar y una cada 5 segundos. Los eventos entregados se quedan en la tabla como historial; su limpieza queda pendiente, sin fecha (Gerardo, 2026-10-14).
- Decision (entrada 11): un evento sin ningún handler registrado queda `Entregado` y se registra un aviso (Gerardo, 2026-10-14).
- Decision (entrada 11): cada handler de evento tiene 30 segundos para terminar. Al vencerse se cancela su `CancellationToken`, la entrega cuenta como intento fallido según el calendario y la vuelta sigue con los demás eventos (Gerardo, 2026-10-14).
- Decision (entrada 11): la columna `tipo` del outbox guarda el nombre de la clase del evento. Los nombres de evento son únicos en toda la aplicación y no se renombra un evento con filas pendientes (Gerardo, 2026-10-14).
- Decision (entrada 6): un error de validación que no es de ningún campo (de `IValidatableObject`, sin miembros) va bajo la propiedad `""`, la convención de Blazor; un error sin mensaje lleva "El valor no es válido." (Gerardo, 2026-10-15).
- Decision (entrada 6): todo atributo de validación de un comando lleva su `ErrorMessage` en español; el decorador no traduce. Lo hace cumplir una prueba que recorre todos los comandos (Gerardo, 2026-10-15).
- Decision (entrada 6): `ComandoSinHandler`, de prueba y sin handler a propósito, queda fuera de la prueba de un handler por caso de uso en una lista explícita; en esa lista solo entran tipos de prueba (Gerardo, 2026-10-15).
- Decision (entrada 6): `ValidacionDeComandos.Siguiente` se queda pública aunque solo la usen las pruebas (Gerardo, 2026-10-15).
- Decision (entrada 6): Scrutor 6.1.0 corre sobre .NET 11 rc.1; la fila del Stack de la arquitectura deja su `[ASSUMPTION]` (Gerardo, 2026-10-15).
- Decision (entrada 7): el puerto del actor es `IActorActual` en el Kernel, junto a `IClock`, con `Task<Actor> ObtenerAsync()`; lo implementa el host (`ActorDelHost`) leyendo el `AuthenticationStateProvider` del ámbito (Gerardo, 2026-10-15).
- Decision (entrada 7): `Sistema` se declara, no se deduce: quien crea el ámbito, desde el host, llama a `ActorDelHost.DeclararSistema()` (Gerardo, 2026-10-15).
- Decision (entrada 7): un ámbito que no viene de una página ni de un circuito y no se declaró `Sistema` no tiene actor: pedirlo lanza `InvalidOperationException`; es un error de programación, no un `Resultado` (Gerardo, 2026-10-15).
- Decision (entrada 7): el rechazo de administración va por fuera de la validación (rechazo → validación → handler): sin sesión, un comando marcado devuelve `NoAutorizado` aunque sus datos sean inválidos (Gerardo, 2026-10-15).
- Decision (entrada 7): la advertencia BL0013 se suprime solo en la línea de `ActorDelHost` que pide el estado de autenticación; el estado no se guarda, se pide en cada llamada (Gerardo, 2026-10-15).
- Open question (entrada 7): cómo obtiene un ámbito `Sistema` el trabajo programado de un módulo (Recordatorio, Agenda del día, en Notificaciones), que no ve `ActorDelHost`. Se decide con el primer trabajo programado; las salidas a la vista son que el host cree el ámbito y llame al trabajo, o un puerto en el Kernel para abrir un ámbito `Sistema` (Gerardo, 2026-10-15).
- Decision (entrada 8): Bootstrap 5.3.8 y Montserrat (400, 500, 600, 700) se sirven desde el sitio (`wwwroot`), con sus licencias; ninguna visita pide nada a un CDN ni a Google Fonts (Gerardo, 2026-10-15).
- Decision (entrada 8): la entrada 8 agrega una sola línea al host, `app.MapStaticAssets();`, para servir esos archivos; la restricción "sin tocar la configuración del host" existía para correr en paralelo con la 4 a la 7, ya terminadas (Gerardo, 2026-10-15).
- Decision (entrada 8): la página pública de la entrada muestra el layout público (encabezado con nombre y especialidad en texto) y en Inicio el rótulo de la especialidad, el nombre en display, un párrafo y el botón principal "Agendar una cita" sin destino todavía (Gerardo, 2026-10-15).
- Decision (entrada 8): el encabezado va sin logotipo. Los SVG de `docs/assets/logo/` son el logotipo completo (420–500 KB, con el nombre en letra), no el monograma cuadrado del mockup; el monograma llega cuando Gerardo lo tenga (Gerardo, 2026-10-15).
- Decision (entrada 9): un campo con error lleva su borde de 2 px en `{colors.primary}` y el mensaje debajo en vino, tipografía `help` en negritas; `{colors.danger}` sigue reservado para cancelar. Se agregó a "Campo" en `DESIGN.md` (Gerardo, 2026-10-16).
- Decision (entrada 9): en SSR estático el foco llega al primer campo con error por el atributo `autofocus`, sin script; en un formulario interactivo, por `FocusAsync` de Blazor (Gerardo, 2026-10-16).
- Decision (entrada 9): una página arma cada campo con un componente `Campo` (etiqueta, control, ayuda y error, el "Campo" de `DESIGN.md`), que sabe si es el primer error y pone `autofocus`, `aria-invalid` y `aria-describedby`; los errores del `Resultado` le llegan por un componente padre dentro del `EditForm` (Gerardo, 2026-10-16).
- Decision (entrada 9): los dos modos se prueban con bUnit; el host no cambia y no se configura Interactive Server, que llega con la primera pantalla de administración (Gerardo, 2026-10-16).
- Decision (entrada 9): las pruebas de componentes van en su propio proyecto, `tests/DraSelina.Consultorio.UI.Tests` (bUnit), que no necesita Docker; ahí van también las de las demás épicas (Gerardo, 2026-10-16).
- Decision (entrada 9): los errores que no son de ningún campo (propiedad `""`) se pintan arriba de los campos y, por ser los primeros en orden de lectura, se llevan el foco (Gerardo, 2026-10-16).
- Decision (entrada 9): `ErroresDelResultado` copia los errores de campo del `Resultado` al `EditContext` del formulario, para que el `InputText` pinte `aria-invalid`, y los borra al enviar para que `OnValidSubmit` siempre se dispare; la página no tiene validador. El control lleva también la clase `invalid` de Blazor (Gerardo, 2026-10-16).
- Decision (entrada 9): un `Campo` fuera de `ErroresDelResultado`, o un `ErroresDelResultado` fuera de un `EditForm`, lanza `InvalidOperationException` al pintarse: nunca queda un campo cuyos errores no se ven (Gerardo, 2026-10-16).
- Decision (entrada 9): el id del control es el nombre de la propiedad (`Nombre`), la ayuda `Nombre-ayuda` y los mensajes `Nombre-error-1`…; la ayuda va en `aria-describedby` aunque no haya error. Si un día una página tiene dos formularios con la misma propiedad, se agrega un prefijo entonces (Gerardo, 2026-10-16).
- Open question (entrada 7): cómo obtiene su actor un endpoint de Minimal API, donde Blazor no fija el estado de autenticación y pedir el actor lanza. Se decide con el primer endpoint (archivo de calendario, AD-10) (Gerardo, 2026-10-15).
- Decision (entrada 10): `IActorActual` se agrega a la fila Kernel de la arquitectura y al árbol de carpetas, y al Kernel de la Parte I del documento de dominio (tabla de contextos y tabla de capas); vive ahí desde la entrada 7 (Gerardo, 2026-10-16).
- Decision (entrada 10): la convención Naming de la arquitectura pasa de "un namespace plano por capa" a "un namespace por capa; los sub-namespaces dentro de una capa (`Infrastructure.Migrations`, `UI.Layout`) son de esa capa y no tienen reglas propias", como ya funcionan las pruebas de arquitectura (Gerardo, 2026-10-16).
- Decision (entrada 10): el mensaje de `ActorDelHost` para un ámbito sin actor dice que `Sistema` lo declara quien crea el ámbito, desde el host, y ya no que lo declara un trabajo programado (Gerardo, 2026-10-16).
- Open question (entrada 10, de la entrada 3): `/health` no comprueba PostgreSQL y no hay trazas de Npgsql, porque no se usa la integración cliente de Aspire para Npgsql. Se decide al preparar el despliegue (Gerardo, 2026-10-16).
- Open question (entrada 10, de la entrada 2): `Actor` es un enum de tres valores sin id de usuario, y la bitácora del modelo de datos tiene `actor_id`. Se decide con la primera historia que escriba la bitácora (Gerardo, 2026-10-16).
- Open question (entrada 10, de la entrada 2): no hay forma definida para un comando que no devuelve valor; `ICommand<T>` siempre exige `T`. Se decide con el primer comando así (Gerardo, 2026-10-16).
- Open question (entrada 10, de la entrada 3): en local, Aspire no enmascara la cadena de consulta en las trazas (AD-23). Se revisa cuando exista el enlace de gestión (Gerardo, 2026-10-16).
- Decision: se pasa a .NET 10 (`net10.0`, SDK 10.0.402 del sistema). Razón: .NET 11 RC dio muchos problemas para ejecutar el proyecto. Reemplaza la decisión del 2026-10-05 de crear la solución sobre .NET 11 (Gerardo, 2026-10-23). Cambios: `global.json`, `Directory.Build.props`, `Mvc.Testing` 10.0.12, `IncludeEmbeddedValidationGlobalUsing` en `false` en la web (el SDK Razor 10 agrega un `using` global de `Microsoft.*` que el `Remove` no alcanza) y `dotnet.sh` llama al `dotnet` del sistema.
