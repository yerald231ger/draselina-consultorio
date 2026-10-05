# Trazabilidad — capacidad, requisito, decisión, regla y pantalla

Para escribir una historia: toma la capacidad, lee sus `FR-n` en el PRD (consecuencias verificables), y cita las decisiones y reglas de esta tabla. Las `AD-n` están en la arquitectura; las `E/T/INV/RN/P` en el documento de dominio; las superficies y estados, en `EXPERIENCE.md`.

| Capacidad | FR | Recorrido | Arquitectura | Dominio | Superficie UX |
| --- | --- | --- | --- | --- | --- |
| CAP-1 Tipo y horarios libres | FR-1, FR-2 | UJ-1 | AD-5, AD-11, AD-17 | INV-V1, RN-3, RN-4, RN-14, T-21, `CalculadoraDeDisponibilidad` | Reserva 1 y 2; Reserva en escritorio |
| CAP-2 Reservar y confirmar | FR-3, FR-5 | UJ-1 | AD-7, AD-11, AD-12, AD-19, AD-23, AD-24, AD-26, AD-27, AD-28 | `Cita.Agendar`, INV-C1 a C5, C9, C11, RN-2, RN-20 a RN-23, RN-28 | Reserva 3; Cita confirmada |
| CAP-3 Sin doble reserva | FR-4 | UJ-1 | AD-8, AD-26, AD-27 | RN-1, RN-20, RN-21, T-22 | Estado "Horario tomado al confirmar" |
| CAP-4 Reconocer al Paciente | FR-6 | UJ-1, UJ-4 | AD-18, AD-28 | `Paciente`, INV-P1 a P3, RN-25 a RN-30 | Reserva 3; Agendar cita (administración) |
| CAP-5 Protección contra abuso | FR-7 | — | AD-13, AD-22 | RN-18 | Estado "Tope semanal alcanzado" |
| CAP-6 Ver la Cita | FR-8 | UJ-2 | AD-19, AD-22, AD-23 | RN-22, RN-31 | Tu cita; Enlace no válido |
| CAP-7 Reprogramar | FR-9 | UJ-2 | AD-7, AD-26 | `cita.Reprogramar`, RN-15, RN-17 | Tu cita; Día y hora; Cita reprogramada |
| CAP-8 Cancelar | FR-10 | UJ-2 | AD-9 | `cita.Cancelar`, RN-16, RN-17, E-16 | Cancelar cita; Cita cancelada |
| CAP-9 Acceso | FR-11 | — | AD-22, AD-25 | E-3 | Iniciar sesión |
| CAP-10 Agenda base | FR-12 | — | AD-5, AD-6, AD-17 | `AgendaAtencion.CambiarBase`, INV-B1 a B5, RN-5 a RN-8 | Configuración |
| CAP-11 Agenda custom | FR-13 | UJ-3, UJ-6, UJ-7 | AD-5, AD-6, AD-26 | `RegistrarCustom`, `CambiarCustom`, `BorrarCustom`, INV-A1 a A6, RN-8 a RN-12, P-1, T-5, T-29 a T-31 | Calendario de atención |
| CAP-12 Ver la agenda | FR-14 | UJ-4 | AD-10, AD-11 | RN-19, T-17 | Agenda; Detalle de Cita |
| CAP-13 Cita manual | FR-15 | UJ-4, UJ-6 | AD-7, AD-8, AD-24 | `Cita.AgendarPorConsultorio`, INV-C6, RN-13, RN-14 | Agendar cita (administración) |
| CAP-14 No se presentó y cancelar por el Consultorio | FR-16, FR-17 | — | AD-9 | `MarcarNoSePresento`, `CancelarPorConsultorio`, INV-C7, INV-C8, RN-31 | Detalle de Cita |
| CAP-15 Pacientes e historial | FR-18 | — | AD-10, AD-18 | RN-29, E-12 | Pacientes |
| CAP-16 Configurar reglas | FR-19 | — | AD-14 | `ReglasDeReserva`, `TipoDeVisita`, RN-17, INV-C3 | Configuración |
| CAP-17 Bitácora | FR-20 | — | AD-16, AD-22 | RN-24, E-3, T-31 | Detalle de Cita |
| CAP-18 Avisos al Paciente | FR-21, FR-22, FR-23, FR-26 | UJ-1, UJ-2, UJ-3 | AD-3, AD-19, AD-20 | T-23 a T-27, E-14, E-15 | Mensajes por correo |
| CAP-19 Alerta de Cancelación tardía | FR-24 | UJ-2 | AD-3, AD-20 | E-16, T-27 | Mensajes por correo |
| CAP-20 Agenda del día | FR-25 | UJ-5 | AD-20, AD-23 | T-24, T-27 | Mensajes por correo |
| CAP-21 Sitio | FR-27 a FR-30 | UJ-1 | AD-11, AD-15 | E-5 | Sitio · Inicio; Aviso de privacidad |

## Lo que atraviesa todas las capacidades

| Tema | Dónde |
| --- | --- |
| Referencias entre proyectos y módulos | AD-1, AD-2, AD-3; E-10 a E-13; T-28 |
| Cambio de estado solo por el agregado | AD-4; T-1 a T-7 |
| Casos de uso por `ISender`, con `Resultado` | AD-10; T-13, T-14 |
| Validación de forma | AD-12 |
| Tiempo | AD-17; T-20; RN-31 |
| Telemetría | AD-21 |
| Migraciones y datos iniciales | AD-25; dominio, "Datos iniciales" |
| Modelo de datos | Dominio, Parte II |
| Cómo se hace cumplir cada regla | Dominio, 2.7 |
| Voz, accesibilidad y responsivo | `EXPERIENCE.md` |
| Tokens visuales y componentes | `DESIGN.md` |

## Cómo del PRD que no es regla

- FR-7: limitador de frecuencia de ASP.NET Core y Cloudflare Turnstile.
- FR-29: marcado schema.org `Physician` y Perfil de Negocio de Google.
- Pruebas: xUnit v3, bUnit y Testcontainers (arquitectura, Stack); recorridos con Playwright solo en local (addendum).
