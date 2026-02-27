# Workboard (Single Source Of Truth)

Purpose: this is the canonical tracker for active architecture references, TODOs, and delivery tickets.

## Architecture Path

- Primary architecture doc: `docs/architecture.md`
- System behavior + API scope: `README.md`
- Deterministic eval behavior: `docs/EVAL_DETERMINISTIC.md`

## Active TODOs

- [ ] Frontend CRUD parity: add resume/job edit-update flows.
- [ ] Split single-page UI into dedicated routed pages for analysis and eval dashboards.
- [ ] Complete reliability hardening for CLI-provider runtime path (timeouts/retry behavior and clearer failure telemetry).
- [ ] Finish deployment track and publish a stable hosted environment.

## Delivery Tickets

1. `TICKET-001` Frontend Edit/Update Parity
Status: open
Definition of done: users can edit existing jobs/resumes from UI with validation and success/error states.

2. `TICKET-002` Routed Analysis + Eval Pages
Status: open
Definition of done: analysis and eval each have dedicated routes and page-level state management.

3. `TICKET-003` Runtime Reliability Hardening
Status: open
Definition of done: CLI provider execution has explicit timeout/retry policy, structured failure metrics, and regression tests.

4. `TICKET-004` Deployment Completion
Status: open
Definition of done: production deployment is documented, repeatable, and verified by smoke checks.

## Archive Policy

- Historical planning and handoff artifacts are moved to `docs/archive-ignore/`.
- Do not add new active work items to archived files.

---

Maintainer: codex gpt-5
