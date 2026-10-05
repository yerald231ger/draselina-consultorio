# Reconciliation — Solution Plan vs PRD

Input: `docs/Appointment Booking System — Solution Plan.md`. Done inline by the parent (no subagent).

## Covered
All five patient requirements, all five administrator requirements, the four non-functional items, every business rule under "Reglas de negocio", the notification list, the practice-website pages, security/privacy items, and the "Selina's decisions" table map to FR-1..FR-30, §5 and §6. Tone (sincere apology, kind late-cancellation wording) is kept in §5 and FR-23.

## Gaps found and resolution
1. Tipos de visita were "editable" in the plan but no FR let the Administrador manage them — added to FR-19.
2. `BufferMin` per visit type not in PRD — left out on purpose, recorded in addendum.
3. schema.org markup, Google Business Profile, Turnstile, rate limiter — mechanism, moved to addendum.
4. Plan hard-codes Mon–Fri and 6:00 pm; PRD FR-12 makes them configurable (user request) — conflict recorded in addendum for architecture.
5. Roadmap phases 2–4 have no detail in the plan (embedded image) — PRD lists them only as out of scope.

## Not carried over (belongs downstream)
DDD model, context map, module structure, repositories, factories, events, CI pipeline, hosting, costs — indexed in addendum.md.
