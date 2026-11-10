---
title: 'Reloj desplazado y recorrido de UJ-4 y UJ-6'
type: 'chore'
ticket: '8'
created: '2026-11-10'
status: 'in-progress'
baseline_revision: 'fdb9be71dcdb9759cbcd5eb49e0190f70f70dac9'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: ''
review_source: ''
lenses_ran: []
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Las entradas 5.1 a 5.7 están probadas automáticamente, pero ninguna prueba recorre la administración en un navegador real con el AppHost, Mailpit y el reloj desplazado; sin mockups (Decision 2026-11-09), este recorrido es donde Gerardo aprueba o pide cambios (Done when 2 a 5 de la épica).

**Approach:** Sin código: el reloj desplazado ya existe (`Reloj:Desplazamiento` en `src/DraSelina.Consultorio.Web/appsettings.Development.local.json`, entrada 3.5). Gerardo sigue la guía de abajo; lo que no le guste se vuelve ajuste o entrada nueva.

</frozen-after-approval>

## Implementation Notes

Ruta oneshot sin cambios de código. Fechas de la guía con hoy martes 10 de noviembre de 2026; si el recorrido se hace otro día, se corren igual (mañana, pasado mañana y el sábado siguiente).

## Guía del recorrido

**Preparación**
1. Contraseña inicial, si no se puso: `dotnet user-secrets set "Parameters:clave-administrador" "<contraseña>" --project src/DraSelina.Consultorio.AppHost` (mayúscula, minúscula, dígito y símbolo).
2. Sin desplazamiento: `appsettings.Development.local.json` sin `Reloj:Desplazamiento`, o en `"00:00:00"`.
3. `dotnet run --project src/DraSelina.Consultorio.AppHost`; del panel, abrir la web y Mailpit.

**1. Paciente conocido (preparación de UJ-4)**
- En el sitio, reservar en línea para mañana miércoles 11 a las 4:00 pm: "Mariana López", un celular de prueba, `mariana@correo.com`. En Mailpit, la confirmación.

**2. Entrar (5.1)**
- `/admin`: correo `administrador@consultorio.local` y la contraseña. La primera vez, escanear el QR, escribir el código y guardar los códigos de recuperación. Se llega a la Agenda de hoy.

**3. UJ-4: Cita pactada por WhatsApp (5.3, 5.4)**
- En la Agenda, "Agendar cita". Capturar el celular y el nombre de Mariana: "Este paciente ya existe" y "Próxima: miércoles 11, 4:00 pm".
- Jueves 12 a las 4:10 pm (fuera de la rejilla), un tipo, Consentimiento, guardar. Se vuelve a la Agenda del jueves y la Cita está ahí. En Mailpit, la confirmación.

**4. UJ-6, primera mitad: el sábado de Selina (5.3, 5.4, 5.2)**
- "Agendar cita" para el sábado 14 a las 10:15 con otro Paciente ("Jorge Ramírez"). Aviso de fuera de horario; guardar sin nota da el error en Nota; con "lo pidió Selina, no puede entre semana", guardar.
- Agenda en vista de semana: el sábado con la Cita, "Fuera de horario" y su nota; el domingo atenuado.
- En una ventana privada, `/reservar`: el sábado 14 no se ofrece.

**5. Refresco de la Agenda (5.2)**
- Con la Agenda del miércoles 11 abierta, reservar en línea en otra ventana para el miércoles 5:00 pm. Aparece en menos de 30 segundos sin recargar.

**6. No se presentó con el reloj desplazado (5.5, 5.6)**
- Escribir en `appsettings.Development.local.json` `{ "Reloj": { "Desplazamiento": "<d.hh:mm:ss>" } }` para que "ahora" sea el miércoles 11 a las 4:10 pm (Gerardo pide el valor y se le calcula con la hora del momento). La web lo toma al guardar, sin reiniciar.
- Abrir la Cita de Mariana del miércoles desde la Agenda: solo "No se presentó". Confirmar: queda "No se presentó" y la bitácora suma la entrada del Administrador. Repetir en vista de teléfono (herramientas del navegador).

**7. Cancelar por el Consultorio (5.5, 5.6)**
- Con el mismo reloj, abrir la Cita de Mariana del jueves 12: solo "Cancelar"; la confirmación dice que recibirá una disculpa por correo. Confirmar: "Cancelada (por el consultorio)". En Mailpit, la disculpa con "Elegir otro horario".
- Quitar el desplazamiento.

**8. Pacientes (5.7)**
- "Pacientes": buscar "mariana", "MARIANA" y "lopez". Abrir a Mariana: resumen ("… · 1 no se presentó"), sus Citas de la más reciente a la más antigua, cada una abre su Detalle. "Volver a Pacientes" conserva la búsqueda (y ver si la lista completa parpadea antes de la filtrada).

**9. Textos por revisar** (`[ASSUMPTION: texto]` de las entradas)
- Agendar cita: "Este paciente ya existe.", avisos de horario, mensajes de traslape y hora pasada, texto de la casilla de Consentimiento.
- Detalle: preguntas de confirmación, "Esto no se puede deshacer.", "No, regresar", "Por fuera del sitio", teléfono "444 123 4567".
- Pacientes: "Hay más pacientes con ese nombre…", "Todavía no hay pacientes.", "Volver a Pacientes", solo la hora de inicio en el historial.

## Resultado del recorrido

Lo llena Gerardo: qué aprueba y qué pide cambiar (cada cambio se vuelve ajuste o entrada nueva).

- 

## Verification

**Manual checks:**
- Los pasos 1 a 9 de la guía, hechos por Gerardo con el AppHost.
