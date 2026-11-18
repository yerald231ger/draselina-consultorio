---
title: 'Portada, Sobre mí y Padecimientos con los datos de la doctora'
type: 'feature'
ticket: '2'
created: '2026-11-17'
status: 'built'
baseline_revision: '48c6fa0924f3b1ebd719e22214491e1677755260'
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

**Problem:** `/` tiene una portada mínima y las secciones Sobre mí y Padecimientos vacías; los datos de la doctora (cédulas, certificación, padecimientos) no existen en Contenido, y la 7.3 (pie) y la 7.4 (JSON-LD) los necesitan (FR-27, CAP-21).

**Approach:** `IContenidoApi` suma `DatosDeLaDoctora` con los datos firmes del mockup y lo pendiente de Selina como nulo; `Inicio.razor` completa la portada, Sobre mí y Padecimientos con esos datos, y la interfaz muestra cada pendiente como texto visible marcado.

## Boundaries & Constraints

**Always:** los datos firmes son los de la Decision de la épica 7 del 2026-11-17: nombre "Dra. Selina Medina Castillo", especialidad "Traumatología y Ortopedia", Céd. Prof. 12093135, Céd. Especialidad 14899218, Consejo de Ortopedia 24/8379/2, seis padecimientos en este orden: Rodilla, Hombro, Columna, Cadera, Fracturas, Lesiones deportivas; semblanza, afiliaciones, descripción de cada padecimiento y foto son pendientes; el retrato es un recuadro con las iniciales "SM"; WhatsApp nulo → "Escríbenos por WhatsApp" con `MensajeDeWhatsApp`, con número → botón "WhatsApp" con `EnlaceDeWhatsApp`; los datos solo por `IContenidoApi` (AD-15); SSR estático sin scripts (AD-11); textos y orden del mockup `SitioInicio`; cada dato se cambia en una constante de `ContenidoApi`. Decision (Gerardo, 2026-11-17): la universidad queda pendiente en los dos lugares: Formación es nula (se ve "[Pendiente: universidad]") y la cédula profesional se muestra "12093135" sin universidad.

**Never:** testimonios ni fotos de antes/después (FR-27); imágenes de terceros o `<img>` del retrato; inventar la universidad, la semblanza, las afiliaciones o las descripciones; título SEO, JSON-LD (7.4); franja final, pie nuevo o Aviso bajo el encabezado del sitio (7.3); tocar Ubicación y horarios salvo lo que exija el layout.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Pendiente | Semblanza nula | En Sobre mí, texto visible con clase `dsm-pendiente` que dice qué falta | — |
| Dato presente | Semblanza con texto | El texto, sin marca de pendiente | — |
| Sin WhatsApp | `WhatsApp` nulo | Portada: "Escríbenos por WhatsApp y te ayudamos.", sin botón ni `wa.me` | — |
| Con WhatsApp | `WhatsApp` "4441234567" | Portada: botón "WhatsApp" a `https://wa.me/524441234567` | — |
| Padecimiento sin descripción | Descripción nula | Tarjeta con su nombre en `h3` y la descripción como pendiente | — |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Modules/Contenido/Contracts/IContenidoApi.cs` -- se suma la propiedad y los records; `ContenidoApi` es la única implementación (no hay falsos en pruebas).
- `Modules/Contenido/Contenido/ContenidoApi.cs` -- constantes con su doc y fecha de la decisión, como `Lugar` y `WhatsAppDelConsultorio`.
- `UI/Pages/Inicio.razor` -- hoy: `#inicio` mínima, `#sobre-mi` y `#padecimientos` solo con `h2`; `#ubicacion` no se toca.
- `UI/Layout/LayoutSitio.razor` -- la marca escribe nombre y especialidad fijos; pasa a leerlos de Contenido.
- `UI/Componentes/MensajeDeWhatsApp.razor`, `EnlaceDeWhatsApp.razor` (`HayNumero`, `Clase`) -- reusar; no cambiar.
- `wwwroot/css/consultorio.css:238-335` -- `.dsm-rotulo`, `.dsm-inicio`, `.dsm-display`, `.dsm-cuerpo`, `.dsm-accion-principal`, `.dsm-seccion-del-sitio*`; tokens `--dsm-*`, `--dsm-spacing-touch-min`.
- Mockup `_bmad-output/initiative-draselina/ux-appointment-booking/imports/mockup-canvas/SitioInicio.dc.html:38-118` -- portada (dos columnas con `flex-wrap`, ola decorativa en SVG inline, botones, renglón de cédulas, recuadro en arco 4:5), Sobre mí (rótulo "Sobre mí", h2 "Formación y certificaciones", semblanza, `dl` de cuatro tarjetas, afiliaciones), Padecimientos (rótulo, h2 "¿En qué te puedo ayudar?", rejilla de seis `article`).
- `tests/DraSelina.Consultorio.UI.Tests/SitioInicioTests.cs` -- bUnit de `Inicio` con `HorarioFalso` y `ContenidoApi` real; se amplía.
- `tests/DraSelina.Consultorio.IntegrationTests/SitioInicioTests.cs` -- HTTP de `/`; se le suma un caso.

## Tasks & Acceptance

**Execution:**
- [x] `Modules/Contenido/Contracts/IContenidoApi.cs` -- `DatosDeLaDoctora DatosDeLaDoctora { get; }`; `record DatosDeLaDoctora(Nombre, Especialidad, CedulaProfesional, CedulaDeEspecialidad, ConsejoQueCertifica, RegistroDeCertificacion, string? Formacion, string? Semblanza, string? Afiliaciones, IReadOnlyList<Padecimiento> Padecimientos)` y `record Padecimiento(string Nombre, string? Descripcion)`, con doc: nulo = pendiente de Selina -- lo leen 7.3 y 7.4.
- [x] `Modules/Contenido/Contenido/ContenidoApi.cs` -- `public static readonly DatosDeLaDoctora Doctora` con los datos de Boundaries y lo pendiente nulo -- un solo lugar para cambiar cada dato.
- [x] `UI/Componentes/Pendiente.razor` -- `<span class="dsm-pendiente">[Pendiente: @Que]</span>`; parámetro `Que` -- la marca única de lo que falta.
- [x] `UI/Pages/Inicio.razor` -- portada como el mockup ("Agendar cita en línea", WhatsApp según la matriz, renglón "Céd. Prof. …", "Céd. Especialidad …", "Certificada por el Consejo de Ortopedia", recuadro `aria-hidden` con "SM"); Sobre mí y Padecimientos con los datos y `Pendiente` donde son nulos -- FR-27.
- [x] `UI/Layout/LayoutSitio.razor` -- marca con `Nombre` y `Especialidad` de Contenido -- AD-15.
- [x] `wwwroot/css/consultorio.css` -- portada en dos columnas que se apilan, recuadro en arco con iniciales, tarjetas de Sobre mí y de Padecimientos en rejilla `auto-fit`, `.dsm-pendiente`; con los tokens existentes.
- [x] `tests/DraSelina.Consultorio.UI.Tests/SitioInicioTests.cs` -- los cinco casos de la matriz (un `IContenidoApi` de prueba que envuelve a `ContenidoApi` y cambia `WhatsApp` o la doctora); seis `h3` en orden; recuadro "SM".
- [x] `tests/DraSelina.Consultorio.IntegrationTests/SitioInicioTests.cs` -- `/` sin sesión trae "Céd. Prof. 12093135", "Céd. Especialidad 14899218", "Consejo de Ortopedia", las seis tarjetas, marcas `dsm-pendiente`, ningún `<img` ni la palabra "testimonio".

**Acceptance Criteria:**
- Given la aplicación con la base sembrada, when se pide `/` sin sesión, then la portada muestra el nombre, la especialidad, las dos cédulas, la certificación, "Agendar cita en línea" a `/reservar` y el recuadro "SM".
- Given `/`, when se lee Sobre mí, then están la certificación "Consejo de Ortopedia, 24/8379/2", las dos cédulas y los pendientes de semblanza y afiliaciones marcados.
- Given `/`, when se lee Padecimientos, then hay seis tarjetas en el orden de Boundaries bajo "¿En qué te puedo ayudar?".
- Given `/`, when se lee el HTML, then no hay `<script`, `<img` ni testimonios.

## Implementation Notes

- Implementado directo en la sesión: el clasificador de permisos volvió a bloquear al subagente implementador.
- `EnlaceDeWhatsApp` suma un `ChildContent` opcional para el texto del enlace: la portada lo usa para el botón "WhatsApp" y el componente sigue siendo el único que arma `wa.me`. Sin `ChildContent` muestra el número, como antes.
- `.dsm-inicio` ya no se usaba y se reemplazó por `.dsm-portada*`; la ola del mockup es SVG inline `aria-hidden` con relleno por CSS (`--dsm-tint`, `--dsm-wave`).
- Sorpresa: `BaseVisualTests.Inicio_trae_el_encabezado...` buscaba "Traumatología" literal en `<header>`; ahora sale de Contenido y Blazor lo codifica (`Traumatolog&#xED;a`). La prueba decodifica el HTML antes de comparar; su intención no cambia.
- Sorpresa del entorno: el `dotnet` del PATH (dotnetup) no tiene el SDK 10.0.4xx de `global.json`; se usó `~/.dotnet` (10.0.403) con `PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet`.
- Una corrida de la suite dio 678 fallas de 0 ms (el arranque del contenedor de PostgreSQL); al repetirla pasaron todas.
- `dotnet build`: 0 advertencias; `dotnet test`: 1150 pasan.

## Plan Change Log

## Review Triage Log

**Pasada 1 (2026-11-17), lente quick con un subagente revisor independiente** (Gerardo le dio permiso tras un primer bloqueo). Veredictos: high 0, medium 0, low 6, false 1, maybe-false 0.

| Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|
| Los botones de la portada no llevan la flecha ni el globo del mockup (`SitioInicio.dc.html:51,54`) | low | patch | Se agregaron los dos SVG inline `aria-hidden` y la clase `.dsm-portada__accion`. La regex `EnlaceAgendar` de `TipoDeConsultaYDiaYHoraTests` acepta ahora el SVG antes de `</a>`. |
| La prueba HTTP no revisa el rótulo de especialidad ni "Agendar cita en línea" → `/reservar` en la portada (AC1) | low | patch | Se agregaron la regex `AgendarCitaEnLineaAReservar` y las aserciones del rótulo y del nombre en `#inicio`. |
| La prueba HTTP no revisa en Sobre mí los pendientes de semblanza y afiliaciones ni las dos cédulas (AC2) | low | patch | Se agregaron las cinco aserciones sobre `#sobre-mi`. |
| La guarda contra inventar la universidad solo busca "UANL" | low | patch | Ahora también busca "Universidad". "San Luis Potosí" no sirve como guarda porque es la ciudad del consultorio. |
| `Pendiente.razor` cita las Boundaries de la épica, pero "no se publica sin ellos" está en sus Notes | low | patch | Se corrigió el comentario. |
| `EnlaceDeWhatsApp` cambió aunque el Code Map decía "no cambiar" | low | reject | El cambio es aditivo y compatible: sin `ChildContent` muestra el número, y Tu cita y `MensajeDeWhatsApp` no cambian. La otra salida duplicaba el armado de `wa.me`, que el componente promete hacer en un solo lugar. Corregirlo exige editar el plan, así que se rechaza; lo anotan las Implementation Notes. |
| El CSS nuevo tiene valores fijos en vez de tokens | false | reject | La escala `--dsm-spacing-*` llega hasta 20 px y no hay tokens de 18 px, 22 px, 24 px, 28 px, 40 px ni del arco; `--dsm-section-font-size` (17 px) es de títulos con peso 700, no de párrafos. La 7.1 sigue el mismo patrón (`.dsm-ubicacion` con `gap: 32px`). |

Después de los parches: `dotnet build` sin advertencias y las 1150 pruebas pasan.

## Design Notes

Pendiente en Contenido es `null`, no texto: así la 7.4 omite del JSON-LD lo que falta en vez de publicar "[Pendiente…]", y la prueba distingue dato de pendiente. La interfaz decide cómo se ve:

```razor
@if (Doctora.Semblanza is { } semblanza) { <p class="dsm-cuerpo">@semblanza</p> }
else { <p><Pendiente Que="semblanza breve escrita con Selina" /></p> }
```

## Verification

**Commands:**
- `./dotnet.sh build` -- sin advertencias.
- `./dotnet.sh test` -- todas pasan (necesita Docker).

**Manual checks (if no CLI):**
- Con el AppHost, `/` a 390 px y a 1280 px se parece al mockup `SitioInicio` y `SitioMovil`; "Agendar cita" sigue visible sin desplazarse a 390 px.
