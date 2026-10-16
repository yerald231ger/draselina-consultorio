---
name: "Dra. Selina Medina — Reserva de citas"
description: "Identidad visual del sitio y la reserva de citas de la Dra. Selina Medina Castillo, Traumatología y Ortopedia. Vino sobre rosa pálido, cálido y sereno, pensado para el teléfono."
status: final
created: 2026-10-04
updated: 2026-10-04
sources:
  - ../prd-appointment-booking/prd-appointment-booking.md
  - imports/mockup-canvas/
  - ../../../docs/assets/
colors:
  primary: '#6B2A45'
  primary-hover: '#4F1E33'
  primary-strong: '#5A2339'
  on-primary: '#FFFFFF'
  on-primary-muted: '#F1DCE2'
  ink: '#3A1F29'
  ink-soft: '#4A3039'
  muted: '#6E5560'
  eyebrow: '#8A4F5A'
  disabled: '#7E626C'
  past-day: '#C4AEB3'
  ground: '#FBF6F5'
  surface: '#FFFFFF'
  tint: '#F4E6E6'
  field-readonly: '#F6ECEB'
  track: '#E7D3D3'
  wave: '#E6CDCD'
  border: '#EADBD9'
  border-input: '#CDB3B5'
  border-disabled: '#D9C2C4'
  divider: '#F0E4E3'
  danger: '#8E2A1F'
  warning-bg: '#FBEEDF'
  warning-border: '#EBC9A3'
  warning-icon: '#8A4210'
  warning-title: '#6B320B'
  warning-text: '#4A2810'
typography:
  display:
    fontFamily: Montserrat
    fontSize: 46px
    fontWeight: 600
    lineHeight: 1.1
  page-title:
    fontFamily: Montserrat
    fontSize: 34px
    fontWeight: 600
    lineHeight: 1.15
  step-title:
    fontFamily: Montserrat
    fontSize: 24px
    fontWeight: 600
  status-title:
    fontFamily: Montserrat
    fontSize: 28px
    fontWeight: 700
    lineHeight: 1.15
    letterSpacing: -0.02em
  question:
    fontFamily: Montserrat
    fontSize: 21px
    fontWeight: 700
    letterSpacing: -0.01em
  section:
    fontFamily: Montserrat
    fontSize: 17px
    fontWeight: 700
  body:
    fontFamily: Montserrat
    fontSize: 15px
    fontWeight: 400
    lineHeight: 1.5
  body-small:
    fontFamily: Montserrat
    fontSize: 14px
    fontWeight: 400
    lineHeight: 1.45
  help:
    fontFamily: Montserrat
    fontSize: 13px
    fontWeight: 400
    lineHeight: 1.45
  label:
    fontFamily: Montserrat
    fontSize: 14px
    fontWeight: 700
  input:
    fontFamily: Montserrat
    fontSize: 16px
    fontWeight: 400
  button:
    fontFamily: Montserrat
    fontSize: 16px
    fontWeight: 700
  eyebrow:
    fontFamily: Montserrat
    fontSize: 12px
    fontWeight: 600
    letterSpacing: 0.14em
rounded:
  xs: 2px
  md: 12px
  lg: 14px
  xl: 16px
  2xl: 18px
  3xl: 20px
  full: 9999px
spacing:
  '1': 4px
  '2': 6px
  '3': 8px
  '4': 10px
  '5': 12px
  '6': 14px
  '7': 16px
  '8': 18px
  '9': 20px
  margin-mobile: 20px
  container-max: 1160px
  touch-min: 44px
components:
  button-primary:
    height: 54px
    background: '{colors.primary}'
    color: '{colors.on-primary}'
    radius: '{rounded.lg}'
    typography: '{typography.button}'
  button-secondary:
    height: 52px
    background: '{colors.surface}'
    border: '2px solid {colors.primary}'
    color: '{colors.primary}'
    radius: '{rounded.lg}'
  button-quiet:
    height: 52px
    background: '{colors.surface}'
    border: '2px solid {colors.border}'
    color: '{colors.ink}'
    radius: '{rounded.lg}'
  button-danger:
    height: 54px
    background: '{colors.danger}'
    color: '{colors.on-primary}'
    radius: '{rounded.lg}'
  option-card:
    padding: '{spacing.7}'
    background: '{colors.surface}'
    border: '2px solid {colors.border}'
    border-selected: '2px solid {colors.primary}'
    halo-selected: '0 0 0 4px {colors.tint}'
    radius: '{rounded.xl}'
  day-chip:
    width: 60px
    height: 78px
    radius: '{rounded.lg}'
    border: '1.5px solid {colors.border}'
    background-selected: '{colors.primary}'
    border-full: '1.5px dashed {colors.border-disabled}'
  time-slot:
    height: 52px
    height-desktop: 48px
    radius: '{rounded.md}'
    border: '2px solid {colors.border}'
    background-selected: '{colors.primary}'
    halo-selected: '0 0 0 4px {colors.tint}'
  text-field:
    height: 50px
    radius: '{rounded.md}'
    border: '1.5px solid {colors.border-input}'
    background: '{colors.surface}'
    typography: '{typography.input}'
  progress-segment:
    height: 4px
    radius: '{rounded.xs}'
    done: '{colors.primary}'
    pending: '{colors.track}'
  appointment-card:
    background: '{colors.surface}'
    border: '1px solid {colors.border}'
    radius: '{rounded.2xl}'
    divider: '{colors.divider}'
  info-panel:
    background: '{colors.tint}'
    radius: '{rounded.xl}'
  warning-panel:
    background: '{colors.warning-bg}'
    border: '1px solid {colors.warning-border}'
    radius: '{rounded.xl}'
  status-pill:
    background: '{colors.tint}'
    color: '{colors.primary-strong}'
    radius: '{rounded.full}'
---

# DESIGN.md — Dra. Selina Medina, reserva de citas

Los valores de este documento se extrajeron de los 11 artboards del diseño de Gerardo (`imports/mockup-canvas/`). No se propuso nada nuevo. Si un mockup y este documento se contradicen, gana este documento.

## Brand & Style

El consultorio de una especialista, no un portal hospitalario. La interfaz es cálida y serena: un solo color de marca, vino, sobre un fondo rosa muy pálido; tarjetas blancas de esquinas suaves; mucho aire; una sola acción principal por pantalla.

El logotipo (monograma con el nombre y la especialidad) aparece en el encabezado de la reserva y del sitio. Una onda doble en rosa (`{colors.wave}` sobre `{colors.tint}`) remata el borde superior del primer paso de la reserva; es la única decoración.

Nombre visible: **Dra. Selina Medina Castillo · Traumatología y Ortopedia**.

`[ASSUMPTION: el vino de los mockups (#6B2A45) manda sobre el de la papelería de marca (#6D2D45); la diferencia es casi imperceptible pero no son el mismo valor]`

## Colors

- **Vino (`{colors.primary}`)** es el único color de marca. Lleva la acción principal, la selección (tipo de consulta, día, hora), los títulos de paso, los enlaces y los iconos de las tarjetas. `{colors.primary-hover}` es su estado al pasar el cursor.
- **Tinta (`{colors.ink}`)** es el texto principal. **Tinta suave (`{colors.ink-soft}`)** es el cuerpo de texto sobre fondo rosado. **Apagado (`{colors.muted}`)** es para texto secundario y ayudas. **Rótulo (`{colors.eyebrow}`)** solo para rótulos en mayúsculas ("Paso 1 de 3", la especialidad).
- **Fondo (`{colors.ground}`)** es la página; **superficie (`{colors.surface}`)** son tarjetas, campos y la barra inferior de acción. La jerarquía se hace con tono, no con sombra.
- **Rosa (`{colors.tint}`)** marca lo que el paciente ya eligió o debe conservar: el resumen de la cita, el halo de selección, el panel "Guarda este enlace", la píldora de estado.
- **Bordes:** `{colors.border}` en tarjetas y separadores, `{colors.border-input}` en campos, `{colors.border-disabled}` punteado en días llenos, `{colors.divider}` dentro de una tarjeta.
- **Peligro (`{colors.danger}`)** existe solo para cancelar una cita: como texto en "Cancelar cita" y como fondo en "Sí, cancelar mi cita".
- **Aviso (`{colors.warning-bg}` y familia)** existe solo para la cancelación tardía. Ámbar, no rojo: informa, no regaña.

No se usan: verdes de éxito (el éxito se dice en vino, con una palomita blanca sobre círculo vino), degradados, ni un segundo color de marca.

**Papelería de marca** (`docs/assets/`): vino `#6D2D45`, vino oscuro `#582136`, rosa viejo `#A26769`, crema `#EBE4D0`, oliva `#777E5C`. No se usan en la interfaz salvo dentro del logotipo.

Paleta completa con muestras: [mockups/color-palette.html](mockups/color-palette.html).

**Contraste medido** (texto normal pide 4.5:1): blanco sobre vino 10.3; tinta sobre fondo 13.9; apagado sobre fondo 6.3; rótulo sobre fondo 5.9; blanco sobre peligro 8.4. `[NOTE FOR UX]` El borde de los campos (`{colors.border-input}` sobre blanco) da 1.96:1, por debajo del 3:1 recomendado para delimitar un control.

## Typography

Una sola familia: **Montserrat**, pesos 400, 500, 600 y 700. Respaldo: `'Segoe UI', system-ui, sans-serif`.

- `{typography.display}` solo en el encabezado del sitio (nombre de la doctora).
- `{typography.page-title}` en los títulos de la reserva en escritorio; `{typography.step-title}` en móvil. Ambos en vino.
- `{typography.status-title}` en los momentos de resultado ("Tu cita está confirmada", "Tu cita", "¿Cancelar tu cita?"), en tinta.
- `{typography.question}` para la pregunta del paso ("¿Qué tipo de consulta necesitas?").
- `{typography.eyebrow}` siempre en mayúsculas con espaciado amplio.
- Los campos usan 16 px para que el teléfono no haga zoom al enfocar.

La fecha y hora de una cita se muestran grandes y en negritas (24–26 px, 700): es el dato que el paciente vino a buscar.

## Layout & Spacing

- **Móvil primero.** Lienzo de referencia 390 px, una columna, margen lateral `{spacing.margin-mobile}`.
- **Barra de acción inferior.** La acción principal vive al pie de la pantalla, sobre `{colors.surface}` con borde superior `{colors.border}`, y bajo ella una línea de apoyo centrada.
- **Escritorio.** Contenedor centrado de `{spacing.container-max}`. La reserva usa dos columnas: el contenido del paso a la izquierda y el resumen "Tu cita" a la derecha. Las rejillas se arman con `auto-fit` y mínimos de 240–300 px, de modo que colapsan a una columna sin puntos de quiebre fijos.
- **Ritmo.** Separaciones de 6 a 20 px dentro de un bloque; 16–28 px entre bloques.
- **Objetivo táctil mínimo:** `{spacing.touch-min}`.

## Elevation & Depth

Sin sombras de elevación. La única "sombra" es el halo de selección (`0 0 0 4px {colors.tint}`), que es un anillo plano, no profundidad. Las capas se distinguen por tono: fondo, superficie, rosa.

## Shapes

Esquinas suaves y consistentes: `{rounded.md}` en campos, horas y el botón de regresar; `{rounded.lg}` en botones y chips de día; `{rounded.xl}` en tarjetas de opción y paneles; `{rounded.2xl}` en la tarjeta de cita; `{rounded.3xl}` en las tarjetas del sitio; `{rounded.full}` en la píldora de estado y en el círculo de confirmación. El retrato del sitio usa un arco (`200px 200px 24px 24px`).

Iconos de línea, trazo 2–2.6 px, puntas redondeadas, en el color del texto que acompañan. Sin emojis.

## Components

- **Botón principal** — vino sólido, texto blanco, 54 px de alto, ancho completo en móvil. Puede llevar una flecha a la derecha cuando avanza de paso.
- **Botón secundario** — blanco con borde vino de 2 px ("Agregar a mi calendario").
- **Botón discreto** — blanco con borde `{colors.border}`; con texto en tinta ("Conservar mi cita") o en peligro ("Cancelar cita").
- **Botón de peligro** — fondo `{colors.danger}`, solo en la confirmación de cancelar.
- **Tarjeta de opción** — radio nativo a la izquierda, nombre en negritas con la duración a la derecha, descripción debajo. Seleccionada: borde vino y halo rosa.
- **Chip de día** (móvil) — día de la semana, número y "N libres". Seleccionado: vino sólido. Lleno: borde punteado, texto `{colors.disabled}`, etiqueta "Lleno".
- **Celda de calendario** (escritorio) — número y un punto vino si hay horarios. Mismos estados que el chip; los días fuera de la ventana van en `{colors.past-day}` sin borde.
- **Hora** — botón con la hora en negritas, dos columnas en móvil. Seleccionada: vino sólido con halo.
- **Campo** — etiqueta en negritas arriba, campo de 50 px, ayuda debajo. El prefijo "+52" es un bloque fijo en `{colors.field-readonly}`. Con error, el borde pasa a 2 px en `{colors.primary}` y el mensaje va debajo en `{colors.primary}`, con `{typography.help}` en negritas; el foco va al primer campo con error. Nunca `{colors.danger}` (Gerardo, 2026-10-16).
- **Barra de progreso** — tres segmentos de 4 px; los cumplidos en vino. En escritorio, pasos numerados con su nombre.
- **Tarjeta de cita** — día de la semana como rótulo vino, fecha y hora grandes, tipo y duración; debajo, tras un divisor, el lugar con icono y "Abrir en el mapa".
- **Panel rosa** — resumen de la cita elegida con "Cambiar", o "Guarda este enlace".
- **Panel de aviso** — ámbar, icono de reloj, título y texto.
- **Píldora de estado** — "Agendada" con palomita.

`[ASSUMPTION: el área de administración reutiliza estos mismos componentes y tokens; no hay mockups de administración y no se define aquí ningún componente nuevo para ella]`

## Do's and Don'ts

| Sí | No |
|---|---|
| Una sola acción principal en vino por pantalla | Dos botones vino compitiendo |
| Jerarquía por tono: fondo, superficie, rosa | Sombras, degradados |
| Ámbar para la cancelación tardía | Rojo o tono de regaño |
| Peligro solo al cancelar | Peligro para errores de formulario comunes `[ASSUMPTION]` |
| Iconos de línea en el color del texto | Emojis o iconos rellenos de colores |
| Fecha y hora grandes y en negritas | Fecha enterrada en un párrafo |
| Horarios tomados de la configuración | "Lun–Vie · 3:30–6:00 pm" escrito a mano en el diseño |
