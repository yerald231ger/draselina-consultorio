---
title: 'Franja final, pie con cédulas y Aviso bajo el encabezado del sitio'
type: 'feature'
ticket: '3'
created: '2026-11-17'
status: 'built'
baseline_revision: '8e6ed5febe95c14d21052e0161881d1d20744990'
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

**Problem:** `/` termina en Ubicación y horarios sin la franja final del mockup; el pie solo dice nombre y especialidad, escritos fijos, sin las cédulas (FR-27); y el Aviso de privacidad usa `LayoutPublico`, sin "Agendar cita" en el encabezado (FR-28).

**Approach:** Una franja "Agenda tu consulta en menos de un minuto" con "Agendar cita" al final de `/`; `PieDelSitio` con nombre, especialidad y cédulas de `DatosDeLaDoctora` (entrada 7.2) y el enlace al Aviso; el Aviso pasa a `LayoutSitio`.

## Boundaries & Constraints

**Always:** textos del mockup `SitioInicio`: franja con h2 "Agenda tu consulta en menos de un minuto", la frase "Elige el día y la hora que te acomoden. Sin llamadas ni cuentas; tu cita queda confirmada al instante." y el botón "Agendar cita" a `/reservar`; pie "Dra. Selina Medina Castillo · Traumatología y Ortopedia · Céd. Prof. 12093135 · Céd. Esp. 14899218" y "Aviso de privacidad" a `/aviso-de-privacidad`; los datos del pie solo por `IContenidoApi` (AD-15); SSR estático sin scripts (AD-11); 44 px táctiles; la Assumption de la épica: el encabezado del sitio va en `/` y en el Aviso, la reserva y la gestión conservan el suyo. Decision (Gerardo, 2026-11-17): un solo pie con cédulas en todas las páginas públicas, también reserva, gestión e inicio de sesión.

**Never:** el logotipo del mockup (`/_blob/...`, no está en el repositorio; el encabezado de la 7.1 tampoco lo lleva); cambiar el texto o la versión del Aviso; título SEO, JSON-LD, robots o sitemap (7.4); cambiar el encabezado de `LayoutPublico`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Inicio | `GET /` | Tras `#ubicacion`, la franja con su h2 y "Agendar cita" a `/reservar`; después el pie | — |
| Aviso | `GET /aviso-de-privacidad` | `<header>` del sitio con las anclas `/#…` y "Agendar cita" a `/reservar`; el texto y la versión del Aviso; sin `<script` | — |
| Pie | `/` y `/aviso-de-privacidad` | El pie trae nombre, especialidad, "Céd. Prof. 12093135", "Céd. Esp. 14899218" y el enlace al Aviso | — |
| Reserva | `GET /reservar` | Encabezado de `LayoutPublico` sin anclas; el mismo pie con las cédulas | — |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/UI/Componentes/PieDelSitio.razor` -- hoy nombre y especialidad fijos y el enlace al Aviso; lo usan `LayoutPublico` y `LayoutSitio`.
- `UI/Layout/LayoutSitio.razor` -- encabezado del sitio con marca de Contenido, anclas `/#inicio`… y "Agendar cita"; no cambia.
- `UI/Layout/LayoutPublico.razor` -- layout por omisión (`UI/Routes.razor:9`) de reserva, gestión e Identidad (`Identidad/Paginas/_Imports.razor:11`); su encabezado no cambia.
- `UI/Pages/AvisoDePrivacidad.razor` -- sin `@layout`; recibe `@layout LayoutSitio`. "Volver al inicio" se queda.
- `UI/Pages/Inicio.razor` -- la franja va después de `#ubicacion`, como sección propia de borde a borde (patrón de `.dsm-padecimientos`).
- `Modules/Contenido/Contracts/IContenidoApi.cs` -- `DatosDeLaDoctora` (Nombre, Especialidad, CedulaProfesional, CedulaDeEspecialidad).
- `wwwroot/css/consultorio.css` -- `.dsm-pie*` (fila con `flex-wrap`), `.dsm-padecimientos` como modelo de banda; tokens `--dsm-primary`, `--dsm-on-primary`, `--dsm-on-primary-muted`, `--dsm-surface`.
- Mockup `_bmad-output/initiative-draselina/ux-appointment-booking/imports/mockup-canvas/SitioInicio.dc.html:150-167` -- franja vino con botón blanco; pie blanco.
- `tests/DraSelina.Consultorio.IntegrationTests/SitioInicioTests.cs` -- `Encabezado()`, `AgendarCitaAReservar()`, `Seccion()` (decodifica entidades) para reusar.
- `tests/.../BaseVisualTests.cs` -- decodifica el HTML de `/` antes de buscar el nombre; `WebNoMigraTests` pide `/aviso-de-privacidad` en una base vacía y debe seguir pasando (el layout no toca la base).

## Tasks & Acceptance

**Execution:**
- [x] `UI/Componentes/PieDelSitio.razor` -- `@inject IContenidoApi`; nombre, especialidad y cédulas de `DatosDeLaDoctora` separados por " · "; enlace al Aviso -- FR-27, AD-15.
- [x] `UI/Pages/Inicio.razor` -- sección final `dsm-franja` con `aria-labelledby` al h2, la frase y "Agendar cita" a `/reservar`.
- [x] `UI/Pages/AvisoDePrivacidad.razor` -- `@layout LayoutSitio` y su comentario -- FR-28.
- [x] `wwwroot/css/consultorio.css` -- banda vino de borde a borde con texto `--dsm-on-primary`, frase `--dsm-on-primary-muted`, botón blanco con texto vino (54 px de alto, como el mockup); el texto del pie se parte en renglones en el teléfono.
- [x] `tests/DraSelina.Consultorio.IntegrationTests/SitioInicioTests.cs` -- las filas de la matriz: franja después de `#ubicacion` y antes de `<footer`; Aviso con `<header>` del sitio, anclas y "Agendar cita", sin `<script`; pie con las cédulas en `/` y en el Aviso; la fila Reserva.

**Acceptance Criteria:**
- Given un teléfono de 390 px, when se abre `/aviso-de-privacidad`, then "Agendar cita" está en el encabezado y lleva a `/reservar`.
- Given `/`, when se lee de arriba abajo, then la última sección antes del pie es la franja con su botón.
- Given Contenido con otra cédula, when se pide `/`, then el pie muestra la cédula nueva, sin texto fijo.

## Implementation Notes

- Implementado directo en la sesión: el clasificador de permisos bloqueó al subagente implementador.
- El pie de `LayoutPublico` deja 40 px de margen arriba; tras la franja vino se anula con `main:has(> .dsm-franja:last-child) + .dsm-pie`, para que la banda toque el pie como en el mockup.
- El botón blanco de la franja es `.dsm-franja__agendar`, con las variables de Bootstrap como `.dsm-boton-secundario`, no `btn-primary`; así `BaseVisualTests.BotonAgendar` y `Encabezado()` siguen apuntando al botón del encabezado.
- Prueba bUnit extra `UI.Tests/PieDelSitioTests` para el AC de "otra cédula en Contenido".
- `dotnet build`: 0 advertencias; `dotnet test`: 1156 pasan.

## Plan Change Log

## Review Triage Log

**Pasada 1 (2026-11-17), lente quick con un subagente revisor independiente.** Veredictos: high 0, medium 0, low 5, false 1, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| `.dsm-franja__frase` usa 15 px y el mockup 16 px | low | patch | Ahora `font-size: 16px`. |
| Entre la franja y el pie queda el `border-top` de 1 px de `.dsm-pie`; el mockup no lo tiene | low | patch | La misma regla `main:has(...) + .dsm-pie` suma `border-top: 0`. |
| La prueba de la franja no revisa su frase | low | patch | Se agregó la aserción con la frase del mockup. |
| El comentario de `AvisoDePrivacidad.razor` pasa de 120 columnas | low | patch | Se reacomodó a 120. La línea del `<span>` en `PieDelSitio.razor` es marcado, no prosa, y se queda así. |
| `PieDelSitioTests.ContenidoConDoctora` duplica el falso `ContenidoDePrueba` de `SitioInicioTests` | low | reject | Es privado en cada archivo; compartirlo exige un archivo de apoyo nuevo, no una corrección directa. Queda como candidato al Refactor sweep (7.7). |
| La prueba del Aviso solo busca "Versión " | false | reject | El texto y la versión exacta ya los revisa `TipoDeConsultaYDiaYHoraTests.El_Aviso_de_privacidad_se_abre_desde_la_casilla_y_desde_el_pie_con_su_version`, que sigue pasando con el layout nuevo. |

Después de los parches: `dotnet build` sin advertencias y pasan las 1156 pruebas.

## Verification

**Commands:**
- `PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet ./dotnet.sh build` -- sin advertencias (el SDK 10.0.4xx está en `~/.dotnet`).
- `PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet ./dotnet.sh test` -- todas pasan (necesita Docker).

**Manual checks (if no CLI):**
- Con el AppHost, la franja y el pie de `/` a 390 px y 1280 px se parecen al mockup; en el Aviso a 390 px "Agendar cita" se ve sin desplazarse.
