---
title: 'Alerta de cancelación tardía'
type: 'feature'
ticket: '5'
created: '2026-11-08'
status: 'built'
baseline_revision: 'd28a095822e2ae7b0fa428044944e9c9b635f8c3'
route: 'oneshot'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Cuando un Paciente cancela tarde, el Administrador no se entera (CAP-19, FR-24).

**Approach:** Un handler nuevo de `CitaCancelada` en Notificaciones, solo para el motivo `tarde` que decidió Agenda (E-16, T-27).
- **Qué envía:** a `correo_administrador` de `configuracion_de_envio`, en cuanto se entrega el evento, el nombre del Paciente y la fecha y hora de la Cita.
- **Cuántas veces:** una sola por `EventoId` (tipo `AlertaTardia`), con el patrón de la 4.1.
- **Paciente sin correo:** la alerta sale igual, porque va al Administrador.
- **Sin botón "Abrir la Agenda":** lo agrega la épica 5 cuando exista la Agenda de administración (Decision de Gerardo, 2026-11-08).
- **Asunto:** "Cancelación tardía: {nombre}, {fecha}, {hora}". `[ASSUMPTION]`
- **Saludo:** "Hola:", sin nombre. `[ASSUMPTION]`

</frozen-after-approval>

## Implementation Notes

Ruta oneshot: un handler, su plantilla, un puerto de lectura de la configuración y sus pruebas.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 0, low 2, false 1, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Un Paciente desconocido hace lanzar el handler en vez de omitirse | false | rechazado | No es alcanzable: Pacientes no tiene camino para borrar un Paciente y toda Cita guarda el suyo. Fallar con ruido ante un estado imposible es lo mismo que hace `CitaPorEnlaceHandler` (3.1); omitir en silencio escondería un dato roto. |
| La mitad del Consultorio en la prueba de "no alerta" podía pasar sin probar nada | low | patch | Real: `DarUnaVuelta` cuenta intentos. Se agrega que salió la disculpa y que el evento quedó `Entregado`. |
| La prueba sin correo de la 4.4 quedó a medias con la alerta | low | patch | Real: se renombra a "al Paciente" y se comprueba una alerta para `tarde` y ninguna para los demás casos. |

## Verification

**Commands:**
- `dotnet build`: debe compilar sin advertencias.
- `dotnet test`: debe pasar la suite completa.
