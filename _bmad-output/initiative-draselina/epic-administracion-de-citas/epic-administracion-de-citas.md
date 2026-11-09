---
type: epic
title: "El administrador opera las citas desde una sola pantalla"
parent: initiative-draselina
covers: [CAP-9, CAP-12, CAP-13, CAP-14, CAP-15, CAP-17]
after: []
assignee: ""
risk: high
---

# El administrador opera las citas desde una sola pantalla

## Description

Gerardo entra con segundo factor, ve la agenda del día y de la semana, captura las Citas pactadas por WhatsApp (incluso fuera de horario), marca ausencias, cancela a nombre del Consultorio y consulta Pacientes con su historial y la bitácora de cada Cita.

## Outcome

Las excepciones se resuelven en un solo lugar; la señal es SM-C1 y SM-C3 del PRD.

## Requirements

La fuente son las capacidades de `covers` en el spec, con las consecuencias verificables de sus `FR-n` en el PRD; `tickets.toml` dice qué entrada entrega qué parte.

- **CAP-9** (FR-11): Identity en el host, segundo factor TOTP obligatorio, sin registro público; todo comando y consulta de administración lleva `IRequiereAdministrador` (AD-22). Entrada 1.
- **CAP-12** (FR-14): Agenda por día y por semana con los estados de `EXPERIENCE.md` y refresco a intervalos (AD-11). Entradas 1, 2 y 6.
- **CAP-13** (FR-15): Cita del consultorio con `Cita.AgendarPorConsultorio` (AD-7, INV-C6, RN-13, RN-14) y la pantalla Agendar cita. Entradas 3 y 4.
- **CAP-14** (FR-16, FR-17): `MarcarNoSePresento` y `CancelarPorConsultorio` (AD-9, RN-31), con la disculpa de la épica 4. Entradas 5 y 6.
- **CAP-15** (FR-18): `BuscarPacientes` en Pacientes e `HistorialDelPaciente` en Agenda (Decision del 2026-11-09). Entrada 7.
- **CAP-17** (FR-20), su parte: las entradas de bitácora de la Cita del consultorio, No se presentó y la cancelación por el Consultorio, y la vista de la bitácora en el Detalle de Cita. Entradas 3, 5 y 6. Los cambios de base y custom son de la épica 6.
- **CAP-21**, su parte: `/admin` no es indexable. Entrada 1.

## Done when

1. Sin sesión con segundo factor no responde ninguna página, comando ni consulta de administración; no existe registro público.
2. UJ-4 y la primera mitad de UJ-6 completos en local: Cita manual reconociendo al Paciente, y Cita en sábado con nota obligatoria que el público no ve.
3. Una Cita reservada en línea aparece en la agenda sin recargar a mano; se distinguen Atendida, "sin correo" y fuera de horario.
4. No se presentó solo se ofrece desde el inicio de la Cita; cancelar por el Consultorio, solo antes, y el Paciente recibe la disculpa.
5. La lista de Pacientes busca por nombre y su historial distingue Atendida, No se presentó, Cancelada y Cancelación tardía; el detalle de una Cita muestra su bitácora.

## Boundaries

El acceso (ASP.NET Core Identity en el host) y el marco de administración, la Agenda, el Detalle de Cita, Agendar cita y Pacientes. De CAP-17 entrega las entradas de sus cambios y la vista de la bitácora. Las páginas de administración no son indexables (parte de CAP-21 que le toca). No incluye el Calendario de atención ni Configuración (épica 6).

## References

- parent — _bmad-output/initiative-draselina/initiative-draselina.md
- spec — _bmad-output/initiative-draselina/spec-appointment-booking/spec-appointment-booking.md, sección Capabilities
- trazabilidad — _bmad-output/initiative-draselina/spec-appointment-booking/trazabilidad.md
- architecture — _bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md, AD-7 a AD-11, AD-16, AD-18, AD-22, AD-24, AD-25
- domain — _bmad-output/initiative-draselina/architecture-appointment-booking/ddd-and-data-model-appointment-booking.md, sección 2.2, RN-13, RN-14, RN-19, RN-29, RN-31
- design — _bmad-output/initiative-draselina/ux-appointment-booking/EXPERIENCE.md, Administración y State Patterns (Administrador)

## Notes

- Decision: "terminado" significa que corre en local con el AppHost y pasan sus pruebas de integración contra PostgreSQL real; no hay producción todavía (Gerardo, 2026-10-05).
- Decision: el segundo factor es una app autenticadora (TOTP de ASP.NET Core Identity); la primera vez se escanea un QR, después cada inicio de sesión pide el código de 6 dígitos. No hay código por correo (Gerardo, 2026-11-09, inception).
- Waits on epic-plataforma-base because: necesita el marcador de administración y el servicio de actor.
- Waits on epic-reserva-en-linea because: necesita la Cita, Pacientes y la bitácora.
- Waits on epic-gestion-por-el-paciente because: necesita `CitaCancelada` con su motivo y la marca de Cancelación tardía que distingue el historial.
- Waits on epic-notificaciones because: necesita `ObtenerContactos` y la disculpa por cancelación del Consultorio para comprobar FR-17.
- Decision: ninguna pantalla de esta épica lleva mockup; se construyen con `DESIGN.md` y las tablas de `EXPERIENCE.md`, y una entrada hitl al final recorre UJ-4 y la primera mitad de UJ-6 con el AppHost; lo que no guste ahí se vuelve ajuste o entrada nueva. Resuelve, para esta épica, la Open question de mockups de administración; el Calendario de atención y Configuración (épica 6) la deciden en su inception (Gerardo, 2026-11-09, inception).
- Decision: el historial de FR-18 son dos consultas, cada una de su módulo. `BuscarPacientes(nombre)` en Pacientes (id, nombre, teléfono, si tiene correo; nada de citas, RN-29) y `HistorialDelPaciente(PatientId)` en Agenda (Citas con su estado, Atendida derivada y tardía distinguida), cuyo handler pide el encabezado a `IPacientesApi.ObtenerContactos` (AD-10). Ninguna página compone dos módulos. Resuelve la Open question de FR-18 (Gerardo, 2026-11-09, inception).
- Decision: el camino trazador es la entrada 1, entrar con segundo factor y ver las citas del día: recorre Identity del host, una página Interactive Server, una consulta con `IRequiereAdministrador`, la lectura en Agenda y los contactos en lote de Pacientes. Es también lo menos seguro de la épica (Gerardo, 2026-11-09, inception).
- Decision: dos carriles después del trazador, lectura y pantallas (1, 2) y la Cita (3, 5, que tocan `Cita.cs` en serie); la 4 y la 6 los juntan, la 7 va al final porque comparte Pacientes con la 4 y enlaza al Detalle de la 6; la 8 es el recorrido de Gerardo y la 9 el Refactor sweep (Gerardo, 2026-11-09, inception).
- Decision: la navegación de administración muestra solo las pantallas que existen; la 5.1 deja Agenda y Salir, la 5.7 agrega Pacientes y la épica 6 agrega Calendario de atención y Configuración (Gerardo, 2026-11-09, plan de la 5.1).
- Traspaso de la épica 4 (entrada 5, Gerardo, 2026-11-08): la alerta de cancelación tardía al Administrador sale sin el botón "Abrir la Agenda" de EXPERIENCE.md; esta épica lo agrega a la plantilla de Notificaciones cuando la Agenda de administración tenga su ruta.
