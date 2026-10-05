# Reconciliation — design canvas vs spines

Input: 11 artboards in `imports/mockup-canvas/`. Done inline by the parent (no subagent). Seven artboards read in full; `SitioInicio`, `WebReserva`, `WebDatos`, `WebConfirmada` read by extracted text, colours, type sizes, radii, grids and (for WebReserva) the calendar logic — not line by line.

## Carried into the spines
All 28 colours (three one-off desktop calendar greys folded into `past-day`/`border-disabled`), the Montserrat ramp, radii, component sizes, the three-step mobile flow, the two-column desktop flow, manage-by-link, late cancellation, every microcopy string that sets tone, the site's section order.

## Gaps found and resolved in this pass
1. Site section composition was not described — added to EXPERIENCE.md Information Architecture.
2. "Si agendas para otra persona…" helper dropped — added to the form pattern.
3. Palette page promoted from `.working/` to `mockups/` and linked from DESIGN.md.

## Mockup content that conflicts with the PRD (spines follow the PRD)
- "Lun–Vie · 3:30–6:00 pm" hard-coded in header and site → must come from configuration (FR-12).
- Desktop calendar fixed to five weekday columns → open question 4.
- "Te enviamos los detalles a tu correo" shown unconditionally → no-email variant specified.

## Content inconsistency inside the mockup
- Cédula profesional labelled "(UANL)" while Formación says Universidad Autónoma de San Luis Potosí → open question 7.

## Not captured
- Exact pixel layout of the four larger artboards beyond the measures listed above.
- The logo image itself (referenced as a canvas upload; `docs/assets/logo` holds the brand files).
