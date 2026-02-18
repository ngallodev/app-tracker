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
