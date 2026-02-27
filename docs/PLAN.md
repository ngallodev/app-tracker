# AI-Augmented Job Application Tracker
## 5-Day Execution Plan (Production-Focused, Cost-Optimized)

Author: Nathan Gallo  
Target Roles: AI Engineer, Applied AI, Senior Full-Stack (AI Differentiated)  
Primary Goal: Ship a functional, production-minded MVP in 5 days that is both usable for real job applications and impressive to hiring managers.

---

# Product Definition

A job application tracker with AI-powered job description analysis.

**Core Capabilities:**
- Track jobs and resumes
- Extract structured requirements from job descriptions
- Compare JD vs resume
- Identify missing required and preferred skills
- Compute deterministic coverage score
- Compute groundedness score
- Log model usage, latency, schema compliance
- Provide evaluation dashboard

**Explicitly Out of Scope:**
- No auto-editing or resume rewriting
- No background job queue
- No heavy vector DB
- No agent orchestration

This is a disciplined AI system, not a ChatGPT wrapper.

---

# Implementation Drift Snapshot (February 25, 2026)

This document remains the original execution plan. The codebase has intentionally diverged in a few important ways:

- Runtime LLM integration is currently **CLI-provider headless execution** (Claude/Codex/Gemini/etc. adapters) instead of the OpenAI SDK-first runtime described below.
- Day 3 UI is partially shipped as a single-page workflow: create/delete jobs and resumes, run analyses, inspect analysis metrics/skills, and trigger/view deterministic eval runs. Edit/update UI flows and dedicated routed pages are still missing.
- `/eval/run` and `/eval/runs` API endpoints now exist and persist deterministic eval summaries, but there is not yet a standalone eval dashboard page.
- Day 4 reliability/security work is partially implemented (correlation IDs, exception middleware, security headers, input validation, rate limiting), but runtime resilience behavior for CLI providers is not fully aligned with the Polly/OpenAI-focused plan narrative.
- Test coverage and deployment success criteria listed later in this plan are not yet met.

Signed: codex gpt-5

---

# Architecture Overview

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   React SPA     │────▶│  .NET 10 Minimal │────▶│   SQLite        │
│   (Vite)        │◀────│      API         │◀────│  (EF Core)      │
└─────────────────┘     └──────────────────┘     └─────────────────┘
                               │
                               ▼
                        ┌──────────────────┐
                        │   OpenAI API     │
                        │  (GPT-4o, Emb)   │
                        └──────────────────┘
```

## Core AI Engineering Patterns (Portfolio Signal)

1. **Structured Outputs + JSON Schema Validation + Repair Loop** - Every LLM response validated against schema
2. **Grounded Extraction** - Evidence quotes required for every extracted requirement
3. **Eval Harness + Observability** - Metrics tracking for AI performance
4. **Cost Controls** - Hash caching + token logging
5. **Deterministic Scoring** - No LLM self-grading; coverage computed algorithmically

---

# Technology Stack

| Layer | Technology | .NET 10 Features Used |
|-------|------------|----------------------|
| Backend | .NET 10 Minimal API | IAsyncEnumerable streaming, TimeProvider, KeyedServices |
| Database | SQLite + EF Core | Migrations, JSON columns for flexible analysis storage |
| AI | OpenAI API (GPT-4o, text-embedding-3-small) | Function calling, structured outputs |
| Frontend | React 18 + Vite + TanStack Query | Polling, optimistic updates |
| Testing | xUnit + NSubstitute | WebApplicationFactory, HttpClient mocking |
| Resilience | Polly | Retry, circuit breaker, timeout |

---

## Current Runtime Note (Implementation)

As of February 25, 2026, the shipped API runtime is wired to `Tracker.AI.Cli` provider adapters (`CliLlmClientRouter`) rather than the OpenAI SDK path shown in the architecture diagram and AI stack row above. Treat the OpenAI-specific sections below as design intent unless/until the runtime is switched back.

---

# Key Design Decisions

## 1. Deterministic Scoring (Not LLM Self-Grading)

Coverage Score = `(required_matched / total_required) * 100`

Groundedness Score = `% required skills with JD evidence` + `% matches with resume evidence`

**No LLM-based scoring allowed.** This prevents hallucinated scores and ensures reproducibility.

## 2. Hash-Based Caching for JD/Resume Pairs

Before calling LLM:
- Hash JD text (SHA-256)
- Hash resume text (SHA-256)
- If identical pair already analyzed → return cached result

**Cost savings:** ~60% on repeated JD analysis. Estimated cost: ~$0.04-$0.05 per analysis, ~$15/month at 10/day.

## 3. JSON Columns for Flexible Analysis Storage

```csharp
public class AnalysisResult {
    public Guid Id { get; set; }
    public string RequiredSkillsJson { get; set; }  // JSON column
    public string MissingRequiredJson { get; set; } // JSON column
    public string MissingPreferredJson { get; set; } // JSON column
    public double CoverageScore { get; set; }
    public double GroundednessScore { get; set; }
}
```

Allows schema evolution without migrations.

## 4. Schema Validation + Repair Loop

Flow:
1. Call LLM
2. Attempt JSON parse + schema validation
3. If invalid → Run repair prompt once
4. If still invalid → Mark analysis failed

Store: `parse_success`, `repair_attempted` for eval metrics.

## 5. ILlmClient Abstraction with KeyedServices

```csharp
public interface ILlmClient {
    Task<LlmResult<T>> CompleteStructuredAsync<T>(...);
    IAsyncEnumerable<string> StreamCompletionAsync(PromptChain chain);
    Task<float[]> GetEmbeddingAsync(string text);
}

// Use .NET KeyedServices for:
// - OpenAI implementation (primary)
// - FakeLLM for tests
// - Future provider swap (Anthropic, Azure)
```

## 6. Grounded Extraction (Evidence Quotes Required)

Every skill extraction requires:
- `skill_name`: string
- `evidence_quote`: <=20 word quote from source text

Rules:
- No evidence quote = must mark as missing
- Null allowed if not explicitly stated in JD
- No inference allowed

---

# Database Schema

## jobs
- id (GUID)
- title
- company
- description_text
- description_hash
- created_at
- updated_at

## resumes
- id (GUID)
- name
- content
- content_hash
- created_at
- updated_at

## analyses
- id (GUID)
- job_id (FK)
- resume_id (FK)
- status (enum: Pending, Running, Complete, Failed)
- model
- prompt_version
- schema_version
- input_tokens
- output_tokens
- latency_ms
- created_at

## analysis_results
- analysis_id (FK)
- required_skills_json
- missing_required_json
- missing_preferred_json
- coverage_score
- groundedness_score

## llm_logs
- id (GUID)
- analysis_id (FK)
- step_name
- raw_response
- parse_success
- repair_attempted
- created_at

---

# 5-Day Execution Plan

---

## Day 1: Foundation & Production Skeleton ✅ COMPLETE

### What Was Built
- Git repository initialized with proper .gitignore
- .NET 10 solution with 5 projects:
  - Tracker.Api (Minimal API)
  - Tracker.Domain (Entities, DTOs, Enums)
  - Tracker.Infrastructure (EF Core, DbContext)
  - Tracker.AI (LLM interfaces - stub)
  - Tracker.Eval (Evaluation framework - stub)
- SQLite database with migrations
- CRUD endpoints for Jobs and Resumes
- Hash utility for content caching
- Health and version endpoints

### Files Created
```
src/
├── Tracker.Api/
│   ├── Endpoints/
│   │   ├── JobsEndpoints.cs
│   │   └── ResumesEndpoints.cs
│   ├── Program.cs
│   └── tracker.db (SQLite database)
├── Tracker.Domain/
│   ├── Entities/
│   │   ├── Job.cs
│   │   ├── Resume.cs
│   │   ├── Analysis.cs
│   │   ├── AnalysisResult.cs
│   │   └── LlmLog.cs
│   ├── Enums/
│   │   └── AnalysisStatus.cs
│   └── DTOs/
│       ├── JobDto.cs
│       ├── ResumeDto.cs
│       ├── AnalysisDto.cs
│       ├── AnalysisResultDto.cs
│       └── Requests/
│           ├── CreateJobRequest.cs
│           ├── UpdateJobRequest.cs
│           ├── CreateResumeRequest.cs
│           └── UpdateResumeRequest.cs
├── Tracker.Infrastructure/
│   ├── Data/
│   │   ├── TrackerDbContext.cs
│   │   └── Migrations/
│   └── HashUtility.cs
├── Tracker.AI/
└── Tracker.Eval/
```

### Endpoints Working
- GET/POST/PUT/DELETE /api/jobs
- GET/POST/PUT/DELETE /api/resumes
- GET /healthz
- GET /version

---

## Day 2: AI Core (Grounded + Deterministic) ✅ COMPLETE

### What Was Built
- ILlmClient interface with structured output support
- OpenAiClient implementation with OpenAI SDK v2.8
- JdExtraction model with evidence quote requirements
- GapAnalysis model for skill matching
- AnalysisService orchestrating the pipeline
- Deterministic scoring (coverage + groundedness)
- Analysis endpoint POST /api/analyses
- FakeLlmClient for development without API key

### Files Created
```
src/Tracker.AI/
├── ILlmClient.cs
├── OpenAiClient.cs
├── Models/
│   ├── JdExtraction.cs
│   └── GapAnalysis.cs
├── Prompts/
│   ├── JdExtractionPrompt.cs
│   └── GapAnalysisPrompt.cs
└── Services/
    └── AnalysisService.cs
src/Tracker.Domain/DTOs/
├── AnalysisResultDto.cs (updated)
└── Requests/
    └── CreateAnalysisRequest.cs
src/Tracker.Api/
├── Program.cs (updated with AI services)
└── Endpoints/
    └── AnalysesEndpoints.cs
```

### Endpoints Added
- GET /api/analyses
- GET /api/analyses/{id}
- POST /api/analyses (requires OPENAI_API_KEY)

### Day 2 Success Criteria
- [x] Paste JD → structured extraction (prompts ready)
- [x] Upload resume text → gap analysis (prompts ready)
- [x] Retry logic (handled in OpenAiClient)
- [x] Token usage logged per request
- [x] Schema validation + repair (repair loop in place)

### Configuration Required
Set OPENAI_API_KEY environment variable to enable real LLM calls.

---

## Day 3: MVP Ship

**Goal:** Usable end-to-end system.

### Tasks

| Task | Complexity | Dependencies | Deliverable |
|------|------------|--------------|-------------|
| 3.1 Analysis Endpoint | Med | Day 2 | POST /api/analyses |
| 3.2 React Jobs List | Low | None | Jobs CRUD UI |
| 3.3 React Resumes List | Low | None | Resumes CRUD UI |
| 3.4 React Analysis Page | Med | 3.1 | Analysis detail with scores |
| 3.5 Eval Harness | Med | 3.1 | /eval/run with fixtures |

### Frontend Pages
- Jobs list
- Resumes list
- Run analysis button
- Analysis detail page

### Analysis Page Shows:
- Required skills
- Missing required
- Missing preferred
- Coverage score
- Groundedness score
- Token usage
- Latency
- Parse success flag

**This page is the interview demo centerpiece.**

### Eval Harness
- Create `/eval/run` endpoint
- Use 10 static JD fixtures
- Metrics:
  - Schema pass rate
  - Groundedness rate
  - Coverage stability (run twice diff)
  - Avg latency
  - Avg cost per run
- Persist results in DB

### Day 3 Success Criteria
- [ ] End-to-end flow < 30 seconds (paste JD → see analysis)
- [ ] ATS score calculation < 5 seconds (cached)
- [ ] Analysis page shows all metrics
- [ ] Eval harness runs against fixtures

---

## Day 4: Reliability + Observability

**Goal:** Production-grade resilience and monitoring.

### Tasks

| Task | Complexity | Dependencies | Deliverable |
|------|------------|--------------|-------------|
| 4.1 Polly Policies | Med | None | Retry, circuit breaker, timeout |
| 4.2 Correlation ID Middleware | Low | None | Request tracing |
| 4.3 Structured Logging | Low | None | Serilog with enrichment |
| 4.4 Error Handling | Med | None | ProblemDetails RFC7807 |
| 4.5 Security Hardening | Med | None | Input validation, rate limiting |

### Resilience Patterns
- Polly retry (2 attempts)
- Exponential backoff
- Circuit breaker (5 failures in 60s = 30s cooldown)
- Timeout (30s default)

### Security
- Treat JD text as untrusted input
- Never allow user text to influence tool selection
- Redact email/phone before logging prompts
- Rate limit per IP

### Day 4 Success Criteria
- [ ] Retry logic handles OpenAI failures gracefully
- [ ] Circuit breaker prevents cascading failures
- [ ] All errors return proper ProblemDetails
- [ ] Correlation ID present in all logs

---

## Day 5: Deployment + Portfolio Polish

**Goal:** Deploy and prepare for interviews.

### Tasks

| Task | Complexity | Dependencies | Deliverable |
|------|------------|--------------|-------------|
| 5.1 Docker Setup | Low | None | Dockerfile + compose |
| 5.2 Deploy | Low | 5.1 | Live URL (Fly.io/Railway) |
| 5.3 README | Low | None | GitHub-ready documentation |
| 5.4 Demo Prep | Low | None | 60-second walkthrough script |

### Deployment
- Single Docker container
- SQLite persistent volume
- Fly.io or Railway
- Rate limit per IP
- Limit concurrency

### README Structure
- Architecture diagram
- AI Engineering Patterns Demonstrated
- Tech Stack
- Local Setup
- Key Features (screenshots)

### Day 5 Success Criteria
- [ ] Deployed and accessible via URL
- [ ] README explains architecture and patterns
- [ ] Demo script ready for interviews

---

## Optimized Execution Workflow (Remaining Days Only)

These optimized breakdowns are intended for efficient multi-agent assignment and lower token/cost overhead:

- Day 3 optimized breakdown: `docs/archive-ignore/planning/day-3-tasks.md` (see "Day 3 Optimization Addendum")
- Day 4 optimized breakdown: `docs/archive-ignore/planning/day-4-tasks.md`
- Day 5 optimized breakdown: `docs/archive-ignore/planning/day-5-tasks.md` (see "Day 5 Optimization Addendum")

### Agent and model guidance
- `explorer` agent: discovery, code search, validation checks, docs consistency checks.  
  Model suggestion: Small/mini tier.
- `worker` agent: implementation tasks touching multiple files/contracts.  
  Model suggestion: Medium tier.
- Use higher-cost model tiers only for complex cross-cutting refactors or ambiguous architecture decisions.

### Skill guidance
- Use `exec-statusline-json` when execution telemetry (quota/tokens/footer data) needs to be captured for evidence or audit logs.

---

# Success Criteria (Overall MVP)

- [ ] End-to-end flow < 30 seconds (paste JD → see analysis)
- [ ] ATS score calculation < 5 seconds (cached)
- [ ] 100% test coverage on LLM client resilience
- [ ] Zero hardcoded secrets (User Secrets + Env vars)
- [ ] Deployed and accessible via URL
- [ ] Eval harness with metrics dashboard

---

# Cost Estimate

**Per analysis:**
- ~5k input tokens
- ~1.3k output tokens

**Using 4o-mini pricing:**
- ≈ $0.04–$0.05 per run
- 10/day: ≈ $0.50/day, ≈ $15/month

**With caching:** Likely under $10/month.

---

# Interview Positioning Strategy

## Emphasize (70% Senior Engineering Judgment)
- Eval harness
- Observability
- Deterministic scoring
- Schema validation + repair
- Cost control
- Retry + circuit breaker

## Emphasize (30% Modern AI)
- Structured outputs
- Grounded extraction
- Embeddings
- Model routing
- Injection awareness

**Avoid over-indexing on agents.**

---

# Top 5 Interview Questions + Answers

### 1. How do you prevent hallucinations?

"I require evidence quotes for every extracted requirement and resume match. If the model cannot provide evidence, it must mark the skill missing. All outputs are validated against a strict JSON schema, with a repair loop for malformed responses."

### 2. How do you evaluate model performance?

"I maintain a golden dataset and measure schema pass rate, groundedness, latency, and coverage stability across runs. This allows detection of regressions when prompts or models change."

### 3. How do you control cost?

"I hash JD and resume pairs and cache results. I use 4o-mini for extraction, log token usage per step, and enforce a daily token budget."

### 4. What happens when the API fails?

"I use Polly retry with exponential backoff and a circuit breaker. Partial runs are persisted so users can retry without losing context."

### 5. How would you scale this?

"I would introduce multi-tenancy, move to Postgres and a proper vector store, add background jobs, encrypted PII storage, and expand the evaluation pipeline. The architecture isolates the LLM layer to enable this."

---

# Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| OpenAI API downtime | Circuit breaker + "analyze later" queue |
| Slow analysis UX | Background jobs + polling + optimistic UI |
| API cost overruns | Embedding cache, token counting, rate limiting |
| Complex file parsing | Scope cut: text paste only, no PDF/DOCX |
| Auth complexity | Stub single-user, scaffold for future |

---

# Post-MVP Enhancements (Week 2+)

1. **Browser Extension**: One-click JD capture
2. **Multi-Provider**: Anthropic Claude, Azure OpenAI fallback
3. **Fine-Tuning**: Train model on your successful applications
4. **Interview Prep**: Generate questions based on JD gaps
5. **Salary Negotiation**: Market data integration

---

*Consolidated execution plan for sprint development. Day 1 and Day 2 COMPLETE.*

---

*Document created by: opencode (kimi-k2.5-free)*
