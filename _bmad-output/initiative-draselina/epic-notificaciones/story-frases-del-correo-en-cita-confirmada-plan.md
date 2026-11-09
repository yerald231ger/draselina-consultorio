---
title: 'Frases del correo en Cita confirmada'
type: 'feature'
ticket: '3'
created: '2026-11-08'
status: 'built'
baseline_revision: '3f6d540936d437aec6301a27e6f2178162b01cdd'
route: 'oneshot'
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

**Problem:** Hay un traspaso pendiente de la épica 2. Cita confirmada, con correo, no dice que el correo y el Recordatorio ya salen (EXPERIENCE.md: "Confirmada, con correo" y "Recordatorio omitido").

**Approach:** Notificaciones responde con una consulta nueva, `RecordatorioPrevisto(fecha, hora)`, si el Recordatorio de una Cita con ese inicio saldrá y a qué hora. Usa la misma regla de la 4.2: `HoraDelRecordatorio.ProgramadoPara` frente al reloj. Cita confirmada solo compone el texto (AD-14):
- **Con correo y Recordatorio:** "Te enviamos los detalles a tu correo. Un día antes, a las {hora}, te llegará un recordatorio."
- **Con correo y Recordatorio omitido:** solo la primera frase.
- **Sin correo:** como hoy.

</frozen-after-approval>

## Implementation Notes

Ruta oneshot: una consulta con su handler en Notificaciones, unas líneas en `CitaConfirmada.razor` y sus pruebas.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 0, low 3, false 0, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Si la consulta no tiene éxito, con correo no sale ninguna frase | low | patch | Real aunque hoy no se alcanza (el handler siempre da éxito). Arreglo directo: sin respuesta, la frase del correo sola. |
| Una falla al leer `configuracion_de_envio` tumba la confirmación de una Cita ya guardada | low | rechazado | La fila la siembra la migración y no tiene comando que la borre; una base que no responde ya habría fallado antes, al reservar. Atraparlo protegería un estado que no se mostró alcanzable, y el reenvío es idempotente (AD-27). |
| No hay prueba en el límite exacto de la hora del Recordatorio | low | rechazado | La página usa la misma `HoraDelRecordatorio.ProgramadoPara` que el programador, cuyos casos ya prueba la 4.2. La diferencia (`OcurridoEn` frente a ahora) es de milisegundos en la misma respuesta. |

## Verification

**Commands:**
- `dotnet build`: debe compilar sin advertencias.
- `dotnet test`: debe pasar la suite completa.
