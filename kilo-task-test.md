  Task: Implement Day 4 Pack D1 (Security hardening minimum) in app-tracker

  Repo root:
  - /lump/apps/app-tracker

  Goal:
  Implement minimal security hardening for the API:
  1) Rate limiting on expensive endpoints (especially /api/analyses)
  2) Basic input validation/sanitization and length guards before expensive processing

  Scope (files):
  - src/Tracker.Api/Extensions/RateLimitingExtensions.cs (create if missing)
  - src/Tracker.Api/Middleware/InputValidationMiddleware.cs (create if missing)
  - src/Tracker.Api/Program.cs (wire services + middleware)
  - Any minimal supporting files required by compile (keep small and focused)

  Requirements:
  1. Rate limiting:
  - Add a named policy for analysis endpoints.
  - Apply stricter limits to POST /api/analyses than general endpoints.
  - Return HTTP 429 when exceeded.
  - Include Retry-After header on 429 responses.

  2. Input validation middleware:
  - Reject clearly invalid JSON payload shapes early (where practical).
  - Enforce max content lengths for expensive text fields (JD/resume inputs).
  - Reject payloads containing obvious script/HTML tag injection attempts.
  - Return RFC7807-style ProblemDetails JSON for validation failures (status 400).

  3. Middleware/endpoint wiring:
  - Ensure middleware order is correct and does not break existing endpoints.
  - Apply analysis rate-limit policy at endpoint mapping level if supported; otherwise via path-aware
  middleware/policy.

  4. Keep implementation deterministic and lightweight:
  - No new external services.
  - No unnecessary packages unless truly needed.
  - Keep changes minimal and reviewable.

  Acceptance criteria:
  - Burst requests to /api/analyses trigger 429 with Retry-After.
  - Invalid/oversized payloads are rejected before expensive downstream work.

  Verification to run:
  - dotnet build Tracker.slnx
  - (If possible) add one small smoke test script or documented curl commands proving:
    - 429 behavior on /api/analyses
    - 400 ProblemDetails on invalid payload
    - normal request still succeeds under limit

  Deliverable format:
  1) Summary of changes by file
  2) Exact commands run and outcomes
  3) Any assumptions made
  4) Remaining risks/open questions