#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -lt 2 ]]; then
  echo "Usage: $0 <stage-name> <command> [args...]" >&2
  exit 1
fi

stage_name="$1"
shift

artifact_dir="${ARTIFACT_DIR:-artifacts}"
log_dir="${artifact_dir}/logs"
metrics_jsonl="${artifact_dir}/stage-metrics.jsonl"
mkdir -p "${log_dir}"

stage_slug="$(printf '%s' "${stage_name}" | tr '[:upper:]' '[:lower:]' | tr -cs 'a-z0-9' '_')"
log_file="${log_dir}/${stage_slug}.log"

start_epoch_ms="$(date +%s%3N)"
start_iso="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

set +e
"$@" > >(tee "${log_file}") 2>&1
exit_code=$?
set -e

end_epoch_ms="$(date +%s%3N)"
end_iso="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
duration_ms="$((end_epoch_ms - start_epoch_ms))"

escaped_stage_name="$(printf '%s' "${stage_name}" | sed 's/\\/\\\\/g; s/"/\\"/g')"
escaped_log_file="$(printf '%s' "${log_file}" | sed 's/\\/\\\\/g; s/"/\\"/g')"

printf '{"stage":"%s","startedAt":"%s","finishedAt":"%s","durationMs":%s,"exitCode":%s,"log":"%s"}\n' \
  "${escaped_stage_name}" \
  "${start_iso}" \
  "${end_iso}" \
  "${duration_ms}" \
  "${exit_code}" \
  "${escaped_log_file}" >> "${metrics_jsonl}"

exit "${exit_code}"
