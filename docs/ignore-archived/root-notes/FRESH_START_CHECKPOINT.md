# Fresh Start Checkpoint

Generated: 2026-02-16 20:59
Branch: fmf-tryday3

## Progress/Status

### Day 3 status
- Completed: analysis metadata contract + status endpoint.
- Completed: deterministic eval fixtures expanded to 10 total (`src/Tracker.Eval/Fixtures/*.json`).
- Completed: MVP frontend scaffold under `web/` with Jobs, Resumes, and Analysis pages.

### Day 4 status
- Completed: resilience policy layer for LLM calls (retry + timeout + circuit breaker) in:
  - `src/Tracker.AI/PollyPolicies.cs`
  - `src/Tracker.AI/OpenAiClient.cs`
- Completed: correlation ID middleware and global RFC7807 problem details pipeline in:
  - `src/Tracker.Api/Middleware/CorrelationIdMiddleware.cs`
  - `src/Tracker.Api/Middleware/ExceptionMiddleware.cs`
  - `src/Tracker.Api/Extensions/ProblemDetailsExtensions.cs`
- Completed: dedicated health and readiness endpoints in:
  - `src/Tracker.Api/Endpoints/HealthEndpoints.cs`
- Completed: smoke test script:
  - `scripts/day4_smoke.sh`
- Completed: startup schema migration application (`Database.Migrate()`), replacing `EnsureCreated()`.

### Day 5 status
- Completed: deployment/container baseline:
  - `Dockerfile`
  - `.dockerignore`
  - `docker-compose.yml`
  - `fly.toml`
- Completed: documentation and demo package:
  - `README.md`
  - `docs/architecture.md`
  - `docs/portfolio-case-study.md`
  - `docs/demo-script.md`
  - `docs/pre-demo-checklist.md`

## Validation Snapshot
- Confirmed:
  - `Tracker.AI` and `Tracker.Infrastructure` build in this environment.
  - `docker compose config` parses successfully.
  - `scripts/day4_smoke.sh` passes shell syntax check.
- Blocked by environment:
  - `Tracker.Api` build fails in SDK/MSBuild project-reference resolution stage with no emitted compiler diagnostics.
  - Frontend install/build could not be fully executed due npm install limitations in this sandbox session.

## Notes
- Local runtime verification is still partially blocked by environment/.NET SDK resolver behavior in this terminal context.
- Deterministic-first path is now the default direction; LLM use is reduced to extraction + fallback cases.

---

*Checkpoint documented by: opencode (kimi-k2.5-free)*
