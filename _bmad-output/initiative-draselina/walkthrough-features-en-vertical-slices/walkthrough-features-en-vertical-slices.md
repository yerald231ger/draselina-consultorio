# Recorrido guiado: Features en vertical slices

**Objetivo:** revisar el refactor que organiza `Modules/<Módulo>/Features/` por caso de uso, con una carpeta por caso de uso y lo compartido en `Features/Compartido/`.

**Target:** commits `9366177` (solo movimientos) y `aaf1976` (extracciones a `Compartido/`, prueba y documentos) en `main`.
**Baseline:** `ac89cf7`.

**Plan:** [plan-features-en-vertical-slices.md](../plan-features-en-vertical-slices.md)

**Bitácora:** [walkthrough-features-en-vertical-slices-log.md](walkthrough-features-en-vertical-slices-log.md)

**Estado del recorrido:** completo (7 de 7 bloques aceptados).

Cada bloque queda revisado solo cuando Gerardo lo dice. Estado de cada bloque: `pendiente` o `aceptado`.

---

## Bloques

### 1. Intención

- [x] Estado: aceptado

Tomada textual del plan, sección Intent.

#### Intent

**Problem:** `Modules/<Módulo>/Features/` es una carpeta plana con todo junto: en Agenda, 60 archivos con 32 casos de uso, 18 interfaces y 10 helpers; en Notificaciones, 26 con casos de uso, puertos y plantillas mezclados. Cuesta encontrar los casos de uso, y Gerardo quiere que Features muestre los casos de uso.

**Approach:** vertical slices, con una carpeta por caso de uso dentro de `Features/` y un archivo por tipo. Lo que solo usa ese caso de uso (puerto de lectura, records, helpers, plantillas) va en su carpeta. Lo que usan dos o más casos de uso, y los puertos transversales (transacción, outbox, candados, bitácora), va en `Features/Compartido/`. Las reglas de dependencia no cambian.

**Decisiones (Gerardo, 2026-11-21):**
- Namespace plano: todo sigue en `DraSelina.Consultorio.<Módulo>.Features`, sin importar la subcarpeta. Un namespace por slice choca con el comando del mismo nombre (CS0118, comprobado). En Rider se apaga la inspección de namespace contra carpeta solo en el proyecto Web.
- Una carpeta por caso de uso, no por área.
- Cada plantilla de correo exclusiva va al slice que la usa. Las compartidas van a `Compartido/`.
- Ningún caso de uso llama código del slice de otro. Lo que hoy se comparte desde un handler se saca a `Compartido/` (Gerardo, 2026-11-21, al ver las cuatro llamadas entre slices).

### 2. Trazo general

- [x] Estado: aceptado

Dos commits: `9366177` mueve 90 archivos sin tocar contenido; `aaf1976` saca a `Compartido/` lo que un caso de uso tomaba de otro, agrega la prueba que protege la estructura y actualiza los documentos. Para entenderlo, abre en este orden:

1. [DetalleDeCita/](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/DetalleDeCita/) — un slice típico: el handler y los dos puertos que solo él usa.
2. [Compartido/](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/Compartido/) — lo que usan dos o más casos de uso y los puertos transversales.
3. [CalendarioDeLaCita.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/Compartido/CalendarioDeLaCita.cs) — la extracción más grande.
4. [SolucionEntregadaTests.cs:41](../../../tests/DraSelina.Consultorio.ArchitectureTests/SolucionEntregadaTests.cs#L41) — la prueba que impide archivos sueltos en la raíz de Features.

### 3. Los movimientos (commit 9366177)

- [x] Estado: aceptado

Cada archivo de Features pasó, con el mismo contenido y el mismo namespace, a la carpeta de su caso de uso (nombre del comando o consulta, sin "Handler") o a `Compartido/`. Como el namespace no cambia, ni `Infrastructure` ni las pruebas tuvieron que tocar un `using`.

Ejemplo, Agenda: [DetalleDeCita/](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/DetalleDeCita/) tiene [DetalleDeCitaHandler.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/DetalleDeCita/DetalleDeCitaHandler.cs), [ICitaDelDetalle.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/DetalleDeCita/ICitaDelDetalle.cs) e [IBitacoraDeLaCita.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/DetalleDeCita/IBitacoraDeLaCita.cs).

Ejemplo, Notificaciones: [AvisoAlCancelar/](../../../src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/AvisoAlCancelar/) tiene [AvisoAlCancelar.cs](../../../src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/AvisoAlCancelar/AvisoAlCancelar.cs) y [PlantillaDeCancelacion.cs](../../../src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/AvisoAlCancelar/PlantillaDeCancelacion.cs).

Mismo trato: [Agenda/Features/](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/) (33 slices), [Notificaciones/Features/](../../../src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/) (11) y [Pacientes/Features/](../../../src/DraSelina.Consultorio.Web/Modules/Pacientes/Features/) (2).

### 4. CalendarioDeLaCita (commit aaf1976)

- [x] Estado: aceptado

`ArchivoDeCalendarioPorEnlaceHandler` recibía la clase `AgendaApi` solo para pedirle el archivo. El enlace de gestión y el archivo de calendario pasaron a `CalendarioDeLaCita`, en `Compartido/`; `AgendaApi` delega en ella y el handler la recibe directo. `GeneradorDeCalendario` se fue con ella.

- Nuevo: [CalendarioDeLaCita.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/Compartido/CalendarioDeLaCita.cs).
- [AgendaApi.cs:18](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/AgendaApi/AgendaApi.cs#L18) y [AgendaApi.cs:21](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/AgendaApi/AgendaApi.cs#L21) — delegan en el calendario.
- [ArchivoDeCalendarioPorEnlaceHandler.cs:23](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/ArchivoDeCalendarioPorEnlace/ArchivoDeCalendarioPorEnlaceHandler.cs#L23) — usa el calendario directo.
- [Registro.cs:54](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Infrastructure/Registro.cs#L54) — registro nuevo.
- [EnlaceDeGestionTests.cs:141](../../../tests/DraSelina.Consultorio.IntegrationTests/EnlaceDeGestionTests.cs#L141) — arma `AgendaApi` con el calendario.

### 5. Los helpers estáticos

- [x] Estado: aceptado

Cambio al aceptar (commit `bb494af`, sin push): `OmitidoDelRecordatorio.RegistrarAsync` pasó de `public` a `internal`, y `SemanaTipoGuardada.DeLunesADomingo` pasó de `internal` a `private`. Build con 0 advertencias; pruebas de arquitectura 29 de 29.

Cuatro helpers estáticos vivían dentro de un handler y otro los llamaba. Salieron, sin cambiar su lógica, a clases `internal static` en `Compartido/`.

- [RastroDeAgendaCustom.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/Compartido/RastroDeAgendaCustom.cs) — usado en [GuardarAgendaCustomHandler.cs:121](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/GuardarAgendaCustom/GuardarAgendaCustomHandler.cs#L121), [:134](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/GuardarAgendaCustom/GuardarAgendaCustomHandler.cs#L134) y en [BorrarAgendaCustomHandler.cs:83](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/BorrarAgendaCustom/BorrarAgendaCustomHandler.cs#L83), [:93](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/BorrarAgendaCustom/BorrarAgendaCustomHandler.cs#L93).
- [ValoresDeReglasDeReserva.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/Compartido/ValoresDeReglasDeReserva.cs) — usado en [ReglasDeReservaVigentesHandler.cs:14](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/ReglasDeReservaVigentes/ReglasDeReservaVigentesHandler.cs#L14) y [CambiarReglasDeReservaHandler.cs:39](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/CambiarReglasDeReserva/CambiarReglasDeReservaHandler.cs#L39).
- [SemanaTipoGuardada.cs](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/Compartido/SemanaTipoGuardada.cs) — usado en [SemanaTipoHandler.cs:16](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/SemanaTipo/SemanaTipoHandler.cs#L16) y [SemanaDeAtencionHandler.cs:19](../../../src/DraSelina.Consultorio.Web/Modules/Agenda/Features/SemanaDeAtencion/SemanaDeAtencionHandler.cs#L19).
- [OmitidoDelRecordatorio.cs](../../../src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/Compartido/OmitidoDelRecordatorio.cs) (Notificaciones) — usado en [RecordatorioAlAgendar.cs:47](../../../src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/RecordatorioAlAgendar/RecordatorioAlAgendar.cs#L47) y [RecordatorioAlReprogramar.cs:48](../../../src/DraSelina.Consultorio.Web/Modules/Notificaciones/Features/RecordatorioAlReprogramar/RecordatorioAlReprogramar.cs#L48). Este lo encontró la revisión: estaba como segunda clase dentro de `RecordatorioAlAgendar.cs`.

### 6. Lo que protege la estructura

- [x] Estado: aceptado

Cambio al aceptar (commit `626f1e6`, ya en `origin/main` junto con `bb494af`): se agregó la prueba que protege la regla entre carpetas. `Verificador.VerificarCasosDeUso` revisa que ningún archivo de `Features/<Carpeta>/` nombre un tipo de primer nivel de la carpeta de otro caso de uso del mismo módulo, y que `Compartido/` no nombre tipos de un caso de uso. Comentarios, cadenas y lo que declara la propia carpeta no cuentan. Lo cubren 3 casos sintéticos en `VerificadorTests` y la prueba real `Ningun_caso_de_uso_usa_codigo_de_la_carpeta_de_otro`. Prueba de mutación: se inyectaron dos referencias cruzadas, ambas se detectaron y luego se revirtieron. Resultado: 1181 de 1181 pruebas en verde.

Antes, en este bloque, la regla entre carpetas no estaba protegida por ninguna prueba; ahora sí.

- [SolucionEntregadaTests.cs:41](../../../tests/DraSelina.Consultorio.ArchitectureTests/SolucionEntregadaTests.cs#L41) — la prueba nueva falla si aparece un `.cs` en la raíz de cualquier `Features`.
- [Verificador.cs:109](../../../tests/DraSelina.Consultorio.ArchitectureTests/Verificador.cs#L109) — el namespace plano sigue validado por el verificador existente (namespace igual a la capa o dentro de ella).
- [DraSelina.Consultorio.Web.csproj.DotSettings](../../../src/DraSelina.Consultorio.Web/DraSelina.Consultorio.Web.csproj.DotSettings) — apaga la inspección de namespace contra carpeta en Rider.

Un namespace por carpeta no compila: la carpeta `AgendarCitaEnLinea` choca con el comando `AgendarCitaEnLinea` (CS0118). Por eso el namespace es plano.

### 7. Periferia

- [x] Estado: aceptado

- [architecture-appointment-booking.md](../architecture-appointment-booking/architecture-appointment-booking.md) — tabla de capas, AD-4 y árbol.
- [ddd-and-data-model-appointment-booking.md](../architecture-appointment-booking/ddd-and-data-model-appointment-booking.md) — 2.6, políticas e historial.
- [README.md](../../../README.md) — dónde va cada cosa y rutas de transacción y outbox.
- [estructura-del-proyecto.md](../../../docs/estructura-del-proyecto.md) — árbol y tabla.
- [.memlog.md](../architecture-appointment-booking/.memlog.md) — las dos decisiones.
- [plan-features-en-vertical-slices.md](../plan-features-en-vertical-slices.md) — plan, notas y triage de la revisión.
