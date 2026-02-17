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
