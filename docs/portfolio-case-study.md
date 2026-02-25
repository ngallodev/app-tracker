# Portfolio Case Study: AI Job Application Tracker

## Project Summary

AI-assisted backend that compares resumes to job descriptions using a deterministic-first pipeline with LLM fallback. The service exposes CRUD endpoints for jobs/resumes and analysis endpoints with persisted scoring and telemetry.

## Problem

Most resume-vs-JD tools are expensive and inconsistent when every comparison is fully LLM-driven. The goal here was to keep results practical while reducing token usage and preserving debuggability.

## Solution Snapshot

- Minimal API (`Tracker.Api`) for CRUD + analysis orchestration.
- Analysis pipeline (`Tracker.AI`) with:
  - LLM JD extraction
  - deterministic gap matching
  - optional LLM fallback for low-confidence deterministic results
- SQLite + EF Core persistence (`Tracker.Infrastructure`) for entities, results, and LLM step logs.
- Fixture-based deterministic eval runner (`Tracker.Eval`).

## AI Engineering Patterns and Cost Controls

1. Deterministic-first orchestration
   JD skill matching is attempted deterministically before any fallback LLM analysis.
2. Confidence-gated fallback
   LLM fallback triggers only on low-confidence deterministic outcomes.
3. Content-hash cache reuse
   Reuses prior completed analyses for identical normalized JD/resume text pairs.
4. Structured-output parse hardening
   JSON-mode responses plus one repair pass reduce schema failure risk.
5. Token/latency telemetry by analysis
   `InputTokens`, `OutputTokens`, and `LatencyMs` persisted for measurement and tuning.

Cost note:
- The code tracks token counts but does not compute dollar cost directly.
- Operational cost can be estimated per analysis as:
  - `(input_tokens / 1_000_000 * input_price_per_million) + (output_tokens / 1_000_000 * output_price_per_million)`
- Plug in your provider’s current model pricing for exact numbers.

## Deployment Guidance

- Local container flow is ready: `docker-compose.yml` (`api`, `web`, SQLite volume).
- API can be deployed directly from `Dockerfile.api`.
- For persistent SQLite in containers, mount `/app/data` and point connection string to `/app/data/tracker.db`.
- Frontend container is currently Vite dev server; production-grade static hosting/build step is a clear next increment.

## Tradeoffs

- SQLite is simple and fast for single-instance deployments but limits horizontal scale.
- Deterministic matching improves stability/cost but may miss nuanced semantic matches without fallback.
- Current frontend is intentionally minimal while backend analysis workflow is prioritized.

## Interview Talking Points

1. Describe how fallback thresholds were chosen and how you would calibrate them using fixtures.
2. Explain cache invalidation strategy and why normalized hashing is used.
3. Describe how you would evolve from SQLite to Postgres with minimal endpoint changes.
4. Explain what telemetry is enough to tune cost/latency and what is still missing.
5. Discuss failure handling when provider calls fail or output cannot be repaired.

---

*Documentation maintained by: codex gpt-5*
