# Restart Handoff

Date: 2026-02-19
Agent: codex gpt-5
Branch: review/code-review-notes

## Summary
This handoff captures the completed redesign to run LLM calls through local headless CLI providers and the current repository state after integration and verification.

## Implemented
- Replaced runtime LLM wiring with CLI provider routing in `src/Tracker.Api/Program.cs`.
- Added multi-provider CLI abstraction and adapters in `src/Tracker.AI/Cli/` for:
  - `claude`, `codex`, `gemini`, `qwen`, `kilocode`, `opencode`.
- Extended `ILlmClient` for provider override and provider metadata in results.
- Updated analysis orchestration and endpoints to accept optional `provider` on `POST /api/analyses`.
- Added provider/execution metadata to `AnalysisResultDto`.
- Replaced OpenAI dependency health probe with CLI provider availability health probe.
- Kept strict JSON structured output contract with one repair pass.
- Added appsettings CLI provider config under `Llm`.

## Verified
- `dotnet build Tracker.slnx -v minimal` -> PASS
- `/healthz` and `/healthz/deps` runtime check -> PASS
  - Current environment shows `claude` available and other configured provider binaries unavailable in PATH.
- Deterministic eval runner status unchanged from prior baseline:
  - `./scripts/run_deterministic_eval.sh` -> 1 PASS / 1 FAIL (`backend_api_engineer` fixture)

## Known Issues / Notes
- Provider runtime availability depends on local binary presence in PATH.
- Embeddings in CLI mode intentionally return `embeddings_not_supported_in_cli_mode`.
- Transient SQLite runtime files may appear (`tracker.db-wal`, `tracker.db-shm`) after local API runs.

## Next Steps After Restart
1. Install/configure additional provider CLIs (or set absolute command paths in `Llm:Providers:*:Command`).
2. Add endpoint-level tests for invalid provider and provider-unavailable paths.
3. Decide whether to keep legacy OpenAI client code paths or remove them fully in a cleanup commit.
4. Resolve failing deterministic fixture (`backend_api_engineer`) if required for green eval baseline.

Signed-off-by: codex gpt-5
