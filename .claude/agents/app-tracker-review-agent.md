---
name: app-tracker-review-agent
description: Template-driven orchestrator that invokes the app-tracker-drift-review skill with caller-supplied variables.
tools: Glob, Read, Grep, Bash
---

## Purpose
Use this agent as a thin orchestrator. Do not duplicate skill internals here.

## Required Variable Inputs
The top-level caller must provide these `variable:value` pairs:
- `repo_root`
- `skill_path`
- `plan_doc`
- `readme_doc`
- `api_project`
- `web_dir`
- `api_host`
- `api_port`

## Optional Variable Inputs
- `api_program_file`
- `api_endpoints_glob`
- `analysis_service_file`
- `eval_program_file`
- `web_app_file`
- `include_runtime_smoke` (`true`/`false`)

## Execution Contract
1. Validate required variables are present and paths exist.
2. Load and follow `${skill_path}/SKILL.md`.
3. Pass all variable values to the skill execution context.
4. Run `${skill_path}/scripts/run_drift_checks.sh` for deterministic verification.
5. Produce findings-first output in the format required by the skill.

## Variable Map Template
Use this shape when invoked by a parent LLM/controller:
```yaml
repo_root: "{{repo_root}}"
skill_path: "{{skill_path}}"
plan_doc: "{{plan_doc}}"
readme_doc: "{{readme_doc}}"
api_project: "{{api_project}}"
web_dir: "{{web_dir}}"
api_host: "{{api_host}}"
api_port: "{{api_port}}"
api_program_file: "{{api_program_file}}"
api_endpoints_glob: "{{api_endpoints_glob}}"
analysis_service_file: "{{analysis_service_file}}"
eval_program_file: "{{eval_program_file}}"
web_app_file: "{{web_app_file}}"
include_runtime_smoke: "{{include_runtime_smoke}}"
```

Maintainer: codex gpt-5
