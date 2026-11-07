---
title: 'Límite de frecuencia y verificación anti-bots'
type: 'feature'
ticket: '11'
created: '2026-11-04'
status: 'done'
baseline_revision: '67cca608b842e2ba9e4b5dad88bfb5db2e9fc7da'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-draselina/epic-reserva-en-linea/epic-reserva-en-linea.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Un bot puede pedir las páginas de reserva sin freno y enviar Tus datos cientos de veces; FR-7 (CAP-5) pide límite de frecuencia y verificación anti-bots.

**Approach:** Límite de frecuencia de ASP.NET Core por dirección IP en las páginas de reserva, y una verificación anti-bots sin scripts de terceros en el envío de Tus datos: un campo trampa oculto y un tiempo mínimo entre pintar y enviar el formulario, comprobados en el servidor antes del handler.

## Boundaries & Constraints

**Always:**
- Decisión (Gerardo, 2026-11-04): defensa sin scripts de terceros, no Cloudflare Turnstile; se mantienen "sin scripts" y "sin CDN".
- Decisión (Gerardo, 2026-11-04): por IP, 10 envíos (POST) de Tus datos cada 10 minutos y 60 visitas (GET) por minuto a las páginas de reserva (`/reservar`, `/reservar/horario`, `/reservar/datos`); los valores viven en configuración. Al pasarlo, respuesta 429 con una página amable: "Demasiados intentos. Espera unos minutos y vuelve a intentarlo." Nunca un error técnico.
- Campo trampa: un campo del formulario de Tus datos invisible para personas (fuera de pantalla, `tabindex="-1"`, `autocomplete="off"`, oculto a lectores de pantalla); si llega con valor, no se reserva.
- Tiempo mínimo: Tus datos lleva el instante en que se pintó, protegido contra alteración (Data Protection); un envío antes de 3 segundos, o con el instante ausente o alterado, no reserva. El mismo instante se conserva al volver desde "horario tomado" (entrada 8).
- Un envío que no pasa la verificación no crea Paciente ni Cita y vuelve a Tus datos con "No pudimos agendar tu cita. Inténtalo de nuevo." sin decir por qué; nada se registra con datos del formulario (AD-23).
- La verificación vive en la interfaz o el host, no en el dominio ni en el handler (AD-12: forma; esto es protección del formulario).
- La dirección IP sale de la conexión; el despliegue detrás de un proxy (encabezados reenviados) se decide al preparar producción.

**Never:**
- Scripts, Turnstile o cualquier servicio externo.
- Bloquear la Cita del consultorio o la administración (no existen todavía).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Envío humano | Formulario lleno después de 3 s, campo trampa vacío | La Cita se crea | — |
| Campo trampa lleno | El campo oculto trae valor | Sin Paciente ni Cita; Tus datos con el mensaje genérico | Sin detalle |
| Demasiado rápido | Envío a menos de 3 s de pintado | Sin Cita; mensaje genérico | Sin detalle |
| Instante alterado o ausente | Campo de tiempo cambiado o quitado | Sin Cita; mensaje genérico | Sin detalle |
| Reintento tras horario tomado | Elegir otra hora y confirmar | Pasa la verificación con el instante original | — |
| Límite de envíos | 11.º POST de la misma IP en 10 min | 429 con la página amable; sin Cita | Sin error técnico |
| Límite de visitas | 61.ª visita de la misma IP en un minuto | 429 con la página amable | Sin error técnico |
| Otra IP | Una segunda IP tras agotar la primera | Se atiende normalmente | — |

</frozen-after-approval>

## Code Map

Rutas bajo `src/DraSelina.Consultorio.Web/` salvo que digan otra cosa.

- `Program.cs` -- `AddRazorComponents`, `UseAntiforgery`, `MapRazorComponents`, `MapAgenda`; el limitador se registra y aplica aquí (`AddRateLimiter`, `UseRateLimiter`) sin tocar `/health`, los recursos estáticos ni `/c/…`.
- `appsettings.json` -- sección nueva para los límites.
- `UI/Pages/Reserva/TusDatos.razor` -- `EditForm FormName="datos"`, `SolicitudId` oculto, guarda de fechas con `IClock`; la verificación va antes de `Agendar.HandleAsync`. `HorarioTomado.razor` reenvía los datos ocultos al elegir otra hora: también debe llevar el instante protegido.
- `UI/Componentes/` -- un componente para los campos anti-bots si conviene.
- `Kernel/IClock.cs` -- el reloj para medir el tiempo mínimo (las pruebas usan `RelojDePrueba`).
- `tests/DraSelina.Consultorio.IntegrationTests/TusDatosTests.cs` -- POST con antiforgery y los campos del formulario; `Arnes` arranca la web: las pruebas de límite necesitan valores bajos por configuración y una IP controlada.

## Tasks & Acceptance

**Execution:**
- [x] Límite de frecuencia -- opciones en configuración, políticas por IP para GET y POST de reserva, página 429 amable.
- [x] Anti-bots -- campo trampa y tiempo mínimo protegido en Tus datos y en `HorarioTomado`; comprobación antes del handler con el mensaje genérico.
- [x] Pruebas -- cada fila de la matriz contra la web en memoria.
- [x] Documentos -- proponer a Gerardo el texto para la tabla Stack de la arquitectura, el C4 (dos lugares), la trazabilidad del spec y el addendum del PRD, que nombran Turnstile; se guardan solo con su visto bueno.

**Acceptance Criteria:**
- Given un envío de Tus datos de un humano, when pasa la verificación y el límite, then se reserva; given un envío con el campo trampa lleno, demasiado rápido o por encima del límite, then no se crea ninguna Cita y la respuesta es amable.
- Given la solución, when corren todas las pruebas y arranca el AppHost, then todo pasa.

## Implementation Notes

- Límite de frecuencia: `LimiteDeFrecuencia.cs` (host) registra la política `reserva` con `AddRateLimiter`; las tres páginas la piden con `@attribute [EnableRateLimiting(...)]`, así `/`, el Aviso, `/health`, los estáticos y `/c/…` no la llevan. Partición por IP de la conexión, ventana fija sin cola; envíos (POST) y visitas (resto) cuentan por separado. Valores en `appsettings.json`, sección `LimiteDeFrecuencia` (`Envios`/`Visitas`, `Permitidas`/`Duracion`), validados al arrancar.
- Página 429: `UI/Pages/DemasiadosIntentos.razor`, no es una ruta; `OnRejected` la pinta con `RazorComponentResult<App>` y el parámetro nuevo `App.Pagina`, que la envuelve en `LayoutPublico`. Lleva `Retry-After`.
- Anti-bots: `UI/Pages/Reserva/VerificacionAntiBots.cs` (Data Protection + `IClock`, singleton registrado en `Program.cs`) y `UI/Componentes/CamposAntiBots.razor` (oculto `Pintado` y trampa `SitioWeb` en `.dsm-fuera-de-pantalla`, `aria-hidden`, `tabindex="-1"`, `autocomplete="off"`). `TusDatos` comprueba antes de `Agendar.HandleAsync`; si no pasa, aviso "No pudimos agendar tu cita. Inténtalo de nuevo." con lo escrito. El instante se conserva mientras sea legítimo (envíos fallidos y `HorarioTomado`); ausente, alterado o de más de 2 horas (`TiempoMaximo`, para que un bot no reutilice un mismo valor por días), no pasa y se pinta uno nuevo.
- Pruebas: `Escenario.ClienteDeUnaPersona()` mueve el reloj de prueba 20 s antes de cada POST (las pruebas existentes de Tus datos lo usan); la IP en memoria se controla con un `IStartupFilter` que lee `X-Ip-De-Prueba`. Nuevas en `LimiteDeFrecuenciaYAntiBotsTests.cs` (parcial de `TusDatosTests`).

## Plan Change Log

## Review Triage Log

**Pasada 1 (quick), 2026-11-04.** Veredictos: high 0, medium 1, low 2, false 0, maybe-false 0, más la tarea de documentos.

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | El instante `Pintado` no tiene edad máxima: un bot lo pide una vez y lo reutiliza días | medium | patch | Confirmado en `VerificacionAntiBots.Pasa`; edad máxima de 2 horas (nadie tarda tanto en llenar Tus datos; el reintento tras horario tomado sigue cabiendo), con pruebas |
| 2 | La tarea Documentos sigue abierta y los documentos nombran Turnstile | — | resuelto con Gerardo | Se le presentó el texto y lo aprobó: Stack, C4, trazabilidad, addendum, iniciativa y Notes de la épica, con memlogs |
| 3 | Elegir otra hora tras horario tomado gasta un envío del límite | low | rechazado | Con 10 envíos por 10 minutos un Paciente puede perder varios horarios y reintentar sin tocar el límite |
| 4 | Una línea de comentario de ~170 caracteres en `HorarioTomado.razor` | low | patch | Corrección directa |

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` -- expected: todas pasan (Docker arriba).
- AppHost y `scripts/verificar-arranque.sh` -- expected: las tres filas pasan.
