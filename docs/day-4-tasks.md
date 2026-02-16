# Day 4: Reliability + Observability (Optimized Breakdown)

## Goal
Production-grade reliability with minimal implementation risk and low token overhead.

## Remaining outcomes
- Retry/timeout/circuit behavior around external AI calls.
- Request correlation and structured error envelopes.
- Input guardrails and basic rate limiting.
- Minimal observability evidence for interview/demo.

## Agent + model assignment matrix

| Lane | Scope | Agent Type | Model Tier Suggestion | Skills |
|------|-------|------------|------------------------|--------|
| A | AI resilience policies (`OpenAiClient`) | `worker` | Medium | None required |
| B | API middleware (`Program.cs`) for correlation, ProblemDetails, limits | `worker` | Medium | None required |
| C | Validation and guardrails (`AnalysesEndpoints`) | `worker` | Small/Medium | None required |
| D | Verification scripts + runbook evidence | `explorer` | Small | `exec-statusline-json` when collecting execution telemetry |

## Execution order
1. Lane A and B in parallel.
2. Lane C after middleware contract is clear.
3. Lane D validation pass and failure-path drills.

## Task packs

### Pack A1: Resilience around LLM client
- Add retry with bounded attempts and jittered backoff.
- Add per-call timeout.
- Add circuit break threshold for repeated failures.
- Return typed `LlmException` context for caller decisions.

Acceptance:
- Transient failures retry and recover.
- Persistent failures stop quickly with circuit open behavior.

### Pack B1: API reliability middleware
- Add request correlation ID propagation.
- Add centralized exception mapping to ProblemDetails.
- Add low-friction rate limit policy for analysis endpoints.

Acceptance:
- All error responses use ProblemDetails JSON.
- Correlation ID is present in logs and response headers.

### Pack C1: Input and request guardrails
- Enforce max JD/resume text lengths.
- Reject malformed requests with clear client errors.
- Ensure failure states persist cleanly for analysis records.

Acceptance:
- Bad payloads fail fast with 400.
- No partial corrupted records after exceptions.

### Pack D1: Validation runbook
- Create a repeatable smoke script:
  - healthy call
  - forced LLM failure simulation
  - large payload rejection
- Save outcome notes for Day 5 docs/demo.

Acceptance:
- Script/checklist can be rerun before deploy.

## Token and cost controls
- Use deterministic eval + unit-like checks to validate most changes.
- Avoid repeated full pipeline AI calls while testing resilience wiring.
