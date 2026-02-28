# Build Log

---

## Entry 1 — 2026-02-17 — claude-sonnet-4-5-20250929

**Branch:** feature/deterministic-first-matching
**Commit scope:** CLAUDE.md update + build-log bootstrap

### Changes
- `CLAUDE.md`: Added rule #9 requiring a build log entry before every commit.
- `build-log.md`: Created this file to satisfy rule #9.

### Build Notes
- No compilation run in this session; changes are docs/config only.
- No issues encountered.

### Review Notes (from code review of this branch)
- Deterministic-first gap matching is solid; synonym expansion and `ShouldFallbackToLlm` heuristic work correctly.
- **Action items before wider use:**
  - Sanitize `ex.Message` in analysis error responses (leaks internals to clients).
  - Add pagination to `GET /api/analyses` — unbounded query at scale.
  - Add `UseForwardedHeaders()` if deploying behind a reverse proxy (rate limiting uses raw `RemoteIpAddress`).
  - Extend `InputValidationMiddleware` to cover `POST /api/analyses`.
  - Add unit tests for `DeterministicGapMatcher` and `ShouldFallbackToLlm` edge cases.
  - Document why `ColumnExists` uses a non-parameterized PRAGMA (safe because table name is hardcoded).

Signed-off-by: claude-sonnet-4-5-20250929

---

## Entry 2 — 2026-02-17 — opencode-kimi-k2.5-free

**Branch:** main
**Commit scope:** Dockerize project with docker-compose

### Changes
- `docker-compose.yml`: Created root-level compose file defining API and web services
- `Dockerfile.api`: Multi-stage .NET 10.0 build for Tracker.Api with SQLite volume support
- `Dockerfile.web`: Node.js 20 Alpine container for Vite React frontend
- `web/package.json`: Created missing package.json for React + Vite + TypeScript setup
- `web/index.html`, `web/src/main.tsx`, `web/src/App.tsx`: Created minimal React app structure
- `web/tsconfig*.json`: Created TypeScript configuration files
- `.dockerignore`: Added ignore patterns for both root and web directories

### Build Notes
- API runs on port 5278 (mapped from container port 8080)
- Web dev server runs on port 5173 with API proxy configured
- SQLite database persisted in Docker volume `sqlite_data`
- Optional OPENAI_API_KEY can be passed via environment variable
- Healthcheck configured for API service

### Usage
```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down

# With OpenAI API key
OPENAI_API_KEY=your-key docker-compose up -d
```

### Issues Encountered
- Web directory had node_modules and package-lock.json but no package.json
- No React source files existed in web/src/
- Solution: Created complete minimal React app structure with TypeScript

Signed-off-by: opencode-kimi-k2.5-free

---

## Entry 3 — 2026-02-19 — codex gpt-5

**Branch:** review/code-review-notes
**Commit scope:** CLI-based LLM provider redesign + multi-provider stubs + API integration + deployment/docs updates

### Changes
- Added CLI-backed provider architecture in `src/Tracker.AI/Cli/` with:
  - provider catalog, options, executor, adapter abstraction, and router
  - provider adapters for `claude`, `codex`, `gemini`, `qwen`, `kilocode`, `opencode`
- Updated `ILlmClient` to support per-request provider override and provider metadata in `LlmResult<T>`.
- Updated `AnalysisService` metadata and provider threading through analysis calls.
- Updated `CreateAnalysisRequest` with optional `Provider` and expanded `AnalysisResultDto` with provider/execution metadata.
- Updated `AnalysesEndpoints` to:
  - validate provider values
  - pass provider override to analysis service
  - map provider-aware error handling and response metadata
- Replaced OpenAI-specific dependency health check with CLI provider availability health check.
- Updated `Program.cs` DI wiring to CLI router and provider adapters.
- Added `Llm` provider configuration in `src/Tracker.Api/appsettings.json`.
- Added and updated documentation/deployment artifacts from Day 4/5 work:
  - `README.md`, `docs/architecture.md`, `docs/demo-script.md`, `docs/portfolio-case-study.md`, `docs/technical-blurb.md`, `fly.toml`, `Dockerfile.api`, `docker-compose.yml`.
- Added restart handoff file: `RESTART_HANDOFF.md`.

### Build Notes
- `dotnet build Tracker.slnx -v minimal` succeeded.
- Runtime health checks succeeded (`/healthz`, `/healthz/deps`) with provider availability details.
- Deterministic eval runner result remains baseline-incomplete:
  - `./scripts/run_deterministic_eval.sh` => 1 pass / 1 fail (`backend_api_engineer`).

### Issues Encountered
- API runtime checks generated SQLite WAL/SHM artifacts (`tracker.db-wal`, `tracker.db-shm`) that are intentionally excluded from commit.
- Additional CLI providers beyond `claude` are currently unavailable in PATH in this environment and report as unavailable via health checks.

Signed-off-by: codex gpt-5

---

## Entry 4 — 2026-02-27 — codex gpt-5

**Branch:** main
**Commit scope:** Emergency recovery of broken build after commit-cleanup reset

### Changes
- `src/Tracker.AI/OpenAiClient.cs`: Restored a known-good implementation from commit `f5b0224` and retained the structured-output repair/error-snippet logging path without malformed merge fragments.
- `src/Tracker.Api/Program.cs`: Removed duplicated `using System.Data` and duplicate startup migration logger/migration invocation introduced by merge artifacts.

### Build Notes
- `dotnet build Tracker.slnx -v minimal` succeeded after recovery.
- `cd web && npm run build` succeeded.
- `./scripts/proof_of_life.sh` completed with API/UI liveness checks passing (`/healthz`, `/api/analyses`, and frontend root all 200).

### Issues Encountered
- Analysis creation still returns provider-level 502 in this environment (`claude` invalid structured output after repair), but application startup and baseline runtime health are restored.

Signed-off-by: codex gpt-5

---

## Entry 5 — 2026-02-27 — codex gpt-5

**Branch:** main
**Commit scope:** Stabilize deterministic eval CI stage, fix matcher drift, and archive legacy planning docs into single-workboard model

### Changes
- `scripts/ci_stage.sh`: Replaced process-substitution logging with pipe+`tee` and `PIPESTATUS` capture to avoid Jenkins stage hang/spin after child failure.
- `scripts/run_deterministic_eval.sh`:
  - Added deterministic runner artifact outputs in `artifacts/`.
  - Added stable `dotnet build -m:1` prebuild and `dotnet run --no-build` execution path.
  - Added JSON summary generation (`artifacts/deterministic-eval-summary.json`) including resume-ready bullets.
- `src/Tracker.AI/Services/DeterministicGapMatcher.cs`:
  - Fixed token-boundary detection for symbol skills (e.g., `C#`).
  - Added Node.js/nodejs synonym coverage for preferred-skill matching parity.
  - Result: deterministic eval now passes all fixtures.
- Archived legacy planning/review/root-note docs into `docs/archive-ignore/`.
- Added `docs/WORKBOARD.md` as canonical source of truth for active architecture pointers, TODOs, and delivery tickets.
- Updated `README.md` and `docs/PLAN.md` references to align with new archive/workboard structure.

### Build Notes
- `./scripts/ci_stage.sh deterministic_eval ./scripts/run_deterministic_eval.sh` now exits cleanly and no longer hangs.
- Deterministic eval result: `Fixtures: 10, Passed: 10, Failed: 0`.
- `artifacts/deterministic-eval-summary.json` generated with `passRatePercent: 100.0`.

### Issues Encountered
- .NET SDK 10.0.103 exhibited intermittent parallel-build behavior for `Tracker.Eval` in CI context; single-node prebuild (`-m:1`) produced stable execution.

Signed-off-by: codex gpt-5

---

## Entry 6 — 2026-02-27 — codex gpt-5

**Branch:** chore/deterministic-eval-and-doc-archive
**Commit scope:** Normalize app-tracker review agent/skill architecture with templated contracts, extracted scripts/assets, and single-copy symlink strategy

### Changes
- Added `.claude/agents/app-tracker-review-agent.md` as the canonical templated orchestrator agent.
- Updated `.claude/agents/app-tracker-review.md` to a back-compat alias (no duplicated workflow logic).
- Refactored `.codex/skills/app-tracker-drift-review/SKILL.md` to a variable-driven contract (no hardcoded repo paths).
- Added `.codex/skills/app-tracker-drift-review/scripts/run_drift_checks.sh` and moved executable checks out of markdown.
- Added `.codex/skills/app-tracker-drift-review/assets/findings-template.md` for report structure.
- Added `.claude/skills -> ../.codex/skills` symlink so skills exist in one canonical location and are referenced from both trees.

### Build Notes
- Executed extracted skill script with explicit variable inputs:
  - `REPO_ROOT=/lump/apps/app-tracker API_PROJECT=src/Tracker.Api/Tracker.Api.csproj WEB_DIR=web API_HOST=127.0.0.1 API_PORT=5278 INCLUDE_RUNTIME_SMOKE=false .codex/skills/app-tracker-drift-review/scripts/run_drift_checks.sh`
- Verification results:
  - `dotnet build src/Tracker.Api/Tracker.Api.csproj` succeeded
  - `npm run build --prefix web` succeeded

### Issues Encountered
- None; refactor completed as structural/documentation normalization.

Signed-off-by: codex gpt-5

---

## Entry 7 — 2026-02-27 — codex gpt-5

**Branch:** chore/deterministic-eval-and-doc-archive
**Commit scope:** Stop tracking generated frontend dist artifacts

### Changes
- `.gitignore`: added `web/dist/` so frontend build outputs are ignored.
- Removed tracked `web/dist` artifacts from git index (kept locally on disk).

### Build Notes
- No rebuild required for this change; it is repository hygiene only.
- Verified ignore behavior with `git check-ignore` against `web/dist/index.html`.

### Issues Encountered
- None.

Signed-off-by: codex gpt-5

---

## Entry 8 — 2026-02-28 — codex gpt-5

**Branch:** feat/lmstudio-metrics-observability
**Commit scope:** Add LM Studio provider integration, strict structured JSON schema mode, observability throughput metrics, docs references, and targeted tests

### Changes
- Added OpenAI-compatible LM Studio provider path with config-backed model/endpoints:
  - `src/Tracker.AI/LmStudioOptions.cs`
  - `src/Tracker.AI/HybridLlmClientRouter.cs`
  - `src/Tracker.Api/Program.cs`
  - `src/Tracker.Api/appsettings.json`
  - `src/Tracker.AI/Cli/LlmProviderCatalog.cs`
- Added secure API key file loading for LM Studio (`Llm:LmStudio:ApiKeyFile`) and local secret ignore rule:
  - `.gitignore`
  - `src/Tracker.Api/Program.cs`
- Enabled strict structured outputs (`response_format: json_schema`) for analysis types in OpenAI-compatible flow:
  - `src/Tracker.AI/StructuredOutputSchemas.cs`
  - `src/Tracker.AI/OpenAiClient.cs`
- Expanded analysis observability metrics:
  - Added `tokensPerSecond` in analysis response DTO and endpoint mapping.
  - Added aggregate throughput metrics to `/api/analyses/metrics` (`averageTokensPerSecond`, deterministic/fallback throughput).
  - Added persisted step metrics logs in `llm_logs` (`metrics_jd`, `metrics_gap`, `metrics_overall`) with token/latency/throughput payloads.
  - Updated execution-mode attribution for openai-compatible flow.
- Added focused test project for LM Studio observability/config correctness:
  - `tests/Tracker.AI.Tests/Tracker.AI.Tests.csproj`
  - `tests/Tracker.AI.Tests/StructuredOutputSchemasTests.cs`
  - `tests/Tracker.AI.Tests/LlmProviderCatalogTests.cs`
  - `tests/Tracker.AI.Tests/HybridLlmClientRouterTests.cs`
  - Added tests folder to `Tracker.slnx`.
- Added and linked local LM Studio docs/examples references:
  - `docs/LMSTUDIO_NOTES.md`
  - `README.md`
  - Example files under `docs/` and `docs/archive-ignore/`.

### Build Notes
- `dotnet build Tracker.slnx -v minimal` succeeded (1 non-fatal MSBuild copy warning observed for nested bin path behavior).
- `dotnet test Tracker.slnx -v minimal` succeeded (`Passed: 4, Failed: 0`).
- `./scripts/ci_stage.sh deterministic_eval ./scripts/run_deterministic_eval.sh` succeeded (`Fixtures: 10, Passed: 10, Failed: 0`).
- Live LM Studio smoke test via API (`provider=lmstudio`) succeeded:
  - Analysis response included `tokensPerSecond`.
  - `/api/analyses/metrics` included throughput metrics.
  - `llm_logs` captured `metrics_jd`, `metrics_gap`, `metrics_overall` rows.

### Issues Encountered
- Fixed compile error in throughput aggregation caused by `double` to `decimal` conversion mismatch in endpoint helper.

Signed-off-by: codex gpt-5

---

## Entry 9 — 2026-02-28 — codex gpt-5

**Branch:** feat/lmstudio-metrics-observability
**Commit scope:** Fix Jenkins post-always jq interpolation failure (`MissingPropertyException: buildNumber`)

### Changes
- Updated `Jenkinsfile` post `always` jq filter to escape jq variable references inside Groovy multiline string:
  - `buildNumber: (\$buildNumber | tonumber)`
  - `jobName: \$jobName`
  - `buildUrl: \$buildUrl`
  - `result: \$result`
- This prevents Groovy from attempting to resolve `buildNumber` as a Groovy binding property while building the shell command.

### Build Notes
- Validated updated jq expression is present in `Jenkinsfile`.
- Root-cause confirmation from `artifacts/logs/tracker-fail.log`: failing run checked out `origin/main` at `0a949b1` and therefore did not include this fix.

### Issues Encountered
- None after escaping jq variable references.

Signed-off-by: codex gpt-5

---

## Entry 10 — 2026-02-28 — codex gpt-5

**Branch:** main
**Commit scope:** Harden Jenkins proof-of-life against port collisions and launch profile overrides

### Changes
- `scripts/run_api.sh`:
  - Added `--no-launch-profile` to `dotnet run` so runtime ports from env (`ASPNETCORE_URLS`) are honored in CI.
  - Fixes cases where `launchSettings.json` forced `http://0.0.0.0:5278` despite requested alternate port.
- `scripts/proof_of_life.sh`:
  - Added free-port selection helpers for API/UI startup (`pick_free_port`, `is_port_in_use`).
  - Added host/port variables (`API_HOST`, `FRONT_HOST`, `API_PORT`, `FRONT_PORT`) and wired them into `run_local.sh` launch.
  - Updated default URLs to derive from resolved host/port values.
- `Jenkinsfile`:
  - Updated Proof Of Life stage to run with `SKIP_ANALYSIS=1` to avoid external provider dependency and reduce CI flakiness.

### Build Notes
- Reproduced port-collision scenario by occupying port `5278`; verified proof-of-life now starts API on next free port and completes.
- Verified Jenkins-equivalent proof stage command succeeds:
  - `SKIP_ANALYSIS=1 ./scripts/ci_stage.sh proof_of_life ./scripts/proof_of_life.sh`

### Issues Encountered
- Root cause was `dotnet run` honoring `launchSettings.json` in CI startup path, causing bind failures on `5278`.

Signed-off-by: codex gpt-5

---

## Entry 11 — 2026-02-28 — codex gpt-5

**Branch:** main
**Commit scope:** Stabilize proof-of-life stage startup by decoupling from run_local wait race

### Changes
- `scripts/proof_of_life.sh`:
  - Replaced `run_local.sh` wrapper invocation with direct process management for API + frontend.
  - Added dedicated `API_PID` / `FRONT_PID` tracking and targeted service liveness checks while waiting for URLs.
  - Added diagnostic log tail emission when a service exits before readiness.
  - Added `apiUrl` and `uiUrl` fields to proof summary output for easier CI artifact debugging.
  - Retained dynamic free-port selection and wired URLs to resolved ports.
  - Updated startup log message for accuracy.

### Build Notes
- Verified Jenkins-equivalent stage command passes:
  - `SKIP_ANALYSIS=1 ./scripts/ci_stage.sh proof_of_life ./scripts/proof_of_life.sh`
- Confirmed summary artifact includes `apiUrl` and `uiUrl` and returns HTTP 200 for UI/API/eval checks.

### Issues Encountered
- Prior implementation depended on `run_local.sh` `wait -n` behavior, which could terminate the stack if either child process exited early, creating intermittent CI startup failures.

Signed-off-by: codex gpt-5

---

## Entry 9 — 2026-02-28 — codex gpt-5

**Branch:** main
**Commit scope:** Implement URL/file ingestion, provider-aware analysis UX, LM Studio chunking, test-data lifecycle controls, and relational application tracking

### Changes
- Expanded domain schema and EF model for product requirements:
  - Added job metadata fields (`workType`, `employmentType`, salary range/currency, recruiter/contact links, careers URL, `isTestData`).
  - Added resume salary preference and `isTestData` fields.
  - Added analysis error categorization, `isTestData`, and salary alignment output fields.
  - Added relational application model: `job_applications` and `job_application_events`.
- Added migration `20260228135256_AddJobImportAndApplications` and updated model snapshot.
- Added deterministic job ingestion service:
  - URL fetch + HTML text extraction.
  - Deterministic extraction of work type, employment type, salary signal, and contact fields.
- Updated job endpoints:
  - URL-only create-job support (title/company auto-filled when available).
  - `POST /api/jobs/extract-from-url` for preview/import assist.
- Updated analysis endpoints:
  - `GET /api/analyses/providers` to return provider availability + default provider selection.
  - Added salary alignment score/note in responses.
  - Added error category/details propagation and test-data propagation.
- Added development cleanup endpoint:
  - `DELETE /api/dev/test-data` for deleting test-generated jobs/resumes/analyses/applications.
- Added applications API:
  - list/get/create/update app records.
  - add timeline events with status transitions (interview/offer/rejection).
- Improved LM Studio success behavior in `AnalysisService`:
  - chunk long JD payloads into multiple structured extraction calls.
  - merge partial structured outputs before deterministic gap scoring.
- Rebuilt active frontend `web/src/App.tsx` with:
  - provider dropdown defaulting to available configured provider.
  - URL import action for jobs.
  - `.txt`/`.md` upload + drag/drop for job description and resume content.
  - explicit analysis metric explanations (coverage/groundedness) and failure detail surface.
  - salary preference capture + salary match display in analysis detail.
  - application tracking UI (mark applied, status changes, events timeline).
  - one-click clear test-data action.
- Added frontend execution skill artifact:
  - `.codex/skills/app-tracker-frontend-optimizer/SKILL.md`.
- Updated active planning docs:
  - `docs/WORKBOARD.md` expanded with new in-progress tickets for lifecycle, ingestion/test-data ops, and analysis UX/provider reliability.
  - `README.md` endpoint inventory updated for new APIs.

### Build Notes
- `dotnet build Tracker.slnx -v minimal` succeeded (0 warnings, 0 errors).
- `dotnet ef migrations add AddJobImportAndApplications --project src/Tracker.Infrastructure --startup-project src/Tracker.Api` succeeded.
- `cd web && npm run build` succeeded.
- `dotnet test tests/Tracker.AI.Tests/Tracker.AI.Tests.csproj -v minimal` passed (4/4).

### Issues Encountered
- EF tools emitted version warning (`10.0.2` tools vs `10.0.3` runtime); migration generation still completed successfully.
- Bookmarklet runtime verification in a live browser session was not executed in this terminal-only pass; backend ingestion compatibility for bookmarklet-style payloads is implemented.

Signed-off-by: codex gpt-5

---

## Entry 10 — 2026-02-28 — codex gpt-5

**Branch:** main
**Commit scope:** Fix URL-only job create validation and ingestion fallback; complete live smoke verification of new application/test-data workflows

### Changes
- `src/Tracker.Api/Middleware/InputValidationMiddleware.cs`
  - Allowed URL-only create-job flow by requiring `title/company` only when `sourceUrl` is absent.
  - Added special-case validation for `POST /api/jobs/extract-from-url` to require only `sourceUrl`.
- `src/Tracker.Api/Services/JobIngestionService.cs`
  - Wrapped remote URL fetch in safe fallback to avoid unhandled `HttpRequestException` on non-200 source pages.

### Build Notes
- `dotnet build Tracker.slnx -v minimal` succeeded.
- `cd web && npm run build` succeeded.

### Runtime Smoke Notes
- Verified `GET /api/analyses/providers` returns provider list/default.
- Verified URL-only `POST /api/jobs` now succeeds and creates fallback title/company when source fetch is unavailable.
- Verified application lifecycle flow:
  - `POST /api/applications`
  - `POST /api/applications/{id}/events`
  - `PUT /api/applications/{id}`
- Verified `DELETE /api/dev/test-data` removes test-tagged rows.
- Verified bookmarklet-style job payload extraction captures work type, employment type, salary, and recruiter contact fields.

Signed-off-by: codex gpt-5
