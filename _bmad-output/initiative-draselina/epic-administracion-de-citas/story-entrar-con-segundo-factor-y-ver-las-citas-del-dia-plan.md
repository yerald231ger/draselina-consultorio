---
title: 'Entrar con segundo factor y ver las citas del día'
type: 'feature'
ticket: '1'
created: '2026-11-09'
status: 'built'
baseline_revision: 'c759f87c1c157308d72b28264d5fd3d247716bbe'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** No hay área de administración: la web no tiene autenticación, ni Interactive Server, ni una sola página para el Administrador; sin eso no se puede construir nada de la épica 5 (CAP-9, CAP-12).

**Approach:** Camino trazador. Identity en el host con TOTP obligatorio y la cuenta sembrada por el MigrationService; `/admin` en Interactive Server, protegido y no indexable; y la primera página, Agenda, que lista las Citas de hoy pidiendo una consulta de administración de Agenda que trae los nombres en lote de Pacientes.

## Boundaries & Constraints

**Always:**
- Identity vive en el host (`Web/Identidad/`, namespace `DraSelina.Consultorio.Web.Identidad`), con su DbContext en el esquema `identidad` y su propio historial de migraciones (AD-25); la aplica el MigrationService; la web nunca migra.
- Segundo factor = app autenticadora TOTP (Decision 2026-11-09). Sin TOTP activo no hay sesión de Administrador: la contraseña sola solo lleva al enrolamiento.
- Inicio de sesión, segundo factor y enrolamiento en SSR estático (AD-11); las páginas de administración en Interactive Server. El sitio público sigue sin `blazor.web.js`.
- La consulta lleva `IRequiereAdministrador` (AD-22); la página no compone módulos: el handler pide los nombres a `IPacientesApi` en lote (AD-10).
- Nombre, motivo y correo nunca en registros ni telemetría (AD-23). `/admin` lleva `X-Robots-Tag: noindex, nofollow`, `no-store` y meta robots (CAP-21).
- El QR del enrolamiento lo genera QRCoder (NuGet, MIT) como SVG en el servidor; ningún script (Gerardo, 2026-11-09).
- La navegación de administración muestra solo lo que existe: en esta entrada, Agenda y "Salir"; la 5.7 agrega Pacientes y la épica 6 Calendario de atención y Configuración (Gerardo, 2026-11-09).
- Plan conservado completo, por encima de 1,600 tokens (Gerardo, 2026-11-09).
- Bloqueo por intentos y códigos de recuperación: los de Identity por defecto `[ASSUMPTION]`.
- Contraseña inicial: parámetro secreto del AppHost sin valor por omisión (lo captura Gerardo; se guarda en user-secrets del AppHost); correo: parámetro con valor por omisión `administrador@consultorio.local` `[ASSUMPTION]`. La siembra es idempotente: si la cuenta existe, no la toca.

**Never:** registro público, páginas de Identity de plantilla (`/Account/...`), cambiar contraseña o restablecerla, código por correo, roles, revalidación periódica de sesión, navegación entre días o vista de semana (entrada 2), enlaces al Detalle (entrada 6).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Sin sesión | GET `/admin` | 302 a `/admin/entrar?ReturnUrl=...` | — |
| Primera vez | contraseña correcta, TOTP no activo | Enrolamiento: QR y clave; `/admin` sigue redirigiendo | — |
| Activar | código de 6 dígitos válido | TOTP activo, muestra los códigos de recuperación una vez, sesión iniciada | Código inválido: mensaje, sin activar |
| Entrar | contraseña + código válidos | Agenda de hoy | Contraseña o código mal: mensaje genérico; tras el límite, bloqueo |
| Código de recuperación | contraseña + un código de recuperación | Sesión iniciada; el código ya no vale | — |
| Registro | GET `/Account/Register`, `/admin/registro` | 404 | — |
| Consulta sin sesión | `CitasDeLaFecha` como Paciente | `Resultado` no autorizado | — |
| Citas de hoy | 4:00 Agendada (ya pasó), 5:00 Agendada, 5:30 Cancelada tarde, 3:30 No se presentó | En orden: 3:30 No se presentó, 4:00 Atendida, 5:00 Agendada, 5:30 Cancelada (tardía), cada una con nombre y motivo | — |
| Día vacío | sin Citas | "No hay citas este día." | — |
| Paciente borrado | `PatientId` sin fila en Pacientes | La Cita se lista sin nombre ("Paciente no encontrado") | Sin excepción |

</frozen-after-approval>

## Code Map

- `src/DraSelina.Consultorio.Web/Program.cs` -- composición: agregar Identity, autenticación/autorización, `AddCascadingAuthenticationState`, `AddInteractiveServerComponents`/`AddInteractiveServerRenderMode`, `UseAuthentication`/`UseAuthorization` antes de `UseAntiforgery`, y extender el middleware de `/c` a `/admin`.
- `src/DraSelina.Consultorio.Web/ActorDelHost.cs` -- autenticado = Administrador; agregar el id (claim `NameIdentifier`). La cookie parcial de 2FA no es el esquema por omisión: no cuenta como sesión.
- `src/DraSelina.Consultorio.Web/Kernel/IActorActual.cs` -- puerto del actor; hoy solo `ObtenerAsync()`.
- `src/DraSelina.Consultorio.MigrationService/Program.cs` -- `Migrar<T>` en orden Agenda → Pacientes → Notificaciones; no hay paso de siembra fuera de `HasData`; `ActorSistema` interno. Agregar `IdentidadDbContext` y, después de migrar, la siembra del Administrador con `UserManager` (hash de contraseña: no cabe en `HasData`).
- `src/DraSelina.Consultorio.AppHost/AppHost.cs` -- recursos `postgres/consultorio`, `mailpit`, `migraciones`, `web`; patrón de parámetro secreto: `clave-enlace-de-gestion`. Agregar `clave-administrador` (secreto) y `correo-administrador` a `migraciones`. El csproj no tiene `UserSecretsId`.
- `Directory.Packages.props` -- versiones centrales (EF Core 10.0.12); agregar `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.12.
- `src/DraSelina.Consultorio.Web/UI/App.razor`, `UI/Routes.razor` -- hoy sin scripts; `blazor.web.js` solo cuando la ruta empieza con `/admin`. `RouteView` → `AuthorizeRouteView`.
- `src/DraSelina.Consultorio.Web/UI/Layout/LayoutPublico.razor`, `UI/Componentes/SinIndice.razor` -- estilo de layout y meta robots a reutilizar.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/CitasDelDia.cs`, `Features/ICitasDelDia.cs` -- modelo de puerto de lectura (solo Agendadas, para Notificaciones): no se toca; el nuevo puerto lee todos los estados. Ojo: `CitaDelDia`/`CitasDelDia` ya existen; la consulta nueva es `CitasDeLaFecha`.
- `src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/Registro.cs` -- registro explícito, una línea por handler y puerto.
- `src/DraSelina.Consultorio.Web/Modules/Pacientes/Contracts/IPacientesApi.cs`, `Features/PacientesApi.cs`, `Domain/IPacientes.cs`, `Infrastructure/Pacientes.cs` -- `ObtenerContactosAsync` de uno; agregar sobrecarga en lote.
- `src/DraSelina.Consultorio.Web/Modules/Notificaciones/Contracts/ConfiguracionDeEnvioVigente.cs` + su handler -- modelo de consulta de administración.
- `src/DraSelina.Consultorio.Web/DecoradoresDeHandlers.cs` -- el rechazo sin Administrador ya decora toda consulta registrada; no se toca.
- `tests/DraSelina.Consultorio.IntegrationTests/Arnes.cs`, `MigrationService.cs` -- el arnés corre el MigrationService como proceso con variables de entorno: agregar las del Administrador. `ActorYAdministracionTests.FijarUsuario` simula sesión. `ReservaEnLineaTests` tiene un cliente con cookies que envía formularios con antiforgery.
- `tests/DraSelina.Consultorio.ArchitectureTests/Reglas.cs` -- el host (fuera de Kernel, BuildingBlocks, Modules, UI) puede usar todo; UI solo Contracts y Kernel. Las páginas de Identity van en el host, no en UI.
- `tests/DraSelina.Consultorio.UI.Tests` -- bUnit con `BunitContext`, handlers falsos registrados en el constructor, `SetRendererInfo`.

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Packages.props`, `src/DraSelina.Consultorio.Web/DraSelina.Consultorio.Web.csproj` -- paquete de Identity EF Core.
- [x] `src/DraSelina.Consultorio.Web/Identidad/IdentidadDbContext.cs` + migración inicial en `Identidad/Migrations` -- `IdentityDbContext<IdentityUser>` en esquema `identidad`, historial propio.
- [x] `src/DraSelina.Consultorio.Web/Identidad/Registro.cs` -- `AddIdentidad(cadena)`: Identity core + SignInManager + token de autenticador, cookie con `LoginPath=/admin/entrar`, `AccessDeniedPath`, sin UI por omisión; `MapIdentidad()`: POST `/admin/salir`.
- [x] `src/DraSelina.Consultorio.Web/Identidad/SiembraDelAdministrador.cs` -- crea la cuenta si no existe; si la contraseña no cumple la política, falla con mensaje claro.
- [x] `src/DraSelina.Consultorio.Web/Identidad/Paginas/Entrar.razor`, `SegundoFactor.razor`, `ActivarSegundoFactor.razor`, `CodigoDeRecuperacion.razor` -- SSR estático, layout del sitio, textos de tú; enrolamiento con QR y clave en texto; códigos de recuperación mostrados una vez.
- [x] `src/DraSelina.Consultorio.MigrationService/Program.cs`, `src/DraSelina.Consultorio.AppHost/AppHost.cs` (+ `UserSecretsId` en su csproj) -- migrar identidad y sembrar; parámetros.
- [x] `src/DraSelina.Consultorio.Web/Program.cs`, `ActorDelHost.cs`, `Kernel/IActorActual.cs` -- composición y el id del Administrador (`IdDelAdministradorAsync`, nulo si no es Administrador).
- [x] `src/DraSelina.Consultorio.Web/UI/App.razor`, `Routes.razor`, `UI/Layout/LayoutAdministracion.razor` -- script solo en `/admin`, `AuthorizeRouteView`, layout con navegación (solo Agenda) y "Salir", `SinIndice`.
- [x] `src/DraSelina.Consultorio.Web/Modules/Pacientes/...` -- `ObtenerContactosAsync(IReadOnlyCollection<PatientId>)` → diccionario, una sola consulta.
- [x] `src/DraSelina.Consultorio.Web/Modules/Agenda/Contracts/CitasDeLaFecha.cs`, `Features/CitasDeLaFechaHandler.cs`, `Features/ICitasDeLaFecha.cs`, `Infrastructure/CitasDeLaFecha*.cs`, `Registro.cs` -- consulta `IRequiereAdministrador` con `CitaId`, hora de inicio y fin locales, nombre, motivo, estado mostrado (Agendada, Atendida derivada por RN-31, NoSePresento, Cancelada + motivo).
- [x] `src/DraSelina.Consultorio.Web/UI/Pages/Administracion/Agenda.razor` -- `/admin`, `[Authorize]`, Interactive Server, fecha de hoy por `IClock`, lista o "No hay citas este día.", con tokens de `DESIGN.md`.
- [x] Pruebas: integración (redirecciones, 404 de registro, encabezados de `/admin`, sin `blazor.web.js` en `/`, flujo completo de contraseña → código TOTP calculado en la prueba → Agenda, contraseña sin TOTP no entra, código de recuperación, consulta con y sin sesión, siembra idempotente); arquitectura (toda página con ruta bajo `/admin` que no sea de `/admin/entrar` lleva `[Authorize]`); bUnit de la Agenda (matriz de estados, día vacío).

**Acceptance Criteria:**
- Given el AppHost arriba por primera vez, when Gerardo captura la contraseña, entra, escanea el QR con su teléfono y escribe el código, then ve la Agenda de hoy y sus códigos de recuperación una sola vez.
- Given una sesión iniciada, when se reinicia la web, then el siguiente inicio de sesión pide contraseña y código, no QR.
- Given cualquier página con ruta `/admin/...` nueva sin `[Authorize]`, when corren las pruebas de arquitectura, then fallan.

## Review Triage Log

Pasada 1 (lente quick): high 0, medium 0, low 6, false 0, maybe-false 0; 5 a patch, 1 rechazado (low con arreglo complejo).

| # | Hallazgo | Veredicto | Ruta | Evidencia |
|---|---|---|---|---|
| 1 | `Agenda.razor` `ClaseDeEtiqueta`: Cancelada (tardía) y No se presentó usan `dsm-chip-de-estado--cancelada` (`--dsm-danger`) | low | patch | DESIGN.md Colors: peligro solo para cancelar una cita; aviso (ámbar, `--dsm-warning-*`, ya definidos en `consultorio.css:69-73`) solo para la cancelación tardía. Arreglo: variante ámbar para tardía y neutra para las demás. |
| 2 | `SiembraDelAdministrador`: el mensaje de la excepción une `Errors` con su `Description`, que en InvalidEmail/InvalidUserName incluye el correo; el MigrationService la registra con `LogCritical(error, ...)` | low | patch | Con un correo mal formado en el parámetro, el correo llega al registro, contra el comentario de la propia siembra. Arreglo directo: solo los `Code`. |
| 3 | `SiembraDelAdministrador`: cambiar el parámetro del correo crea una segunda cuenta de Administrador | low | patch | `FindByEmailAsync(correo)` es la única comprobación; la cuenta anterior sigue entrando. Arreglo directo: si ya existe cualquier cuenta, `YaExistia`. |
| 4 | `Registro.RegresoSeguro`: `/admin?fecha=...` no pasa y se vuelve `/admin` | low | patch | Solo acepta `== "/admin"` o `"/admin/"...`; la cookie arma `ReturnUrl` con la cadena de consulta. La entrada 2 navega por días. Arreglo directo: aceptar también `/admin?`. |
| 5 | `Agenda.razor`: con prerender, la consulta corre dos veces y al conectar el circuito la lista desaparece hasta que vuelve la segunda | low | patch | `OnInitializedAsync` corre en el prerender y en el circuito; `_citas` vuelve a ser nulo en el segundo pase y esa rama no pinta nada. Se ve en cada carga. Arreglo: conservar el estado del prerender (`[PersistentState]` de .NET 10). |
| 6 | `Verificador.VerificarPaginasDeAdministracion`: no ve rutas por `[Route]` en `.cs`, ni `@attribute [Route]`, y un `@attribute [Authorize]` dentro de `@* *@` la satisface | low | rechazado | Real, pero el proyecto declara toda ruta con `@page` en `.razor` y nadie escribe el atributo dentro de un comentario; cubrir esos casos agrega análisis de C# y de comentarios. |

## Design Notes

Contraseña correcta sin TOTP activo: no se llama a `PasswordSignInAsync` (iniciaría sesión completa). Se usa `CheckPasswordSignInAsync` (respeta el bloqueo) y se guarda el id en el esquema parcial `IdentityConstants.TwoFactorUserIdScheme`, que solo abre `/admin/entrar/activar`. Al verificar el código: `SetTwoFactorEnabledAsync(true)`, `GenerateNewTwoFactorRecoveryCodesAsync(10)`, `SignInAsync`. Con TOTP activo, `PasswordSignInAsync` devuelve `RequiresTwoFactor` y sigue `TwoFactorAuthenticatorSignInAsync`.

## Verification

**Commands:**
- `dotnet build` -- sin advertencias nuevas.
- `dotnet test` -- todas pasan, incluidas las de arquitectura.

**Manual checks (if no CLI):**
- Con el AppHost: primer inicio con QR, segundo inicio con código, `/` sin `blazor.web.js`, `/admin` con `X-Robots-Tag`.
