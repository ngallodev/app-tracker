#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_DIR="${ARTIFACT_DIR:-${ROOT_DIR}/artifacts}"
API_HOST="${API_HOST:-127.0.0.1}"
FRONT_HOST="${FRONT_HOST:-127.0.0.1}"
API_PORT="${API_PORT:-5278}"
FRONT_PORT="${FRONT_PORT:-5173}"
API_URL="${API_URL:-}"
UI_URL="${UI_URL:-}"
STARTUP_TIMEOUT_SECONDS="${STARTUP_TIMEOUT_SECONDS:-60}"
ANALYSIS_PROVIDER="${ANALYSIS_PROVIDER:-}"
SKIP_ANALYSIS="${SKIP_ANALYSIS:-0}"

LOG_DIR="${ARTIFACT_DIR}/logs"
RUN_LOG="${LOG_DIR}/proof_of_life.log"
METRICS_JSON="${ARTIFACT_DIR}/analysis-metrics.json"
SUMMARY_JSON="${ARTIFACT_DIR}/proof-of-life-summary.json"

mkdir -p "${LOG_DIR}"
: > "${RUN_LOG}"

RUNNER_PID=""

cleanup() {
  if [[ -n "${RUNNER_PID}" ]] && kill -0 "${RUNNER_PID}" 2>/dev/null; then
    kill "${RUNNER_PID}" 2>/dev/null || true
    wait "${RUNNER_PID}" 2>/dev/null || true
  fi
}

trap cleanup EXIT INT TERM

is_port_in_use() {
  local port="$1"
  if command -v lsof >/dev/null 2>&1; then
    lsof -nP -iTCP:"${port}" -sTCP:LISTEN >/dev/null 2>&1
    return $?
  fi

  ss -H -ltn | awk '{print $4}' | grep -Eq "[:.]${port}$"
}

pick_free_port() {
  local start_port="$1"
  local max_tries="${2:-100}"
  local candidate="${start_port}"
  local tries=0

  while [[ "${tries}" -lt "${max_tries}" ]]; do
    if ! is_port_in_use "${candidate}"; then
      printf '%s\n' "${candidate}"
      return 0
    fi

    candidate=$((candidate + 1))
    tries=$((tries + 1))
  done

  return 1
}

wait_for_url() {
  local url="$1"
  local timeout_seconds="$2"
  local elapsed=0

  while [[ "${elapsed}" -lt "${timeout_seconds}" ]]; do
    if curl -fsS "${url}" >/dev/null 2>&1; then
      return 0
    fi

    if [[ -n "${RUNNER_PID}" ]] && ! kill -0 "${RUNNER_PID}" 2>/dev/null; then
      echo "Local runner exited before ${url} became available." >&2
      return 1
    fi

    sleep 1
    elapsed=$((elapsed + 1))
  done

  echo "Timed out waiting for ${url}" >&2
  return 1
}

echo "Starting local stack via scripts/run_local.sh ..."
resolved_api_port="$(pick_free_port "${API_PORT}")"
resolved_front_port="$(pick_free_port "${FRONT_PORT}")"
if [[ -z "${API_URL}" ]]; then
  API_URL="http://${API_HOST}:${resolved_api_port}"
fi
if [[ -z "${UI_URL}" ]]; then
  UI_URL="http://${FRONT_HOST}:${resolved_front_port}"
fi

API_HOST="${API_HOST}" API_PORT="${resolved_api_port}" FRONT_HOST="${FRONT_HOST}" FRONT_PORT="${resolved_front_port}" \
  bash "${ROOT_DIR}/scripts/run_local.sh" >"${RUN_LOG}" 2>&1 &
RUNNER_PID="$!"

wait_for_url "${UI_URL}/" "${STARTUP_TIMEOUT_SECONDS}"
wait_for_url "${API_URL}/healthz" "${STARTUP_TIMEOUT_SECONDS}"

ui_status="$(curl -s -o /dev/null -w '%{http_code}' "${UI_URL}/")"
health_status="$(curl -s -o /dev/null -w '%{http_code}' "${API_URL}/healthz")"
analyses_list_status="$(curl -s -o /dev/null -w '%{http_code}' "${API_URL}/api/analyses")"

job_id="$(curl -sS -X POST "${API_URL}/api/jobs" \
  -H 'Content-Type: application/json' \
  -d '{"title":"Proof of Life Backend Engineer","company":"Acme","descriptionText":"Need C#, ASP.NET Core, SQL, Docker.","sourceUrl":"https://example.com/job/proof-of-life"}' | jq -r '.id // empty')"

resume_id="$(curl -sS -X POST "${API_URL}/api/resumes" \
  -H 'Content-Type: application/json' \
  -d '{"name":"Proof of Life Resume","content":"Experienced in C#, ASP.NET Core, SQL, and Docker."}' | jq -r '.id // empty')"

analysis_status="skipped"
analysis_body=""

if [[ "${SKIP_ANALYSIS}" != "1" ]] && [[ -n "${job_id}" ]] && [[ -n "${resume_id}" ]]; then
  analysis_payload="$(jq -cn \
    --arg jobId "${job_id}" \
    --arg resumeId "${resume_id}" \
    --arg provider "${ANALYSIS_PROVIDER}" \
    'if ($provider | length) > 0 then {jobId: $jobId, resumeId: $resumeId, provider: $provider} else {jobId: $jobId, resumeId: $resumeId} end')"

  analysis_response="$(curl -sS -X POST "${API_URL}/api/analyses" \
    -H 'Content-Type: application/json' \
    -d "${analysis_payload}" \
    -w $'\n%{http_code}\n')"
  analysis_body="$(printf '%s' "${analysis_response}" | sed '$d')"
  analysis_status="$(printf '%s' "${analysis_response}" | tail -n1)"
fi

eval_response="$(curl -sS -X POST "${API_URL}/eval/run" -w $'\n%{http_code}\n')"
eval_body="$(printf '%s' "${eval_response}" | sed '$d')"
eval_status="$(printf '%s' "${eval_response}" | tail -n1)"

curl -sS "${API_URL}/api/analyses/metrics" > "${METRICS_JSON}"

jq -n \
  --arg startedVia "scripts/run_local.sh" \
  --arg uiStatus "${ui_status}" \
  --arg apiHealthStatus "${health_status}" \
  --arg analysesListStatus "${analyses_list_status}" \
  --arg analysisStatus "${analysis_status}" \
  --arg analysisBody "${analysis_body}" \
  --arg evalStatus "${eval_status}" \
  --arg evalBody "${eval_body}" \
  --arg jobId "${job_id}" \
  --arg resumeId "${resume_id}" \
  '{
    startedVia: $startedVia,
    uiStatus: ($uiStatus | tonumber),
    apiHealthStatus: ($apiHealthStatus | tonumber),
    analysesListStatus: ($analysesListStatus | tonumber),
    analysisRequestStatus: (if $analysisStatus == "skipped" then $analysisStatus else ($analysisStatus | tonumber) end),
    analysisRequestBody: $analysisBody,
    evalRunStatus: ($evalStatus | tonumber),
    evalRunBody: $evalBody,
    jobId: $jobId,
    resumeId: $resumeId
  }' > "${SUMMARY_JSON}"

echo "Proof-of-life complete."
echo "Summary: ${SUMMARY_JSON}"
echo "Metrics: ${METRICS_JSON}"
echo "Runner log: ${RUN_LOG}"

if [[ "${ui_status}" != "200" || "${health_status}" != "200" || "${analyses_list_status}" != "200" ]]; then
  echo "Critical proof-of-life checks failed." >&2
  exit 1
fi
