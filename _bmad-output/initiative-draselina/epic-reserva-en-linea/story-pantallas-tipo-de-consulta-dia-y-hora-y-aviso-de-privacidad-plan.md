---
title: 'Pantallas Tipo de consulta, Día y hora y Aviso de privacidad'
type: 'feature'
ticket: '5'
created: '2026-11-02'
status: 'built'
baseline_revision: '7d8888ad21953a18b6d1bc0037cc6efab6693dee'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-draselina/ux-appointment-booking/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Un Paciente no puede llegar a Tus datos sin escribir la dirección a mano: no existen los pasos 1 y 2 de la reserva, el botón "Agendar una cita" de Inicio no lleva a ningún lado y la casilla de Consentimiento no enlaza al Aviso de privacidad.

**Approach:** Las pantallas Tipo de consulta y Día y hora en móvil, según los mockups `Main` y `Horario`, en SSR estático y navegando solo con enlaces y la cadena de consulta; la página del Aviso de privacidad con su versión; y el botón de Inicio con destino.

## Boundaries & Constraints

**Always:**
- AD-11: SSR estático, sin scripts ni nada pedido a un CDN; en la cadena de consulta solo `tipo`, `fecha` y `hora`. Elegir un día y una hora son enlaces que recargan la página con esos parámetros.
- Tus datos se alcanza con `?tipo=<TipoDeVisitaId>&fecha=AAAA-MM-DD&hora=HH:mm` (contrato de la entrada 1).
- Tipo de consulta lista solo los tipos activos y reservables en línea, en su `orden`, sin precios (FR-1); "Primera consulta" llega preseleccionada (UJ-1).
- Decisión (Gerardo, 2026-11-02): descripciones de los tipos: "Primera consulta": "Si es tu primera vez con la doctora."; "Consulta de seguimiento": "Si ya te atendió y vienes a revisión." Se siembran con una migración de datos de Agenda (los ids fijos de `TiposDeVisitaSembrados`).
- Día y hora usa solo la consulta `HorariosLibres` (T-21): ninguna fecha ni hora escrita en la página. Horas en hora del centro de México (FR-2).
- Decisión (Gerardo, 2026-11-02): "Lleno" solo cuando el día tiene horarios en la rejilla del tipo y todos están ocupados por Citas; un día sin ningún horario posible para el tipo (hoy ya pasado con anticipación 0, o un tipo que no cabe en el tramo) no aparece. `CalculadoraDeDisponibilidad` y `HorariosLibres` cambian para distinguirlo.
- Sin horarios en toda la ventana: un mensaje único con el WhatsApp de `IContenidoApi` (EXPERIENCE.md, State Patterns).
- Decisión (Gerardo, 2026-11-02): el WhatsApp todavía no se conoce. `IContenidoApi` lo expone como opcional y hoy vacío; sin número, el mensaje dice "escríbenos por WhatsApp" sin número, y al tenerlo basta con poner el valor en Contenido.
- El Aviso de privacidad es una página aparte (`/aviso-de-privacidad`) que muestra la versión vigente de `IContenidoApi`; la casilla de Consentimiento de Tus datos y el pie del layout público enlazan a ella.
- El botón "Agendar una cita" de Inicio pasa a ser un enlace a Tipo de consulta, con el mismo aspecto.
- Decisión (Gerardo, 2026-11-02): el Aviso de privacidad es una página provisional breve que dice que el aviso está en revisión y nombra al responsable (Dra. Selina Medina Castillo), la finalidad (agendar y gestionar tu cita) y la dirección del consultorio, con la versión `2026-11-borrador`; no es un texto legal y se reemplaza por el revisado antes del primer Paciente.

**Never:**
- Escritorio en dos columnas (entrada 12), regreso desde Tus datos con datos conservados (entrada 8), anti-bots (entrada 11).
- Precios, cuentas o pasos extra.
- Escribir en la página el horario del encabezado del mockup `Main` ("Lun–Vie · 3:30–6:00 pm"): sale de la Agenda base y lo resuelve la épica del sitio; aquí se omite.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Inicio | Tocar "Agendar una cita" | Tipo de consulta, Primera consulta elegida | — |
| Tipo → horarios | Elegir Consulta de seguimiento y "Ver horarios disponibles" | Día y hora de ese tipo | — |
| Día y hora | Base sembrada, sin Citas | Días hábiles de la ventana; al elegir uno, sus horas 3:30 a 5:30 pm | — |
| Día lleno | Todas las horas de un día con Cita | El día se ve "Lleno" y no se puede elegir | — |
| Día sin horario posible | Tipo que no cabe en el tramo de una custom | Ese día no aparece | — |
| Sin horarios en la ventana | Ningún día con horas libres | Mensaje único con el WhatsApp | — |
| Elegir hora | Día y hora elegidos | Resumen y "Continuar" a Tus datos con `tipo`, `fecha`, `hora` | — |
| Tipo inválido en la dirección | `tipo` inexistente o no reservable | Mensaje y enlace a Tipo de consulta | Sin error técnico |
| Aviso de privacidad | Enlace de la casilla o del pie | Página con el texto y su versión | — |

</frozen-after-approval>

## Code Map

Rutas bajo `src/DraSelina.Consultorio.Web/` salvo que digan otra cosa.

- `UI/Pages/Inicio.razor` -- el botón "Agendar una cita" (`btn btn-primary dsm-accion-principal`, comentario que anuncia esta entrada); `tests/.../BaseVisualTests.cs` lo busca con una expresión regular (`BotonAgendar()`): ajustarla al enlace.
- `UI/Pages/Reserva/TusDatos.razor` -- patrón de página SSR con `[SupplyParameterFromQuery]`, guarda de fechas con `IClock`, clases `dsm-reserva`, `dsm-paso`, `dsm-titulo-de-paso`, `dsm-aviso`; la casilla de Consentimiento hoy tiene "Aviso de privacidad" en negritas: pasa a enlace.
- `UI/Pages/Reserva/FormatoDeCita.cs` -- fechas y horas en español sin depender de la cultura; reutilizar.
- `UI/Layout/LayoutPublico.razor` -- agregar el pie con el enlace al Aviso.
- `wwwroot/css/consultorio.css` -- tokens `--dsm-*`, clases `dsm-*` de la entrada 1.
- `Modules/Agenda/Contracts/HorariosLibres.cs` -- `IQuery<IReadOnlyList<DiaConHorarios>>`, `DiaConHorarios(Fecha, Horas)`.
- `Modules/Agenda/Domain/CalculadoraDeDisponibilidad.cs` -- `Calcular`: hoy agrega toda fecha con tramos; cambiar para omitir la fecha cuando su rejilla, después del corte de `now`, no tiene ningún inicio (decisión de Boundaries); `CalculadoraDeDisponibilidadTests` y `HorariosLibresTests` cubren el cambio.
- `Modules/Agenda/Domain/TipoDeVisita.cs`, `ITiposDeVisita.cs` -- `SeAgendaEnLinea`, `Orden`, `Descripcion`; el puerto hoy solo obtiene uno: la lista va por un puerto de lectura en `Features`.
- `Modules/Contenido/Contracts/IContenidoApi.cs`, `Contenido/ContenidoApi.cs` -- datos del consultorio y versión del aviso; agregar el WhatsApp y el texto del aviso.

## Tasks & Acceptance

**Execution:**
- [x] `Modules/Agenda/Domain/CalculadoraDeDisponibilidad.cs` + pruebas -- omitir fechas sin ningún inicio posible; mantener las llenas con lista vacía.
- [x] `Modules/Agenda/Contracts/TiposDeVisitaReservables.cs`, `Features/` (handler y puerto de lectura), `Infrastructure/` -- consulta de los tipos activos y reservables en línea, en orden.
- [x] `Modules/Contenido/` -- WhatsApp y texto del aviso según las decisiones de este plan.
- [x] `Modules/Agenda/Infrastructure/TiposDeVisitaSembrados.cs` + migración `DescribirTiposDeVisita` -- las dos descripciones de Boundaries.
- [x] `UI/Pages/Reserva/TipoDeConsulta.razor` (`/reservar`) -- mockup `Main`.
- [x] `UI/Pages/Reserva/DiaYHora.razor` (`/reservar/horario?tipo=&fecha=&hora=`) -- mockup `Horario`: días como enlaces con su estado, horas del día elegido como enlaces, resumen y "Continuar".
- [x] `UI/Pages/AvisoDePrivacidad.razor` (`/aviso-de-privacidad`), `UI/Layout/LayoutPublico.razor`, `TusDatos.razor` -- página, pie y enlace de la casilla.
- [x] `UI/Pages/Inicio.razor`, `BaseVisualTests.cs` -- el botón pasa a enlace.
- [x] `wwwroot/css/consultorio.css` -- estilos de los pasos 1 y 2 y del aviso.
- [x] `tests/DraSelina.Consultorio.IntegrationTests/` -- pruebas de página para cada fila de la matriz contra la base migrada.

**Acceptance Criteria:**
- Given el AppHost arriba y un teléfono a 390 px, when se toca "Agendar una cita", se elige Primera consulta, un día y una hora de la Agenda base, then se llega a Tus datos con ese horario.
- Given Tus datos, when se toca "Aviso de privacidad" en la casilla, then se abre el Aviso con su versión.
- Given la solución, when corren todas las pruebas, then pasan, y ninguna página nueva carga scripts.

## Implementation Notes

- Tipo de consulta: las tarjetas son radios de un `<form method="get" action="/reservar/horario">` (sin scripts; la dirección solo lleva `tipo`). Se eligió sobre "una tarjeta = un enlace" por el piso de accesibilidad (radios en `fieldset` con leyenda) y porque un toque elige sin recargar. `?tipo=` en `/reservar` preselecciona ese tipo: así "Regresar" desde Día y hora conserva lo elegido.
- Día y hora: sin `fecha` válida se elige el primer día con horas; la `hora` solo cuenta si es del día que pide la dirección y está libre. La selección de día y hora se expone con `aria-current="true"` (son enlaces, no botones). "Continuar" sin hora es un `span` deshabilitado. "Hasta el …" es la última fecha que devuelve `HorariosLibres`. Chip en singular: "1 libre".
- `IContenidoApi`: `WhatsApp` (nulo hoy) y `TextoDelAvisoDePrivacidad`. Con número, `MensajeDeWhatsApp` enlaza a `https://wa.me/52<10 dígitos>`.
- La casilla de Tus datos abre el Aviso con `target="_blank"` para no perder lo escrito.
- El anillo del mockup (`box-shadow`) va como `outline`: la hoja no usa sombras.

## Plan Change Log

## Review Triage Log

**Pasada 1 (quick), 2026-11-02.** Veredictos: high 0, medium 2, low 4, false 0, maybe-false 0.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | El chip "Lleno" lleva `aria-label`/`aria-disabled` en un `<span>` sin rol: el anuncio de fecha y estado no es fiable y dice "sin horarios" en lugar de "Lleno" | medium | patch | EXPERIENCE.md, Accessibility Floor; ARIA 1.2 no da nombre a elementos genéricos. Texto oculto con la fecha junto al "Lleno" visible |
| 2 | La fila de días cubre varios meses pero cada chip solo dice día de la semana y número: "Mar 1" de diciembre parece de noviembre | medium | patch | Con la siembra la ventana va de noviembre a enero; el encabezado solo nombra el mes del día elegido. Mes corto en el primer chip de cada mes |
| 3 | Al elegir un día fuera del ancho visible, la recarga deja la fila al inicio y el chip elegido fuera de vista | low | patch | Real en 390 px después del quinto chip; corrección directa con id y fragmento en el enlace |
| 4 | `Un_dia_lleno_por_una_Cita_del_consultorio_fuera_de_la_rejilla_tambien_aparece_vacio` usa una Cita que empieza en la rejilla | low | patch | El nombre no describe lo que prueba; se renombra |
| 5 | `SinScripts` no revisa `_framework/`, a diferencia de `BaseVisualTests` | low | patch | Corrección directa |
| 6 | La fila "Tipo → horarios" no recorre el formulario de Tipo de consulta | low | rechazado | `Agendar_una_cita_…` ya comprueba el `action` del formulario GET y el `name` del radio; el navegador arma la dirección por sí mismo |

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` -- expected: todas pasan (Docker arriba).

**Manual checks (if no CLI):**
- En el teléfono a 390 px, Tipo de consulta y Día y hora se parecen a los mockups `Main` y `Horario`.
