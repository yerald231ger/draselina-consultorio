---
title: 'Refactor sweep'
type: 'refactor'
ticket: '7'
created: '2026-11-17'
status: done
baseline_revision: 'a508a868ae05ed7eac02ab7494d05b2b5f35134e'
route: 'oneshot'
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

**Problem:** La épica 7 dejó duplicados y textos fijos que pueden divergir. Los encontraron sus registros de revisión y una pasada por el código, y Gerardo aprobó los cuatro el 2026-11-17:
- Hay dos falsos iguales de `IContenidoApi` en `UI.Tests`.
- "Dra. Selina Medina Castillo" y "Traumatología y Ortopedia" están escritos fijos en `LayoutPublico`, `LayoutAdministracion`, `DemasiadosIntentos` y el Aviso de `ContenidoApi`, aunque `DatosDeLaDoctora` ya existe (AD-15).
- `Inicio.MapaIncrustado` arma por su cuenta la misma búsqueda de lugar, dirección y ciudad que `ContenidoApi.BusquedaEnGoogleMaps`.
- `./dotnet.sh` falla cuando el `dotnet` del PATH no tiene el SDK de `global.json`.

**Approach:** Solo limpieza, sin comportamiento visible nuevo:
1. Un solo falso de Contenido compartido en `UI.Tests`, que envuelve a `ContenidoApi` y deja cambiar el WhatsApp y la doctora. Lo usan `SitioInicioTests` y `PieDelSitioTests`.
2. Los tres layouts y `DemasiadosIntentos` leen nombre y especialidad de `IContenidoApi`; el "Responsable" del Aviso usa `Doctora.Nombre`. Los `PageTitle` de reserva y gestión y los correos quedan fuera.
3. Contenido arma también el src del mapa incrustado, como nuevo dato opcional de `DatosDelConsultorio` junto a `EnlaceDelMapa`, e `Inicio` lo usa. La URL resultante es la misma de hoy.
4. `dotnet.sh` usa `~/.dotnet` cuando el `dotnet` del PATH no resuelve el SDK de `global.json` y existe `~/.dotnet/dotnet`.

</frozen-after-approval>

## Implementation Notes

Oneshot: cuatro limpiezas mecánicas aprobadas, sin decisiones abiertas.

- `UI.Tests/ContenidoDePrueba.cs` (interno) reemplaza a los dos falsos privados.
- `LayoutPublico`, `LayoutAdministracion` y `DemasiadosIntentos` inyectan `IContenidoApi`.
- En `ContenidoApi`, `Doctora` se declara antes de `TextoDelAviso`, porque los inicializadores estáticos corren en orden de declaración y el Aviso usa `Doctora.Nombre`.
- `DatosDelConsultorio.MapaIncrustado` es opcional; `Inicio` omite el iframe si es nulo. `TextoDeBusqueda` es el único armado de la búsqueda.
- `dotnet.sh` prueba `dotnet --version` en la raíz (falla si no resuelve `global.json`) y entonces usa `~/.dotnet`. El README lo dice.
- `./dotnet.sh build`: 0 advertencias; `./dotnet.sh test`: 1169 pasan, sin tocar el PATH.

## Review Triage Log

**Pasada 1 (2026-11-17), lente quick con un subagente revisor independiente.** Veredictos: high 0, medium 0, low 3, false 0, maybe-false 0; además, una nota de alcance.

- low, patch: el README seguía diciendo "No hace falta un SDK local", contra el párrafo nuevo y `dotnet.sh`. La sección del requisito nombra ahora `~/.dotnet`.
- low, patch: la rama sin `MapaIncrustado` no tenía prueba y la URL no estaba fijada. Se agregó la bUnit `El_mapa_es_el_de_Contenido_y_sin_el_no_hay_iframe`, con la URL exacta de antes y sin iframe cuando es nulo; `ContenidoDePrueba` suma `ConsultorioDePrueba`.
- low, patch: `dotnet.sh` comprobaba el SDK en la raíz pero corría el comando en la carpeta actual. Ahora comprueba en la misma carpeta y es más simple.
- Nota de alcance, no defecto: el nombre fijo sigue en el cuerpo de `CitaConfirmada.razor:55` y `ConfirmacionDeReprogramacion.razor:28`, en los `PageTitle` de reserva y gestión, en `PlantillaDeConfirmacion` y en `GeneradorDeCalendario`. Quedan fuera del alcance aprobado; se le plantea a Gerardo.

## Verification

**Commands:**
- `./dotnet.sh build` -- sin advertencias, ahora sin `PATH=$HOME/.dotnet:...` delante.
- `./dotnet.sh test` -- todas pasan: arquitectura, dominio, integración y UI.
