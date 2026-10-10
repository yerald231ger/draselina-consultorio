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
- Decision: la solución se crea sobre .NET 11. En la prueba de arranque (entrada 1) Wolverine 6.45.0, el AppHost de Aspire 13.6.0, EF Core 10.0.12 y Npgsql 10.0.3 arrancaron juntos y atendieron un comando de punta a punta sobre .NET 11.0.0-rc.1; no se retrocede a .NET 10 (Gerardo, 2026-10-05). Registro de la corrida: `spikes/arranque/REGISTRO.md`.
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
- Open question: cuánto vive el contexto en las pantallas de administración (Interactive Server), donde el ámbito dura lo que el circuito y dos operaciones del mismo módulo pueden cruzarse sobre el mismo contexto. Se decide con la primera historia que tenga una pantalla Interactive Server; las salidas a la vista son un ámbito por página (`OwningComponentBase`) o un ámbito por operación. Lo construido en la entrada 5 sirve para cualquiera (Gerardo, 2026-10-10).
- Decision (entrada 11): un evento cuya entrega falla se reintenta con espera creciente y, al llegar al tope, queda marcado como fallido en la tabla y se registra un error; los demás eventos siguen fluyendo (Gerardo, 2026-10-10).
- Decision (entrada 11): el calendario es de 7 intentos, con esperas de 1 min, 5 min, 15 min, 1 h, 4 h y 12 h entre uno y otro (unas 17 horas en total). Los valores viven en un solo lugar del código (Gerardo, 2026-10-10).
- Decision (entrada 11): el servicio de entrega aparta los eventos pendientes con `FOR UPDATE SKIP LOCKED`, para que dos instancias de la web nunca entreguen el mismo evento a la vez (Gerardo, 2026-10-10).
- Decision (entrada 11): orden por clave. Un evento no se entrega mientras haya otro anterior con la misma clave de orden todavía pendiente; para Agenda la clave es el `CitaId`. Los eventos de otras claves siguen fluyendo, y si el anterior queda fallido los siguientes ya no esperan (Gerardo, 2026-10-10).
