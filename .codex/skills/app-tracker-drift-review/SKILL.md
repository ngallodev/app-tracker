--- 
name: app-tracker-drift-review
description: Use this skill when reviewing the repo state, plan drift, and what is actually runnable.
---

## Goal
- Produce a critical, findings-first review of `docs/PLAN.md` vs implementation.
- Separate code defects/drift from environment/tooling constraints.

## Minimal Workflow
1. Read:
- `docs/PLAN.md`
- `README.md`
- `src/Tracker.Api/Program.cs`
- `src/Tracker.Api/Endpoints/*.cs`
- `src/Tracker.AI/Services/AnalysisService.cs`
- `web/src/App.tsx`
- `src/Tracker.Eval/Program.cs`
2. Verify:
- `git status --short`
- `DOTNET_CLI_HOME=/tmp NUGET_PACKAGES=/tmp/.nuget dotnet build src/Tracker.Api/Tracker.Api.csproj`
- `npm run build --prefix web` (if dependencies are present)
3. Optional runtime smoke:
- `DOTNET_CLI_HOME=/tmp NUGET_PACKAGES=/tmp/.nuget HOST=127.0.0.1 PORT=5278 ./scripts/run_api.sh`
- `curl /healthz`, `/healthz/deps`, `/version`
4. Report sections:
- Findings (severity first)
- Working / Not Working
- Plan Drift
- Recommended implementation order

## app-tracker Notes
- Frontend is currently minimal/read-only; do not assume Day 3 UI is complete.
- CLI-provider runtime may differ from the OpenAI-centric plan text.
- `Tracker.Eval` may be blocked by offline NuGet in sandboxed environments.

## Output Style
- Concise, implementation-first.
- Always include file references.
- Call out exact commands used for verification.

Signed
- codex gpt-5
