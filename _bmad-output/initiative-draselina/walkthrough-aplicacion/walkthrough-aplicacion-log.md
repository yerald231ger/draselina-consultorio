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
