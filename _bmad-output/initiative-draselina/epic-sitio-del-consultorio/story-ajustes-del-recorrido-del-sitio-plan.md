---
title: 'Ajustes del recorrido del sitio'
type: 'feature'
ticket: '6'
created: '2026-11-17'
status: 'built'
baseline_revision: '5a077daaeb15d7c436d2f104b2c55c0976ea304b'
route: 'oneshot'
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

**Problem:** En el recorrido de la entrada 7.5, Gerardo pidió dos cambios: los íconos de lugar, reloj y WhatsApp en Ubicación y horarios, como en el mockup `SitioInicio`, y que `robots.txt` deje de bloquear `/admin` y `/c/`. Un buscador que no rastrea esas rutas no ve su `noindex` y podría indexar la URL por enlaces externos (Decision de FR-29 actualizada, 2026-11-17).

**Approach:**
- En `Inicio.razor`, cada dato de Ubicación (dirección, horario, WhatsApp) lleva a su izquierda el ícono SVG inline del mockup, con `aria-hidden`.
- `robots.txt` queda en `User-agent: *` y `Sitemap: …`, sin `Disallow`. El meta robots y el `X-Robots-Tag` noindex de `/admin` y `/c/` siguen como están.
- El logotipo sigue pendiente hasta que Gerardo mande el archivo. El src directo a `/maps/embed`, que el tester sugirió como opcional, no se hace.

</frozen-after-approval>

## Implementation Notes

Oneshot: dos cambios chicos sin decisiones abiertas.

- `Inicio.razor`: cada `.dsm-ubicacion__dato` lleva el SVG del mockup (lugar, reloj, globo) con `aria-hidden` y `focusable="false"`, y su texto en `.dsm-ubicacion__texto`. El ícono toma `--dsm-primary` por `currentColor`.
- `ArchivosParaBuscadores.Robots` sin `Disallow`; la prueba se renombró a `Robots_no_bloquea_nada_y_apunta_al_sitemap`.
- Prueba bUnit nueva: tres datos en Ubicación, cada uno con su ícono primero y oculto al lector.
- `dotnet build`: 0 advertencias; `dotnet test`: 1168 pasan.

## Review Triage Log

**Pasada 1 (2026-11-17), lente quick con un subagente revisor independiente.** Revisó íconos, CSS, `robots.txt`, el `noindex` que sigue en `/admin` y `/c/`, y lo que quedó fuera. Veredictos: high 0, medium 0, low 1, false 0, maybe-false 0.

- low, patch: la prueba de los íconos no distinguía cuál era cuál ni revisaba `focusable="false"`. Ahora comprueba el pin (círculo r 2.5), el reloj (r 9) y el globo en orden, y `focusable`. UI.Tests: 176 pasan.

## Verification

**Commands:**
- `PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet ./dotnet.sh build` -- sin advertencias.
- `PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet ./dotnet.sh test` -- todas pasan.
