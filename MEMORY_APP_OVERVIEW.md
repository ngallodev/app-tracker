# Memory: App Overview

## Product intent (from `PLAN.md`)
- Build a production-minded MVP job tracker that uses AI for grounded JD/resume analysis.
- Differentiate with deterministic scoring, observability, and cost controls instead of generic chat UX.

## Current implementation snapshot
- Backend exists as .NET 10 Minimal API with projects: `Tracker.Api`, `Tracker.Domain`, `Tracker.Infrastructure`, `Tracker.AI`, `Tracker.Eval` (stub).
- CRUD endpoints are implemented for jobs and resumes.
- Analysis endpoint exists and runs a 2-step LLM pipeline:
  - JD extraction
  - Resume gap analysis
- Deterministic scores (coverage, groundedness) are computed in code.
- EF Core + SQLite schema and initial migration are present.

## Not yet implemented (major)
- No React frontend scaffold or pages.
- No eval harness endpoint (`/eval/run`) or fixture runner.
- No hash-based cache lookup before LLM calls.
- No robust repair loop, retry/circuit breaker, correlation IDs, rate limiting, or ProblemDetails pipeline.
- No deployment assets (Dockerfile/compose/readme polish) yet.

## Key files to revisit quickly
- `PLAN.md`
- `src/Tracker.Api/Program.cs`
- `src/Tracker.Api/Endpoints/AnalysesEndpoints.cs`
- `src/Tracker.AI/Services/AnalysisService.cs`
- `src/Tracker.AI/OpenAiClient.cs`
- `src/Tracker.Infrastructure/Data/TrackerDbContext.cs`

## Verification caveat
- Local `dotnet restore/build` failed in this environment due SDK resolver error (`MSB4276`), so runtime behavior still needs verification on a healthy .NET install.
