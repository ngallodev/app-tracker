#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_HOST="${API_HOST:-0.0.0.0}"
API_PORT="${API_PORT:-5278}"
FRONT_HOST="${FRONT_HOST:-0.0.0.0}"
FRONT_PORT="${FRONT_PORT:-5173}"
CHOKIDAR_USEPOLLING="${CHOKIDAR_USEPOLLING:-1}"
CHOKIDAR_INTERVAL="${CHOKIDAR_INTERVAL:-1000}"

PIDS=()
CLEANED_UP=0

cleanup() {
  if [[ "${CLEANED_UP}" -eq 1 ]]; then
    return
  fi
  CLEANED_UP=1

  echo "Stopping local services..."
  for pid in "${PIDS[@]}"; do
    if kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null || true
    fi
  done
}

trap cleanup EXIT INT TERM

start_api() {
  echo "Starting Tracker.Api on http://${API_HOST}:${API_PORT}"
  HOST="${API_HOST}" PORT="${API_PORT}" bash "${ROOT_DIR}/scripts/run_api.sh" &
  PIDS+=("$!")
}

start_frontend() {
  echo "Starting frontend on http://${FRONT_HOST}:${FRONT_PORT}"
  (
    cd "${ROOT_DIR}/web"
    CHOKIDAR_USEPOLLING="${CHOKIDAR_USEPOLLING}" \
    CHOKIDAR_INTERVAL="${CHOKIDAR_INTERVAL}" \
      npm run dev -- --host "${FRONT_HOST}" --port "${FRONT_PORT}"
  ) &
  PIDS+=("$!")
}

start_api
start_frontend

wait -n
first_exit_code=$?

if [[ "${first_exit_code}" -ne 0 ]]; then
  echo "One local service exited with code ${first_exit_code}. Shutting down remaining services."
fi

cleanup
wait || true

exit "${first_exit_code}"
