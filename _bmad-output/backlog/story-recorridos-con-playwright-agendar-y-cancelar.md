---
id: 1
type: story
title: "Recorridos con Playwright: agendar con y sin correo, y cancelar desde el correo"
parent: none
covers: []
after: []
assignee: ""
refined: true
hitl: false
risk: low
---

# Recorridos con Playwright: agendar con y sin correo, y cancelar desde el correo

## Description

Un proyecto nuevo de pruebas de extremo a extremo recorre en un navegador real, en vista de celular, los dos caminos felices del Paciente: agendar una Cita (con correo y sin correo) y cancelarla con el Enlace de gestión que llega por correo. Las pruebas arrancan el AppHost completo y corren solo en local. Recuperan los recorridos que el plan tenía y que se perdieron al pasar a la arquitectura.

## Acceptance Criteria

1. **Agendar con correo**
   **Given** el AppHost recién arrancado por la prueba, con la Agenda base sembrada
   **When** el Paciente, en vista de celular, va de "Agendar cita" a Tipo de consulta, Día y hora (el primer horario libre) y Tus datos con nombre, celular, correo, motivo y Consentimiento, y toca "Confirmar cita"
   **Then** ve "Tu cita está confirmada" con la fecha y la hora elegidas y el Enlace de gestión
   **And** Mailpit recibe un correo de confirmación para ese correo, con el mismo Enlace de gestión
2. **Agendar sin correo**
   **Given** el AppHost arrancado por la prueba
   **When** el Paciente hace el mismo recorrido sin escribir correo
   **Then** ve la confirmación con la frase de "sin correo" y el panel del Enlace de gestión antes de la tarjeta de la Cita
   **And** Mailpit no recibe ningún correo para esa Cita
3. **Cancelar desde el correo**
   **Given** una Cita que la propia prueba agendó con correo, a más de 2 horas de su inicio
   **When** el Paciente abre el Enlace de gestión tomado del correo en Mailpit, toca "Cancelar cita" y confirma
   **Then** ve la confirmación de la cancelación
   **And** al abrir de nuevo el enlace, "Tu cita" muestra la Cita cancelada
4. **Cada prueba es independiente**
   **Given** las tres pruebas
   **When** se corre cualquiera sola, o las tres en cualquier orden
   **Then** cada una pasa, porque agenda su propia Cita con datos que no chocan con las otras
5. **La verificación anti-bots se respeta**
   **Given** Tus datos con su campo trampa y su tiempo mínimo
   **When** la prueba envía el formulario
   **Then** lo hace después del tiempo mínimo y sin llenar el campo trampa; no hay ningún interruptor que apague la verificación para las pruebas
6. **Solo en local**
   **Given** el flujo de GitHub Actions
   **When** corre en un push
   **Then** no ejecuta estas pruebas, y las demás pruebas corren como hoy
7. **Arranque en una compu nueva**
   **Given** una compu con Docker y el SDK de .NET, sin PowerShell
   **When** se siguen las instrucciones del proyecto de pruebas
   **Then** los navegadores de Playwright se instalan y las tres pruebas pasan con un solo comando

## Boundaries

- Must not change: el comportamiento de la web, del AppHost ni de la verificación anti-bots; las pruebas existentes y lo que corre en GitHub Actions.
- Fuera de alcance: reprogramar, el recorrido del Administrador, revisiones de accesibilidad y capturas de regresión visual (decisión del 2026-11-20).

## References

- architecture — _bmad-output/initiative-draselina/architecture-appointment-booking/architecture-appointment-booking.md, Consistency Conventions (fila Pruebas) y Stack
- ux — _bmad-output/initiative-draselina/ux-appointment-booking/EXPERIENCE.md, Key Flows UJ-1 y UJ-2
- plan — docs/Appointment Booking System — Solution Plan.md, Pruebas de UI/UX

## Notes

- Decision: solo en local, solo caminos felices, en vista de celular; estas tres pruebas y no más por ahora (Gerardo, 2026-11-20).
- Decision: las pruebas arrancan el AppHost con Aspire.Hosting.Testing, en la versión de Aspire (17.0.0), y leen el correo de la API de Mailpit (Gerardo, 2026-11-20).
- Decision: Microsoft.Playwright 1.65.1. PowerShell no está instalado: los navegadores se instalan llamando al instalador de Playwright desde .NET (Gerardo, 2026-11-20).
- Decision: el proyecto se llama `DraSelina.Consultorio.E2ETests`, vive en `tests/` y está en la solución para que compile en GitHub Actions; el flujo lo excluye al correr las pruebas. En local, `dotnet test` de la solución sí lo corre (Gerardo, 2026-11-20).
- Decision: cada prueba usa un celular distinto, para no tocar el tope semanal por teléfono (entrada 2.7) (Gerardo, 2026-11-20).
- El límite de frecuencia por IP (entrada 2.11) no estorba: permite 10 envíos cada 10 minutos, las tres pruebas hacen unos 4 y el contador vive en memoria, así que empieza en cero con cada arranque del AppHost. No se apaga ni se cambia para las pruebas.
- Contraseña temporal del Administrador: la prueba la pasa como argumento (`--Parameters:clave-administrador`), como en los recorridos con el tester.
