#!/usr/bin/env bash
set -euo pipefail

: "${REPO_ROOT:?REPO_ROOT is required}"
: "${API_PROJECT:?API_PROJECT is required}"
: "${WEB_DIR:?WEB_DIR is required}"

cd "${REPO_ROOT}"

echo "[check] git status --short"
git status --short

echo "[check] dotnet build ${API_PROJECT}"
DOTNET_CLI_HOME=/tmp NUGET_PACKAGES=/tmp/.nuget dotnet build "${API_PROJECT}"

if [[ -d "${WEB_DIR}/node_modules" ]]; then
  echo "[check] npm run build --prefix ${WEB_DIR}"
  npm run build --prefix "${WEB_DIR}"
else
  echo "[skip] npm build: ${WEB_DIR}/node_modules not present"
fi

if [[ "${INCLUDE_RUNTIME_SMOKE:-false}" == "true" ]]; then
  : "${API_HOST:?API_HOST is required when INCLUDE_RUNTIME_SMOKE=true}"
  : "${API_PORT:?API_PORT is required when INCLUDE_RUNTIME_SMOKE=true}"
  echo "[hint] runtime smoke requested; run API and probe http://${API_HOST}:${API_PORT}/healthz"
fi
