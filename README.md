# AI Job Application Tracker

Backend API for comparing resumes to job descriptions with a deterministic-first analysis pipeline and LLM fallback when needed.

## Current Scope

Implemented in this repository:
- .NET 10 Minimal API backend (`src/Tracker.Api`)
- SQLite persistence via EF Core (`src/Tracker.Infrastructure`)
- Analysis pipeline (`src/Tracker.AI`):
  - LLM-based JD extraction
  - deterministic gap matching
  - LLM fallback for gap analysis only when deterministic confidence is low
- Hash-pair analysis caching for repeated JD/resume content
- Fixture-based deterministic evaluation runner (`src/Tracker.Eval`)

The current repository includes a .NET API backend, domain/infrastructure/AI projects, deterministic eval tooling, and a React/Vite frontend under `web/`.
The frontend currently supports create/delete job+resume flows, running analyses, viewing analysis metrics/skills, and triggering/viewing deterministic eval runs.

## Analysis Behavior

`POST /api/analyses` flow:
1. Validate request and load `job` + `resume`.
2. Ensure normalized content hashes exist.
3. Reuse latest completed cached analysis for same JD/resume hash pair.
4. Run pipeline:
   - JD extraction through `ILlmClient`
   - deterministic gap matcher
   - LLM fallback gap analysis only if `ShouldFallbackToLlm(...)` returns true
5. Persist `analysis`, `analysis_results`, and step-level `llm_logs`.
6. Return scores plus `gapAnalysisMode` (`deterministic` or `llm_fallback`).

## Projects

- `src/Tracker.Api`: API host, middleware, endpoints
- `src/Tracker.AI`: prompts, LLM client, deterministic matcher, analysis orchestration
- `src/Tracker.Domain`: entities and DTOs
- `src/Tracker.Infrastructure`: EF Core DB context, migrations, hashing
- `src/Tracker.Eval`: deterministic eval runner and fixtures

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
- `POST /api/jobs/extract-from-url`
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
- `GET /api/analyses/providers`
- `GET /api/analyses/{id}`
- `GET /api/analyses/{id}/status`
- `POST /api/analyses`

Eval:
- `GET /eval/runs`
- `POST /eval/run`

Applications:
- `GET /api/applications`
- `GET /api/applications/{id}`
- `POST /api/applications`
- `PUT /api/applications/{id}`
- `POST /api/applications/{id}/events`

Development:
- `DELETE /api/dev/test-data`

## Security/Request Guardrails (Current)

- Input validation middleware for `POST`/`PUT` on jobs and resumes
- Request size cap: `2MB` (`413` on overflow)
- Basic unsafe HTML/script pattern rejection in text fields
- Strict rate limiting on analysis creation (`429` + `Retry-After`)

## Prerequisites

- .NET SDK `10.0` (target framework is `net10.0`)
- CLI providers available in `PATH` (default auto-detect targets):
  - `claude`, `codex`, `gemini`, `qwen`, `opencode`
  - `kilo` (or `kilocode`) for the `kilocode` provider
- Optional OpenAI-compatible local endpoint provider:
  - `lmstudio` via `Llm:LmStudio:*` config keys (default model `neuraldaredevil-8b-abliterated`, max input `8192`)

Notes:
- You can force a provider binary with `Llm:Providers:<provider>:Command` (absolute path or command name).
- Provider-specific flags can be configured with `Llm:Providers:<provider>:ExtraFlags`.
- For LM Studio/OpenAI-compatible servers, set `Llm:LmStudio:BaseUrl` (for example `http://plat-server.local:1234/v1`).
- Store endpoint token in a local file and set `Llm:LmStudio:ApiKeyFile` (default: `secrets/lmstudio_api_key.txt`).
- Tool-calling capability notes for local LM Studio are documented in `docs/LMSTUDIO_NOTES.md`.
- Deterministic eval runner works without any provider CLI.

## Local Run

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

Optional fixture directory override:

```bash
./scripts/run_deterministic_eval.sh src/Tracker.Eval/Fixtures
```

Runner behavior:
- loads `*.json` fixtures
- runs deterministic matcher checks
- prints pass/fail summary
- exits non-zero on failure

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

- Active plan/tickets source of truth: `docs/WORKBOARD.md`
- Archived planning and handoff notes: `docs/archive-ignore/`
- `.dev-report-cache.md` is intentionally ignored.

## Roadmap (High-Level)

Near-term planned work includes:
- reliability/observability completion for Day 4 scope
- deployment and portfolio artifacts for Day 5 scope
- frontend CRUD parity for edit/update flows (create/delete/run-analysis/detail are implemented)
- dedicated routed pages for analysis and eval dashboards (current UI is a single-page workflow)

## Footnotes: Current TODOs and Next Steps

- Canonical active TODOs/tickets are tracked in `docs/WORKBOARD.md`.
- Day 5 documentation artifacts are available in `docs/architecture.md`, `docs/demo-script.md`, and `docs/portfolio-case-study.md`.
- Historical planning notes are preserved in `docs/archive-ignore/planning/`.

---

*Documentation maintained by: opencode (kimi-k2.5-free)*
