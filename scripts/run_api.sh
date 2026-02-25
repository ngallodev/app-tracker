#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HOST="${HOST:-0.0.0.0}"
PORT="${PORT:-5278}"

export ASPNETCORE_URLS="http://${HOST}:${PORT}"
NO_RESTORE="${NO_RESTORE:-1}"
NO_BUILD="${NO_BUILD:-1}"

RUN_ARGS=()
if [[ "${NO_RESTORE}" == "1" ]]; then
  RUN_ARGS+=("--no-restore")
fi
if [[ "${NO_BUILD}" == "1" ]]; then
  RUN_ARGS+=("--no-build")
fi

echo "Starting Tracker.Api on ${ASPNETCORE_URLS}"
exec dotnet run "${RUN_ARGS[@]}" --project "${ROOT_DIR}/src/Tracker.Api/Tracker.Api.csproj"
