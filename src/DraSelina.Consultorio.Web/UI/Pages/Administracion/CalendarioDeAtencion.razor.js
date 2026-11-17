// El editor del día del Calendario de atención como modal (entrada 6.9). Solo showModal() deja el fondo inerte, cierra
// con Esc y mantiene el foco adentro, y solo se llama desde JS. Lo importa la página; solo corre bajo /admin (AD-11).

/**
 * Abre el diálogo si está cerrado y lleva el foco a lo que la página pide (selector): el título del día al abrir, la
 * lista de Citas que quedan fuera cuando aparece, el título de quitar el horario propio.
 */
export function abrir(dialogo, selector) {
  if (!dialogo.open) {
    dialogo.showModal();
  }

  dialogo.querySelector(selector)?.focus();
}

/** Cierra el diálogo si está abierto y regresa el foco al botón del día tocado (fecha yyyy-MM-dd), si sigue en el mes. */
export function cerrar(dialogo, fecha) {
  if (dialogo.open) {
    dialogo.close();
  }

  if (fecha) {
    document.querySelector(`td[data-fecha="${fecha}"] button`)?.focus();
  }
}
