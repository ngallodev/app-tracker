# Jenkins CI/CD Plan

This plan sets up a local Jenkins pipeline that produces measurable, resume-grade metrics for this app.

## Goals

- Automate build, tests, deterministic eval, and proof-of-life checks on each run.
- Publish machine-readable metrics artifacts for trend tracking.
- Answer the core questions from runtime telemetry:
  - deterministic resolution rate without fallback
  - average token usage per request
  - token usage when fallback triggers
  - cache hit rate on repeated JDs
  - P95 latency for cold deterministic, fallback, and cached paths

## Pipeline Stages

1. Restore
2. Build
3. Tests
4. Deterministic Eval
5. Proof Of Life (starts API + frontend locally, exercises endpoints, captures analysis metrics)

Each stage is timed and logged via `scripts/ci_stage.sh`.

## Artifacts Produced

- `artifacts/stage-metrics.jsonl`: one JSON line per stage with duration and exit code
- `artifacts/build-metrics.json`: aggregated stage metrics (when `jq` is available)
- `artifacts/proof-of-life-summary.json`: endpoint status and smoke-request results
- `artifacts/analysis-metrics.json`: KPI snapshot from `GET /api/analyses/metrics`
- `artifacts/logs/*.log`: per-stage logs and proof-of-life runner log

## Local Jenkins Setup (Docker)

1. Run Jenkins locally:

```bash
docker run -d --name jenkins-local \
  -p 8080:8080 -p 50000:50000 \
  -v jenkins_home:/var/jenkins_home \
  -v /var/run/docker.sock:/var/run/docker.sock \
  jenkins/jenkins:lts
```

2. Install required tools on your Jenkins agent/controller environment:
   - `dotnet` SDK 10.x
   - `node` + `npm`
   - `jq`
3. In Jenkins, create a Pipeline job that points to this repo root and uses `Jenkinsfile`.
4. Run the pipeline; inspect artifacts from the build page.

## KPI Query Workflow

- Runtime analysis KPIs come from API telemetry:

```bash
curl -sS http://localhost:5278/api/analyses/metrics | jq
```

- Deterministic reproducibility comes from `/eval/run` and is included in `resumeBullets` in the metrics response.

## Resume Output Mapping

Use `analysis-metrics.json` and the endpoint `resumeBullets` output to fill bullets such as:

- Deterministic-first resolution rate without fallback
- Cache-eliminated redundant requests on repeated JDs
- Reproducibility rate across fixture eval cases
- Deterministic vs fallback token profile

---

Signed-off-by: codex gpt-5
