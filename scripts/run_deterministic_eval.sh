#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FIXTURE_DIR="${1:-$ROOT_DIR/src/Tracker.Eval/Fixtures}"
ARTIFACT_DIR="${ARTIFACT_DIR:-$ROOT_DIR/artifacts}"
EVAL_SUMMARY_PATH="${ARTIFACT_DIR}/deterministic-eval-summary.json"
RUN_LOG_PATH="${ARTIFACT_DIR}/logs/deterministic_eval_runner.log"

mkdir -p "${ARTIFACT_DIR}" "${ARTIFACT_DIR}/logs"

# Work around intermittent parallel MSBuild failures in SDK 10.0.103 during project-reference TFM discovery.
dotnet build -m:1 -p:NuGetAudit=false "$ROOT_DIR/src/Tracker.Eval/Tracker.Eval.csproj" -v minimal >/dev/null

set +e
dotnet run --no-build -p:NuGetAudit=false --project "$ROOT_DIR/src/Tracker.Eval/Tracker.Eval.csproj" -- "$FIXTURE_DIR" \
  | tee "${RUN_LOG_PATH}"
eval_exit=${PIPESTATUS[0]}
set -e

python3 - <<'PY' "${RUN_LOG_PATH}" "${EVAL_SUMMARY_PATH}"
import json
import re
import sys
from pathlib import Path

log_path = Path(sys.argv[1])
summary_path = Path(sys.argv[2])
text = log_path.read_text(encoding="utf-8")

totals_match = re.search(r"Fixtures:\s*(\d+),\s*Passed:\s*(\d+),\s*Failed:\s*(\d+)", text)
if totals_match:
    fixtures = int(totals_match.group(1))
    passed = int(totals_match.group(2))
    failed = int(totals_match.group(3))
else:
    fixtures = 0
    passed = 0
    failed = 0

failures = []
for line in text.splitlines():
    m = re.match(r"^\[FAIL\]\s+(.+?)\s+-\s+(.+)$", line.strip())
    if m:
        failures.append({"name": m.group(1), "detail": m.group(2)})

pass_rate = round((passed / fixtures) * 100.0, 2) if fixtures else 0.0

summary = {
    "fixtures": fixtures,
    "passed": passed,
    "failed": failed,
    "passRatePercent": pass_rate,
    "failedFixtures": failures,
    "resumeBullets": [
        f"Built a deterministic eval harness with {fixtures} fixture-based scenarios for repeatable resume-vs-JD matching QA.",
        f"Achieved {pass_rate}% deterministic reproducibility ({passed}/{fixtures} passing) in the latest CI eval run."
    ] if fixtures else [],
}

summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
PY

echo "Wrote eval summary artifact: ${EVAL_SUMMARY_PATH}"
exit "${eval_exit}"
