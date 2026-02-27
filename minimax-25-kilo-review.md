# Review: Pack D1 Security Hardening Submission

Date: 2026-02-16
Reviewer: Codex (GPT-5)

## Findings (ordered by severity)

1. Medium: Expensive text validation can be bypassed on update paths.
- `InputValidationMiddleware` validated only `POST /api/jobs` and `POST /api/resumes`.
- `PUT /api/jobs/{id}` and `PUT /api/resumes/{id}` were not validated, allowing oversized or unsafe text payloads through.
- References:
  - `src/Tracker.Api/Middleware/InputValidationMiddleware.cs`
  - `src/Tracker.Api/Endpoints/JobsEndpoints.cs`
  - `src/Tracker.Api/Endpoints/ResumesEndpoints.cs`

2. Medium: Text length guard was too permissive for hardening intent.
- `MaxTextFieldLength` was set to `500000`, which weakens early rejection for expensive text fields.
- Expected behavior is tighter bounds aligned to JD/resume practical limits.
- Reference:
  - `src/Tracker.Api/Middleware/InputValidationMiddleware.cs`

3. Low: Incorrect status code semantics for payload-size rejection.
- Request body over-limit returned `400` with title `Payload Too Large`.
- Correct status should be `413 Payload Too Large`.
- Reference:
  - `src/Tracker.Api/Middleware/InputValidationMiddleware.cs`

## Verification notes
- `dotnet build Tracker.slnx` failed in this environment without emitted diagnostics.
- No command-evidence artifacts were found proving `429` + `Retry-After` and validation-failure paths.

## Requested fixes
- Extend middleware validation to include update routes for jobs/resumes.
- Tighten expensive text size limits to practical guardrails.
- Return `413` for body-size rejection and keep ProblemDetails shape.
