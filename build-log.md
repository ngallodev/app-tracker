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
