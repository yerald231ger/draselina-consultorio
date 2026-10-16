---
title: 'Los errores de validación se muestran en el formulario'
type: 'feature'
ticket: '9'
created: '2026-10-16'
status: 'built'
baseline_revision: '9b634373f8b06a7dbf25b6bbde4cdd42f6c8a074'
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

**Problem:** Un comando inválido ya vuelve como `Resultado` inválido con errores por propiedad (entrada 6), pero ninguna página sabe mostrarlos. La primera historia de reserva no debe construir esa pieza (P7, P10; EXPERIENCE.md "Campo inválido": mensaje bajo el campo y foco al primer error).

**Approach:** Dos componentes en `UI/`: un padre que recibe el `Resultado` dentro de un `EditForm` y lo pasa en cascada, y un `Campo` que pinta etiqueta, control, ayuda y su error, y que, si es el primer error, se lleva el foco: con `autofocus` en SSR estático y con `FocusAsync` en interactivo. Probado con bUnit en un proyecto de pruebas nuevo.

## Boundaries & Constraints

**Always:**
- Decisiones de Gerardo (2026-10-16, notas de la épica): error en vino (borde de 2 px `--dsm-primary`, mensaje en `--dsm-primary` con la tipografía `help` en 700), nunca `danger`; foco con `autofocus` en SSR y `FocusAsync` en interactivo; componente `Campo` alimentado por un padre en cascada; los dos modos se prueban con bUnit; el host no cambia.
- Componentes en `src/DraSelina.Consultorio.Web/UI/Componentes/` (namespace `DraSelina.Consultorio.UI.Componentes`, `@using` en `UI/_Imports.razor`). Nombres sugeridos: `ErroresDelResultado` (padre, `Resultado` + `ChildContent`, pasa los errores con `CascadingValue`) y `Campo`. Solo usan Kernel y Blazor (regla de la capa UI).
- `Campo`: parámetros `Para` (`Expression<Func<string>>`, como `ValueExpression` de Blazor), `Etiqueta`, `Ayuda` opcional, `Tipo` (`text` por omisión; `email`, `tel`) y atributos adicionales que pasan al control (p. ej. `autocomplete`, `inputmode`). Pinta `<label for>` en negritas, un `InputText` de 50 px, la ayuda y, si su propiedad tiene errores en el `Resultado`, cada mensaje debajo en un elemento con id propio. Con error: `aria-invalid="true"` y `aria-describedby` con los ids de ayuda y error. El id del control sale del nombre de la propiedad.
- Primer error = el primero en orden de lectura del formulario. El padre lleva la cuenta de quién reclamó el foco en esta pintada; el primer `Campo` con error que se pinta lo reclama.
- `[ASSUMPTION]` Errores bajo la propiedad `""` (del objeto, decisión de la entrada 6) los pinta el padre arriba de los campos, y si los hay, ese mensaje (`tabindex="-1"`) es el que recibe el foco por ser el primero en orden de lectura.
- Interactivo: al llegar un `Resultado` inválido nuevo, el primer error recibe el foco una vez, después de pintar (`ElementReference.FocusAsync`). En SSR estático no se llama a JS.
- Estilos en `wwwroot/css/consultorio.css` solo con tokens `--dsm-*`; campo con radio `md`, borde `border-input` de 1.5 px, fondo `surface`, tipografía `input` (16 px).
- Pruebas en un proyecto nuevo `tests/DraSelina.Consultorio.UI.Tests` `[ASSUMPTION]` (Razor SDK, xunit.v3, bUnit 2.11.3 en `Directory.Packages.props` con comentario, referencia a Web), agregado a `DraSelina.Consultorio.slnx`. No necesita Docker. El formulario de prueba y su comando de prueba viven ahí; el comando lleva `ErrorMessage` en español (decisión de la entrada 6). El handler del formulario se resuelve por `ICommandHandler<,>` desde los servicios de bUnit, envuelto en el `ValidacionDeComandos` real, para que los mensajes salgan de DataAnnotations.
- Build con 0 advertencias; las 117 pruebas existentes siguen pasando; las de arquitectura no se tocan.

**Never:**
- Tocar `Program.cs`, `App.razor`, el host o el registro de servicios; configurar Interactive Server; agregar scripts.
- `DataAnnotationsValidator` en el formulario ni otra validación del lado de la página: los errores vienen solo del `Resultado` (AD-12).
- Colores, tamaños o radios literales en la hoja cuando hay token; `danger` en errores de formulario.
- Componentes para controles que no sean texto (número, fecha, lista, área de texto): llegan con la historia que los necesite.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Sin resultado | Formulario recién abierto | Campos sin mensajes, sin `aria-invalid`, sin `autofocus` | — |
| Inválido en SSR | Formulario de prueba enviado con dos campos inválidos (pintado estático) | Cada mensaje bajo su campo; solo el primer campo inválido en orden de lectura lleva `autofocus`; los dos con `aria-invalid` y `aria-describedby` hacia su mensaje | Sin excepción |
| Varios mensajes en un campo | Un campo con dos errores | Los dos mensajes bajo ese campo | — |
| Error del objeto | Error bajo `""` y uno de campo | El del objeto arriba del formulario y con el foco; el de campo bajo su campo, sin `autofocus` | — |
| Inválido interactivo | Envío con datos inválidos en el formulario interactivo de bUnit | Mensajes bajo cada campo; se llama a enfocar el primer campo con error una vez | — |
| Corregido | Segundo envío válido tras uno inválido | Mensajes y `aria-invalid` desaparecen | — |

</intent-contract>

## Code Map

- `src/DraSelina.Consultorio.Web/Kernel/Resultado.cs` -- `Errores`: diccionario propiedad → mensajes, en orden; `Forma`. No cambiar.
- `src/DraSelina.Consultorio.Web/ValidacionDeComandos.cs` (raíz de Web) -- decorador público de la entrada 6 (`ValidacionDeComandos<TComando,T>(ICommandHandler<TComando,T> siguiente)`); reutilizar en las pruebas. No cambiar.
- `src/DraSelina.Consultorio.Web/UI/_Imports.razor` -- agregar `@using DraSelina.Consultorio.UI.Componentes` y lo que haga falta de `Microsoft.AspNetCore.Components.Forms`.
- `src/DraSelina.Consultorio.Web/UI/Pages/Inicio.razor`, `UI/Layout/LayoutPublico.razor` -- estilo de los `.razor` existentes. No cambiar.
- `src/DraSelina.Consultorio.Web/wwwroot/css/consultorio.css` -- tokens `--dsm-*` (`--dsm-primary`, `--dsm-border-input`, `--dsm-surface`, `--dsm-rounded-md`, `--dsm-help-*`, `--dsm-label-*`, `--dsm-input-*`); clases `.dsm-*` existentes. Agregar los estilos de campo y error.
- `tests/DraSelina.Consultorio.Kernel.Tests/*.csproj` -- modelo de proyecto de pruebas sin Docker (Exe, xunit.v3, `<Using Include="Xunit" />`).
- `tests/DraSelina.Consultorio.IntegrationTests/BaseVisualTests.cs` -- la prueba que compara cada color de `DESIGN.md` con su variable: sigue pasando sin cambios.
- `tests/DraSelina.Consultorio.ArchitectureTests/Reglas.cs` -- capa UI puede usar Kernel, Contratos base y contratos de módulo; `Lector` lee los `using` de cada archivo de `UI/`. No cambiar.
- `Directory.Packages.props`, `DraSelina.Consultorio.slnx` -- agregar bUnit 2.11.3 y el proyecto nuevo.
- `README.md` -- agregar sección corta "Errores en el formulario": cómo arma una página su formulario (ejemplo de Design Notes), que toda propiedad validada necesita su `Campo` para que su error se vea, y cómo se prueba.

## Tasks & Acceptance

**Execution:**
- [x] `src/DraSelina.Consultorio.Web/UI/Componentes/ErroresDelResultado.razor` -- padre en cascada: guarda el `Resultado`, pinta arriba los errores de `""`, coordina quién reclama el foco por pintada, y en interactivo enfoca el primer error tras pintar un `Resultado` nuevo -- un solo dueño del foco.
- [x] `src/DraSelina.Consultorio.Web/UI/Componentes/Campo.razor` -- etiqueta, `InputText`, ayuda, mensajes, atributos de accesibilidad y `autofocus` si reclama el foco -- el "Campo" de DESIGN.md.
- [x] `src/DraSelina.Consultorio.Web/UI/_Imports.razor` -- `@using` de componentes y formularios.
- [x] `src/DraSelina.Consultorio.Web/wwwroot/css/consultorio.css` -- estilos del campo y del error con tokens.
- [x] `tests/DraSelina.Consultorio.UI.Tests/` -- proyecto, comando y formulario de prueba (`EditForm` + `[SupplyParameterFromForm]` o modelo, `ErroresDelResultado`, `Campo`), y una prueba por fila de la matriz.
- [x] `Directory.Packages.props`, `DraSelina.Consultorio.slnx` -- bUnit y el proyecto.
- [x] `README.md` -- sección "Errores en el formulario".

**Acceptance Criteria:**
- Given el proyecto de pruebas nuevo, when se corre `./dotnet.sh test`, then sus pruebas corren junto con las 117 existentes y todas pasan sin Docker para las de UI.
- Given el formulario de prueba pintado con un `Resultado` inválido, when se inspecciona el HTML, then no hay `<script` ni color literal en el marcado, y cada mensaje está dentro del mismo `Campo` que su control, después de él.

## Implementation Notes

- `aria-invalid` no se le puede pasar al `InputText`: `InputBase` lo quita de sus atributos si su `EditContext` no tiene mensajes para ese campo. Por eso `ErroresDelResultado` copia los errores de campo del `Resultado` a un `ValidationMessageStore` del `EditContext` en cascada y los borra en `OnValidationRequested` (lo primero que hace el `EditForm` al enviarse), para que `OnValidSubmit` se siga disparando siempre. Ningún validador corre en la página; los mensajes siguen saliendo solo del `Resultado`. Efecto lateral: el `InputText` también pinta su clase `invalid`. `[ASSUMPTION]`
- `Campo` fuera de `ErroresDelResultado`, o `ErroresDelResultado` fuera de un `EditForm`, lanza `InvalidOperationException` al pintarse. `[ASSUMPTION]`
- Ids: control = nombre de la propiedad (`Nombre`), ayuda `Nombre-ayuda`, mensajes `Nombre-error-1`... La ayuda va en `aria-describedby` también sin error. `[ASSUMPTION]`
- El "SSR estático" de las pruebas es bUnit con `RendererInfo("Static", false)`: pinta como SSR y no llama a JS, pero el envío es un evento, no un POST real.
- Pruebas: 9 en `UI.Tests` (una por fila de la matriz, la de aceptación del marcado, una de etiqueta/tipo/ayuda/atributos y una de `Campo` sin padre). `./dotnet.sh build` 0/0; `./dotnet.sh test` tres veces: 126 de 126.

## Plan Change Log

## Review Triage Log

### 2026-10-16 — Review pass
- verdicts: 3 findings — high 0, medium 1, low 2, false 0, maybe-false 0
- findings:
  - `[medium]` `[patch]` El ejemplo del README no declara `Comando` con `[SupplyParameterFromForm]`; en SSR estático el POST no lo llena y quien copie el ejemplo recibe siempre "obligatorio" — el ejemplo suma el bloque `@code` con `[SupplyParameterFromForm]` y una frase que lo explica.
  - `[low]` `[patch]` En `Campo.razor`, `@attributes` iba al final: un `class`, `id` o `aria-describedby` de quien llama reemplazaba los del componente (estilo, `label for`, ids de ayuda y error) — `@attributes` pasa antes de los atributos propios, el README lo dice y una prueba nueva lo fija.
  - `[low]` `[patch]` `ErroresDelResultado` vaciaba el `ValidationMessageStore` en cada envío y solo lo volvía a llenar con otra instancia de `Resultado`; un envío que no lo reemplaza dejaba los mensajes sin `aria-invalid` ni borde vino — los errores se copian al `EditContext` en cada `OnParametersSet`; el foco sigue atado a la instancia nueva; prueba interactiva nueva.

## Design Notes

- **Cómo lo usa una página** (la reserva lo hará así):

  ```razor
  <EditForm Model="Comando" FormName="datos" OnValidSubmit="EnviarAsync">
      <ErroresDelResultado Resultado="resultado">
          <Campo Para="() => Comando.Nombre" Etiqueta="Nombre completo" autocomplete="name" />
          <Campo Para="() => Comando.Correo" Etiqueta="Correo" Tipo="email" Ayuda="Te enviamos la confirmación." />
      </ErroresDelResultado>
      <button type="submit" class="btn btn-primary">Continuar</button>
  </EditForm>
  ```
  `EnviarAsync` pide el `ICommandHandler` y guarda el `Resultado` que vuelve; sin `DataAnnotationsValidator`, `OnValidSubmit` siempre se dispara.
- **Por qué `autofocus` y no script.** La 1.8 fijó que el sitio no carga scripts. La respuesta a un POST en SSR estático es una página completa: el navegador aplica `autofocus` al cargarla. En interactivo el DOM se parchea y `autofocus` no vuelve a actuar; ahí va `FocusAsync`, que usa el script de Blazor que ese modo ya trae.

## Verification

**Commands:**
- `./dotnet.sh build` -- expected: 0 advertencias, 0 errores.
- `./dotnet.sh test` (tres veces seguidas) -- expected: todas pasan, 117 previas más las nuevas.

**Manual checks (if no CLI):**
- Ninguno: el host no cambia, así que no hace falta `verificar-arranque.sh`; se corre de todos modos si es barato.

## Auto Run Result

- **Cambio:** dos componentes en `UI/Componentes/`. `ErroresDelResultado`, dentro del `EditForm`, recibe el `Resultado`, pinta arriba los errores del objeto, copia los de campo al `EditContext` (para que `InputText` pinte `aria-invalid`) y reparte el foco. `Campo` pinta etiqueta, control, ayuda y mensajes en vino, con `autofocus` en SSR estático o `FocusAsync` en interactivo para el primer error. Estilos con tokens; bUnit 2.11.3 en un proyecto de pruebas nuevo sin Docker.
- **Archivos:**
  - `src/DraSelina.Consultorio.Web/UI/Componentes/ErroresDelResultado.razor` — padre en cascada, errores del objeto, foco.
  - `src/DraSelina.Consultorio.Web/UI/Componentes/Campo.razor` — el "Campo" de DESIGN.md.
  - `src/DraSelina.Consultorio.Web/UI/_Imports.razor` — `@using` de formularios y componentes.
  - `src/DraSelina.Consultorio.Web/wwwroot/css/consultorio.css` — `.dsm-campo*` y `.dsm-errores-del-objeto`.
  - `tests/DraSelina.Consultorio.UI.Tests/` — proyecto, `ComandoDePrueba`, `FormularioDePrueba`, 11 pruebas.
  - `Directory.Packages.props`, `DraSelina.Consultorio.slnx` — bUnit y el proyecto.
  - `README.md` — sección "Errores en el formulario" y lista de proyectos de prueba.
  - `_bmad-output/.../ux-appointment-booking/DESIGN.md` — estado de error de "Campo" (decisión de Gerardo).
- **Revisión:** 3 hallazgos, los 3 corregidos (1 medium, 2 low); ninguno diferido ni rechazado.
- **Revisión de seguimiento recomendada:** no (un solo medium corregido, ningún high).
- **Verificación:** `./dotnet.sh build` 0 advertencias, 0 errores; `./dotnet.sh test` tres veces tras los parches: 128 de 128, 0 omitidas. Las seis filas de la matriz tienen prueba que corrió. El host no cambió; no se corrió `verificar-arranque.sh`.
- **Riesgos:** el SSR estático se simula con bUnit (sin POST real); el primer POST real llega con la historia de reserva. El foco depende del orden de pintado de los `Campo`. Un error de una propiedad sin `Campo` no se ve.
