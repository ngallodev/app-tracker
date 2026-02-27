# Pre-Demo Checklist

## 1) Environment

- [ ] .NET SDK 10 installed (`dotnet --version`)
- [ ] Restore/build succeeds:

```bash
dotnet restore Tracker.slnx
dotnet build Tracker.slnx -v minimal
```

- [ ] API starts:

```bash
dotnet run --project src/Tracker.Api/Tracker.Api.csproj
```

## 2) Fast Health Checks

- [ ] `GET /healthz` returns healthy
- [ ] `GET /version` returns environment + `openAiConfigured`

Example:

```bash
curl -sS http://localhost:5278/healthz
curl -sS http://localhost:5278/version
```

## 3) Demo Data Path

- [ ] Job create endpoint works (`POST /api/jobs`)
- [ ] Resume create endpoint works (`POST /api/resumes`)
- [ ] Analysis endpoint behavior is known for current key state

## 4) API Key Decision Gate

- [ ] If `OPENAI_API_KEY` is set: run one live analysis (`POST /api/analyses`)
- [ ] If key is not set: do not attempt live analysis as primary demo

## 5) Required Fallback Flow (No API Key)

- [ ] Show `GET /version` with `openAiConfigured: false`
- [ ] State expected behavior: analysis calls fail due to fake client
- [ ] Run deterministic eval:

```bash
./scripts/run_deterministic_eval.sh
```

- [ ] Confirm pass/fail summary is visible

## 6) Talking Points

- [ ] Deterministic-first gap matching reduces cost/latency
- [ ] LLM fallback is conditional, not default
- [ ] Hash-pair caching avoids repeated analysis work
- [ ] Eval command is deterministic and API-key independent

## 7) Anti-Overclaim Check

Before presenting, verify you are not claiming:
- [ ] frontend shipped in this repo
- [ ] production deployment already configured here
- [ ] API evaluation endpoints (`/api/eval/*`) exist
