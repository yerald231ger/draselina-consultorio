---
title: 'Aviso de privacidad integral y consentimiento aparte para el motivo de consulta'
type: 'feature'
ticket: ''
created: '2026-11-24'
status: 'built'
baseline_revision: 'e37ff8ea1c31ef6819110226c93c3dbc3c1f4837'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/README.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** La página del Aviso de privacidad tiene un texto provisional de cuatro líneas. La reserva pide una sola casilla que junta la aceptación del aviso con el consentimiento para el dato de salud, y el art. 8 de la LFPDPPP 2025 pide consentimiento expreso para los datos sensibles. Selina aprobó el texto del aviso integral y del simplificado (Gerardo, 2026-11-24).

**Approach:** Publicar en `/aviso-de-privacidad` el aviso integral aprobado, con secciones, y poner el aviso simplificado en Tus datos. Dividir la casilla en dos, las dos obligatorias. Guardar en el Consentimiento de cada Cita, por separado, que se dio el consentimiento para el Motivo de consulta.

El texto aprobado está en la pestaña "Borrador del aviso de privacidad" del documento https://claude.ai/artifact/BhGUc8Zta7ERRqf4DMcNTs. Se copia literal en `ContenidoApi`, con dos excepciones:
- El correo para las solicitudes y el código postal del consultorio aún no existen. Se muestran con el marcador `Pendiente` del sitio y se llenan después.
- La versión del aviso cambia, porque cambió el texto.

Decisiones de Gerardo (2026-11-24):
- Los derechos se ejercen por correo o por escrito en el consultorio.
- Los datos se guardan 5 años desde la última Cita.
- Dos casillas en la reserva en línea:
  1. "Acepto el Aviso de privacidad."
  2. "Doy mi consentimiento expreso para que se use el motivo de mi consulta con el fin de agendar y preparar mi cita."
- Administración ("Agendar cita", canal externo) conserva su casilla única, que ya nombra los datos de salud; su Consentimiento se guarda con el motivo incluido (Gerardo, 2026-11-24).
- La versión nueva del aviso es `2026-11-24-borrador` (Gerardo, 2026-11-24).

## Boundaries & Constraints

**Always:**
- El texto del aviso vive solo en Contenido (AD-15) y se despliega con la aplicación. Agenda guarda la versión y lee la vigente de `IContenidoApi`, como hoy.
- Sin cualquiera de las dos casillas no hay reserva. Cada una tiene su propio mensaje de error, que se anuncia como los demás del formulario.
- Las citas existentes conservan un Consentimiento válido.
- Los documentos y el memlog quedan con la fuente: Gerardo, 2026-11-24.

**Never:**
- Implementar el borrado automático a los 5 años: hoy es una revisión manual.
- Pedir el motivo como opcional.
- Agregar scripts de terceros o cookies nuevas.
- Editar migraciones ya existentes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Las dos casillas marcadas | Tus datos completo | Cita Agendada; su Consentimiento tiene la versión nueva y el consentimiento del motivo | No error expected |
| Falta la del aviso | Solo marcada la del motivo | Sin reserva; error en la casilla del aviso | El foco va a esa casilla |
| Falta la del motivo | Solo marcada la del aviso | Sin reserva; error propio en la casilla del motivo | El foco va a esa casilla |
| Horario tomado y reenvío | Las dos marcadas y el horario se ocupa | La pantalla de horario tomado conserva las dos casillas al reenviar | No error expected |
| Página del aviso | `/aviso-de-privacidad` | Las secciones del texto aprobado; el correo y el C.P. marcados Pendiente; la versión nueva | No error expected |
| Detalle de cita | Una Cita en línea nueva | El Consentimiento dice que incluye el motivo de consulta | No error expected |

</frozen-after-approval>

## Code Map

- `_bmad-output/initiative-draselina/aviso-de-privacidad-aprobado.md` -- EL TEXTO APROBADO, literal (copia de la pestaña del documento). Los marcadores entre corchetes son los datos pendientes y la versión.

- `Modules/Contenido/Contracts/IContenidoApi.cs` y `Modules/Contenido/Contenido/ContenidoApi.cs`:
  - Hoy `TextoDelAvisoDePrivacidad` es un `IReadOnlyList<string>` (:136, :173) y `AvisoDePrivacidad = "2026-11-borrador"`.
  - Pasa a secciones con título y bloques: párrafos y listas, con viñetas o numeradas.
  - Agregar el aviso simplificado y los datos pendientes como nulos: el correo de privacidad y el código postal.
  - `tests/DraSelina.Consultorio.UI.Tests/ContenidoDePrueba.cs:31` lo envuelve.
- `UI/Pages/AvisoDePrivacidad.razor` -- hoy pinta párrafos. Debe pintar las secciones y usar `<Pendiente Que="…"/>` (usado en `Inicio.razor:82`) donde falte el correo o el C.P.
- `UI/Pages/Reserva/TusDatos.razor:133-146` -- la casilla única. Debe llevar el aviso simplificado arriba y dos casillas, con el mismo patrón de `DescritoPor`, `LlevaElFoco` y `Mensajes`.
- `UI/Pages/Reserva/HorarioTomado.razor:51-53` -- reenvía `Comando.Consentimiento` oculto. Debe reenviar también el segundo.
- `Modules/Agenda/Contracts/AgendarCitaEnLinea.cs:62-64` -- `Consentimiento` con `[AllowedValues(true)]`. Agregar la propiedad del motivo, con su mensaje.
- `Modules/Agenda/Domain/Consentimiento.cs` -- hoy tiene `En`, `VersionDelAviso` y `Canal`. Agregar si incluye el Motivo de consulta. `Cita.Agendar` en `Cita.cs:114` exige el canal en línea; una Cita en línea exige además el consentimiento del motivo.
- `Modules/Agenda/Features/AgendarCitaEnLinea/AgendarCitaEnLineaHandler.cs:81` y `AgendarCitaPorConsultorio/AgendarCitaPorConsultorioHandler.cs:64` -- `Consentimiento.Crear(...)`.
- `Modules/Agenda/Infrastructure/AgendaDbContext.cs:227` -- `ComplexProperty` de Consentimiento, con columnas `consentimiento_*`. Columna nueva por migración: `./dotnet.sh ef migrations add … --context AgendaDbContext` (README). Las filas existentes quedan en `true`, porque su casilla única ya cubría los datos de salud.
- `Modules/Agenda/Contracts/DetalleDeCita.cs:95`, `Features/DetalleDeCita/DetalleDeCitaHandler.cs:49` y `UI/Pages/Administracion/DetalleDeCita.razor:418` -- la línea "En línea · fecha · aviso X" debe decir si incluye el motivo.
- `UI/Pages/Administracion/AgendarCita.razor:166-178` y `Contracts/AgendarCitaPorConsultorio.cs:21,67` -- la casilla de canal externo se queda como está; el handler crea el Consentimiento con el motivo incluido.
- Pruebas que tocan el Consentimiento:
  - Dominio: `CitaTests`, `CitaDelConsultorioTests`.
  - UI: `AgendarCitaTests`, `DetalleDeCitaTests`.
  - Integración: `ReservaEnLineaTests`, `TusDatosTests`, `AgendarCitaPorConsultorioTests`, `DetalleDeCitaTests` y otros cuatro con una línea.
  - E2E: `E2ETests/RecorridosDelPacienteTests.cs`, que marca la casilla.
- Documentos:
  - PRD FR-3 (Consentimiento).
  - Arquitectura AD-24 ("queda por validar con el abogado").
  - Modelo de datos: columnas `consentimiento_*` de `cita`, RN-23 e INV-C9.
  - `EXPERIENCE.md`: Tus datos.
  - `docs/despliegue.md`: la versión del aviso en las condiciones de lanzamiento.

## Tasks & Acceptance

**Execution:**
- [x] Contenido: contrato, texto aprobado literal en secciones, aviso simplificado, pendientes nulos y versión nueva -- una sola fuente del aviso.
- [x] Página del aviso y Tus datos, con el aviso simplificado, dos casillas y sus errores -- lo que ve el Paciente.
- [x] `HorarioTomado.razor` -- reenvía las dos casillas.
- [x] Contrato, dominio, handlers y migración del Consentimiento con el motivo -- se registra por separado.
- [x] Detalle de cita, y el handler de administración con el motivo incluido.
- [x] Pruebas de cada fila de la matriz, más las existentes ajustadas, incluida la E2E.
- [x] Documentos del Code Map y memlog de arquitectura (AD-24).

**Acceptance Criteria:**
- Given un Paciente en Tus datos, when lee la pantalla, then ve el aviso simplificado con el enlace al aviso completo antes de las dos casillas.
- Given una base con Citas anteriores, when se aplica la migración, then esas Citas siguen leyéndose y su Consentimiento incluye el motivo.

## Implementation Notes

- Contenido: `IContenidoApi.TextoDelAvisoDePrivacidad` pasa a `IReadOnlyList<SeccionDelAviso>` (título y bloques: `ParrafoDelAviso`, `ListaDelAviso` con `Numerada`); el texto va en tramos (`TextoDelAviso` con negritas, `DatoDelAviso` que puede faltar, `EnlaceDelAviso`). Nuevos `AvisoDePrivacidadSimplificado` y `CorreoDePrivacidad` (nulo); `DatosDelConsultorio.CodigoPostal` (nulo). Lo pinta `UI/Componentes/FragmentosDelAviso.razor`, que usa `<Pendiente>` para un dato nulo.
- La línea "Versión …" del aviso la pinta la página con la versión vigente, con punto final como el texto aprobado.
- El mensaje de la casilla del motivo no venía en el plan: "Para agendar tu cita necesitas dar tu consentimiento para el uso del motivo de tu consulta." (`AgendarCitaEnLinea.MensajeDeConsentimientoDelMotivo`).
- Dominio: `Consentimiento.IncluyeMotivoDeConsulta`; `Cita.Agendar` lanza `ArgumentException` sin él. Migración `20261125013626_AgregarConsentimientoDelMotivoACita` (columna `consentimiento_incluye_motivo`, `default true` para las filas existentes).
- Detalle de cita: "… · aviso X · incluye el motivo de consulta" (o "sin el motivo de consulta").
- AD-24 y RN-23 conservan "queda por validar con el abogado": el abogado aún no valida el registro por Cita.

## Plan Change Log

## Review Triage Log

Pasada 1 (lente quick): 0 high, 2 medium, 1 low, 1 false, 1 maybe-false.

| Hallazgo | Veredicto | Ruta | Evidencia |
| --- | --- | --- | --- |
| AD-24 y RN-23 siguen diciendo "queda por validar con el abogado" | false (en el texto) / low (memlog y notas) | patch | El orquestador lo restituyó a propósito: Gerardo no decidió quitarlo y el abogado no ha validado el registro por Cita. Lo incongruente es el memlog y las Implementation Notes, que dicen que se quitó; se corrigen. |
| `IContenidoApi.CorreoDePrivacidad` es una segunda fuente que nadie lee | medium | patch | Los fragmentos se arman con la constante; un `IContenidoApi` con correo seguiría pintando Pendiente. Se quita la propiedad. |
| `DEFAULT true` permanente en `consentimiento_incluye_motivo` | medium | patch | Un insert que omita la columna registra en silencio un consentimiento expreso de salud; `Arnes.SembrarCitaAsync` ya lo hace. Backfill sin dejar el default. |
| Una falla sin nombre en la corrida completa de integración | maybe-false | verificación | Las clases que toca el diff pasan 171/171; se aclara con una corrida completa sola, sin otra en paralelo. |

## Verification

**Commands:**
- `dotnet build` -- expected: sin errores ni advertencias nuevas
- `dotnet test` (con Docker encendido) -- expected: todo verde
- `git diff --stat -- '*Migrations/2026110*' '*Migrations/2026111*' '*Migrations/20261124*'` -- expected: vacío, porque ninguna migración anterior cambia

**Manual checks (if no CLI):**
- `/aviso-de-privacidad` y Tus datos en vista de celular: las secciones se leen, y las dos casillas se tocan completas, como la de hoy.
