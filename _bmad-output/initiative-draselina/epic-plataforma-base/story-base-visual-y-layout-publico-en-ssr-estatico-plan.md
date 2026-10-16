---
title: 'Base visual y layout público en SSR estático'
type: 'feature'
ticket: '8'
created: '2026-10-15'
status: done
baseline_revision: '4c28a4ce3b01d60fad14417bfe9fd4fa55504186'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/initiative-draselina/ux-appointment-booking/DESIGN.md'
  - '{project-root}/README.md'
warnings: ['oversized']
deferred: []
---

<intent-contract>

## Intent

**Problem:** La web pinta HTML sin estilo: no hay Bootstrap, ni la fuente, ni los tokens de `DESIGN.md`, ni un layout público. Las historias de reserva y del sitio necesitan esa base para no construirla cada una (AD-11, P10).

**Approach:** Bootstrap 5.3.8 y Montserrat servidos desde `wwwroot` del proyecto Web; una hoja propia con los tokens de `DESIGN.md` como variables CSS que mandan sobre las de Bootstrap; un layout público móvil primero en `UI/` y la página de Inicio como muestra, todo en SSR estático, sin script de Blazor.

## Boundaries & Constraints

**Always:**
- Decisiones de Gerardo (2026-10-15, notas de la épica): Bootstrap y Montserrat locales, con sus licencias (MIT y OFL) junto a los archivos; ninguna petición a CDN ni a Google Fonts. Una sola línea nueva en el host: `app.MapStaticAssets();`. Encabezado sin logotipo.
- Archivos estáticos en `src/DraSelina.Consultorio.Web/wwwroot/`: `lib/bootstrap/` (solo `bootstrap.min.css` 5.3.8, sin JS), `fonts/montserrat/` (woff2 latín 400/500/600/700) y una hoja propia (p. ej. `css/consultorio.css`). Se descargan de la distribución oficial (npm de `bootstrap@5.3.8` y `@fontsource/montserrat` vía jsDelivr, o equivalente); el README dice de dónde y con qué versión.
- La hoja propia define cada color, radio, espaciado y tipografía de la cabecera YAML de `DESIGN.md` como variable CSS (`--dsm-…`, nombres derivados de las claves), y fija sobre ellas las variables de Bootstrap que importan aquí (`--bs-body-font-family`, `--bs-body-bg`, `--bs-body-color`, `--bs-link-color`, `--bs-link-hover-color`, borde) y el botón principal (`.btn-primary`: vino, hover `primary-hover`, texto blanco, 54 px, radio `lg`, tipografía `button`). Respaldo de fuente: `'Segoe UI', system-ui, sans-serif`. Sin sombras ni degradados.
- Layout público (`UI/Layout/`, nombre libre) como layout por defecto de `Routes.razor`: encabezado sobre `surface` con borde inferior `border`; nombre "Dra. Selina Medina Castillo" (15 px, 700, vino) y "Traumatología y Ortopedia" (rótulo: 11–12 px, 600, mayúsculas, 0.14em, `eyebrow`); contenedor máximo 1160 px, margen lateral 20 px en móvil; objetivo táctil ≥ 44 px.
- Inicio (`/`): rótulo "Traumatología y Ortopedia", nombre en `display` (46 px, 600, vino), un párrafo en cuerpo y el botón principal "Agendar una cita", ancho completo en móvil. `[ASSUMPTION]` Sin destino todavía, el botón es `<button type="button" class="btn btn-primary">`; la historia de reserva lo cambia por un enlace.
- `App.razor` enlaza las hojas con `@Assets["…"]` (huella de `MapStaticAssets`) y no incluye `blazor.web.js` ni ningún otro script: la página se entrega completa sin conexión interactiva (AD-11). `lang="es-MX"` se queda.
- Build con 0 advertencias; las pruebas existentes (111) siguen pasando; las de arquitectura no se tocan.

**Never:**
- Nada de JS de Bootstrap, iconos, logotipo, onda decorativa, navegación del sitio, WhatsApp, pie de página ni contenido del sitio (épica de Contenido).
- No tocar el host salvo `app.MapStaticAssets();`. No paquetes NuGet nuevos.
- No modo Interactive Server ni `@rendermode` en ninguna página.
- No cambiar `DESIGN.md` ni los documentos de arquitectura.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Página pública | `GET /` contra la web del arnés | 200, HTML con el encabezado, el nombre en `h1`, el botón "Agendar una cita" y los `<link>` a Bootstrap y a la hoja propia | — |
| Sin script interactivo | `GET /` | El HTML no contiene `<script` (en particular, ni `blazor.web.js` ni `_framework/`) | — |
| Hojas servidas | `GET` de cada `href` de hoja que trae `/` | 200, `text/css`; la de Bootstrap dice 5.3.8; la propia define `--dsm-primary: #6B2A45` | — |
| Fuente servida | `GET` de cada woff2 que la hoja propia declara en `@font-face` | 200 para los cuatro pesos | — |
| Sin terceros | El HTML de `/` y la hoja propia | Ninguna URL absoluta `http(s)://` en `href`/`src`/`url(...)` | — |
| Tokens completos | Cabecera YAML de `DESIGN.md` (colors) | Cada color tiene su `--dsm-<clave>` con el mismo valor en la hoja propia | La prueba nombra el que falte o difiera |

</intent-contract>

## Code Map

- `src/DraSelina.Consultorio.Web/UI/App.razor` -- documento raíz: `<head>` con `HeadOutlet`, `<body>` con `Routes`. Aquí van los `<link>`; sin scripts.
- `src/DraSelina.Consultorio.Web/UI/Routes.razor` -- `Router` con `RouteView`; pasar a `RouteView DefaultLayout="typeof(...)"`.
- `src/DraSelina.Consultorio.Web/UI/Pages/Inicio.razor` -- hoy "Consultorio en construcción."; se reemplaza por la muestra.
- `src/DraSelina.Consultorio.Web/UI/_Imports.razor` -- `@namespace DraSelina.Consultorio.UI`; agregar `@using DraSelina.Consultorio.UI.Layout` (o el namespace del layout) si hace falta.
- `src/DraSelina.Consultorio.Web/Program.cs` -- `app.UseAntiforgery(); app.MapRazorComponents<App>();`: agregar `app.MapStaticAssets();` antes de `MapRazorComponents`. Nada más.
- `src/DraSelina.Consultorio.Web/DraSelina.Consultorio.Web.csproj` -- `<Using Remove="Microsoft.*" />` (afecta a `.cs`, no a `.razor`). `wwwroot/` no existe todavía.
- `tests/DraSelina.Consultorio.ArchitectureTests/` -- `CarpetasReservadas` = Kernel, BuildingBlocks, Modules, UI; `wwwroot/` no es carpeta reservada. Un `.razor` nuevo en `UI/Layout/` debe caer en la capa UI (namespace `DraSelina.Consultorio.UI.*`).
- `tests/DraSelina.Consultorio.IntegrationTests/WebNoMigraTests.cs:20-55` -- modelo de prueba HTTP: `web.CreateClient()` y `GetAsync("/")`. `Arnes.ArrancarWeb()` da la web real.
- `_bmad-output/initiative-draselina/ux-appointment-booking/imports/mockup-canvas/SitioInicio.dc.html` (encabezado y sección `#inicio`) y `SitioMovil.dc.html` (390 px) -- referencia visual, solo lectura.
- `README.md` -- agregar una sección corta: de dónde salen Bootstrap y Montserrat (versión, fuente, licencias), dónde están los tokens y cómo se usan, y la regla de no cargar nada de terceros.

## Tasks & Acceptance

**Execution:**
- [x] `src/DraSelina.Consultorio.Web/wwwroot/lib/bootstrap/` -- `bootstrap.min.css` 5.3.8 y `LICENSE` -- base de estilos local.
- [x] `src/DraSelina.Consultorio.Web/wwwroot/fonts/montserrat/` -- woff2 400/500/600/700 (latín) y `OFL.txt` -- fuente local.
- [x] `src/DraSelina.Consultorio.Web/wwwroot/css/consultorio.css` -- `@font-face`, tokens `--dsm-*`, variables de Bootstrap sobre los tokens, `.btn-primary`, layout público y tipografías usadas en Inicio.
- [x] `src/DraSelina.Consultorio.Web/Program.cs` -- `app.MapStaticAssets();`.
- [x] `src/DraSelina.Consultorio.Web/UI/App.razor`, `Routes.razor`, `UI/Layout/…razor`, `UI/Pages/Inicio.razor` -- enlaces, layout por defecto, encabezado e Inicio.
- [x] `tests/DraSelina.Consultorio.IntegrationTests/BaseVisualTests.cs` (nombre libre) -- una prueba por fila de la matriz contra la web del arnés; la de tokens lee `DESIGN.md` del repositorio (ruta relativa a la raíz de la solución, como ya resuelvan otras pruebas que leen archivos del repo) y compara los colores.
- [x] `README.md` -- la sección de la base visual.

**Acceptance Criteria:**
- Given la web del arnés, when se pide `/` y cada hoja y fuente que enlaza, then todo responde 200 desde el propio sitio, sin scripts ni URLs de terceros.
- Given el AppHost, when Gerardo abre `http://localhost:5080/` a 390 px, then ve Montserrat, el fondo `ground`, el nombre en vino y el botón principal vino de ancho completo, y la página llega completa sin conexión interactiva (hitl; lo confirma Gerardo).
- Given la solución, when `./dotnet.sh build` y `./dotnet.sh test`, then 0 advertencias y todas pasan, incluidas las 111 existentes.
- Given el AppHost, when corre `scripts/verificar-arranque.sh`, then sale con 0.

## Implementation Notes

- Bootstrap 5.3.8 (`dist/css/bootstrap.min.css` y `LICENSE`, npm vía jsDelivr) sin modificar; conserva su comentario final `sourceMappingURL` (el `.map` no se sirve; solo afecta a las herramientas del navegador). Montserrat de `@fontsource/montserrat@5.3.0` (`files/montserrat-latin-{400..700}-normal.woff2` y `LICENSE` como `OFL.txt`).
- Nombres de tokens: colores `--dsm-<clave>`; tipografías `--dsm-<estilo>-font-size|font-weight|line-height|letter-spacing` y `--dsm-font-family`; radios `--dsm-rounded-<clave>`; espaciados `--dsm-spacing-<clave>`.
- Bootstrap 5.3 pinta los enlaces con `--bs-link-color-rgb` y `--bs-link-hover-color-rgb`: se fijan esas además de `--bs-link-color` y `--bs-link-hover-color`, que solas no cambian el color. El anillo de foco del botón queda (en vino) por accesibilidad; no es una sombra de elevación.
- Layout: `UI/Layout/LayoutPublico.razor` (namespace `DraSelina.Consultorio.UI.Layout`, `@using` en `UI/_Imports.razor`). El nombre del encabezado es un enlace a `/` con 44 px de alto mínimo, como en el mockup. El rótulo usa el token `eyebrow` (12 px).
- Inicio: el párrafo es el del mockup `SitioInicio.dc.html`. El botón ocupa el ancho completo por debajo de 576 px.
- `BaseVisualTests` (6 pruebas, una por fila de la matriz): 117 en total. La de tokens se comprobó quitando `--dsm-wave` y cambiando `--dsm-track`: falló nombrando los dos.

## Plan Change Log

## Review Triage Log

### 2026-10-15 — Review pass
- verdicts: 5 findings — high 0, medium 0, low 5, false 0, maybe-false 0
- findings:
  - `[low]` `[patch]` El vino se repetía como tripletas RGB (`--bs-link-color-rgb`, `--bs-link-hover-color-rgb`, `--bs-btn-focus-shadow-rgb`): si cambia `--dsm-primary`, enlaces y anillo de foco quedan con el color viejo y la prueba de tokens no lo ve — se quitaron las tripletas; `a`/`a:hover` toman el color del token y el anillo usa `color-mix` sobre `--dsm-primary`. Comprobado en Chrome sin interfaz: enlace y anillo en vino.
  - `[low]` `[reject]` `--bs-body-color-rgb`, `--bs-body-bg-rgb` y `--bs-emphasis-color-rgb` siguen con los valores de Bootstrap; solo los leen utilidades (`.text-body`, `.bg-body`…) que ninguna página usa, y arreglarlo exige volver a escribir colores como tripletas, lo que deshace el hallazgo anterior. Se decide si una historia usa esas utilidades.
  - `[low]` `[patch]` Valores escritos que el README prohibía ("nunca el valor escrito") — `15px` del nombre pasa a `var(--dsm-body-font-size)`; el README dice ahora que lo que tiene token se escribe con su variable y que las medidas de layout sin token pueden ir escritas.
  - `[low]` `[patch]` README inexacto: `MapStaticAssets` no renombra archivos (solo las URL de `@Assets[...]` llevan huella; las fuentes, sin ella) y el bloque `components:` no son variables — corregidas las dos frases.
  - `[low]` `[patch]` La prueba de tokens lanzaba `ArgumentException` sin nombrar el color si se declaraba dos veces — agrupa las declaraciones y nombra el repetido; comprobado duplicando `--dsm-wave`.

## Design Notes

- **Tokens sobre Bootstrap.** Los tokens son la fuente; Bootstrap se ajusta a ellos, no al revés:

  ```css
  :root {
    --dsm-primary: #6B2A45;
    --dsm-ground: #FBF6F5;
    --bs-body-bg: var(--dsm-ground);
    --bs-body-font-family: 'Montserrat', 'Segoe UI', system-ui, sans-serif;
  }
  .btn-primary { --bs-btn-bg: var(--dsm-primary); --bs-btn-hover-bg: var(--dsm-primary-hover); }
  ```
- **Por qué sin script.** AD-11 pone el sitio y la reserva en SSR estático: sin `blazor.web.js` no hay circuito ni navegación mejorada; cada página es un documento completo. La prueba lo fija para que nadie lo agregue sin decidirlo.

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` (tres veces seguidas) -- expected: todas pasan cada vez.
- AppHost en segundo plano + `bash scripts/verificar-arranque.sh` -- expected: sale con 0; detener con `pkill -INT` y luego `-TERM` si sigue.

**Manual checks (if no CLI):**
- Gerardo abre `http://localhost:5080/` a 390 px y lo compara con `SitioMovil.dc.html` (tipografía, colores, botón principal).

## Auto Run Result

- Resumen: Bootstrap 5.3.8 (solo CSS) y Montserrat 400/500/600/700 servidos desde `wwwroot` con sus licencias; `consultorio.css` con los colores, tipografías, radios y espaciados de `DESIGN.md` como variables `--dsm-*` y Bootstrap ajustado a ellas; layout público (encabezado con nombre y especialidad) y la página de Inicio en SSR estático, sin ningún script. El host solo suma `app.MapStaticAssets();`.
- Archivos:
  - `src/DraSelina.Consultorio.Web/wwwroot/lib/bootstrap/` — `bootstrap.min.css` 5.3.8 y `LICENSE`.
  - `src/DraSelina.Consultorio.Web/wwwroot/fonts/montserrat/` — cuatro woff2 latín y `OFL.txt`.
  - `src/DraSelina.Consultorio.Web/wwwroot/css/consultorio.css` — tokens, Bootstrap sobre ellos, botón principal, layout e Inicio.
  - `src/DraSelina.Consultorio.Web/Program.cs` — `app.MapStaticAssets();`.
  - `src/DraSelina.Consultorio.Web/UI/App.razor`, `Routes.razor`, `_Imports.razor`, `Layout/LayoutPublico.razor`, `Pages/Inicio.razor` — hojas con huella, layout por defecto, encabezado e Inicio.
  - `tests/.../BaseVisualTests.cs` — 6 pruebas, una por fila de la matriz.
  - `README.md` — sección "Base visual".
- Revisión (lente `quick`): 5 hallazgos low; 4 parchados, 1 rechazado con su razón en el Triage Log; nada diferido.
- Revisión de seguimiento: `false`. Parches por veredicto: high 0, medium 0, low 4.
- Verificación (orquestador): `./dotnet.sh build` → 0 advertencias, 0 errores; `./dotnet.sh test` tres veces → 117 de 117 cada vez, antes y después de los parches; `scripts/verificar-arranque.sh` contra el AppHost → 0, las tres filas pasan, después de los parches. Las seis filas de la matriz tienen prueba que corrió y pasó.
- Pendiente (hitl): Gerardo compara `http://localhost:5080/` a 390 px con `SitioMovil.dc.html` (tipografía, colores, botón principal).
- Para Gerardo:
  - `[ASSUMPTION]` El botón "Agendar una cita" es `<button type="button">` sin destino; la historia de reserva lo cambia por un enlace.
  - El párrafo de Inicio se copió del mockup; el rótulo del encabezado usa 12 px (token `eyebrow`) donde el mockup usa 11 px; el nombre del encabezado enlaza a `/`.
  - El botón conserva el anillo de foco de Bootstrap, en vino al 50 %, por accesibilidad (es un `box-shadow`).
  - `bootstrap.min.css` conserva su comentario `sourceMappingURL` y el `.map` no se sirve: solo las herramientas de desarrollo ven un 404.
- Riesgos: las utilidades de Bootstrap que leen `--bs-body-color-rgb` y afines siguen con los colores de Bootstrap (rechazado en la revisión).
