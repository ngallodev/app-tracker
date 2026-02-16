# Pack D1 PR Review Checklist

## 1. Scope control
- Changes are limited to:
  - `src/Tracker.Api/Extensions/RateLimitingExtensions.cs`
  - `src/Tracker.Api/Middleware/InputValidationMiddleware.cs`
  - `src/Tracker.Api/Program.cs`
  - minimal required support files only
- No unrelated refactors or formatting-only churn.

## 2. Build and compile
- `dotnet build Tracker.slnx` succeeds.
- No new warnings indicating broken nullability or middleware registration issues.

## 3. Rate limiting behavior
- Analysis endpoint has stricter policy than general endpoints.
- `POST /api/analyses` returns `429` under burst load.
- `429` response includes `Retry-After`.
- Non-analysis endpoints are not accidentally over-throttled.

## 4. Input validation middleware
- Oversized expensive text inputs are rejected early with `400`.
- Obvious `<script>` or HTML-tag injection attempts are rejected.
- Validation failure responses use ProblemDetails shape (RFC7807 style).
- Validation does not block valid normal payloads.

## 5. Pipeline and middleware order
- Middleware order in `Program.cs` is correct:
  - compatibility with correlation/error handling is preserved
  - validation runs before expensive processing
  - rate limiting is enforced on targeted endpoints
- No duplicate middleware registrations.

## 6. Contract consistency
- Error payload fields are consistent with existing API conventions.
- Status codes are deterministic (`400` for validation, `429` for throttling).
- No leaking stack traces or internal exception details in client responses.

## 7. Endpoint safety
- `/api/analyses` is protected as intended.
- Existing `jobs` and `resumes` endpoints still behave normally for valid requests.
- GUID/path handling is not regressed.

## 8. Operational evidence
- Author provided reproducible commands (curl/script) showing:
  - one successful analysis request under limit
  - throttled request with `429` and `Retry-After`
  - invalid payload rejected with `400` ProblemDetails
- Evidence is concrete, not just claimed.

## 9. Risk scan
- No heavy package additions for simple validation/rate-limit logic unless justified.
- No hidden performance regressions (for example repeated expensive body parsing).
- No brittle security checks that block normal input while missing obvious attacks.

## 10. Merge readiness decision
- Pass only if all acceptance criteria from Pack D1 are demonstrably met.
- If partial, classify findings as:
  - Must fix before merge: incorrect status codes, missing `Retry-After`, broken middleware order, build failure.
  - Can follow up: threshold tuning, stricter sanitization heuristics, extra tests.
