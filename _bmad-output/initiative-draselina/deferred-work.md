# Trabajo diferido

Deuda técnica que Gerardo decidió dejar para el final de todas las épicas.

- source_plan: `_bmad-output/initiative-draselina/epic-reserva-en-linea/story-reserva-en-escritorio-plan.md`
  summary: La confirmación no cambia de URL: tras "Confirmar cita" la pestaña sigue en /reservar/datos?… con el título "Tus datos".
  evidence: Visto por Gerardo en su recorrido de UJ-1 (2026-11-06). Es a propósito desde la entrada 2.1 (una URL con el CitaId dejaría ver la Cita y su enlace); recargar es seguro porque el mismo SolicitudId devuelve la misma Cita (entrada 2.8). Opciones al retomarlo: título de pestaña propio, o redirigir a una página de confirmación bajo el token del enlace (/c/<token>), nunca con el CitaId.
- source_plan: `_bmad-output/initiative-draselina/epic-reserva-en-linea/story-enlace-de-gestion-en-la-confirmacion-plan.md`
  summary: Con correo, Cita confirmada no muestra ningún mensaje de confirmación por correo. **Resuelto (2026-11-08, entrada 4.3, commit 31bd245):** Cita confirmada dice la frase del correo y la del Recordatorio con su hora, o solo la del correo si el Recordatorio se omite.
  evidence: Visto por Gerardo en su recorrido de UJ-1 (2026-11-06). Es su decisión de la entrada 2.9: la frase "Te enviamos los detalles a tu correo…" y el estado "Recordatorio omitido" los agrega la épica 4 cuando los correos existan (traspaso en las Notes de epic-notificaciones).
- source_plan: `_bmad-output/initiative-draselina/epic-reserva-en-linea/story-reserva-en-escritorio-plan.md`
  summary: En el teléfono, la hora de la tarjeta de la cita se parte en dos renglones y "pm" queda sola abajo.
  evidence: Visto por Gerardo en su recorrido de UJ-1 (2026-11-06) en vista de teléfono; detalle de CSS (la hora debería no partirse, por ejemplo con white-space: nowrap).
- source_plan: `_bmad-output/initiative-draselina/epic-notificaciones/story-refactor-sweep-plan.md`
  summary: `HorariosLibresHandler` lee la Agenda de atención por `IAgendaAtencion`, documentado como puerto de escritura.
  evidence: Visto en la revisión de la entrada 4.8 (2026-11-09). Es de la épica 2; la calculadora necesita la `AgendaAtencion` entera, así que el puerto `ITramosDeLaFecha` de la 4.8 no le sirve. Al retomarlo: un puerto de lectura de la agenda completa, o documentar que `IAgendaAtencion` también sirve lecturas sin rastrear.
