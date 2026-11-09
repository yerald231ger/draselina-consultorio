---
title: 'Refactor sweep'
type: 'refactor'
ticket: '8'
created: '2026-11-09'
status: done
baseline_revision: 'b0ecd77198e00c6777ba3bf9e2c398f417def63a'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Notificaciones repite piezas que la épica construyó en siete entradas:
- La confirmación (4.1) y el Recordatorio (4.2) arman su propio HTML completo, mientras las otras cuatro plantillas usan `MarcoDeCorreo`.
- Cuatro handlers repiten el mismo patrón de envío único por evento.
- `AgendaDelDiaAsync` lee la Agenda base por un puerto documentado como de escritura.
- Un comentario de Cita confirmada quedó desactualizado.

**Approach:** Limpieza sin cambio de comportamiento, con el alcance que fijó Gerardo el 2026-11-09:
1. `PlantillaDeConfirmacion` y `PlantillaDeRecordatorio` usan `MarcoDeCorreo`.
2. Un ayudante concentra "enviar una vez por evento" y lo usan `ConfirmacionAlAgendar`, `AvisoAlReprogramar`, `AvisoAlCancelar` y `AlertaDeCancelacionTardia`.
3. Un puerto de lectura de Agenda da los Tramos vigentes de una fecha a `AgendaApi.AgendaDelDiaAsync`.
4. Se corrige el comentario de `CitaConfirmada.razor` sobre el mapa.

Fuera: los hallazgos rechazados en las revisiones, el formato de fecha copiado, el registro por reflexión y `deferred-work.md`.

## Boundaries & Constraints

**Always:**
- Los correos no cambian: mismos asuntos, mismos textos, mismo HTML visible, mismos adjuntos. Las pruebas de la épica que leen asunto, HTML y texto siguen pasando sin tocar sus aserciones.
- El ayudante conserva el orden y las garantías de la 4.1:
  - transacción de Notificaciones;
  - `ExisteAsync(EventoId, tipo)`;
  - registrar `Enviado`;
  - enviar;
  - confirmar con `CancellationToken.None`.

  Cada handler sigue decidiendo si envía (motivo, contactos) y qué mensaje arma.
- El puerto de lectura vive en `Agenda/Features` y su implementación en `Agenda/Infrastructure`. Lee sin rastrear. `IAgendaAtencion` queda solo para escritura.
- Las pruebas de arquitectura siguen pasando.

**Never:**
- Cambiar textos, asuntos o el comportamiento de los envíos.
- Tocar los handlers del Recordatorio o la vuelta de envíos programados, salvo lo que exija el marco.
- Cambiar la forma de registrar handlers.

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/MarcoDeCorreo.cs`: el marco, con `Texto`, `Html`, `FechaYHora`, `Tipo`, `Parrafo`, `Boton` y el saludo con nombre opcional.
- `.../Notificaciones/Features/PlantillaDeConfirmacion.cs` y `PlantillaDeRecordatorio.cs`: su HTML y su texto propios. Las constantes `Doctora` y `Especialidad` viven en `PlantillaDeConfirmacion` y las usa el marco; se pueden mover al marco.
- `.../Notificaciones/Features/ConfirmacionAlAgendar.cs`, `AvisoAlReprogramar.cs`, `AvisoAlCancelar.cs` y `AlertaDeCancelacionTardia.cs`: el patrón repetido.
- `.../Modules/Agenda/Features/AgendaApi.cs` (`AgendaDelDiaAsync`) y `Domain/IAgendaAtencion.cs`; `Infrastructure/AgendaAtencionPersistida.cs` y `Registro.cs`.
- `.../UI/Pages/Reserva/CitaConfirmada.razor`, en su comentario de cabecera: "Sin 'Copiar' ni 'Abrir en el mapa' (Contenido no tiene enlace de mapa todavía)". Contenido sí tiene `EnlaceDelMapa` desde la 4.1; la página sigue sin mostrarlo.
- Pruebas que protegen el comportamiento: `ConfirmacionPorCorreoTests`, `RecordatorioTests`, `AvisosDeCambioTests`, `AlertaDeCancelacionTardiaTests`, `AgendaDelDiaTests` y `AgendaApiTests`.

## Tasks & Acceptance

**Execution:**
- [ ] `.../Notificaciones/Features/PlantillaDeConfirmacion.cs`, `PlantillaDeRecordatorio.cs` y `MarcoDeCorreo.cs`: un solo marco.
- [ ] `.../Notificaciones/Features/EnvioUnico.cs` (o un nombre equivalente) y los cuatro handlers.
- [ ] `.../Agenda/Features/ITramosDeLaFecha.cs` (o un nombre equivalente), su implementación, `Registro.cs` y `AgendaApi.cs`; ajustar `EnlaceDeGestionTests` si construye `AgendaApi` a mano.
- [ ] `.../UI/Pages/Reserva/CitaConfirmada.razor`: el comentario.

**Acceptance Criteria:**
- Given las pruebas de la épica 4, when corren después del cambio, then pasan sin modificar ninguna aserción sobre el contenido de los correos.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 0, low 1, false 0, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| `HorariosLibresHandler` sigue leyendo por `IAgendaAtencion`, aunque el plan dice que queda solo para escritura | low | defer | Es previo a esta entrada (épica 2): la calculadora necesita la `AgendaAtencion` entera, no los Tramos de una fecha. El Intent solo nombra `AgendaDelDiaAsync`; la frase de Always fue más amplia que el Intent. Queda en `deferred-work.md`. |

El revisor confirmó que el HTML y el texto de la confirmación, el Recordatorio y la reprogramación salen iguales, comparados línea por línea; que `EnvioUnico` conserva el orden de la 4.1; y que ninguna aserción de prueba cambió (suite completa 698 de 698).

## Verification

**Commands:**
- `dotnet build`: debe compilar sin advertencias.
- `dotnet test`: debe pasar la suite completa, arquitectura incluida.
