# Fresh Start Checkpoint

Generated: 2026-02-16 11:22
Branch: feature/deterministic-first-matching

## Completed Work

### Core backend improvements
- Added hash-pair cache reuse in analysis endpoint (`/api/analyses`) to avoid repeat LLM calls.
- Hardened structured-output parsing with a one-pass JSON repair path before failing.
- Persisted step-level LLM logs (`jd_extraction`, `gap_analysis_*`) with parse/repair metadata.

### Deterministic-first analysis path
- Added deterministic skill gap matcher with synonym support.
- Added low-confidence fallback to LLM gap analysis to balance cost and quality.
- Exposed analysis mode metadata in API/DTO:
  - `gapAnalysisMode`
  - `usedGapLlmFallback`

### Cost-efficiency tooling/docs
- Added bookmarklet workflow for local JD preprocessing:
  - `docs/bookmarklet_jd_capture.js`
  - `docs/BOOKMARKLET.md`
- Added deterministic eval harness (fixture-based, low-cost):
  - `src/Tracker.Eval/Program.cs`
  - `src/Tracker.Eval/Fixtures/*.json`
  - `scripts/run_deterministic_eval.sh`
  - `docs/EVAL_DETERMINISTIC.md`

### Planning/doc organization
- Moved plan files into `docs/`.
- Added `CLAUDE.md` with token-efficient build style.
- Added symlink `AGENTS.md -> CLAUDE.md`.
- Optimized uncompleted day breakdowns with agent/skill/model guidance:
  - `docs/day-3-tasks.md` (optimization addendum)
  - `docs/day-4-tasks.md` (new optimized breakdown)
  - `docs/day-5-tasks.md` (optimization addendum)
  - `docs/PLAN.md` (links + execution workflow section)

## Commit History (latest first)
- `68d8d3b` Add analysis mode metadata, deterministic eval runner, and optimized remaining-day plans
- `6d59938` Add bookmarklet-based JD preprocessing workflow
- `a04cd1b` Use deterministic gap matching with low-confidence LLM fallback
- `c4c539d` Reorganize planning docs and add deterministic gap matcher
- `1165431` Bootstrap app tracker and add analysis cache/repair logging

## Current Working State
- Repo is mostly clean on this branch.
- Untracked files currently present at repo root:
  - `day-3-tasks.md`
  - `day-4-tasks.md`
  - `day-5-tasks.md`

## Notes
- Local runtime verification is still partially blocked by environment/.NET SDK resolver behavior in this terminal context.
- Deterministic-first path is now the default direction; LLM use is reduced to extraction + fallback cases.

---

*Checkpoint documented by: opencode (kimi-k2.5-free)*
