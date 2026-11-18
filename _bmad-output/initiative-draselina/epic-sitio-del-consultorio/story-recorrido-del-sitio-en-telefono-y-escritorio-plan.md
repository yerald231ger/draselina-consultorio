---
title: 'Recorrido del sitio en teléfono y escritorio'
type: 'chore'
ticket: '5'
created: '2026-11-17'
status: 'built'
baseline_revision: '6094d7399657ee57114ca33053bb69dfabb6915a'
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

**Problem:** Las entradas 7.1 a 7.4 están probadas automáticamente, pero nadie ha visto `/` y el Aviso de privacidad en un navegador real contra los mockups `SitioInicio` y `SitioMovil` (Done when 1 y 2 de la épica).

**Approach:** Sin código. Gerardo sigue la guía de abajo con el AppHost; lo que apruebe y lo que pida cambiar se anota aquí, y los cambios van a la entrada 7.6.

</frozen-after-approval>

## Implementation Notes

Ruta oneshot sin cambios de código. La base sembrada es de lunes a viernes, 3:30 a 6:00 pm. Mockups en `_bmad-output/initiative-draselina/ux-appointment-booking/imports/mockup-canvas/SitioInicio.dc.html` (escritorio) y `SitioMovil.dc.html` (390 px). Lo pendiente de Selina (foto, semblanza, afiliaciones, descripciones, universidad, WhatsApp) se ve como `[Pendiente: …]` o "Escríbenos por WhatsApp" a propósito.

## Guía del recorrido

**Preparación**
1. `dotnet run --project src/DraSelina.Consultorio.AppHost` (con `PATH=$HOME/.dotnet:$PATH` si el SDK del PATH no es 10.0.4xx); del panel, abrir la web.
2. Tener a mano el mockup en otra pestaña. En las herramientas del navegador, vista de dispositivo a 390 × 844.

**1. Teléfono, 390 px: arriba del pliegue (FR-28)**
- En `/`, sin desplazarse: marca, navegación (baja de renglón) y "Agendar cita" a la vista.
- Tocar "Agendar cita": lleva a `/reservar`, paso Tipo de consulta. Regresar.

**2. Teléfono: la página de arriba abajo**
- Portada: especialidad, nombre, frase, "Agendar cita en línea" con su flecha a lo ancho, "Escríbenos por WhatsApp y te ayudamos.", el renglón de cédulas y certificación, el arco con "SM" y la ola.
- Sobre mí: "Formación y certificaciones", semblanza pendiente, las cuatro tarjetas (Formación pendiente, Certificación, las dos cédulas) y afiliaciones pendientes.
- Padecimientos: "¿En qué te puedo ayudar?" y seis tarjetas en una columna, cada una con su descripción pendiente.
- Ubicación y horarios: Hospital Star Médica, la dirección, "Lunes a viernes · 3:30 a 6:00 pm, con cita", WhatsApp, "Cómo llegar" (abre Google Maps) y el mapa, que carga al bajar.
- Franja vino "Agenda tu consulta en menos de un minuto" con "Agendar cita" blanco, pegada al pie.
- Pie: nombre, especialidad, cédulas y "Aviso de privacidad".

**3. Anclas**
- Tocar Inicio, Sobre mí, Padecimientos y Ubicación y horarios: cada una lleva a su sección, con el título a la vista.

**4. Aviso de privacidad, en teléfono y escritorio**
- Desde el pie: el encabezado del sitio con "Agendar cita" visible sin desplazarse; el texto, "Versión 2026-11-borrador", "Volver al inicio" y el mismo pie.
- Desde el Aviso, las anclas llevan a las secciones de `/`.

**5. Escritorio, 1280 px y más**
- Encabezado en una fila; portada en dos columnas (texto y arco); Sobre mí con título a un lado y tarjetas al otro; Padecimientos en rejilla; Ubicación con datos y mapa lado a lado; franja con el texto a la izquierda y el botón a la derecha. Comparar con `SitioInicio`.

**6. El horario sale de la Agenda base**
- En `/admin`, Configuración, Semana tipo: abrir el sábado de 10:00 a 1:00 pm y guardar.
- Recargar `/`: aparece "Sábado · 10:00 am a 1:00 pm, con cita". Regresar el sábado a "No se atiende".

**7. Lo que no se ve (buscadores)**
- Título de la pestaña: "Traumatóloga en San Luis Potosí · Dra. Selina Medina Castillo"; en el Aviso, con "Aviso de privacidad · " delante.
- `/robots.txt` y `/sitemap.xml` abren con lo esperado.
- Opcional: pegar el JSON-LD de `/` (ver código fuente) en validator.schema.org.

**8. Por decidir en este recorrido**
- De la revisión de la 7.4: `robots.txt` bloquea `/admin` y `/c/`, y un buscador que no los rastrea no ve su `noindex`. ¿Se queda o se quita el `Disallow` y se deja solo el `noindex`?
- De la 7.2: `EnlaceDeWhatsApp` ganó un texto opcional para el botón de la portada aunque el plan decía no tocarlo.
- Lo que el mockup tiene y se dejó fuera: el logotipo (no está en el repositorio) y los íconos de lugar, reloj y WhatsApp en Ubicación.

## Resultado del recorrido

Recorrido el 2026-11-17 por la sesión consultorio-tester con Gerardo mirando, en el AppHost local. La contraseña del Administrador fue temporal, pasada como argumento de `dotnet run`. Capturas en la carpeta del tester: `7.5-telefono-pliegue.jpg`, `7.5-telefono-completo.jpg`, `7.5-escritorio-1440.jpg`, `7.5-aviso-390.jpg`.

**Pasos:** 1 a 7 pasan en lo técnico.
- 1: a 390 × 844, "Agendar cita" termina en y = 214, sin desplazarse y sin scroll horizontal.
- 2: la franja queda pegada al pie.
- 3: las anclas dejan el título a 86–94 px.
- 4: el Aviso, a 390 y 1440 px, trae el encabezado del sitio.
- 5: a 1280 y 1440 px coincide con `SitioInicio`.
- 6: con el sábado abierto aparece en `/` y en el JSON-LD; regresado, desaparece.
- 7: títulos, robots, sitemap y JSON-LD como se esperaba; no se pasó por validator.schema.org.

**Observaciones del tester:**
- WhatsApp sin número ni enlace (el mockup tiene 81 2699 2831) y Formación "[Pendiente: universidad]" (el mockup dice UASLP). Los dos son a propósito: Decision de la épica 7 y de la 7.2 (2026-11-17).
- En Ubicación, el rótulo "WhatsApp" queda sobre el texto "Escríbenos por WhatsApp y te ayudamos." sin número.
- El mapa se quedó en blanco al principio. Verificado después por el tester (`7.5-mapa.png`): con el panel del navegador oculto, un iframe `loading="lazy"` nunca arranca. Al forzar la carga se ve el pin del Hospital Star Médica con su dirección. `/maps?q=…&output=embed` responde 301 a `/maps/embed?…`, que se puede embeber (sin XFO ni `frame-ancestors` en la respuesta final). No hay nada que arreglar.
- `SitioMovil.dc.html` no se pinta solo (depende de `support.js`); se comparó contra `SitioInicio`.

**Decisiones de Gerardo (2026-11-17, por el tester):**
- `EnlaceDeWhatsApp`: aprueba el texto opcional del botón de la portada. Sigue sin número, así que sin enlace.
- Logotipo: pendiente; no hay archivo y Gerardo lo manda después.
- Del recorrido técnico no pidió más cambios.
- `robots.txt`: aprueba la recomendación del tester. Se quitan los `Disallow` de `/admin` y `/c/` y queda solo el `noindex` (meta robots y `X-Robots-Tag`). Actualizada la Decision de FR-29 en la épica.

**Va a la entrada 7.6:**
- Íconos de lugar, reloj y WhatsApp en Ubicación, como en el mockup.
- `robots.txt` sin `Disallow` de `/admin` ni `/c/`: `User-agent: *` y `Sitemap:`, con sus pruebas y el comentario de `ArchivosParaBuscadores`.
