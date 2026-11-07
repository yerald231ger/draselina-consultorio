---
title: 'Reprogramar desde Tu cita'
type: 'feature'
ticket: '3'
created: '2026-11-06'
status: 'built'
baseline_revision: 'c1c38f1cbe9fb64ebec7c633888a70e4df886a85'
route: 'full'
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

**Problem:** `ReprogramarCitaPorEnlace` (3.2) existe, pero el Paciente no tiene cómo usarlo. Tu cita no ofrece "Reprogramar" y no hay una pantalla para elegir el horario nuevo (CAP-7, UJ-2 pasos 1 a 3).

**Approach:** Tu cita muestra "Reprogramar" o, dentro del umbral, el panel ámbar. `/c/{token}/reprogramar` reutiliza Día y hora: el selector se extrae a un componente que comparten las dos páginas, con la Cita actual visible, sin paso de tipo y apartando su propia hora. Confirmar envía el comando de 3.2 y pinta Cita reprogramada, o vuelve con un aviso.

## Boundaries & Constraints

**Always:**
- Todo en SSR estático, sin scripts (AD-11), en móvil y escritorio, como Día y hora (ticket 2.12).
- Elegir día y hora son enlaces GET con `?fecha=AAAA-MM-DD&hora=HH:mm` bajo `/c/{token}/reprogramar`. El token va solo en la ruta (AD-23) y la telemetría ya la enmascara.
- Confirmar es un POST con antiforgery al mismo `/c/{token}/reprogramar`, con fecha y hora en campos ocultos. La página lleva `[EnableRateLimiting(DemasiadosIntentos.Politica)]`, el meta robots y los encabezados de `/c/`, igual que 3.1.
- `CitaDelEnlace` agrega `CitaId`, `TipoDeVisitaId`, `PuedeReprogramar` (Agendada y con al menos el umbral antes del inicio, RN-17) y `UmbralMin`. El handler de `CitaPorEnlace` los calcula con `IReglasDeReserva`. La UI usa `CitaId` solo para la consulta: nunca lo pinta ni lo pone en una dirección.
- `HorariosLibres` agrega el parámetro opcional `CitaId? Aparte`. El handler quita esa Cita de las Citas activas antes de llamar a la calculadora, que no cambia (T-21). `ICitasActivas.EntreAsync` recibe la Cita a excluir. Decision del 2026-11-06.
- En el día actual de la Cita, su propia hora se pinta "Tu hora actual", sin enlace y sin contar como libre.
- Tu cita, cuando la Cita está Agendada:
  - con `PuedeReprogramar`: botón "Reprogramar" y la nota "Puedes reprogramar hasta {FormatoDeCita.HastaAntes(UmbralMin)} de tu cita." (mockup Gestionar);
  - sin él: sin botón, con el panel ámbar "Faltan menos de {umbral}" y la tarjeta "¿Buscabas otro horario?" con "A esta hora ya no se puede reprogramar. Cancela y vuelve a agendar en el horario que prefieras." (mockup Cancelar).

  El panel es un componente que la 3.4 reutiliza.
- La página de reprogramar muestra "Reprogramar tu cita" y la Cita actual (fecha, hora, tipo · duración). El panel "Tu cita" lleva "Tu cita actual" y "Nuevo horario", y el botón "Confirmar nuevo horario". `[ASSUMPTION: textos]`
- Al confirmar:
  - **Éxito:** Cita reprogramada en la misma respuesta, con "Tu cita se reprogramó" `[ASSUMPTION: texto]`, la nueva fecha y hora, tipo, lugar, "Agregar a mi calendario" y "Ver mi cita" (que lleva a `/c/{token}`).
  - **`HorarioOcupado`:** la misma página con el aviso "Ese horario se acaba de ocupar. Elige otro." y los horarios recalculados.
  - **`FueraDeHorario`, `FueraDeVentana` o `EnElPasado`:** el aviso "Ese horario ya no está disponible. Elige otro." `[ASSUMPTION: texto]`
  - **`ReprogramacionCerrada`:** el panel ámbar con "Ver mi cita".
  - **`CitaNoAgendada`:** "Esta cita ya está cancelada." con "Ver mi cita". `[ASSUMPTION: texto]`
  - **`CitaNoEncontrada`:** Enlace no válido con 404.
- Abrir la página de reprogramar (GET) aplica las mismas salidas: enlace no válido → 404; no Agendada → mensaje de cancelada; sin `PuedeReprogramar` → panel ámbar.
- Si `HorariosLibres` responde `TipoNoReservable` (una Cita del consultorio de un tipo que no se agenda en línea), se muestra "Esta cita no se puede reprogramar en línea." con el WhatsApp de Contenido. `[ASSUMPTION]`
- Decision (Gerardo, 2026-11-06): el plan se conserva completo aunque pasa de 1,600 tokens.
- `DiaYHora.razor` conserva exactamente su comportamiento: sus pruebas existentes pasan sin cambios.

**Never:**
- Cancelar o su pantalla (3.4).
- Pedir datos del Paciente otra vez.
- Cambiar el tipo de visita al reprogramar.
- Poner el CitaId o el token en la cadena de consulta.
- Duplicar el cálculo de la rejilla en la UI.

## I/O & Edge-Case Matrix

Con el reloj en el lunes 2 de noviembre a las 18:00 UTC y la Cita el martes 3 a las 16:00 local (30 min).

| Escenario | Estado o acción | Resultado |
|---|---|---|
| Fuera del umbral | Tu cita | Botón "Reprogramar" hacia `/c/{token}/reprogramar` y la nota del plazo; sin panel ámbar |
| Dentro del umbral | reloj a menos de 120 min del inicio | sin "Reprogramar"; panel ámbar y "¿Buscabas otro horario?" |
| Cancelada | Tu cita | sin "Reprogramar" (como en 3.1) |
| Abrir reprogramar | GET | la Cita actual visible, días y horas; el martes 3 muestra 4:00 pm como "Tu hora actual", sin enlace |
| Elegir hora | GET `?fecha=2026-11-05&hora=17:00` | el panel muestra el nuevo horario y el botón "Confirmar nuevo horario" |
| Confirmar | POST jueves 5, 17:00 | Cita reprogramada con "Jueves 5 de noviembre, 5:00 pm"; Tu cita muestra la nueva fecha |
| Horario tomado | POST a una hora que otra Cita ocupó | 200 con el aviso de horario ocupado y los horarios restantes; la Cita no cambia |
| Dentro del umbral al confirmar | POST con el reloj ya dentro | panel ámbar con "Ver mi cita"; sin cambios |
| Cita cancelada | GET o POST | "Esta cita ya está cancelada." con "Ver mi cita" |
| Enlace no válido | GET o POST con token inválido | 404 Enlace no válido |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/UI/Pages/Reserva/DiaYHora.razor`: 481 líneas.
  - Chips de teléfono, calendario de escritorio (`Semanas`, `Celda`, `MesVecino`), horas (`dsm-horas__rejilla`), el panel `dsm-tu-cita` y la lógica de `OnParametersSetAsync` (día elegido, `_diaLleno`, `_sinHorarios`).
  - Todos los enlaces salen de `Ruta(tipo, fecha, hora)`.
  - Extraer el selector a `UI/Componentes/SelectorDeDiaYHora.razor`, parametrizado por `Dias`, la fecha y hora pedidas, una función `Ruta(fecha, hora?)`, un `RenderFragment` para el panel derecho y una `HoraActual` opcional (fecha y hora a marcar).
  - La página conserva la navegación de tipos y "Continuar".
- `.../UI/Pages/Reserva/HorarioTomado.razor` y `TusDatos.razor`: el patrón de formulario POST en SSR (`@formname`, `AntiforgeryToken`, `SupplyParameterFromForm`) y de respuesta sin redirección.
- `.../UI/Pages/Reserva/CitaConfirmada.razor`: la tarjeta, el lugar y el enlace al `.ics`, a imitar en Cita reprogramada.
- `.../UI/Pages/Reserva/FormatoDeCita.cs`: `Largo`, `Hora`, `FechaYHora` y `HastaAntes(min)` (el "2 horas" sale del umbral).
- `.../UI/Pages/Gestion/TuCita.razor` y `EnlaceNoValido.razor` (3.1): agregar los botones y el panel. La técnica del 404 con `Response.OnStarting` se reutiliza.
- `.../Modules/Agenda/Contracts/CitaPorEnlace.cs` y `Features/CitaPorEnlaceHandler.cs`: extender `CitaDelEnlace`; su `PrintMembers` sigue ocultando el nombre.
- `.../Modules/Agenda/Contracts/HorariosLibres.cs`, `Features/HorariosLibresHandler.cs`, `Features/ICitasActivas.cs` e `Infrastructure/CitasActivas.cs`: excluir la Cita.
- `.../Modules/Agenda/Contracts/ReprogramarCitaPorEnlace.cs` (3.2): el comando y `CitaReprogramadaPorEnlace(Fecha, Hora)`. Los códigos están en `CodigosDeAgenda`.
- `_bmad-output/initiative-draselina/ux-appointment-booking/imports/mockup-canvas/Gestionar.dc.html` y `Cancelar.dc.html`: el botón, la nota y el panel ámbar.
- Pruebas modelo: `tests/DraSelina.Consultorio.IntegrationTests/TuCitaTests.cs`, `TipoDeConsultaYDiaYHoraTests.cs` (HTML por HTTP) y `TusDatosTests.cs` (POST con antiforgery desde un GET previo).

## Tasks & Acceptance

**Execution:**
- [x] `.../Agenda/Contracts/CitaPorEnlace.cs` y `Features/CitaPorEnlaceHandler.cs`: agregar los campos nuevos de `CitaDelEnlace`.
- [x] `.../Agenda/Contracts/HorariosLibres.cs`, `Features/HorariosLibresHandler.cs`, `Features/ICitasActivas.cs` e `Infrastructure/CitasActivas.cs`: agregar `Aparte`.
- [x] `.../UI/Componentes/SelectorDeDiaYHora.razor` (nuevo) y `UI/Pages/Reserva/DiaYHora.razor`: extraer el selector sin cambiar el HTML de Día y hora.
- [x] `.../UI/Pages/Gestion/PanelDentroDelUmbral.razor` (nuevo): el panel ámbar con la tarjeta.
- [x] `.../UI/Pages/Gestion/TuCita.razor`: agregar "Reprogramar" y su nota, o el panel.
- [x] `.../UI/Pages/Gestion/Reprogramar.razor` (nuevo, `@page "/c/{Token}/reprogramar"`): GET y POST según Boundaries.
- [x] `.../UI/Pages/Gestion/CitaReprogramada.razor` (nuevo): la confirmación.
- [x] `wwwroot/css/consultorio.css`: el panel ámbar, "Tu hora actual" y lo que falte, con los tokens existentes.
- [x] `tests/.../IntegrationTests/HorariosLibresTests.cs`: con `Aparte`, la hora de esa Cita aparece libre; sin él, no.
- [x] `tests/.../IntegrationTests/ReprogramarDesdeTuCitaTests.cs` (nuevo): todas las filas de la matriz por HTTP, con encabezados, meta robots y 404, y que el HTML no contenga el CitaId.

**Acceptance Criteria:**
- Given la suite de Día y hora existente, when corre después del cambio, then pasa sin modificar ninguna prueba.
- Given una Cita reprogramada desde la página, when se abre su Enlace de gestión, then Tu cita muestra la nueva fecha y la bitácora tiene una entrada `Reprogramada` con actor Paciente.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 1, low 3, false 0, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Un día cuya única hora abierta es la de la propia Cita se ve disponible ("0 libres") y el mensaje de "no hay horarios" nunca aparece | medium | patch | Real: `LibresDe` excluye la hora actual, pero el chip, la celda y la elección de `_dia` usan `Horas.Count` crudo; con `Aparte` la hora propia vuelve en `Horas`. Contradice "sin contar como libre". |
| Con umbral 0, Tu cita dice "hasta la hora de tu cita de tu cita" | low | patch | Real: `HastaAntes(0)` ya incluye "de tu cita" y `ReglasDeReserva` acepta 0. Arreglo directo del texto. |
| Un POST a reprogramar una Cita de un tipo que no se agenda en línea sí la mueve, aunque el GET diga que no se puede | low | rechazado | Real, pero coherente con el `[ASSUMPTION]` aprobado en 3.2 (reprogramar no revisa el tipo) y con un horario válido según las reglas. Solo se alcanza con un formulario viejo o un POST hecho a mano. Arreglarlo agrega una rama de validación nueva. |
| Sin fecha en la dirección, la página abre en la fecha de la Cita aunque no tenga horarios y muestra el mensaje del cambio de tipo | low | patch | Real cuando la Cita quedó fuera de los tramos (agenda cambiada). Arreglo directo: usar la fecha de la Cita solo si viene en los días calculados. |

## Verification

**Commands:**
- `dotnet build`: debe compilar sin advertencias.
- `dotnet test tests/DraSelina.Consultorio.IntegrationTests --filter "FullyQualifiedName~ReprogramarDesdeTuCitaTests|FullyQualifiedName~TuCitaTests|FullyQualifiedName~HorariosLibresTests|FullyQualifiedName~TipoDeConsultaYDiaYHoraTests"`: deben pasar.
- `dotnet test`: debe pasar la suite completa.

**Manual checks:**
- Con el AppHost, reprogramar una Cita desde su enlace a 390 px y en escritorio.
