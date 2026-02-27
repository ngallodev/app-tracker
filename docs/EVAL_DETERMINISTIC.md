# Deterministic Eval Runner

This runner validates deterministic skill matching behavior using static fixtures and no LLM calls.

## Why use it
- Cheap regression signal during rapid iteration.
- Verifies matching logic and synonym handling.
- Runs locally without API keys.

## Run
```bash
./scripts/run_deterministic_eval.sh
```

Optional fixture directory:
```bash
./scripts/run_deterministic_eval.sh src/Tracker.Eval/Fixtures
```

## Output
- Per-fixture `PASS`/`FAIL` lines.
- Summary counts.
- Non-zero exit code when any fixture fails.

## Fixture format
Each fixture includes:
- required skills
- preferred skills
- resume text
- simple expectation thresholds (`minRequiredMatches`, `maxMissingRequired`, `minPreferredMatches`)

Example fixtures:
- `src/Tracker.Eval/Fixtures/backend_api_engineer.json`
- `src/Tracker.Eval/Fixtures/frontend_fullstack.json`

---

*Document created by: opencode (kimi-k2.5-free)*
