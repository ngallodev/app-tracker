# AI Job Application Tracker

Deterministic-first job/resume analysis service with LLM-assisted extraction, fallback gap analysis, caching, and fixture-based evaluation.

## What This Project Does

This backend helps compare a resume against a job description and returns:
- coverage and groundedness scores
- missing required/preferred skills
- analysis mode metadata (`deterministic` vs `llm_fallback`)
- token and latency metadata for traceability

The current repository is API + domain + infrastructure + eval tooling (no frontend app checked in yet).

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

Base URL in development: `http://localhost:5278`

Utility:
- `GET /healthz`
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
- `POST /api/analyses`

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
- Optional: `OPENAI_API_KEY` for live LLM-powered analysis calls

Notes:
- Without `OPENAI_API_KEY`, the API starts, but live analysis calls will fail through `FakeLlmClient`.
- Deterministic eval runner works without any API key.

## Local Development

Restore/build:

```bash
dotnet restore Tracker.slnx
dotnet build Tracker.slnx -v minimal
```

Run API:

```bash
dotnet run --project src/Tracker.Api/Tracker.Api.csproj
```

API is available at `http://localhost:5278` by default (see `src/Tracker.Api/Properties/launchSettings.json`).

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

## Important Repository Notes

- Planning docs and day-by-day execution breakdowns are in `docs/`.
- Multi-agent task packs/checklists are in `docs/review-checklists/`.
- `.dev-report-cache.md` is intentionally ignored.

## Roadmap (High-Level)

Near-term planned work includes:
- reliability/observability completion for Day 4 scope
- deployment and portfolio artifacts for Day 5 scope
- frontend app integration (currently not checked in)
