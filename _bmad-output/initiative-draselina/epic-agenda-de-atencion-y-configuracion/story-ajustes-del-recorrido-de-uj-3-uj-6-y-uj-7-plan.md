---
title: 'Ajustes del recorrido de UJ-3, UJ-6 y UJ-7'
type: 'feature'
ticket: '9'
created: '2026-11-17'
status: done
baseline_revision: '9203b207790c71dcbf449be01cb1e46af28ddef4'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-draselina/ux-appointment-booking/DESIGN.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** El recorrido de la 6.8 encontró que el editor del día queda abajo del calendario y no se ve sin hacer scroll; que una Cita en línea conservada fuera de los Tramos no lleva "Fuera de horario"; textos que confunden; el correo de Avisos validado por el navegador; la ventana sin máximo; y `/reservar` dice "no tiene horarios para este tipo" en un día que no se atiende.

**Approach:** El editor del día (y el panel de quitar) en un `<dialog>` modal; "Fuera de horario" sin importar el origen; los textos acordados; `novalidate` en Avisos y Reglas; la ventana con máximo de 12 meses en `ReglasDeReserva`; el texto de `/reservar` acordado.

## Boundaries & Constraints

**Always:**
- Modal: se abre al tocar un día y se cierra con "Cerrar", Esc, al guardar o al quitar con éxito. Al abrir, el foco va al título del día; al cerrar, vuelve al botón del día tocado. Cerrar sin guardar descarta lo escrito, sin confirmar. En escritorio centrado; a 640 px o menos, hoja desde abajo a todo el ancho. Con scroll adentro: la lista de Citas fuera puede ser larga.
- "Fuera de horario": la regla de `Cita.QuedaFueraDe` para Citas de cualquier origen, solo en las no canceladas (Gerardo, 2026-11-17).
- `/reservar`, día sin horarios del tipo pedido (no se atiende o no caben): "Este día no tiene horarios disponibles. Elige otro día en el calendario." Sin cambiar `HorariosLibres` (Gerardo, 2026-11-17).
- El `<dialog>` se abre con `showModal()` desde un módulo JS junto a la página (`CalendarioDeAtencion.razor.js`), cargado solo en `/admin`; el primer JS propio del proyecto (Gerardo, 2026-11-17).
- Días de la semana con minúscula a media frase ("del sábado 21"); al inicio de frase o como título, como hoy.
- Con una sola Cita fuera, ayuda en singular y sin "Cancelar todas".
- El éxito al cancelar dice que reciben la disculpa solo los Pacientes con correo; si alguno no tiene: avisar a mano.
- "Horario actual:" en vez de "Hoy vale:". Volver a la semana tipo sin Tramos: "El día vuelve a la semana tipo, que no atiende ese día." `[ASSUMPTION: texto]`
- "Sin correo" separado del nombre para lector de pantalla (texto oculto ", ") en las dos listas de Citas fuera.
- "Tope semanal por celular (citas)" y "Escribe el tope semanal por celular.".
- `ReglasDeReserva.Cambiar` rechaza ventana mayor a 12 meses (nuevo `RechazoDeReglasDeReserva`), mensaje bajo el campo "La ventana puede ser de hasta 12 meses." `[ASSUMPTION: texto]`. El modelo de datos lo anota.
- Avisos y Reglas con `novalidate`: los errores son los de la app, bajo cada campo.

**Never:**
- Cambiar `EmailAddressAttribute` (acepta `a@b`, igual que la reserva y Agendar cita). "(por el Consultorio)": ya es igual en Agenda y Detalle y "Consultorio" es término del glosario; no cambia.
- Script en páginas públicas (AD-11).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Tocar un día | Calendario | Modal con "Horario propio del sábado 21 de noviembre", foco en el título | — |
| Esc o Cerrar | Modal con cambios | Se cierra sin guardar; foco al día | — |
| Guardar con éxito | Modal | Se cierra; aviso en la página; foco al día | — |
| Cita en línea fuera | Agendada en línea 5:30, Tramo hasta 5:00 | "Fuera de horario" en la Agenda | — |
| Cancelada fuera | Cita cancelada fuera de los Tramos, de cualquier origen | Sin "Fuera de horario" | — |
| Sábado en `/reservar` | Día sin Tramos pedido en la dirección | "Este día no tiene horarios disponibles…" | — |
| Una Cita fuera | 1 Cita | Ayuda singular, sin "Cancelar todas" | — |
| Cancelar con y sin correo | 2 canceladas, una sin correo | El aviso pide avisar a mano a quien no tiene correo | — |
| Ventana 13 | Reglas | Nada cambia | Mensaje bajo Ventana |
| Correo `no-es-un-correo` | Avisos | Nada cambia | Mensaje de la app bajo el campo |

</frozen-after-approval>

## Code Map

- `UI/Pages/Administracion/CalendarioDeAtencion.razor` -- editor (`form.dsm-atencion__editor`, l. 128) y panel de quitar (l. 107) bajo `@if (_seleccionada …)`; `Abrir`, `Cerrar` (l. 333), `GuardarAsync` (l. 358, cierra en l. 397), quitar (cierra en l. 455); `Cancelaciones` (l. 534), `VuelveA` (l. 541); "Hoy vale" (l. 130); `FormatoDeCita.Largo` a media frase en l. 109, 129, 395, 396, 454.
- `UI/Pages/Administracion/ListaDeCitasFuera.razor` -- `Ayuda` (l. 55), botón "Cancelar todas" (l. 16), chip sin correo (l. 30). `HorarioConCitasFuera.Citas` trae `SinCorreo` para el éxito.
- `UI/Pages/Administracion/SeccionSemanaTipo.razor` -- chip sin correo (l. 109), fechas a media frase.
- `UI/Pages/Reserva/FormatoDeCita.cs:29` -- `Largo`; agregar la variante en minúscula.
- `Modules/Agenda/Features/DiasDeLaAgendaHandler.cs:88` -- quitar `cita.Origen == OrigenDeCita.Consultorio`; actualizar el comentario y el doc de `CitaDeLaAgenda.FueraDeHorario` (`Contracts/DiasDeLaAgenda.cs:86`).
- `Modules/Agenda/Domain/ReglasDeReserva.cs:79` -- `Validar`; `CambiarReglasDeReservaHandler` mapea el rechazo nuevo; `Contracts/CambiarReglasDeReserva.cs` mensajes y "celular".
- `UI/Pages/Administracion/SeccionReglasDeReserva.razor`, `SeccionAvisosPorCorreo.razor` -- `<form>` sin `novalidate`; etiqueta del tope.
- `UI/Componentes/SelectorDeDiaYHora.razor:144` -- texto de un día sin horarios.
- `ErroresDelResultado.razor:141`, `Campo.razor:98` -- patrón `FocusAsync` existente. No hay JS propio aún; el módulo se importa con `IJSRuntime` (`import`) y se libera al desechar la página.
- `ddd-and-data-model-appointment-booking.md:93` -- ventana: anotar el máximo.
- Pruebas: `CalendarioDeAtencionTests`, `SeccionSemanaTipoTests`, `ReglasYAvisosDeConfiguracionTests`, `CambiarReglasDeReservaTests` (InlineData del máximo), dominio de `ReglasDeReserva`, integración de `DiasDeLaAgenda` (marca).

## Tasks & Acceptance

**Execution:**
- [ ] `FormatoDeCita.cs` -- `LargoEnFrase` (día en minúscula) -- textos a media frase.
- [ ] `CalendarioDeAtencion.razor` + `CalendarioDeAtencion.razor.js` + CSS en `consultorio.css` -- `<dialog>` modal con título enfocable, Cerrar, Esc, foco de vuelta; textos.
- [ ] `ListaDeCitasFuera.razor`, `SeccionSemanaTipo.razor` -- singular, separador, fechas.
- [ ] `DiasDeLaAgendaHandler.cs`, `DiasDeLaAgenda.cs` -- marca para todo origen, solo no canceladas.
- [ ] `ReglasDeReserva.cs`, `CambiarReglasDeReserva*.cs`, `SeccionReglasDeReserva.razor`, `SeccionAvisosPorCorreo.razor` -- máximo, "celular", `novalidate`.
- [ ] `SelectorDeDiaYHora.razor` -- texto que sirve para los dos casos.
- [ ] `ddd-and-data-model-appointment-booking.md` -- ventana hasta 12 meses.
- [ ] Pruebas de cada fila de la matriz (bUnit con JSInterop para el modal; integración para la marca y la ventana; dominio para el rechazo).

**Acceptance Criteria:**
- Given el Calendario de atención en un teléfono, when se toca un día, then el editor aparece encima sin hacer scroll.
- Given la suite completa, when corre, then pasa sin advertencias.

## Implementation Notes

- Implementado y revisado en esta sesión (el entorno niega los subagentes; misma decisión que en las entradas anteriores).
- Modal: `<dialog>` siempre en la página; `CalendarioDeAtencion.razor.js` (`abrir(dialogo, selector)`, `cerrar(dialogo, fecha)`) se importa con `IJSRuntime` en `OnAfterRenderAsync` cuando cambia lo que tiene el foco: el título del día, la lista de Citas fuera al aparecer, o el título de quitar. Esc lo cierra el navegador y `@onclose` olvida el día. Un botón "Cerrar" arriba; "Cancelar" sigue. La página pasa a `IAsyncDisposable` para soltar el módulo. El build publica el módulo en `UI/Pages/Administracion/CalendarioDeAtencion.razor.js`.
- `FormatoDeCita.LargoEnFrase` para fechas a media frase; la semana tipo pone el día en minúscula en sus textos ocultos ("desde el lunes").
- `Cancelaciones` lee `SinCorreo` de la vista previa: uno, todos o algunos sin correo.
- "Fuera de horario": `cita.Estado != EstadoCita.Cancelada && QuedaFueraDe(...)`; `NoSePresento` fuera la conserva.
- `ReglasDeReserva.VentanaMaximaMeses = 12` y `RechazoDeReglasDeReserva.VentanaMayorAlMaximo`; el modelo de datos lo anota.
- Pruebas: bUnit del modal (abrir con foco, Cerrar, Esc, guardar, lista y quitar con su foco), singular y aviso sin correo, `novalidate` y "celular"; dominio e integración de la ventana; integración de la marca (en línea fuera sí, cancelada no). Textos actualizados en las pruebas existentes. Suite completa: 1,125 pasan.
- Revisión en navegador (sesión tester, 2026-11-17): el comportamiento del modal pasa (foco, Tab, Esc, Cerrar, lista, quitar, guardar) y los demás puntos también. Falló lo visual: "Cerrar" con `float` dejaba al formulario (flex) sin ancho; ahora va en su propia fila (`dsm-atencion__cabeza-del-dialogo`). Y la ayuda en singular prometía la disculpa a una Cita sin correo: `AyudaDeUnaSinCorreo`.

## Plan Change Log

## Review Triage Log

**Pasada 1 (lente Quick, en esta sesión): 0 high, 1 medium, 2 low, 0 false, 0 maybe-false.**

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Al aparecer la lista de Citas fuera dentro del modal el foco no se movía; la entrada dice que el modal cierra el foco pendiente de la 6.3. | medium | patch | `FocoDelDialogo` pide `.dsm-citas-fuera__titulo` (legend con `tabindex="-1"`) cuando aparece la lista; prueba de la lista. |
| Al pasar del editor a "Quitar horario propio" el botón tocado desaparece y el foco se pierde dentro del diálogo. | low | patch | El mismo cambio enfoca `#quitar-del-dia`; prueba de quitar. |
| Un `JSException` al importar o llamar el módulo no se atrapaba y tumbaría el circuito del Administrador. | low | patch | `catch (JSException)` con registro de advertencia. |

## Verification

**Commands:**
- `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"; dotnet build` -- sin advertencias.
- `dotnet test` -- todas pasan.

**Manual checks:**
- Gerardo abre el modal en escritorio y en teléfono (o a 390 px), con una lista de Citas fuera larga, y prueba Esc y el foco.
