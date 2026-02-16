#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FIXTURE_DIR="${1:-$ROOT_DIR/src/Tracker.Eval/Fixtures}"

dotnet run -p:NuGetAudit=false --project "$ROOT_DIR/src/Tracker.Eval/Tracker.Eval.csproj" -- "$FIXTURE_DIR"
