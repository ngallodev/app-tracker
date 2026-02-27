# Demo Script (10-12 Minutes)

## 1. Setup (1 minute)

1. Start stack:
   ```bash
   docker compose up --build
   ```
2. Confirm API health:
   ```bash
   curl -sS http://localhost:5278/healthz
   ```

## 2. Explain Problem + Architecture (2 minutes)

Use `docs/architecture.md` and highlight:
- deterministic-first analysis flow
- LLM fallback only for low-confidence cases
- hash-pair cache to avoid repeated LLM work
- persisted observability fields (`InputTokens`, `OutputTokens`, `LatencyMs`, `LlmLog`)

## 3. Live API Walkthrough (4-5 minutes)

1. Create job:
   ```bash
   curl -sS -X POST http://localhost:5278/api/jobs \
     -H "Content-Type: application/json" \
     -d '{"title":"Backend Engineer","company":"Acme","descriptionText":"Need C#, ASP.NET Core, SQL, Docker."}'
   ```
2. Create resume:
   ```bash
   curl -sS -X POST http://localhost:5278/api/resumes \
     -H "Content-Type: application/json" \
     -d '{"name":"Resume A","content":"Built APIs in C#, ASP.NET Core, and SQL."}'
   ```
3. Create analysis:
   ```bash
   curl -sS -X POST http://localhost:5278/api/analyses \
     -H "Content-Type: application/json" \
     -d '{"jobId":"<JOB_ID>","resumeId":"<RESUME_ID>"}'
   ```
4. Re-run same analysis and explain cache hit behavior.
5. Show list/status:
   ```bash
   curl -sS http://localhost:5278/api/analyses
   curl -sS http://localhost:5278/api/analyses/<ANALYSIS_ID>/status
   ```

## 4. Risk/Guardrail Demo (1-2 minutes)

- Mention rate limit on analysis creation (`2/min/IP` strict policy).
- Mention payload and input validation middleware (`413` for >2MB, unsafe HTML/script patterns blocked).

## 5. Deployment Story (1 minute)

- Current deployable artifact: `Dockerfile.api` + mounted SQLite volume.
- Current frontend container is dev-mode (`npm run dev`), so production frontend serving is an explicit next step.

## Interview Questions (with concise answers)

1. Why deterministic-first instead of full LLM?
   Deterministic matching is cheaper, faster, and repeatable; LLM is only used when confidence is low.
2. How do you control LLM cost?
   Hash-pair caching, deterministic-first flow, and token telemetry per analysis.
3. How do you handle malformed model output?
   Structured JSON mode plus one repair pass; parse/repair status is logged.
4. What happens without `OPENAI_API_KEY`?
   API runs, but analysis calls fail via `FakeLlmClient` with a clear error.
5. How is persistence handled?
   EF Core + SQLite with migrations, including legacy SQLite migration-history backfill logic.
6. What prevents abuse?
   Strict fixed-window rate limit on `POST /api/analyses` and request validation middleware.

---

*Documentation maintained by: codex gpt-5*
