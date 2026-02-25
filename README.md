# AI Job Application Tracker

Deterministic-first job/resume analysis service with LLM-assisted extraction, fallback gap analysis, caching, and fixture-based evaluation.

## What This Project Does

This backend helps compare a resume against a job description and returns:
- coverage and groundedness scores
- missing required/preferred skills
- analysis mode metadata (`deterministic` vs `llm_fallback`)
- token and latency metadata for traceability

The current repository includes a .NET API backend, domain/infrastructure/AI projects, deterministic eval tooling, and a React/Vite frontend under `web/`.
The frontend currently supports create/delete job+resume flows, running analyses, viewing analysis metrics/skills, and triggering/viewing deterministic eval runs.

## Architecture

Solution projects:
- `src/Tracker.Api`: Minimal API host and endpoint composition
- `src/Tracker.AI`: LLM client, prompts, deterministic matcher, analysis orchestration
- `src/Tracker.Domain`: Entities and DTOs
- `src/Tracker.Infrastructure`: EF Core SQLite context + migrations + hash utilities
- `src/Tracker.Eval`: deterministic fixture runner (no API key needed)

Core request flow (`POST /api/analyses`):
1. Validate request and resolve `job` + `resume`.
2. Normalize and hash JD/resume text.
3. Reuse cached completed analysis for identical hash pair when available.
4. Run analysis pipeline:
   - JD extraction (`LLM`)
   - Deterministic gap matching
   - LLM fallback gap analysis only when confidence is low
5. Persist `Analysis`, `AnalysisResult`, and step-level `LlmLog` records.
6. Return structured result DTO with mode metadata.

## Current API Surface

Base URL in development: `http://0.0.0.0:5278` (or `http://localhost:5278` from the same machine)

Utility:
- `GET /healthz`
- `GET /healthz/ready`
- `GET /healthz/deps`
- `GET /version`

Jobs:
- `GET /api/jobs`
- `GET /api/jobs/{id}`
- `POST /api/jobs`
- `PUT /api/jobs/{id}`
- `DELETE /api/jobs/{id}`

Resumes:
- `GET /api/resumes`
- `GET /api/resumes/{id}`
- `POST /api/resumes`
- `PUT /api/resumes/{id}`
- `DELETE /api/resumes/{id}`

Analyses:
- `GET /api/analyses`
- `GET /api/analyses/{id}`
- `GET /api/analyses/{id}/status`
- `POST /api/analyses`

Eval:
- `GET /eval/runs`
- `POST /eval/run`

## Security/Request Guardrails (Current)

- Rate limiting enabled for analysis creation endpoint with strict policy.
- `429` responses include `Retry-After`.
- Input validation middleware on `POST`/`PUT` for jobs and resumes:
  - JSON object shape checks
  - required string-field checks for create operations
  - max JD and resume content lengths
  - basic HTML/script/event-handler pattern rejection
- oversized request body rejected with `413` and problem JSON.

## Prerequisites

- .NET SDK `10.0` (target framework is `net10.0`)
- CLI providers available in `PATH` (default auto-detect targets):
  - `claude`, `codex`, `gemini`, `qwen`, `opencode`
  - `kilo` (or `kilocode`) for the `kilocode` provider

Notes:
- You can force a provider binary with `Llm:Providers:<provider>:Command` (absolute path or command name).
- Provider-specific flags can be configured with `Llm:Providers:<provider>:ExtraFlags`.
- Deterministic eval runner works without any provider CLI.

## Local Development

Restore/build:

```bash
dotnet restore Tracker.slnx
dotnet build Tracker.slnx -v minimal
```

Run API:

```bash
./scripts/run_api.sh
```

API binds to `http://0.0.0.0:5278` by default, so it is reachable from other machines on your network.
Use `HOST` and `PORT` to override:

```bash
HOST=0.0.0.0 PORT=5278 ./scripts/run_api.sh
```

## Minimal API Smoke Flow

1. Create a job:

```bash
curl -sS -X POST http://localhost:5278/api/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Backend Engineer",
    "company": "Acme",
    "descriptionText": "Need C#, ASP.NET Core, SQL, Docker.",
    "sourceUrl": "https://example.com/job/1"
  }'
```

2. Create a resume:

```bash
curl -sS -X POST http://localhost:5278/api/resumes \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Candidate Resume",
    "content": "Experienced in C#, ASP.NET Core, and SQL."
  }'
```

3. Trigger analysis:

```bash
curl -sS -X POST http://localhost:5278/api/analyses \
  -H "Content-Type: application/json" \
  -d '{
    "jobId": "<JOB_ID>",
    "resumeId": "<RESUME_ID>"
  }'
```

4. List analyses:

```bash
curl -sS http://localhost:5278/api/analyses
```

## Deterministic Evaluation Harness

HTTP eval endpoints:
- `POST /eval/run` runs the deterministic fixture suite and persists an `eval_runs` summary row
- `GET /eval/runs` returns recent persisted eval summaries

Frontend support:
- `web/src/App.tsx` includes an eval panel to trigger `/eval/run` and view recent eval history

CLI eval runner:

Run fixture evals:

```bash
./scripts/run_deterministic_eval.sh
```

Or specify a fixture directory:

```bash
./scripts/run_deterministic_eval.sh src/Tracker.Eval/Fixtures
```

Eval runner behavior:
- reads `*.json` fixtures
- runs deterministic matcher
- prints pass/fail summary
- exits non-zero if any fixture fails

See:
- `docs/EVAL_DETERMINISTIC.md`
- `src/Tracker.Eval/Fixtures/`

## Deterministic Input Preprocessing

Bookmarklet tooling is included to capture and clean JD text locally before submission:
- `docs/bookmarklet_jd_capture.js`
- `docs/BOOKMARKLET.md`

## Day 5 Documentation Pack

- Architecture diagrams + runtime/deployment notes: `docs/architecture.md`
- Interview-ready demo walkthrough: `docs/demo-script.md`
- Portfolio case-study narrative + cost/patterns: [`docs/portfolio-case-study.md`](docs/portfolio-case-study.md)
- Technical one-paragraph summary: [`docs/technical-blurb.md`](docs/technical-blurb.md)

## Important Repository Notes

- Planning docs and day-by-day execution breakdowns are in `docs/`.
- Multi-agent task packs/checklists are in `docs/review-checklists/`.
- `.dev-report-cache.md` is intentionally ignored.

## Roadmap (High-Level)

Near-term planned work includes:
- reliability/observability completion for Day 4 scope
- deployment and portfolio artifacts for Day 5 scope
- frontend CRUD parity for edit/update flows (create/delete/run-analysis/detail are implemented)
- dedicated routed pages for analysis and eval dashboards (current UI is a single-page workflow)

## Footnotes: Current TODOs and Next Steps

- Frontend in `web/src/App.tsx` now supports create/delete jobs and resumes, running analyses, viewing analysis metrics/skills, and deterministic eval run/history; remaining UI gaps are edit/update flows and dedicated routed pages.
- Reliability and observability hardening remain active implementation targets per `docs/day-4-tasks.md`, especially aligning runtime resilience behavior for CLI-provider execution with the original Polly-focused design intent.
- Day 5 documentation artifacts are now available in `docs/architecture.md`, `docs/demo-script.md`, and `docs/portfolio-case-study.md`; production deployment execution remains a follow-on task.

---

*Documentation maintained by: opencode (kimi-k2.5-free)*
