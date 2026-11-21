# Review log: walkthrough-aplicacion

Target: repositorio completo (src/ y tests/) en 3e9c6cb; empieza por el módulo Agenda.

## 1 — Orientación — alcance y forma

Session: session_01JaGktu7pbV2KB1pfiosriH · Timestamp: 2026-11-21T00:00:00-06:00

- Action: acordar objetivo y orden con Gerardo.
- Result: objetivo = entender el sistema, la arquitectura y la calidad del código, y conocer los módulos importantes; forma "muy muy estructurada"; empezar por Agenda (Parte A, bloques A1–A13), luego partes B–H.
- Open: ningún hallazgo todavía.

## 2 — A1 — ejemplo de la frontera

Session: session_01JaGktu7pbV2KB1pfiosriH · Timestamp: 2026-11-21T00:10:00-06:00

- Action: mostrar cómo Notificaciones usa Agenda sin tocar sus tablas.
- Result: ConfirmacionAlAgendar escucha CitaAgendada y pide a IAgendaApi el .ics, el tipo de visita y el enlace; Reglas.cs solo deja a Features usar los Contracts de otros módulos. Sin hallazgos.
- Evidence: src/.../Notificaciones/Features/ConfirmacionAlAgendar.cs:25-58; tests/.../ArchitectureTests/Reglas.cs:78-82

## 3 — A3 — pendiente: IRequiereAdministrador

Session: session_01JaGktu7pbV2KB1pfiosriH · Timestamp: 2026-11-21T00:30:00-06:00

- Action: Gerardo pidió dejar pendiente revisar si hay un mecanismo mejor que la interfaz marcadora IRequiereAdministrador.
- Result: open; anotado en deferred-work.md (lista de diferidos del proyecto). Retomarlo en la Parte B (decoradores, RechazoSinAdministrador.cs).
- Evidence: src/.../Agenda/Contracts/TiposDeVisitaEnConfiguracion.cs:44; src/.../Web/RechazoSinAdministrador.cs
- Open: comparar alternativas y decidir.

## 4 — A4 — pregunta: ¿State pattern?

Session: session_01JaGktu7pbV2KB1pfiosriH · Timestamp: 2026-11-21T00:45:00-06:00

- Action: Gerardo preguntó si la Cita usa State pattern.
- Result: no; enum + cláusulas de guarda (`if (Estado != Agendada)` en 5 métodos). Aceptado como está: solo Agendada tiene comportamiento, los otros dos son finales; se reconsideraría con 4+ estados activos (preferir tabla de transiciones). A4 lo cerró Gerardo al pasar a A5.
- Evidence: src/.../Agenda/Domain/Cita.cs:331,378,427,451; EstadoCita.cs

## 5 — A5 — preguntas y observación de diseño

Session: session_01JaGktu7pbV2KB1pfiosriH · Timestamp: 2026-11-21T01:00:00-06:00

- Action: explicar AgendaAtencion como agregado (único, AgendaCustom interna, Citas fuera por T-4/T-5).
- Result: A5 cerrado por Gerardo. Observación sin decidir: la rejilla se ancla al inicio del Tramo en pasos de la duración del tipo; con tipos de duraciones distintas (p. ej. 45 y 30 min) puede dejar huecos no reservables. No se anotó en deferred-work (Gerardo no lo pidió).
- Evidence: src/.../Agenda/Domain/CalculadoraDeDisponibilidad.cs (Rejilla); ddd-and-data-model, Agregados

## 6 — A6–A8 — observaciones sin decidir

Session: session_01JaGktu7pbV2KB1pfiosriH · Timestamp: 2026-11-21T01:20:00-06:00

- Action: recorrer reglas de reserva, agendar en línea y concurrencia.
- Result: A6–A8 cerrados por Gerardo. Observaciones presentadas, no anotadas en deferred-work (no lo pidió): reglas se aplican con su valor actual; tope semanal blando (AD-13); AgendarCitaEnLineaHandler con 15 dependencias; cambiar la semana tipo no toma candados de fecha (reserva concurrente queda "fuera de horario", visible en Citas que quedan fuera).
- Evidence: Agenda/Features/AgendarCitaEnLineaHandler.cs:26-41; CambiarAgendaBaseHandler.cs:43

## 7 — A9–A13 — cierre de la Parte A y forma

Session: session_01JaGktu7pbV2KB1pfiosriH · Timestamp: 2026-11-21T01:35:00-06:00

- Action: Gerardo pidió cerrar la Parte A y ver A10–A13 juntos, "más breve".
- Result: Parte A cerrada. Restricción de forma para los bloques que siguen: más breves (mecanismo corto + 2-3 referencias + una observación de calidad). Observación A9 sin anotar: rotar la clave del Enlace de gestión invalida todos los enlaces (el hash guardado depende de la clave); para el piloto, fijar la clave de producción una vez, en un lugar seguro.
- Evidence: Agenda/Domain/EnlaceDeGestion.cs (Derivar); Agenda/Features/CitaPorEnlaceHandler.cs:28-33

## 8 — B3–B4 — IRequiereAdministrador

Session: session_01JaGktu7pbV2KB1pfiosriH · Timestamp: 2026-11-21T01:50:00-06:00

- Action: comparar alternativas a la marca IRequiereAdministrador (23 marcados, 10 abiertos).
- Result: recomendación presentada: B (invertir: marcar los abiertos; falla cerrada) o C (prueba de arquitectura que exija la marca a lo inyectado en UI/Pages/Administracion); D (Authorize de ASP.NET en Contracts) descartada por la regla de capas. Gerardo cerró B4 sin decidir: sigue open en deferred-work.md. B3: códigos string, no enum (observación).
- Evidence: src/.../Web/RechazoSinAdministrador.cs; DecoradoresDeHandlers.cs:17
- Open: decisión B/C/dejar.

## 9 — B5–B7 — cierre de la Parte B

Session: session_01JaGktu7pbV2KB1pfiosriH · Timestamp: 2026-11-21T02:05:00-06:00

- Action: actor actual, outbox y periferia; Gerardo cerró la Parte B.
- Result: observaciones no anotadas en deferred-work (Gerardo no lo pidió): DeclararSistema/Actor.Sistema sin uso en producción (solo pruebas); "circuito = administración" depende de que lo público siga en SSR estático (AD-11); entrega al menos una vez, handlers deben ser idempotentes; eventos Fallidos solo dejan LogError, sin alerta (recomendado para el piloto, junto con la rotación de clave de A9).
- Evidence: src/.../Web/ActorDelHost.cs:34; EntregaDeEventos.cs:63-128,159-200

## 10 — D–H y cierre de la sesión

Session: session_01JaGktu7pbV2KB1pfiosriH · Timestamp: 2026-11-21T02:40:00-06:00

- Action: Partes D–H presentadas juntas a pedido de Gerardo; resumen completo publicado como doc de Claude Docs para discutirlo en otro chat.
- Result: D–H presentadas, sin cierre explícito de Gerardo. Pendientes de piloto 1–5 (SMTP sin auth, alerta de Fallidos, rotación de clave, HSTS/CSP/frame-ancestors, sondas de salud) y decisiones 6–9 recogidas en el doc; solo IRequiereAdministrador está en deferred-work.md.
- Evidence: https://claude.ai/artifact/EaFGspJkngP86GXjJyLUsR
- Open: decisiones que traiga Gerardo del otro chat; anotar 1–5 en deferred-work o convertirlas en tickets.
