---
id: SPEC-appointment-booking
companions:
  - trazabilidad.md
  - ../prd-appointment-booking/prd-appointment-booking.md
  - ../architecture-appointment-booking/architecture-appointment-booking.md
  - ../architecture-appointment-booking/ddd-and-data-model-appointment-booking.md
  - ../architecture-appointment-booking/c4-appointment-booking.md
  - ../ux-appointment-booking/DESIGN.md
  - ../ux-appointment-booking/EXPERIENCE.md
sources:
  - ../prd-appointment-booking/addendum.md
  - ../brainstorm-agenda-horario-configuracion/brainstorm-agenda-horario-configuracion.md
---

> **Contrato canónico.** Este SPEC y los archivos de `companions:` son el contrato completo de qué construir, probar y validar. Los documentos de `sources:` quedan solo para trazabilidad.

# Sistema de citas — Dra. Selina Medina

## Why

Un dolor que resolver. Toda la agenda de la Dra. Selina (Ortopedia y Traumatología) vive en WhatsApp: cada cita, cambio y cancelación pasa por ella. El piloto le quita esa carga: los Pacientes reservan, reprograman y cancelan solos desde el teléfono, sin cuenta; el Administrador (Gerardo) atiende las excepciones desde una sola pantalla; Selina no usa el sistema y solo recibe un correo con sus citas del día. Ante cualquier duda de diseño, gana la opción que le quite trabajo a Selina (E-1).

## Capabilities

Cada capacidad cita sus requisitos. Las consecuencias verificables de cada `FR-n` están en el PRD y son parte del contrato; `trazabilidad.md` dice qué decisiones y reglas gobiernan cada capacidad.

**Reserva en línea**

- **CAP-1** (FR-1, FR-2)
  - **intent:** El Paciente elige un Tipo de visita reservable en línea y ve únicamente los Horarios libres de la Ventana de reserva.
  - **success:** Con la Agenda base inicial, un día de atención ofrece como máximo 3:30, 4:00, 4:30, 5:00 y 5:30 pm; no aparecen horarios de hoy, de fechas sin Tramos, posteriores a la ventana ni traslapados con una Cita, incluida una manual fuera de horario.
- **CAP-2** (FR-3, FR-5)
  - **intent:** El Paciente reserva un Horario libre con nombre, teléfono, correo opcional, Motivo de consulta y Consentimiento, y la Cita queda firme al instante.
  - **success:** Sin Consentimiento o con un teléfono imposible para México no hay reserva; al confirmar, la pantalla muestra fecha, hora, dirección, Enlace de gestión y "agregar a mi calendario", aunque no haya correo; todo en menos de 1 minuto desde un teléfono.
- **CAP-3** (FR-4)
  - **intent:** El sistema garantiza que dos Citas no canceladas nunca se traslapen.
  - **success:** Dos intentos simultáneos sobre el mismo horario producen exactamente una Cita; el otro ve los Horarios libres restantes, nunca un error; confirmar dos veces el mismo formulario devuelve la misma Cita.
- **CAP-4** (FR-6)
  - **intent:** El sistema reconoce a un Paciente existente por teléfono y nombre, o lo registra.
  - **success:** Mismo teléfono y nombre (sin distinguir mayúsculas, acentos ni espacios) dan el mismo Paciente; mismo teléfono con otro nombre da uno distinto.
- **CAP-5** (FR-7)
  - **intent:** El sistema limita las reservas automatizadas o excesivas.
  - **success:** Las páginas de reserva tienen límite de frecuencia y verificación anti-bots; una reserva en línea que supera el tope semanal del teléfono se rechaza con una explicación y el contacto de WhatsApp.

**Gestión por el Paciente**

- **CAP-6** (FR-8)
  - **intent:** El Paciente ve su Cita con el Enlace de gestión, sin cuenta.
  - **success:** El enlace muestra solo esa Cita, no es adivinable y deja de funcionar cuando el inicio de la Cita ya pasó.
- **CAP-7** (FR-9)
  - **intent:** El Paciente mueve su Cita a otro Horario libre.
  - **success:** Fuera del umbral de cancelación tardía (hoy 2 horas) la Cita cambia de horario y conserva identificador y enlace; dentro del umbral no se ofrece reprogramar sino cancelar y volver a agendar.
- **CAP-8** (FR-10)
  - **intent:** El Paciente cancela su Cita en cualquier momento antes de su inicio.
  - **success:** Nunca se impide; dentro del umbral queda como Cancelación tardía; el horario vuelve a ofrecerse de inmediato.

**Administración**

- **CAP-9** (FR-11)
  - **intent:** Solo el Administrador entra al área de administración.
  - **success:** Sin sesión con segundo factor, ninguna página, comando ni consulta de administración responde; no existe registro público.
- **CAP-10** (FR-12)
  - **intent:** El Administrador configura la semana tipo: por cada día, cero o más Tramos.
  - **success:** El cambio aplica de inmediato a las fechas sin Agenda custom; antes de guardar se listan las Citas que quedan fuera (ninguna se cancela) y las fechas con custom que no cambian; se rechaza un Tramo encimado o donde no quepa una Cita del tipo más corto.
- **CAP-11** (FR-13)
  - **intent:** El Administrador da, edita o borra el horario propio de una fecha, para recortarla, cerrarla o abrirla.
  - **success:** "Ciérrame el sábado y cancela todas excepto la de las 12:30": al guardar o borrar se listan las Citas que quedan fuera, el Administrador elige cancelar o conservar por cada una, las elegidas quedan Canceladas por el Consultorio a nombre del Administrador y sus Pacientes reciben la disculpa; las conservadas no cambian ni reciben aviso.
- **CAP-12** (FR-14)
  - **intent:** El Administrador ve la agenda por día y por semana.
  - **success:** Una Cita reservada en línea aparece sin recargar a mano; cada Cita muestra hora, Paciente, motivo y estado (incluida Atendida); se distinguen "sin correo", fechas con custom y Citas fuera de horario con su nota.
- **CAP-13** (FR-15)
  - **intent:** El Administrador registra una Cita pedida por teléfono o WhatsApp, o aceptada por Selina fuera de horario.
  - **success:** Se guarda fuera de la Rejilla, de la ventana, de los Tramos o en un día cerrado, con nota obligatoria cuando cae fuera de los Tramos; solo la impiden un traslape o una hora pasada; el público no ve disponibilidad por ella.
- **CAP-14** (FR-16, FR-17)
  - **intent:** El Administrador marca una Cita como No se presentó o la cancela a nombre del Consultorio.
  - **success:** No se presentó solo se ofrece desde el inicio de la Cita y es final; cancelar por el Consultorio solo antes del inicio y dispara la disculpa al Paciente.
- **CAP-15** (FR-18)
  - **intent:** El Administrador consulta los Pacientes y el historial de Citas de cada uno.
  - **success:** Busca por nombre; el historial distingue Atendida, No se presentó, Cancelada y Cancelación tardía.
- **CAP-16** (FR-19)
  - **intent:** El Administrador cambia las reglas sin intervención de desarrollo.
  - **success:** Cambiar anticipación mínima, ventana, umbral de cancelación tardía, tope semanal, hora del Recordatorio, hora de la Agenda del día o un Tipo de visita surte efecto en la siguiente reserva o envío, sin desplegar.
- **CAP-17** (FR-20)
  - **intent:** El sistema registra quién cambió qué y cuándo.
  - **success:** Cada reserva, reprogramación, cancelación, No se presentó y cambio de base o custom tiene una entrada con actor Paciente, Administrador o Sistema, escrita junto con el cambio.

**Notificaciones**

- **CAP-18** (FR-21, FR-22, FR-23, FR-26)
  - **intent:** El Paciente con correo recibe confirmación, Recordatorio el día anterior y aviso de reprogramación o cancelación.
  - **success:** Con el proveedor de correo caído, la reserva se completa igual y el correo sale después con reintentos; el Recordatorio se mueve al reprogramar y se elimina al cancelar; ningún mensaje pide confirmar.
- **CAP-19** (FR-24)
  - **intent:** El Administrador se entera de inmediato de una Cancelación tardía.
  - **success:** La alerta sale en cuanto ocurre la cancelación, con Paciente y hora.
- **CAP-20** (FR-25)
  - **intent:** Selina recibe por correo sus citas del día.
  - **success:** Cada fecha con Tramos o con al menos una Cita Agendada llega "Tus citas de hoy" con hora, Paciente y motivo, o diciendo que no hay citas.

**Sitio**

- **CAP-21** (FR-27, FR-28, FR-29, FR-30)
  - **intent:** El visitante conoce el Consultorio y puede iniciar la reserva desde cualquier parte del sitio.
  - **success:** "Agendar cita" es visible sin desplazarse en un teléfono; los horarios mostrados salen de la Agenda base; las páginas públicas se leen sin interfaz interactiva y tienen título con especialidad y ciudad; administración y Enlaces de gestión no son indexables.

## Constraints

- Las reglas `E-n`, `T-n`, `INV-n`, `RN-n` (dominio, Parte I) y `AD-1` a `AD-28` (arquitectura) son invariantes del diseño. Una historia las cita; no las reescribe ni las contradice. Solo Gerardo las cambia, primero en el documento.
- Lo marcado `[ASSUMPTION]` en cualquier documento se construye así, pero no es invariante.
- La primera historia es una prueba de arranque: .NET 11 con Wolverine 6 y el AppHost de Aspire. Si falla, se retrocede a .NET 10 y se pierde solo la validación en navegador.
- Stack fijo: .NET 11, Blazor Web App, PostgreSQL, Wolverine 6, Aspire 13, DataAnnotations, OpenTelemetry (versiones en la arquitectura, sección Stack).
- Quedan fuera: MediatR, MassTransit, Hangfire, FluentAssertions, FluentValidation, Serilog y Blazor WebAssembly standalone.
- Monolito modular: Agenda, Pacientes, Notificaciones y Contenido solo se hablan por `I<Módulo>Api` o por eventos de integración; las referencias entre proyectos son las de la tabla de la arquitectura y se verifican con pruebas de arquitectura (AD-1 a AD-3).
- La disponibilidad se calcula al vuelo; nada guarda slots. Un solo agregado, `AgendaAtencion`, resuelve los tramos vigentes y una sola `CalculadoraDeDisponibilidad` los convierte en horarios libres (AD-5).
- Una Cita ya agendada no se modifica sola. Las cancelaciones derivadas de una custom las elige el Administrador y ocurren después, por evento (AD-6, P-1).
- El no traslape lo garantiza la base de datos, no el código (AD-8).
- Público en SSR estático, sin estado del Paciente en el servidor; administración en Interactive Server (AD-11).
- Motivo de consulta, nombre, teléfono, correo y Enlace de gestión nunca viajan en eventos, registros, telemetría ni cadena de consulta. Ningún dato clínico entra al sistema (AD-23, E-2).
- El Consentimiento se registra por Cita (AD-24).
- Instantes en UTC; tramos y fechas de agenda en hora local de Ciudad de México; el reloj se inyecta (AD-17).
- Vocabulario del Glosario, literal. "Horario de atención", "Bloqueo", "Slot" guardado, "Sede" y "Settings" no existen (E-6, E-7). En pantalla, Tipo de visita se muestra como "Tipo de consulta".
- Móvil primero, en español de México, de tú; identidad visual de `DESIGN.md`. Si un mockup y `EXPERIENCE.md` se contradicen, gana `EXPERIENCE.md`.
- Único canal: correo, solo para Pacientes que lo dieron.
- No hay producción: el único entorno es local, levantado por el AppHost. Aprovisionamiento y despliegue a Azure (Mexico Central) están diferidos.
- El plan de solución en `docs/` es referencia narrativa, no contrato.

## Non-goals

- Expediente clínico: diagnósticos, notas, tratamientos.
- Cobro, anticipos, precios visibles, reglas de aseguradoras.
- Pedir al Paciente que confirme su Cita; aprobar Citas.
- Cuenta o pantalla para Selina; rol de Asistente.
- Recordatorios o avisos por WhatsApp.
- Varias sedes.
- Recurrencia en la Agenda custom.
- Fusionar Pacientes duplicados; retención y derechos ARCO.
- Editar el contenido del sitio desde administración.
- Entorno intermedio, despliegue automático y respaldo en otra región.
- Margen entre Citas (`BufferMin`), `InsurerId` y precio por Tipo de visita: puntos de extensión previstos, no se construyen.

## Success signal

- En las últimas dos semanas del piloto, Selina no agenda, mueve ni cancela ninguna Cita por su cuenta, y 70 % o más de las Citas las crea el propio Paciente en línea, con cero dobles reservas.

## Assumptions

- Las metas del Success signal y la definición del piloto (4 semanas) son supuestos del PRD (§9).
- El resto de los supuestos no se copia aquí: vive indexado en su documento. PRD §11; `EXPERIENCE.md`, "Índice de supuestos"; dominio, sección 3 y "Supuestos de la Parte II"; arquitectura, AD-11, AD-13 y Stack.
- Los que más pesan al construir: tope de 3 Citas por teléfono por semana, de lunes a domingo; sitio de una sola página con secciones en lugar de las páginas separadas de FR-27; toda la administración y todos los correos sin mockup; la agenda de administración se refresca cada 30 segundos; `TipoDeVisita` y `ReglasDeReserva` como agregados propios.

## Open Questions

Ninguna impide dividir el trabajo. Responsable: Gerardo.

**Se resuelven en la primera historia**

- ¿Wolverine 6 y el AppHost de Aspire arrancan sobre .NET 11?
- ¿El middleware de DataAnnotations de Wolverine entrega `Resultado` inválido en lugar de lanzar excepción?
- ¿Un `EditForm` puede ligarse directamente a los comandos de `Contracts`?
- ¿Hace falta `btree_gist` mientras la restricción use solo un rango?

**Antes de construir la superficie afectada**

- ¿El sitio queda en una página con secciones o en páginas separadas (FR-27)? ¿Entra completo al piloto?
- ¿El calendario de escritorio del Paciente muestra siete columnas siempre o solo los días con atención?
- ¿Qué pantallas de administración necesitan mockup?
- ¿Qué vino manda: `#6B2A45` (mockups) o `#6D2D45` (papelería)? ¿Se oscurece el borde de los campos para llegar a 3:1?
- ¿Quién arma el historial de Citas de FR-18, si Pacientes no conoce citas (RN-29)?
- ¿Qué límite de frecuencia se usa en FR-7?

**Antes del primer Paciente**

- ¿El Motivo de consulta viaja por correo en la Agenda del día, o solo hora y Paciente? ¿A qué hora sale cuando hay Citas por la mañana?
- ¿Cómo se avisa a un Paciente sin correo cuando el Consultorio cancela?
- ¿Qué Tipos de visita existen además de los dos públicos, y cuáles son solo "llámanos"?
- ¿Quién es el abogado que revisa el aviso de privacidad, y valida el Consentimiento por Cita? Requisitos vigentes de COFEPRIS para publicidad médica.
- Contenido pendiente de Selina: fotos, semblanza, cédulas, padecimientos, dirección y WhatsApp. ¿Cédula UANL o formación UASLP?
- ¿Qué versión de PostgreSQL ofrece Azure Flexible Server al aprovisionar? ¿Qué estrategia de respaldo, sin réplica en otra región?

**Al cerrar el piloto**

- ¿Hay línea base de la época de WhatsApp para comparar ausencias y cancelaciones tardías?
