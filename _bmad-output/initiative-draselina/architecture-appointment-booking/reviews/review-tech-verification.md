# Revisión: verificación de decisiones tecnológicas contra la web

- **Documento revisado:** `architecture-appointment-booking.md` (spine) y su `.memlog.md`
- **Fecha de la revisión:** 2026-10-04
- **Método:** cada decisión se contrastó hoy contra documentación oficial y páginas de NuGet (no contra memoria). Donde la fuente primaria no se pudo abrir, se indica.
- **Leyenda:** CONFIRMADO · DESACTUALIZADO/INCORRECTO · NO SE PUDO CONFIRMAR

## Veredicto

La mayor parte del stack se sostiene (.NET 11 RC con licencia go-live, validación en navegador de Blazor SSR estático, Aspire 13.6, PostgreSQL Flexible Server en Mexico Central, `btree_gist`), pero **las dos afirmaciones sobre Wolverine están mal**: la versión vigente es 6.x, no 5.x, y sí existe un middleware oficial de DataAnnotations. Además la tabla Stack deja cinco de siete filas sin fijar y AD-12 promete más de lo que el framework da.

## Hallazgos principales

### H-1 · ALTA · Wolverine está en 6.45.0; el spine fija "5.x"

- **Qué dice el spine:** Stack: "Wolverine 5.x, fijar la última estable al crear la solución". Memlog: "WolverineFx.EntityFrameworkCore 5.23.x and WolverineFx.Postgresql 5.x current on NuGet".
- **Qué es cierto hoy:** `WolverineFx`, `WolverineFx.EntityFrameworkCore` y `WolverineFx.Postgresql` están en **6.45.0 (2026-10-02)**, con objetivos `net9.0` y `net10.0`. La 5.23.0 es del 2026-03-24; la rama 5.x llegó hasta 5.41.0 y queda como rama de mantenimiento. La 6.0 trae cambios incompatibles: exige .NET 9 o 10 (sin .NET 8), prohíbe por defecto la localización de servicios (`opts.RestoreV5Defaults()` para volver atrás), saca Newtonsoft.Json del paquete central y mueve el compilador en tiempo de ejecución a `WolverineFx.RuntimeCompilation`.
- **Corrección:** fijar `6.45.x` en la tabla Stack (proyecto nuevo: no hay razón para nacer en la rama de mantenimiento) y reescribir la pregunta abierta 2 como "Wolverine 6.x sobre .NET 11".
- **Fuentes:** https://www.nuget.org/packages/WolverineFx · https://www.nuget.org/packages/WolverineFx.EntityFrameworkCore · https://www.nuget.org/packages/WolverineFx.Postgresql · https://www.nuget.org/packages/WolverineFx/5.23.0 · https://api.nuget.org/v3-flatcontainer/wolverinefx/index.json · https://wolverinefx.net/guide/migration.html
- **Nota:** la guía de migración todavía dice que la 6.0 "está en desarrollo activo"; NuGet la contradice (6.0.0 publicada y 45 versiones menores después). La guía está atrasada.

### H-2 · ALTA · Sí existe un middleware oficial de DataAnnotations para Wolverine

- **Qué dice el spine:** AD-12: "un middleware propio de Wolverine valida antes del handler". Memlog: "No Wolverine DataAnnotations validation middleware found on the web (only WolverineFx.FluentValidation exists)".
- **Qué es cierto hoy:** existe el paquete **`WolverineFx.DataAnnotationsValidation`** (6.45.0, del mismo autor, documentado en el sitio oficial). Se activa con `opts.UseDataAnnotationsValidation()`. Solo existe en la línea 6.x (primera versión: 6.26.0), lo que refuerza H-1. Por defecto **lanza** una `ValidationException` y registra una política que descarta el mensaje; el comportamiento ante fallo se personaliza con `IFailureAction<T>`. Se aplica a todos los handlers.
- **Corrección:** cambiar AD-12 a "middleware oficial de Wolverine + `IFailureAction` propio que produce `Resultado.Invalido`", o conservar el middleware propio dejando escrito por qué (la convención del spine prohíbe lanzar excepciones para errores esperados). Antes de decidir, comprobar en un spike que `IFailureAction<T>` permite devolver un `Resultado` al llamador de `ISender` en vez de lanzar: **eso no se pudo confirmar** con la documentación.
- **Fuentes:** https://www.nuget.org/packages/WolverineFx.DataAnnotationsValidation · https://wolverinefx.net/guide/handlers/dataannotations-validation.html · https://api.nuget.org/v3-flatcontainer/wolverinefx.dataannotationsvalidation/index.json

### H-3 · MEDIA · AD-12 promete más de lo que Blazor entrega

La función existe y es como la describe el memlog, pero la regla "los mismos atributos valen en el navegador, en los formularios y en el backend" solo es cierta con estas condiciones:

1. **Solo 12 atributos integrados** se aplican en el navegador: `Required`, `StringLength`, `MinLength`, `MaxLength`, `Range` (solo numérico), `RegularExpression`, `EmailAddress`, `Url`, `Phone`, `CreditCard`, `Compare`, `FileExtensions`. Un `Range` sobre fechas u horas se valida solo en el servidor, y este dominio trabaja con fechas y horas.
2. **Los atributos propios no llegan al navegador** salvo que implementen `IClientValidationRuleProvider` y se registre un validador JavaScript con `Blazor.formValidation.addValidator`. Esa interfaz vive en `Microsoft.AspNetCore.Components.Forms`: implementarla en un atributo de `*.Contracts` metería una dependencia de ASP.NET en Contracts.
3. **La frase "lo que no tiene equivalente en navegador (teléfono)" es imprecisa:** `[Phone]` sí se valida en el navegador. Lo que no llega es el atributo propio basado en libphonenumber. Conviene decirlo así, y considerar `[Phone]` o `[RegularExpression]` como primera barrera en el navegador.
4. **Solo propiedades de primer nivel**, salvo que se llame a `AddValidation()` y el modelo sea descubierto por el generador (objetos anidados y colecciones).
5. **Solo en SSR estático.** En administración (Interactive Server) la validación corre en .NET por `EditContext`; la función del navegador no participa.
6. Los validadores JavaScript son síncronos; los campos agregados por una actualización de streaming rendering posterior no reciben validación en el navegador.
7. La exclusión se llama `DisableClientValidation` (por formulario en `DataAnnotationsValidator`, global en `RazorComponentsServiceOptions`, o `formnovalidate` por botón).

- **Corrección:** reescribir la regla como "los atributos integrados de la lista soportada valen en los tres lugares; todo lo demás es solo servidor". Añadir un spike: enlazar un `EditForm` directamente a un comando `record` de `*.Contracts` (propiedades `init`, atributos sobre parámetros posicionales) **no se verificó** y es el punto donde esta decisión puede obligar a un modelo de formulario aparte.
- **Fuentes:** https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation-client-side?view=aspnetcore-11.0 · https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation?view=aspnetcore-11.0
- **Limpieza del memlog:** la entrada "public form validation is server-side only … no client-side validation library on public pages" quedó superada por la decisión posterior y debería marcarse como reemplazada.

### H-4 · MEDIA · La tabla Stack deja cinco de siete filas sin fijar

| Fila del spine | Problema | Valor verificado hoy |
| --- | --- | --- |
| .NET 11.0 "versión candidata" | No dice cuál. La licencia go-live de RC1 vence el **2026-10-13**; hay que pasar a RC2 en cuanto salga. | RC1 (2026-09-08) |
| Wolverine "5.x" | Ver H-1 | 6.45.0 |
| EF Core + Npgsql "10.0.x" | Sin parche | EF Core 10.0.12 · Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 |
| PostgreSQL "por fijar" | Sin versión mayor | 18 (18.6). Azure lo soporta y Aspire.Hosting.PostgreSQL usa PostgreSQL 18 por defecto desde 13.4 |
| Bootstrap "5.x" | Sin menor | 5.3.8 (no hay v6) |
| xUnit v3, bUnit, Testcontainers "últimas estables" | Sin número | xunit.v3 4.0.1 · bUnit 2.11.3 (ya incluye `net11.0`) · Testcontainers.PostgreSql 4.15.0 |
| Aspire "13.6" | Único bien fijado | 13.6.0 (2026-09-29) |

- **Corrección:** escribir los valores en la tabla con la fecha de verificación. Faltan filas para lo que los AD ya nombran: proveedor anti-bots (AD-22 no dice cuál), proveedor de correo y libphonenumber.
- **Fuentes:** https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core · https://www.nuget.org/packages/Microsoft.EntityFrameworkCore · https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL · https://www.postgresql.org/support/versioning/ · https://learn.microsoft.com/en-us/azure/postgresql/configure-maintain/concepts-supported-versions · https://www.nuget.org/packages/Aspire.Hosting.PostgreSQL · https://getbootstrap.com/docs/versions/ · https://www.nuget.org/packages/xunit.v3 · https://www.nuget.org/packages/bunit · https://www.nuget.org/packages/Testcontainers.PostgreSql · https://www.nuget.org/packages/Aspire.Hosting.AppHost

### H-5 · MEDIA · La pregunta abierta 1 ya tiene respuesta: sí hay PostgreSQL Flexible Server en Mexico Central

- **Qué dice el spine:** pregunta abierta 1 y memlog: disponibilidad "NOT confirmed".
- **Qué es cierto hoy:** Mexico Central aparece en la tabla de regiones del servicio: cómputo Intel v3/v4, sin AMD, alta disponibilidad en la misma zona disponible, **alta disponibilidad con redundancia de zona temporalmente bloqueada para servidores nuevos**, y **sin respaldo con redundancia geográfica** (la región no tiene región emparejada). La región tiene 3 zonas de disponibilidad y está en Querétaro.
- **Consecuencia:** los datos de salud pueden quedarse en México. A cambio, no hay recuperación ante desastre fuera de la región con el respaldo nativo; si se quisiera, implicaría sacar datos del país, que es justo lo que la pregunta quería evitar.
- **Corrección:** cerrar la pregunta 1 y registrar la limitación de respaldo en "Deferred".
- **Fuentes:** https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/overview · https://learn.microsoft.com/en-us/azure/reliability/regions-list

## Resto de elementos verificados

### .NET 11

| # | Afirmación | Estado | Detalle y fuente |
| --- | --- | --- | --- |
| 1 | .NET 11 está en versión candidata | CONFIRMADO | RC1 publicada el 2026-09-08 con licencia go-live (soportada en producción) hasta el 2026-10-13. https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core · https://devblogs.microsoft.com/dotnet/dotnet-11-rc-1/ |
| 2 | Definitiva el 2026-11-10 | CONFIRMADO EN PARTE | Microsoft Learn solo dice "noviembre de 2026". El día 10 sale de prensa (Visual Studio Magazine, que no se pudo abrir: 403) y coincide con el fin de soporte de .NET 8 y 9 ese mismo día. https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview |
| 3 | STS con 24 meses de soporte (~nov 2028) | CONFIRMADO | La política oficial dice que las STS reciben dos años. Un resumen de búsqueda afirmaba "18 meses, hasta mayo de 2028"; la página oficial lo contradice. https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core |
| 4 | Empezar sobre la versión candidata es razonable | CONFIRMADO | Hay licencia go-live y nada se libera a producción antes de la definitiva. Riesgo real: las dependencias (fila 12). |
| 5 | .NET 10 LTS: 2025-11-11, soporte hasta 2028-11-14, parche 10.0.12 | CONFIRMADO | Misma fuente. Nota: .NET 11 STS y .NET 10 LTS terminan casi a la vez; elegir .NET 11 no acorta el soporte. |

### Blazor

| # | Afirmación | Estado | Detalle y fuente |
| --- | --- | --- | --- |
| 6 | Validación en navegador para formularios SSR estático con `DataAnnotationsValidator`, activa por defecto, sin paquete | CONFIRMADO | Ver H-3 para los límites. https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation-client-side?view=aspnetcore-11.0 |
| 7 | El servidor vuelve a validar al recibir el formulario | CONFIRMADO | "Server-side validation runs after the form is posted and remains authoritative". Misma fuente. |
| 8 | AD-11: flujo de varios pasos en SSR estático con navegación mejorada y estado en la cadena de consulta | CONFIRMADO, con una limitación documentada | No se encontró ninguna limitación para llevar estado en la cadena de consulta. Limitación real: **los scripts agregados por la navegación mejorada no se ejecutan**; la inicialización debe ir en un inicializador JS (`afterWebStarted`) y escuchar `enhancedload`. Afecta al widget anti-bots (fila 21). https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/static-server-rendering |
| 9 | `AddValidation()` + metadatos generados para objetos anidados | CONFIRMADO | https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/validation?view=aspnetcore-11.0 |
| 10 | `EditContext.Validate()` síncrono | DESACTUALIZADO (no está en el spine, pero afecta a la implementación) | En .NET 11 está obsoleto; se usa `ValidateAsync`. Misma fuente. |

### Wolverine

| # | Afirmación | Estado | Detalle y fuente |
| --- | --- | --- | --- |
| 11 | Outbox transaccional con EF Core | CONFIRMADO | `AddDbContextWithWolverineIntegration()` o `MapWolverineEnvelopeStorage()`; varios DbContext mediante `IDbContextOutbox<T>`. Relevante para AD-2 (un DbContext por módulo). https://wolverinefx.net/guide/durability/efcore/outbox-and-inbox.html |
| 12 | Compatibilidad con .NET 11 / EF Core 10 u 11 | NO SE PUDO CONFIRMAR | Los paquetes 6.45.0 solo declaran `net9.0` y `net10.0`; no hay objetivo `net11.0` ni declaración oficial. Funcionarán sobre el runtime 11 por compatibilidad hacia adelante, sin garantía del autor. `WolverineFx.EntityFrameworkCore` pide EF Core ≥ 10.0.4 sin tope superior. La pregunta abierta 2 sigue abierta y es correcta. https://www.nuget.org/packages/WolverineFx.EntityFrameworkCore |
| 13 | Colas locales durables | CONFIRMADO | `opts.Policies.UseDurableLocalQueues()` o `UseDurableInbox()`. https://wolverinefx.net/guide/durability/ |
| 14 | Mensajes programados durables (AD-20) | CONFIRMADO | Se guardan en el almacén de mensajes y sobreviven reinicios. Misma fuente. |
| 15 | Esquema `wolverine` en PostgreSQL (diagrama del Structural Seed) | NO SE PUDO CONFIRMAR | `PersistMessagesWithPostgresql()` existe; la página no detalla el parámetro de esquema. |

### EF Core y Npgsql

| # | Afirmación | Estado | Detalle y fuente |
| --- | --- | --- | --- |
| 16 | EF Core 10 estable hasta que salga la 11 | CONFIRMADO | EF Core 10.0.12 (2026-09-08); 11.0.0-rc.1 es previa. https://www.nuget.org/packages/Microsoft.EntityFrameworkCore |
| 17 | Npgsql 11 "preview.6" | DESACTUALIZADO | Ya está en **11.0.0-rc.1.1 (2026-09-24)**. La estable es 10.0.3 (2026-07-10) y **limita EF Core a < 11.0.0**: no se puede mezclar Npgsql 10 con EF Core 11. El supuesto del spine es el único camino válido hoy. https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL |
| 18 | Tipos de rango | CONFIRMADO | `tstzrange` ↔ `NpgsqlRange<DateTime>`; `Overlaps()` se traduce a `&&`. https://www.npgsql.org/efcore/mapping/range.html |
| 19 | Restricción de exclusión (`EXCLUDE USING gist`) por migraciones (AD-8) | NO SE PUDO CONFIRMAR soporte de primera clase | La documentación del proveedor (índices y rangos) no menciona restricciones de exclusión. Las opciones son SQL crudo en la migración (`migrationBuilder.Sql`) o el paquete de terceros `EFCore.ComplexIndexes.PostgreSQL` (`HasExclusionConstraint`, con filtro `WHERE`). AD-8 debería decir cuál; para un solo desarrollador, SQL crudo evita una dependencia. https://www.npgsql.org/efcore/modeling/indexes.html · https://www.nuget.org/packages/EFCore.ComplexIndexes.PostgreSQL/ |

Observación sin verificar en la web (conocimiento general de PostgreSQL): `btree_gist` solo hace falta si la restricción combina el rango con una columna escalar por igualdad. Con una sola agenda y solo el rango más `WHERE estado <> 'Cancelada'`, GiST sobre `tstzrange` es nativo. Conviene decidirlo al escribir la migración; tenerlo habilitado no estorba y prepara "varias sedes".

### Aspire

| # | Afirmación | Estado | Detalle y fuente |
| --- | --- | --- | --- |
| 20 | Aspire 13.6 es la estable actual | CONFIRMADO | `Aspire.Hosting.AppHost` 13.6.0 (2026-09-29). El memlog cita `Aspire.Hosting.PostgreSQL 13.5.x` y `Aspire.Npgsql 13.4.x`; la primera ya está en 13.6.0. https://www.nuget.org/packages/Aspire.Hosting.AppHost · https://www.nuget.org/packages/Aspire.Hosting.PostgreSQL |
| 21 | El AppHost puede apuntar a `net11.0` | NO SE PUDO CONFIRMAR | El paquete declara `net8.0`, `net9.0` y `net10.0`. Solo un resumen de búsqueda sugiere compilaciones `net11.0`. Opción segura: AppHost en `net10.0` orquestando la aplicación `net11.0` (Aspire ejecuta aplicaciones .NET 8+, según el memlog). Las incidencias 19050 y 19752 citadas en el memlog no se revisaron. |
| 22 | Integración de PostgreSQL | CONFIRMADO | `builder.AddPostgres("pgsql").AddDatabase("mydb")`, volúmenes de datos. Desde 13.4 la imagen por defecto es PostgreSQL 18, con cambio del directorio de datos. https://www.nuget.org/packages/Aspire.Hosting.PostgreSQL |
| 23 | Publicar a Azure App Service (diferido) | CONFIRMADO, en vista previa | `Aspire.Hosting.Azure.AppService` 13.6.0 es paquete estable, pero la página de despliegue lo marca como **Preview**. Publica los proyectos como imágenes de contenedor en Azure Container Registry. Límites: solo endpoints externos `http`/`https`, un único puerto externo por aplicación, sin contenedores de infraestructura (bases de datos). Encaja con un monolito web + PostgreSQL gestionado. https://aspire.dev/deployment/azure/app-service/ · https://www.nuget.org/packages/Aspire.Hosting.Azure.AppService |

### Azure Database for PostgreSQL

| # | Afirmación | Estado | Detalle y fuente |
| --- | --- | --- | --- |
| 24 | `btree_gist` disponible | CONFIRMADO | Versiones 1.5 a 1.8 para PostgreSQL 11 a 18. https://learn.microsoft.com/en-us/azure/postgresql/extensions/concepts-extensions-versions |
| 25 | Hay que permitirla en `azure.extensions` antes de `CREATE EXTENSION` | CONFIRMADO | Portal o `az postgres flexible-server parameter set --name azure.extensions`. Implicación: la migración que crea la extensión falla en Azure si el parámetro no se fijó en el aprovisionamiento. https://learn.microsoft.com/en-us/azure/postgresql/extensions/how-to-allow-extensions |

### Acceso, abuso, pruebas e interfaz

| # | Afirmación | Estado | Detalle y fuente |
| --- | --- | --- | --- |
| 26 | Identity con segundo factor (AD-22) | CONFIRMADO; la obligatoriedad NO SE PUDO CONFIRMAR | Los componentes de Identity de la plantilla soportan autenticación multifactor con aplicación de terceros y se renderizan en SSR estático. No se encontró un interruptor integrado de "2FA obligatorio": la aplicación debe imponerlo (política que exija el claim `amr`, o bloquear hasta inscribir el autenticador). AD-22 debería decir cómo. https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-11.0 |
| 27 | Límite de frecuencia en páginas de reserva | CONFIRMADO | `AddRateLimiter` / `UseRateLimiter`; `RequireRateLimiting` sobre `MapRazorComponents` o `[EnableRateLimiting]` por componente. Aviso oficial: particionar por IP expone a suplantación; detrás del proxy de App Service hay que configurar encabezados reenviados o todos comparten partición. El limitador vive en memoria (correcto para una sola instancia). https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-11.0 |
| 28 | Verificación anti-bots | VAGO en el spine; Cloudflare Turnstile CONFIRMADO como opción | AD-22 no nombra proveedor. Turnstile exige validación en servidor (`POST https://challenges.cloudflare.com/turnstile/v0/siteverify`); el token dura 300 segundos y es de un solo uso. Dos consecuencias para AD-11: el widget va solo en el paso de confirmación, y su script debe inicializarse de forma explícita porque la navegación mejorada no ejecuta scripts nuevos. https://developers.cloudflare.com/turnstile/get-started/server-side-validation/ |
| 29 | xUnit v3 | CONFIRMADO | 4.0.1 (2026-09-12), .NET 8+. Las plantillas de .NET 11 usan xUnit v3 con Microsoft.Testing.Platform por defecto. https://www.nuget.org/packages/xunit.v3 |
| 30 | bUnit | CONFIRMADO | 2.11.3 (2026-09-13), con objetivo `net11.0`. Nota: bUnit prueba componentes interactivos; no ejercita SSR estático ni la validación en navegador. https://www.nuget.org/packages/bunit |
| 31 | Testcontainers | CONFIRMADO | Testcontainers.PostgreSql 4.15.0 (2026-09-06), .NET 8/9/10 y .NET Standard. https://www.nuget.org/packages/Testcontainers.PostgreSql |
| 32 | Bootstrap 5.x | CONFIRMADO | 5.3.8 es la última; no hay v6. https://getbootstrap.com/docs/versions/ |

## Correcciones al memlog

| Entrada | Estado | Realidad |
| --- | --- | --- |
| Wolverine 5.23.x vigente | INCORRECTO | 6.45.0 |
| No existe middleware de DataAnnotations para Wolverine | INCORRECTO | `WolverineFx.DataAnnotationsValidation` |
| Npgsql 11 en preview.6 | DESACTUALIZADO | 11.0.0-rc.1.1 |
| Aspire.Hosting.PostgreSQL 13.5.x | DESACTUALIZADO | 13.6.0 |
| Disponibilidad en Mexico Central sin confirmar | RESUELTO | Disponible, con los límites de H-5 |
| Validación de formularios públicos solo en servidor | SUPERADO por una decisión posterior | Marcar como reemplazada |

## Lo que no se pudo abrir o verificar

- Notas de versión de ASP.NET Core 11 en Learn (`release-notes/aspnetcore-11.0`: 404) y Visual Studio Magazine (403). La información de Blazor se tomó de las páginas de formularios de Learn para `aspnetcore-11.0`, que sí abrieron.
- Fecha de RC2 de .NET 11.
- Incidencias de Aspire 19050 y 19752 (el servidor de GitHub del entorno no conectó).
- Que `IFailureAction<T>` de Wolverine pueda devolver un `Resultado` en lugar de lanzar.
- Enlace directo de `EditForm` a comandos `record` de `*.Contracts`.
- Nombre de esquema configurable para el almacén de mensajes de Wolverine.
