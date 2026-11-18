---
title: 'Encontrable: título, description, Physician, robots.txt y sitemap'
type: 'feature'
ticket: '4'
created: '2026-11-17'
status: 'built'
baseline_revision: '822512d4fad9ba9659f3c440db86fc747eac40bf'
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

**Problem:** `/` y el Aviso no tienen título con especialidad y ciudad ni meta description, no hay datos estructurados del consultorio y faltan `robots.txt` y `sitemap.xml` (FR-29, CAP-21).

**Approach:** Título y description en `/` y el Aviso armados con datos de Contenido; un JSON-LD `Physician` en `/` con los datos de la doctora, la dirección y los días de `HorarioDeAtencion`; dos endpoints de texto, `robots.txt` y `sitemap.xml`, con URLs del host de la petición.

## Boundaries & Constraints

**Always:** Decisiones de Gerardo (2026-11-17):
- Títulos solo en `/`: "Traumatóloga en San Luis Potosí · Dra. Selina Medina Castillo"; y en el Aviso: "Aviso de privacidad · Traumatóloga en San Luis Potosí · Dra. Selina Medina Castillo". Los pasos de la reserva conservan el suyo.
- Description de `/`: "Dra. Selina Medina Castillo, traumatóloga y ortopedista en San Luis Potosí. Rodilla, hombro, columna, cadera, fracturas y lesiones deportivas. Agenda tu cita en línea."; del Aviso: "Aviso de privacidad del consultorio de la Dra. Selina Medina Castillo, traumatóloga en San Luis Potosí."
- JSON-LD solo en `/`; el Aviso no lleva script ni toca la base.
- `sitemap.xml` y la línea `Sitemap:` usan el esquema y host de la petición.

Nombre, ciudad, cédulas, dirección y padecimientos salen de `IContenidoApi` (AD-15). Los horarios del JSON-LD salen de los días de `HorarioDeAtencion`, no del texto agrupado. Un dato nulo de Contenido (WhatsApp, pendientes) no aparece en el JSON-LD. El único `<script` permitido en `/` es `type="application/ld+json"` (AD-11), y el README y `BaseVisualTests` lo dicen así. `robots.txt` bloquea `/admin` y `/c/` y apunta a `sitemap.xml`; `sitemap.xml` lista `/` y `/aviso-de-privacidad`.

**Never:** scripts ejecutables; `UrlBase` ni opciones nuevas; tocar el meta robots ni el `X-Robots-Tag` de `/admin` y `/c/`; el Perfil de Negocio de Google; cambiar títulos de reserva, gestión o administración.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Horario sembrado | lun–vie 15:30–18:00 | `openingHoursSpecification` con un elemento por Tramo: `dayOfWeek` `https://schema.org/Monday`…, `opens` "15:30", `closes` "18:00" | — |
| Dos Tramos un día | mar 10:00–13:00 y 16:00–18:00 | dos elementos para el martes | — |
| Semana vacía | ningún Tramo | sin `openingHoursSpecification` | — |
| Pendientes | WhatsApp nulo | sin `telephone` | — |
| Texto con `<` | un dato de Contenido con `</script>` | el JSON lo escapa (`<`); el bloque no se cierra antes | — |
| robots.txt | `GET /robots.txt` en `http://localhost` | `text/plain`: `User-agent: *`, `Disallow: /admin`, `Disallow: /c/`, `Sitemap: http://localhost/sitemap.xml` | — |
| sitemap.xml | `GET /sitemap.xml` | `application/xml`, `urlset` con `<loc>` de `/` y `/aviso-de-privacidad` absolutos | — |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/UI/Pages/Inicio.razor` -- `<PageTitle>` actual "Dra. Selina Medina Castillo · Traumatología y Ortopedia"; ya lee `HorarioDeAtencion` en `OnInitializedAsync` (guardar también la lista cruda de `DiaDeLaBase`) y tiene `HttpContext` en cascada.
- `UI/Pages/AvisoDePrivacidad.razor` -- `<PageTitle>` actual; no toca la base (`WebNoMigraTests` lo pide en una base vacía).
- `UI/App.razor` -- `<HeadOutlet />` recibe `<HeadContent>`; comentario "Sin blazor.web.js ni ningún otro script" a ajustar.
- `UI/Componentes/SinIndice.razor` -- patrón de `<HeadContent>` con meta.
- `Modules/Contenido/Contracts/IContenidoApi.cs` -- `DatosDeLaDoctora` (Nombre, CedulaProfesional, CedulaDeEspecialidad, Padecimientos), `DatosDelConsultorio` (Lugar, Direccion, Ciudad), `WhatsApp`.
- `Modules/Agenda/Contracts/CambioDeAgendaBase.cs` (`DiaDeLaBase`), `DiasDeLaAgenda.cs` (`TramoDeLaAgenda`).
- `UI/Pages/Sitio/HorarioAgrupado.cs` -- vecino donde va la ficha; patrón de clase estática pura con pruebas en `UI.Tests`.
- `Program.cs:115-120` -- endpoints; aquí se llama al nuevo `MapArchivosParaBuscadores()`.
- `Modules/Agenda/Infrastructure/Registro.cs:110` (`MapAgenda`) -- patrón de `MapGet` en una extensión.
- Razor no deja escribir `<script>` en un componente: el bloque se emite como `MarkupString` completo, con JSON de `System.Text.Json` (su codificador por omisión escapa `<`, `>` y `&`).
- `tests/.../BaseVisualTests.cs:44` (`Inicio_no_trae_ningun_script`) y `SitioInicioTests.cs:89,129` -- hoy prohíben todo `<script` en `/`; pasan a permitir solo el ld+json. `SitioInicioTests.cs:173` (Aviso) sigue sin ninguno.
- `README.md:607` -- regla "Sin script".
- `tests/.../CancelarCitaTests.cs:679`, `EntrarConSegundoFactorTests.cs:488` -- ya cubren el `X-Robots-Tag` de `/c/` y `/admin`.

## Tasks & Acceptance

**Execution:**
- [x] `UI/Pages/Sitio/FichaDelConsultorio.cs` -- `static string JsonLd(DatosDeLaDoctora, DatosDelConsultorio, string? whatsApp, IReadOnlyList<DiaDeLaBase>, string url)`: `@context` schema.org, `@type` Physician, `name`, `url`, `identifier` (dos `PropertyValue`: "Cédula profesional", "Cédula de especialidad"), `address` `PostalAddress` (`streetAddress` Lugar y Dirección, `addressLocality` Ciudad, `addressCountry` "MX"), `telephone` solo con WhatsApp, `knowsAbout` los padecimientos, `openingHoursSpecification` según la matriz -- datos estructurados.
- [x] `UI/Pages/Sitio/TextosDelSitio.cs` -- título y description de `/` y del Aviso con datos de Contenido -- un solo lugar para los textos de buscadores.
- [x] `UI/Pages/Sitio/ArchivosParaBuscadores.cs` + `Program.cs` -- `MapArchivosParaBuscadores`: `GET /robots.txt` y `GET /sitemap.xml` según la matriz.
- [x] `UI/Pages/Inicio.razor` -- `<PageTitle>`, `<HeadContent>` con meta description y el bloque `<script type="application/ld+json">` (url = esquema + host de la petición + "/").
- [x] `UI/Pages/AvisoDePrivacidad.razor` -- `<PageTitle>` y meta description.
- [x] `UI/App.razor`, `README.md` -- el comentario y la regla "Sin script" nombran la excepción ld+json (datos, no código).
- [x] `tests/DraSelina.Consultorio.UI.Tests/FichaDelConsultorioTests.cs` -- las filas de la matriz del JSON-LD, leyendo el JSON con `JsonDocument`.
- [x] `tests/DraSelina.Consultorio.IntegrationTests/EncontrableTests.cs` -- títulos y descriptions de `/` y del Aviso; el JSON-LD de `/` es JSON válido de tipo Physician con los horarios de la base sembrada; `/` no trae otro `<script` y el Aviso ninguno; robots.txt y sitemap.xml.
- [x] `BaseVisualTests.cs`, `SitioInicioTests.cs` -- donde prohíben `<script` en `/`, permitir solo el ld+json.

**Acceptance Criteria:**
- Given la app con la base sembrada, when un buscador pide `/`, then lee título, description y un Physician con horario de lunes a viernes 15:30–18:00 sin ejecutar nada.
- Given `/admin` o `/c/<token>`, when se piden, then siguen con meta robots y `X-Robots-Tag` noindex.

## Implementation Notes

- Implementado directo en la sesión: el clasificador de permisos bloqueó al subagente implementador.
- El JSON-LD se arma con `JsonObject` de `System.Text.Json`: su codificador por omisión escapa `<`, `>` y `&`, como prueba `FichaDelConsultorioTests.Un_dato_con_cierre_de_script_no_cierra_el_bloque`.
- `EncontrableTests.SinMasScriptQueElJsonLd` es la única comprobación de "solo el ld+json". La usan `BaseVisualTests` (prueba renombrada a `Inicio_no_trae_mas_script_que_el_JSON_LD`), `SitioInicioTests` y `EntrarConSegundoFactorTests`. Esta última también pedía `/` sin ningún `<script` y no estaba en el Code Map.
- La URL del sitio en el JSON-LD y en robots/sitemap es `Scheme://Host` de la petición; en bUnit, sin `HttpContext`, es `/`.
- Una corrida completa dio una falla en `EnlaceDeGestionTests.Sin_la_clave_la_web_no_arranca...`. Al repetirla pasó, y esta entrada no toca ese código.
- `dotnet build`: 0 advertencias; `dotnet test`: 1167 pasan.

## Plan Change Log

## Review Triage Log

**Pasada 1 (2026-11-17), lente quick con un subagente revisor independiente.** Veredictos: high 0, medium 0, low 4, false 0, maybe-false 1.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Detrás de un proxy TLS, las URLs del JSON-LD, robots y sitemap saldrían `http://` y con el host interno | maybe-false (medium si se confirma) | defer | `Program.cs` no llama a `UseForwardedHeaders`. Depende del hosting, que aún no se elige; no hay producción. Se confirma al publicar. Anotado en `deferred-work.md`. |
| El comentario de `Inicio.razor` y el resumen de `BaseVisualTests` todavía dicen "sin scripts" | low | patch | Los dos nombran ahora la excepción del JSON-LD. |
| Aserciones de `blazor.web.js` y `_framework/` duplicadas tras `SinMasScriptQueElJsonLd` | low | patch | Se borraron en `BaseVisualTests` y `SitioInicioTests`. |
| `DescriptionDeInicio` pone en minúscula los padecimientos y rompería siglas | low | reject | Es latente: los seis de hoy salen bien; ningún padecimiento de Contenido trae sigla, y la corrección añade lógica. |
| `Disallow: /admin` y `/c/` impiden que el buscador vea el `noindex`; una URL bloqueada puede indexarse solo por enlaces externos | low | reject | Las líneas las fija la intención aprobada y AC2 se cumple. Los Enlaces de gestión solo viajan por correo, no se enlazan en público. Se le planteó a Gerardo. |

Después de los parches: `dotnet build` sin advertencias y pasan las 1167 pruebas.

## Design Notes

El bloque se emite así en `Inicio.razor`, porque Razor rechaza `<script>` literal en componentes:

```razor
<HeadContent>
    <meta name="description" content="@TextosDelSitio.DescriptionDeInicio(Contenido)" />
    @((MarkupString)$"<script type=\"application/ld+json\">{_fichaJson}</script>")
</HeadContent>
```

## Verification

**Commands:**
- `PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet ./dotnet.sh build` -- sin advertencias.
- `PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet ./dotnet.sh test` -- todas pasan (necesita Docker).

**Manual checks (if no CLI):**
- Pegar el JSON-LD de `/` en el validador de schema.org: tipo Physician sin errores.
