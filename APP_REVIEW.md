# App Review: Current State, Remaining Plan, and MVP-Safe Alternatives

## Scope and intent checked
This review is grounded in `PLAN.md` and current code under `src/`.

The plan's core intent is strong: build a disciplined AI system (deterministic scoring, grounded evidence, observability, cost control) rather than a generic chat wrapper. That intent is still the right differentiator.

Current status: solid backend skeleton, but not yet a defensible MVP for the goals stated in `PLAN.md`.

## Critical findings (ordered by severity)

### High: Cost-control claim is not implemented (no hash cache lookup before LLM calls)
- Plan requires hash-based caching for repeated JD/resume pairs (`PLAN.md:82`).
- Current analysis flow always creates a new analysis and always executes LLM calls:
  - `src/Tracker.Api/Endpoints/AnalysesEndpoints.cs:118`
  - `src/Tracker.Api/Endpoints/AnalysesEndpoints.cs:134`
  - `src/Tracker.AI/Services/AnalysisService.cs:77`
  - `src/Tracker.AI/Services/AnalysisService.cs:98`
- Impact:
  - Repeated analyses incur unnecessary token spend.
  - "Cost-optimized" claim is currently not provable.

### High: Parse-failure handling is fragile and can collapse into 500s instead of controlled recovery
- `OpenAiClient` returns `Value = value!` even when parse failed (`src/Tracker.AI/OpenAiClient.cs:89` and `src/Tracker.AI/OpenAiClient.cs:91`).
- Downstream logic assumes non-null structured objects:
  - `src/Tracker.AI/Services/AnalysisService.cs:114`
  - `src/Tracker.AI/Services/AnalysisService.cs:143`
- Plan states schema validation + repair loop (`PLAN.md:106`), but no repair prompt pass is implemented.
- Impact:
  - A malformed model output can hard-fail the pipeline.
  - Reliability story and deterministic behavior are weaker than the plan claims.

### High: MVP centerpiece is missing (no frontend + no eval harness)
- Plan Day 3 defines React pages + analysis demo + `/eval/run` metrics (`PLAN.md:315`, `PLAN.md:325`, `PLAN.md:343`).
- Repository has no frontend app and no eval endpoint implementation.
- Impact:
  - No end-to-end demo loop for interview narrative.
  - No measurable quality baseline for regression checks.

### Medium: Reliability/observability milestones are largely unimplemented
- Plan Day 4 calls for retries, circuit breaker, correlation IDs, ProblemDetails, rate limiting (`PLAN.md:366`).
- Current startup is minimal (`src/Tracker.Api/Program.cs:10` through `src/Tracker.Api/Program.cs:81`).
- `OpenAiClient` has no Polly retry/circuit logic (`src/Tracker.AI/OpenAiClient.cs:31`).
- Impact:
  - Production posture is overstated relative to current behavior.

### Medium: Analysis detail endpoint does not expose structured match arrays needed by planned UI
- `GET /api/analyses/{id}` declares placeholders for parsed matches/missing lists but never populates them (`src/Tracker.Api/Endpoints/AnalysesEndpoints.cs:62` through `src/Tracker.Api/Endpoints/AnalysesEndpoints.cs:76`).
- Response falls back to raw JSON strings (`src/Tracker.Api/Endpoints/AnalysesEndpoints.cs:85` through `src/Tracker.Api/Endpoints/AnalysesEndpoints.cs:87`).
- Impact:
  - Frontend will need extra parsing and loses typed API guarantees.

### Medium: Database model uses `newid()` defaults despite SQLite target
- SQLite-focused setup is used (`src/Tracker.Api/Program.cs:17` and `src/Tracker.Api/Program.cs:18`), but model/migration default GUID SQL is `newid()`:
  - `src/Tracker.Infrastructure/Data/TrackerDbContext.cs:23`
  - `src/Tracker.Infrastructure/Data/Migrations/20260216115740_InitialCreate.cs:18`
- App currently sets IDs in code for core writes, reducing immediate breakage, but schema defaults are not portable to SQLite.

### Low: Generated build artifacts appear in source tree with backslashes in paths
- File listing shows paths like `src/Tracker.Api/bin\Debug/...` under repo tree.
- `.gitignore` patterns target `bin/` and may miss unusual mixed separator paths (`.gitignore:6`).
- Impact:
  - High risk of noisy commits and repository bloat.

## Plan progress review

### Day 1 (Foundation)
- Mostly achieved.
- Core entities, DB context, migrations, and CRUD APIs are in place.

### Day 2 (AI Core)
- Partially achieved.
- LLM abstraction + prompts + analysis orchestration exist.
- Key Day 2 promises still missing in full form:
  - real cache lookup and reuse
  - robust repair loop
  - durable llm_logs step records

### Day 3 (MVP ship)
- Not started materially.
- Missing frontend and eval harness are the largest functional gaps.

### Day 4 (Reliability + observability)
- Not started materially.
- No hardened middleware/error/retry stack yet.

### Day 5 (Deployment + polish)
- Not started materially.
- No Docker/deploy/docs assets checked in yet.

## Outside-the-box alternatives (within 1-week MVP)

### 1) Deterministic-first extraction, AI fallback only
- Replace "LLM always parses JD" with:
  - rules + dictionary extraction first
  - local matching + deterministic scoring
  - AI only for ambiguous skill mapping or quote verification
- Why this works:
  - aligns with plan's deterministic and cost-control principles
  - drastically reduces token use and latency

### 2) User-assisted requirement tagging
- Add a quick UI flow where user confirms/highlights required skills from JD bullets.
- Treat user-confirmed list as ground truth for scoring.
- Use AI only to generate optional evidence phrasing, not primary extraction.

### 3) Scripted eval harness without heavy AI dependence
- Build fixture-based deterministic evaluator first:
  - expected skill sets stored in JSON
  - compare parser + matcher output to expected
  - run AI only on a small subset for sanity checks
- This preserves a quality story without burning budget.

### 4) Browser bookmarklet pre-processing
- A bookmarklet can:
  - capture JD title/company/body from live job pages
  - strip navigation/ads/footer noise
  - send compact payload to API
- Practical payoff in MVP:
  - better input quality
  - fewer prompt tokens
  - faster user workflow

## "Google synonyms in React" assessment

Short answer: not ideal for this MVP.

- Google Programmable Search API is built for web search results, not domain synonym lookup; it also introduces key management and quota/cost concerns.
- Google Cloud Natural Language API provides NLP features (entities/sentiment/classification), not a straightforward synonyms endpoint.
- Frontend-direct calls expose credentials and create brittle client-side dependencies.

Better MVP option:
- Keep synonym expansion local and deterministic:
  - curated JSON dictionary for resume/JD skill aliases
  - optional stemming/normalization in JS
  - cache all results client-side or server-side

## Suggested re-scope for next 1 week

### Day 1
- Implement hash cache lookup for `(jobHash, resumeHash)`.
- Add safe parse-failure path and one repair pass.

### Day 2
- Build deterministic skill parser + synonym map (server or frontend worker).
- Wire AI fallback only for unresolved skills.

### Day 3
- Deliver single analysis-focused React page (jobs, resumes, run, results).

### Day 4
- Add minimal reliability stack:
  - retries + timeout
  - ProblemDetails
  - correlation/request id
  - basic rate limit

### Day 5
- Build deterministic eval runner + lightweight report endpoint.
- Dockerize and document the real architecture (including deterministic-first path).

## Final assessment
The foundation is good and directionally correct, but the current implementation does not yet substantiate the plan's strongest claims (cost control, reliability, eval rigor, demo readiness). The fastest path to a defensible MVP is to move extraction/matching toward deterministic code and reserve AI for ambiguity resolution and evidence support.

## External references consulted (2026-02-16)
- Google Programmable Search JSON API overview: https://developers.google.com/custom-search/v1/overview
- Google Programmable Search docs: https://developers.google.com/custom-search
- Google Cloud Natural Language docs: https://cloud.google.com/natural-language/docs
