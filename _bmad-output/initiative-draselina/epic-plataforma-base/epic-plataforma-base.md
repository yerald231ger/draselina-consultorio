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
- P6: Todo caso de uso es `ICommand<T>` o `IQuery<T>` con un handler, despachado por `ISender`, que devuelve `Resultado` con códigos cerrados; un comando, una transacción, con outbox. (AD-10, Consistency Conventions)
- P7: Validación de forma con DataAnnotations antes del handler, entregada como `Resultado` inválido por campo. (AD-12)
- P8: Un único servicio entrega el actor y un único middleware rechaza lo marcado `IRequiereAdministrador` sin usuario autenticado. (AD-22)
- P9: Telemetría solo por `Microsoft.Extensions.Logging` y OpenTelemetry mediante ServiceDefaults. (AD-21)
- P10: Base visual: tokens de `DESIGN.md` sobre Bootstrap y layout público móvil primero, en SSR estático. (AD-11; DESIGN.md)

## Done when

1. `aspire run` (o F5 en el AppHost) levanta PostgreSQL, aplica las migraciones y deja la aplicación web respondiendo, sobre el runtime que decidió la prueba de arranque.
2. Las pruebas de arquitectura fallan cuando se agrega una referencia que la tabla de AD-1 no permite.
3. Un comando de prueba pasa por `ISender`, se valida, cambia la base y deja su mensaje en el outbox en una sola transacción; con datos inválidos devuelve `Resultado` inválido con errores por campo, sin excepción.
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
- Open question: Testcontainers o el soporte de pruebas de Aspire para el arnés de integración (arquitectura, Deferred); lo decide la historia que lo construye.
- Decision: desglose en diez entradas aprobado (Gerardo, 2026-10-05). Bala trazadora: la entrada 1, el camino más delgado por AppHost, web, Wolverine, EF Core y PostgreSQL.
- Decision: secuencia 1 → 2 → 3 → 4 → 5 → 6 → 7; la 7 espera a la 6 porque ambas tocan la configuración de middlewares de Wolverine en el host. La 8 solo espera a la 3 y puede ir en paralelo con 4 a 7, porque se queda en `Consultorio.UI`.
- Decision: el componente que muestra en el formulario los errores de un `Resultado` inválido se entrega aquí (entrada 9) y no en la épica 2, para que la primera historia de reserva no construya infraestructura (Gerardo, 2026-10-05).
- Decision: la entrada 7 no comprueba el actor Paciente por Enlace de gestión; ese enlace nace en la épica 3 (AD-19).
- Revisión independiente del desglose (2026-10-05): confirmó cobertura de P1 a P10 y del Done when; de ella salieron la entrada 4 separada, la entrada 9 y los traspasos nombrados en las entradas 2, 3 y 7.
- Decision: la solución se crea sobre .NET 11. En la prueba de arranque (entrada 1) Wolverine 6.45.0, el AppHost de Aspire 13.6.0, EF Core 10.0.12 y Npgsql 10.0.3 arrancaron juntos y atendieron un comando de punta a punta sobre .NET 11.0.0-rc.1; no se retrocede a .NET 10 (Gerardo, 2026-10-05). Registro de la corrida: `spikes/arranque/REGISTRO.md`.
- Decision: se agrega `WolverineFx.RuntimeCompilation` 6.45.x al Stack, referenciado por el host; sin él Wolverine 6 no arranca en modo dinámico. Queda abierto, para cuando haya despliegue, si producción usa código pregenerado (`TypeLoadMode.Static`) (Gerardo, 2026-10-05).
- Decision: la validación de forma antes del handler la hace un middleware propio de Wolverine, aplicado a todo mensaje con el marcador de comando, que devuelve `Resultado` inválido con errores por nombre de propiedad y detiene la cadena. El middleware de DataAnnotations de Wolverine, con su configuración por defecto, lanza `ValidationException` y no entrega `Resultado`; su punto de extensión `IFailureAction<T>` no se probó. Cambia la redacción de AD-12 (Gerardo, 2026-10-05).
- Decision: un `EditForm` se liga directo al comando de `Contracts` con `[SupplyParameterFromForm]`; no hay modelos de formulario aparte. Todos los comandos se escriben como clase con propiedades `set`: es la única forma que admite `@bind-Value` y la que no imprime sus datos cuando Wolverine registra un fallo (AD-23). Eventos de integración y DTOs pueden ser `record` (Gerardo, 2026-10-05).
- Decision: los módulos, sus capas, el Kernel, los contratos base y la interfaz no son proyectos: son carpetas y namespaces dentro de `DraSelina.Consultorio.Web`. Quedan cuatro proyectos (`AppHost`, `ServiceDefaults`, `MigrationService`, `Web`) y sus carpetas llevan el nombre completo `DraSelina.Consultorio.*`. Un proyecto por capa no es mantenible para un solo desarrollador; separarlos se decide cuando haga falta (Gerardo, 2026-10-05).
