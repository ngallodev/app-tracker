---
name: app-tracker-drift-review
description: Variable-driven skill for plan-vs-implementation drift review with deterministic checks and findings-first output.
---

## Input Contract
Caller passes variables (no hardcoded repo paths in this skill):
- `repo_root`
- `plan_doc`
- `readme_doc`
- `api_project`
- `web_dir`
- `api_host`
- `api_port`

Optional:
- `api_program_file`
- `api_endpoints_glob`
- `analysis_service_file`
- `eval_program_file`
- `web_app_file`
- `include_runtime_smoke` (`true`/`false`)

## Resolved Paths
Treat variables as authoritative. Resolve all relative paths from `${repo_root}`.

## Workflow
1. Read planning + implementation docs:
- `${plan_doc}`
- `${readme_doc}`
2. Read implementation surfaces:
- `${api_program_file}`
- files matching `${api_endpoints_glob}`
- `${analysis_service_file}`
- `${eval_program_file}`
- `${web_app_file}`
3. Execute deterministic checks via:
- `scripts/run_drift_checks.sh`
4. If `include_runtime_smoke=true`, run smoke checks from the script output guidance.
5. Produce report using `assets/findings-template.md`.

## Required Output
- Findings (severity-ordered, file references first)
- Working / Not Working
- Plan Drift (planned-missing, implemented-undocumented, stale-docs)
- Recommended implementation order
- Exact verification commands executed

## Notes
- Keep skill generic and variable-driven.
- Do not duplicate script logic inside this markdown.
- Distinguish environment constraints from code defects.

Maintainer: codex gpt-5
