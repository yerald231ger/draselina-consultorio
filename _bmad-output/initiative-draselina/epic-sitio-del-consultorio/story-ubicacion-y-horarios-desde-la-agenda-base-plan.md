---
title: 'Ubicación y horarios desde la Agenda base'
type: 'feature'
ticket: '1'
created: '2026-11-17'
status: 'built'
baseline_revision: '6467c2cc114d7797b1b472a5d4dd56e0da8e3739'
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

**Problem:** `/` es una portada mínima sin el encabezado del sitio ni Ubicación y horarios, y nada público puede leer la Agenda base: `SemanaTipo` exige Administrador. El sitio no puede mostrar los días y horas reales (CAP-21, Done when 3).

**Approach:** Camino trazador de la épica 7: una consulta pública `HorarioDeAtencion` en Agenda, un formateador de la interfaz que agrupa los días, y `Inicio.razor` rehecho con el encabezado del sitio, una portada mínima y la sección Ubicación y horarios con datos de Contenido y el iframe del mapa.

## Boundaries & Constraints

**Always:** SSR estático sin `blazor.web.js` (AD-11); datos del consultorio solo por `IContenidoApi` (AD-15); la consulta es de solo lectura, sin `IRequiereAdministrador`, y devuelve los siete días de lunes a domingo como `SemanaTipo`; sin Agendas custom; horas en 12 h como `FormatoDeCita.Hora`; las Decision de la épica 7 del 2026-11-17 (horario, mapa); el encabezado con anclas y "Agendar cita" solo en `/` (el Aviso lo adopta en la 7.3); WhatsApp nulo → "Escríbenos por WhatsApp" con `MensajeDeWhatsApp`. Decision (Gerardo, 2026-11-17): la regla "Nada de terceros" tiene una excepción con nombre: en `src` solo el iframe `https://www.google.com/maps?...&output=embed`, y en `href` solo `https://www.google.com/maps/` y `https://wa.me/`; cualquier otro tercero hace fallar la prueba; README y `BaseVisualTests` lo dicen así.

**Never:** tocar `SemanaTipo` ni su handler; cambiar el encabezado de la reserva o la gestión; los datos de la doctora, Sobre mí, Padecimientos, franja final o pie nuevo (7.2, 7.3); título SEO, JSON-LD, robots o sitemap (7.4); escribir el horario fijo en el markup.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Rango | lun–vie 15:30–18:00 (sembrada) | "Lunes a viernes" · "3:30 a 6:00 pm, con cita" | — |
| Lista | lun, mié, vie 15:30–18:00 | "Lunes, miércoles y viernes" · "3:30 a 6:00 pm, con cita" | — |
| Dos grupos | lun–vie 15:30–18:00, sáb 10:00–13:00 | dos grupos en orden de la semana; el sábado "10:00 am a 1:00 pm, con cita" | — |
| Dos Tramos | mar 10:00–13:00 y 16:00–18:00 | "Martes" · "10:00 am a 1:00 pm y 4:00 a 6:00 pm, con cita" | — |
| Semana vacía | ningún Tramo | sin grupos; la sección dice "Por ahora no hay horario de atención en línea." y el contacto por WhatsApp | — |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/SemanaTipo.cs`, `CambioDeAgendaBase.cs:15` (`DiaDeLaBase`), `DiasDeLaAgenda.cs:69` (`TramoDeLaAgenda`) -- tipos a reusar; no cambiar.
- `Modules/Agenda/Contracts/VentanaDeReservaPublica.cs` + `Features/VentanaDeReservaPublicaHandler.cs` -- modelo de consulta pública a copiar.
- `Modules/Agenda/Features/SemanaTipoHandler.cs` -- lectura a reusar: `IAgendaDelRango.LeerAsync(DateOnly.MinValue, DateOnly.MinValue)` + `TramosBase(dia)` y `DeLunesADomingo`.
- `Modules/Agenda/Infrastructure/Registro.cs:73,86` -- registro de handlers.
- `UI/Pages/Inicio.razor` -- hoy portada mínima; se rehace.
- `UI/Layout/LayoutPublico.razor` -- encabezado y pie de reserva y gestión; no cambia su encabezado.
- `UI/Pages/Reserva/FormatoDeCita.cs` -- `Hora(TimeOnly)`, días en español.
- `UI/Componentes/MensajeDeWhatsApp.razor`, `EnlaceDeWhatsApp.razor` -- WhatsApp con y sin número.
- `Modules/Contenido/Contracts/IContenidoApi.cs` -- `DatosDelConsultorio` (Lugar, Direccion, Ciudad, EnlaceDelMapa), `WhatsApp`.
- `wwwroot/css/consultorio.css` -- `.dsm-encabezado*`, `.dsm-marca*`, `.dsm-inicio`, `.dsm-accion-principal`, `.dsm-pie*`.
- `tests/DraSelina.Consultorio.IntegrationTests/BaseVisualTests.cs` -- `BotonAgendar` (busca "Agendar una cita") y `Ni_Inicio_ni_la_hoja_propia_piden_nada_a_terceros` (ninguna URL absoluta en `href`/`src`) chocan con este plan; se ajustan con la excepción con nombre.
- `tests/.../CambiarAgendaBaseTests.cs:142` (`Sin_sesion_vuelve_no_autorizado`), `ReservaEnLineaTests.SinSesion` -- patrón de prueba sin sesión.
- `README.md:598` -- regla "Nada de terceros".
- Mockup `ux-appointment-booking/imports/mockup-canvas/SitioInicio.dc.html:19-36,120-149` -- encabezado con `flex-wrap` (en teléfono la nav baja de renglón) y la sección Ubicación.

## Tasks & Acceptance

**Execution:**
- [ ] `Modules/Agenda/Contracts/HorarioDeAtencion.cs` -- `public sealed record HorarioDeAtencion : IQuery<IReadOnlyList<DiaDeLaBase>>;` con su doc -- la lectura pública.
- [ ] `Modules/Agenda/Features/HorarioDeAtencionHandler.cs` + `Infrastructure/Registro.cs` -- misma lectura que `SemanaTipoHandler`, reusando `DeLunesADomingo`; registrar -- sin duplicar la regla del rango.
- [ ] `UI/Pages/Sitio/HorarioAgrupado.cs` -- `Agrupar(IReadOnlyList<DiaDeLaBase>) → IReadOnlyList<GrupoDeHorario(string Dias, string Horas)>` según la matriz y Design Notes.
- [ ] `UI/Layout/LayoutSitio.razor` + `UI/Componentes/PieDelSitio.razor` -- encabezado del sitio (marca, `nav aria-label="Principal"` con anclas `#inicio`, `#sobre-mi`, `#padecimientos`, `#ubicacion`, botón "Agendar cita" a `/reservar`); el pie actual se extrae a un componente que usan los dos layouts.
- [ ] `UI/Pages/Inicio.razor` -- `@layout LayoutSitio`; secciones `#inicio` (portada mínima actual con "Agendar cita en línea") y `#ubicacion` (Lugar, Dirección, grupos del horario, WhatsApp, "Cómo llegar" con `EnlaceDelMapa`, iframe `loading="lazy"` `title="Mapa del consultorio"`); `#sobre-mi` y `#padecimientos` quedan para la 7.2 sin enlaces rotos: sus anclas existen como secciones vacías con encabezado.
- [ ] `wwwroot/css/consultorio.css` -- estilos del encabezado del sitio, la sección y el mapa con los tokens existentes; 44 px táctiles.
- [ ] `tests/DraSelina.Consultorio.UI.Tests/HorarioAgrupadoTests.cs` -- los cinco casos de la matriz y el de días sueltos mezclados.
- [ ] `tests/DraSelina.Consultorio.IntegrationTests/SitioInicioTests.cs` -- HorarioDeAtencion sin sesión devuelve la base sembrada; tras cambiar la base con `CambiarAgendaBase` (sesión de Administrador), `/` muestra el sábado; `/` trae "Agendar cita" a `/reservar` dentro de `<header>`, las cuatro anclas, sin `<script`.
- [ ] `BaseVisualTests.cs` + `README.md` -- la prueba de terceros acepta solo la excepción con nombre; `BotonAgendar` pasa a "Agendar cita" dentro de `<header>`; el README describe la excepción.

**Acceptance Criteria:**
- Given la base sembrada, when se pide `/` sin sesión, then la sección Ubicación y horarios dice "Lunes a viernes" y "3:30 a 6:00 pm, con cita", con lugar y dirección de Contenido.
- Given el Administrador abre el sábado 10:00–13:00, when se vuelve a pedir `/`, then aparece "Sábado" con "10:00 am a 1:00 pm, con cita".
- Given `/reservar/...` o `/c/...`, when se pide, then su encabezado sigue siendo el de `LayoutPublico`, sin anclas.
- Given `/`, when se lee el HTML, then no hay `<script` ni `blazor.web.js` y el iframe lleva `loading="lazy"` y `title`.

## Implementation Notes

- Implementado directo en la sesión: el subagente implementador fue bloqueado por el clasificador de permisos.
- `SemanaTipoHandler.LeerAsync` (internal static) es la lectura que comparten `SemanaTipo` y `HorarioDeAtencion`.
- Las anclas del encabezado son `/#inicio`, `/#sobre-mi`, etc., para que funcionen también desde el Aviso de privacidad (7.3).
- El botón de la portada dice "Agendar cita en línea" (mockup); `TipoDeConsultaYDiaYHoraTests.EnlaceAgendar` lo sigue a `/reservar`.
- Sorpresa: `WebNoMigraTests` pedía `/` en una base vacía; ahora `/` lee la Agenda base y daba 500. La prueba pide `/aviso-de-privacidad`, que no toca la base; su intención (la web no migra al arrancar) se conserva.
- Blazor codifica el texto dinámico (`M&#xE9;dica`, `&amp;`): `SitioInicioTests.Seccion` decodifica antes de comparar.
- Prueba bUnit extra `UI.Tests/SitioInicioTests` para la fila "Semana vacía" en pantalla.
- `dotnet build`: 0 advertencias; `dotnet test`: 1143 pasan.
- Revisión: parche de la pasada 1 (sin etiquetas Dirección y Horario); vuelven a pasar las 1143.

## Plan Change Log

## Review Triage Log

**Pasada 1 (2026-11-17), lente quick hecha en la sesión** por decisión de Gerardo, porque el clasificador de permisos bloqueó al subagente revisor; no es una revisión independiente. Veredictos: high 0, medium 0, low 1, false 4, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| `Inicio.razor`: las etiquetas "Dirección" y "Horario" no están en el mockup (Done when 1, "como en los mockups") | low | patch | El mockup pone el lugar como h2, la dirección sola y los días en negritas; solo "WhatsApp" lleva etiqueta. Se borraron las dos etiquetas y pasan las 1143 pruebas. |
| `HorarioAgrupado.Unir` falla con una lista vacía (`piezas[^1]`) | false | — | `Dias` solo recibe grupos con al menos un día y `Horas` grupos con al menos un Tramo, porque `Agrupar` filtra `Tramos.Count > 0`. |
| `HorarioAgrupado.Tramo`: el sufijo de un Tramo que cruza el mediodía | false | — | 10:00–13:00 da "10:00 am a 1:00 pm" y 12:00–14:00 da "12:00 a 2:00 pm"; el criterio es `Hour < 12`, el mismo que `FormatoDeCita.Hora`. Lo cubren las pruebas unitarias. |
| `/` en teléfono: ¿"Agendar cita" queda debajo del pliegue al bajar la navegación de renglón? | false | — | El encabezado mide marca (44 px), navegación (≤ 2 renglones de 44 px) y botón (44 px), con huecos de 12 px: unos 230 px, dentro de una pantalla de 390 × 700. Lo confirma el recorrido de la 7.5. |
| `HorarioDeAtencion` sin `IRequiereAdministrador` expone la semana tipo | false | — | Es lo pedido (Decision de la épica 7) y solo lee la base, no las Citas; `SemanaTipo` sigue exigiendo Administrador (`CambiarAgendaBaseTests.Sin_sesion_vuelve_no_autorizado` pasa). |

## Design Notes

Agrupar: los días con la misma lista de Tramos forman un grupo; los grupos van en el orden de su primer día (lunes primero). Dentro de un grupo, cada racha de días seguidos de 3 o más se escribe "Lunes a viernes"; de 2, "Sábado y domingo"; un día suelto, su nombre; las piezas se unen con ", " y la última con " y " ("Lunes a miércoles y viernes"). `[ASSUMPTION: rachas de 2 con "y" y mezcla de rachas; la regla aprobada solo nombra rango y lista]`. Horas: cada Tramo "3:30 a 6:00 pm" (sufijo una vez si ambos son del mismo lado del mediodía; si no, "10:00 am a 1:00 pm"), Tramos unidos con " y ", y ", con cita" al final.

## Verification

**Commands:**
- `./dotnet.sh build` -- sin advertencias.
- `./dotnet.sh test` -- todas pasan (necesita Docker).

**Manual checks (if no CLI):**
- Con el AppHost, `/` a 390 px: "Agendar cita" visible sin desplazarse; el iframe del mapa carga al bajar.
