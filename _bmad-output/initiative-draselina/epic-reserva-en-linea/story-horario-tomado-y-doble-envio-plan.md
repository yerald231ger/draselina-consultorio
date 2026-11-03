---
title: 'Horario tomado y doble envío'
type: 'feature'
ticket: '8'
created: '2026-11-03'
status: 'built'
baseline_revision: 'ac0b0ecabb351bbea39d4864e869bfaa7b90eb1d'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-draselina/ux-appointment-booking/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Confirmar dos veces el mismo formulario (doble toque, recarga) choca con la propia Cita del Paciente y le dice "horario ocupado"; y cuando alguien toma el horario un instante antes, el Paciente queda en Tus datos con un aviso y sin forma de elegir otra hora sin volver a escribir todo.

**Approach:** Tus datos lleva un `SolicitudId` generado al pintarse, que la Cita guarda; un segundo envío con el mismo `SolicitudId` devuelve la Cita ya creada (AD-27, RN-21). Si el horario se ocupó al confirmar, la respuesta muestra el aviso y los horarios libres restantes del tipo, y elegir uno vuelve a Tus datos con los datos conservados (FR-4, UJ-1 Falla).

## Boundaries & Constraints

**Always:**
- AD-27: `agenda.cita.solicitud_id` (`uuid`, nulo para las del consultorio, único; los nulos no chocan). Un `AgendarCitaEnLinea` con un `SolicitudId` que ya tiene Cita devuelve esa Cita como éxito, con los mismos datos de confirmación, sin crear otra, sin otra entrada de bitácora y sin otro `CitaAgendada`. Dos envíos simultáneos con el mismo `SolicitudId` dan una sola Cita y ambos reciben la confirmación.
- El `SolicitudId` se genera al pintar Tus datos (Guid v7), viaja en un campo oculto del cuerpo y se conserva mientras el Paciente reintenta tras un horario ocupado: es la misma solicitud.
- AD-11: nombre, teléfono, correo y motivo viajan solo en el cuerpo del formulario; en la dirección solo `tipo`, `fecha` y `hora`. Sin scripts.
- Horario tomado (FR-4, EXPERIENCE.md "Horario tomado al confirmar"): la respuesta muestra el aviso "Ese horario se acaba de ocupar. Elige otro; tus datos se conservan." y los días y horas libres restantes del tipo, con la misma calculadora y la misma regla de "Lleno" de Día y hora (entrada 5). Nunca una pantalla de error.
- Elegir otra hora desde esa vista vuelve a Tus datos con el nuevo horario y los datos ya llenos (nombre, teléfono, correo, motivo y la casilla), sin reservar todavía: el Paciente confirma de nuevo.

**Never:**
- Deshabilitar el botón con scripts ("Enviando"): el doble envío lo resuelve el servidor.
- Guardar datos del Paciente en el servidor entre pasos (AD-11) o en la dirección.
- Enlace de gestión (entrada 9), anti-bots (entrada 11), escritorio (entrada 12).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Doble envío | El mismo formulario confirmado dos veces | Una Cita; los dos envíos reciben la misma confirmación; una bitácora y un evento | — |
| Doble envío simultáneo | Dos envíos con el mismo `SolicitudId` a la vez | Una Cita; ambos confirman | Sin error técnico |
| Solicitudes distintas | Dos formularios distintos al mismo horario | Una Cita; el otro ve horario ocupado | `agenda.horario_ocupado` |
| Horario tomado | Otro Paciente reservó ese horario antes | Aviso y horarios libres restantes del tipo; ningún dato en la dirección | — |
| Elegir otra hora | Desde esa vista, otra hora | Tus datos con el nuevo horario y los datos llenos; no se crea Cita hasta confirmar | — |
| Reintento | Confirmar la nueva hora | Cita creada con el mismo `SolicitudId` | — |
| Cita del consultorio | Fila sin `solicitud_id` | No choca con nada por el único | — |

</frozen-after-approval>

## Code Map

Rutas bajo `src/DraSelina.Consultorio.Web/` salvo que digan otra cosa.

- `Modules/Agenda/Contracts/AgendarCitaEnLinea.cs` -- comando (clase con `set`, DataAnnotations con mensajes en español: `MensajesDeValidacionTests`); agregar `SolicitudId` requerido. `CitaAgendadaEnLinea` es lo que pinta la confirmación.
- `Modules/Agenda/Domain/Cita.cs`, `ICitas.cs` -- `Agendar` recibe la solicitud; el puerto gana "obtener por solicitud" (Parte I, Repositorios). `GuardarAsync` ya traduce `23P01`; ahora también el choque del único de `solicitud_id` (`23505`).
- `Modules/Agenda/Features/AgendarCitaEnLineaHandler.cs` -- flujo tx → candado → reglas → conteo → `Cita.Agendar` → guardar → bitácora y evento → confirmar. La búsqueda por solicitud va antes de todo; el choque del único durante el guardado devuelve la Cita existente.
- `Modules/Agenda/Infrastructure/AgendaDbContext.cs` + migración `AgregarSolicitudACita` -- columna e índice único.
- `UI/Pages/Reserva/TusDatos.razor` -- `EditForm FormName="datos"` con `[SupplyParameterFromForm]`; `Aviso(codigo)`; guarda de fechas con `IClock`. El modo "horario tomado" y el formulario de "elegir otra hora" (otro `FormName`, botones con `formaction` a `/reservar/datos?tipo=&fecha=&hora=` y los datos en campos ocultos) viven aquí o en un componente de `UI/Pages/Reserva/`.
- `UI/Pages/Reserva/DiaYHora.razor` -- cómo pinta días, "Lleno" y horas desde `HorariosLibres`; reutilizar su marcado o extraer un componente, sin cambiar su comportamiento.
- `tests/DraSelina.Consultorio.IntegrationTests/ReservaEnLineaTests.cs`, `TusDatosTests.cs`, `BitacoraYCitaAgendadaTests.cs` -- reloj fijo, `ReservaEnLineaTests.SinSesion`; los POST de página llevan el token antiforgery (ver `TusDatosTests`).

## Tasks & Acceptance

**Execution:**
- [x] Contrato, dominio, puerto y migración -- `SolicitudId` en el comando y en la Cita; obtener por solicitud; único en la base.
- [x] `AgendarCitaEnLineaHandler.cs` -- solicitud existente → su confirmación; choque del único al guardar → la Cita existente; sin segunda bitácora ni evento.
- [x] `TusDatos.razor` (y componente si conviene) -- campo oculto con el `SolicitudId`; vista de horario tomado con aviso y horarios restantes; formulario "elegir otra hora" que vuelve a Tus datos con los datos llenos sin reservar.
- [x] Pruebas -- cada fila de la matriz: integración del handler (incluida la simultánea) y de página (POST con antiforgery; la dirección nunca lleva datos personales).

**Acceptance Criteria:**
- Given Tus datos llena, when se confirma dos veces, then hay una sola Cita y las dos respuestas son la misma confirmación.
- Given que otro Paciente tomó el horario, when se confirma, then se ve el aviso con los horarios libres restantes; when se elige otra hora y se confirma, then se crea la Cita sin volver a escribir los datos.
- Given la solución, when corren todas las pruebas, then pasan.

## Implementation Notes

- `SolicitudId` es `Guid` simple (no un id tipado del Kernel): el plan no pedía tipo nuevo. `Cita.Agendar` lo recibe como primer parámetro y rechaza `Guid.Empty` con `ArgumentException`.
- El handler busca la solicitud tres veces: antes de todo (no toca Pacientes ni reglas), después del candado (un envío repetido de la misma fecha hace fila detrás del primero; sin esta lectura chocaría por horario o contaría su propia Cita para el tope) y, si el guardado choca (`23P01` o `23505` en `ux_cita_solicitud_id`), después de deshacer. Si no hay Cita de esa solicitud, es horario ocupado. `ICitas.GuardarAsync` gana `GuardadoDeCita.SolicitudRepetida`.
- La confirmación repetida se arma de la Cita guardada: fecha y hora locales de su inicio, duración de su rango, nombre actual del tipo.
- Vista de horario tomado: componente `UI/Pages/Reserva/HorarioTomado.razor`, formulario `otra-hora` con los datos y el `SolicitudId` ocultos y un botón por hora con `formaction` a `/reservar/datos?tipo=&fecha=&hora=`. Los días van como `<details>` (abierto el primero con horas; "Lleno" sin horas). Tus datos recibe el envío con `[SupplyParameterFromForm(FormName = "otra-hora")] OtraHora`; como Blazor despacha el envío al formulario con ese nombre en el primer pintado, la página arranca en la vista de horario tomado y su `onsubmit` la cambia a Tus datos antes de responder. `Comando` no lleva `FormName`, así se llena con los dos formularios.
- Si el POST de "datos" llega sin `SolicitudId` (una página pintada antes de este cambio), la página genera uno: el comando no queda inválido sin mensaje visible.
- `Guid.Empty` lo rechaza la validación (`[GuidNoVacio]`): no reserva ni toca Pacientes, y la página vuelve a pintar el formulario con una solicitud nueva. Cualquier rechazo de `Cita.Agendar` también busca la solicitud después de deshacer, como el choque al guardar (revisión).
- EXPERIENCE.md dice "Regresa a Día y hora" y, en "Enviando", que el botón se deshabilita; este plan (intención congelada) decide quedarse en Tus datos con los horarios y no usar scripts. No se editó EXPERIENCE.md.
- El camino del `23505` no se puede forzar de forma determinista sin ganchos; lo ejercita `Envios_simultaneos_de_la_misma_solicitud_con_fechas_distintas...` cuando la carrera cae ahí. El único y su nombre se prueban directo en SQL.

## Plan Change Log

## Review Triage Log

**Pasada 1 (quick), 2026-11-03.** Veredictos: high 0, medium 1, low 1, false 0, maybe-false 0.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | Un `SolicitudId` vacío (`00000000-…`) hecho a mano pasa `[Required]` y el `??=` de la página; `Cita.Agendar` lanza y el Paciente ve un 500 | medium | patch | Confirmado en el flujo: el handler confía en la validación de forma (AD-12); la Boundary prohíbe un error técnico. Validación rechaza `Guid.Empty` y la página lo trata como nulo |
| 2 | Un reenvío de la misma solicitud con otra fecha de la misma semana puede contar la Cita del primer envío y recibir `agenda.tope_semanal` en lugar de la confirmación | low | patch | Carrera real (candados por fecha distinta), rara en uso normal; corrección directa: ante cualquier rechazo, deshacer y volver a buscar la solicitud, como ya hace la rama del guardado fallido |

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` -- expected: todas pasan (Docker arriba).
- AppHost y `scripts/verificar-arranque.sh` -- expected: las tres filas pasan.
